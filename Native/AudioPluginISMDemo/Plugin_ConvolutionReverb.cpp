#include "AudioPluginUtil.h"
#include <thread>

namespace ConvolutionReverb
{
    const float MAXLENGTH = 15.0f;
    const int MAXSAMPLE = 16;

    //Mutex sampleMutex;

	// Data container of one impulse response
    struct IRSample
    {
		Mutex sampleMutex;
        float* data;
        int numsamples;
        int numchannels;
        int samplerate;
        int updatecount;
        int allocated;
		bool impulse_upload_only;
        char name[1024];
    };

	// Get impulse response data
    inline IRSample& GetIRSample(int index)
    {
        static bool initialized = false;
        static IRSample samples[MAXSAMPLE];
        if (!initialized)
        {
            memset(samples, 0, sizeof(samples));
			for (int i = 0; i < MAXSAMPLE; ++i)
			{
				samples[i].sampleMutex = Mutex();
			}
            initialized = true;
        }
        return samples[index];
    }

	// Parameters that are shown in Unity plugin
    enum Param
    {
        P_USESAMPLE,
        P_NUM
    };

	// Data container of one channel
    struct Channel
    {
        UnityComplexNumber** h;
        UnityComplexNumber** x;
        float* impulse;
        float* s;
    };

	// Data container of one instance of the plugin
    struct EffectData
    {
        Mutex* mutex;
        float p[P_NUM];
		int numsamples_in;
		int samplerate_in;
        int numchannels;
        int numpartitions;
        int fftsize;
        int hopsize;
        int bufferindex;
        int writeoffset;
        int samplerate;
        float lastparams[P_NUM];
        UnityComplexNumber* tmpoutput;
        Channel* channels;
    };


	// Register all input parameters of the plugin in Unity
    int InternalRegisterEffectDefinition(UnityAudioEffectDefinition& definition)
    {
        int numparams = P_NUM;
        definition.paramdefs = new UnityAudioParameterDefinition[numparams];
		RegisterParameter(definition, "Use Sample", "", 0.0f, MAXSAMPLE - 1, 0.0f, 1.0f, 1.0f, P_USESAMPLE, "indicates the slot of a sample uploaded by scripts via ConvolutionReverb_UploadSample");
        return numparams;
    }


	void ResetData(EffectData* data, const IRSample& s, int numchannels, int blocksize, int samplerate)
	{
		for (int i = 0; i < data->numchannels; i++)
		{
			// delete old channel data
			Channel& c = data->channels[i];
			for (int k = 0; k < data->numpartitions; k++)
			{
				delete[] c.h[k];
				delete[] c.x[k];
			}
			delete[] c.h;
			delete[] c.x;
			delete[] c.s;
			delete[] c.impulse;
		}
		delete[] data->channels;
		delete[] data->tmpoutput;
		// refresh last parameters listing
		memcpy(data->lastparams, data->p, sizeof(data->p));
		// reinitialize data
		data->bufferindex = 0;
		data->writeoffset = 0;
		data->samplerate_in = s.samplerate;
		data->numsamples_in = s.numsamples;
		data->numchannels = numchannels;
		data->hopsize = blocksize;
		data->fftsize = blocksize * 2;
		data->tmpoutput = new UnityComplexNumber[data->fftsize];
		data->channels = new Channel[data->numchannels];
		data->samplerate = samplerate;
		// format temporal output array
		memset(data->tmpoutput, 0, sizeof(UnityComplexNumber) * data->fftsize);
		// calculate length of the impulse in samples
		int reallength = 0;
		if (s.numsamples == 0)
		{
			reallength = 256;
		}
		else
		{
			reallength = (int)ceilf(s.numsamples * (float)samplerate / (float)s.samplerate);
		}
		// calculate length of impulse in samples as a multiple of the number of partitions processed
		data->numpartitions = 0;
		while (data->numpartitions * data->hopsize < reallength)
		{
			data->numpartitions++;
		}
		int impulsesamples = data->numpartitions * data->hopsize;

		for (int i = 0; i < data->numchannels; i++)
		{
			Channel& c = data->channels[i];
			c.impulse = new float[impulsesamples];
			c.s = new float[data->fftsize];
			memset(c.impulse, 0, sizeof(float) * impulsesamples);
			memset(c.s, 0, sizeof(float) * data->fftsize);
			// partition the impulse response
			c.h = new UnityComplexNumber*[data->numpartitions];
			c.x = new UnityComplexNumber*[data->numpartitions];
			for (int k = 0; k < data->numpartitions; k++)
			{
				c.h[k] = new UnityComplexNumber[data->fftsize];
				c.x[k] = new UnityComplexNumber[data->fftsize];
				memset(c.x[k], 0, sizeof(UnityComplexNumber) * data->fftsize);
				memset(c.h[k], 0, sizeof(UnityComplexNumber) * data->fftsize);
			}
		}
	}


	static void irUpload_thread(EffectData* data, int numchannels, int blocksize, int samplerate)
	{
		// Lock the data during initialization
		MutexScopeLock mutexscope0(*data->mutex);
		int usesample = (int)data->p[P_USESAMPLE];
		// Lock the sample data during upload
		MutexScopeLock mutexScope1(GetIRSample(usesample).sampleMutex);

		IRSample& s = GetIRSample(usesample);
		// Reinitialize buffers (can be avoided if numchannels, numpartitions 
		// and hopsize stay the same)
		if (!(s.numchannels == data->numchannels
			&& data->hopsize == blocksize
			&& data->samplerate_in == s.samplerate
			&& data->numsamples_in == s.numsamples))
		{
			ResetData(data, s, numchannels, blocksize, samplerate);
		}
		// calculate individual impulse responses per channel
		float sampletime = 1.0f / (float)samplerate;
		int impulsesamples = data->numpartitions * data->hopsize;
		int n_ch = data->numchannels;
		// Unlock data for now
		for (int i = 0; i < n_ch; i++)
		{
			// === Copy impulse data ===
			Channel& c = data->channels[i];
			if (s.numsamples == 0)
			{
				// Generate dummy IR
				static float dummydata[256 * 8];
				static IRSample dummysample;
				dummysample.data = dummydata;
				dummysample.numchannels = numchannels;
				dummysample.numsamples = 256;
				dummysample.samplerate = samplerate;
				for (int n = 0; n < numchannels; n++)
					dummydata[n] = 1.0f;
				s = dummysample;
			}
			// Interpolate if samplerates differ
			int channel = (i < s.numchannels) ? i : (s.numchannels - 1);
			float speed = (float)s.samplerate / (float)samplerate;
			for (int n = 0; n < impulsesamples; n++)
			{
				float fpos = n * speed;
				int ipos1 = (int)ceilf(fpos);
				if (ipos1 >= s.numsamples)
				{
					ipos1 = s.numsamples - 1;
				}
				int ipos2 = ipos1 + 1;
				if (ipos2 >= s.numsamples)
				{
					ipos2 = s.numsamples - 1;
				}
				fpos -= ipos1;
				float s1 = s.data[ipos1 * s.numchannels + channel];
				float s2 = s.data[ipos2 * s.numchannels + channel];
				c.impulse[n] = s1 + (s2 - s1) * fpos;
			}

			// === Normalize gain ===
			// measure signal power
			// NOTE: applying this breaks the dB meter
			float power = 0.0f;
			for (int n = 0; n < impulsesamples; n++)
			{
				power += c.impulse[n] * c.impulse[n];
			}
			// scale to fit
			float scale = 1.0f / sqrtf(power);
			for (int n = 0; n < impulsesamples; n++)
			{
				c.impulse[n] *= scale;
			}

			// FFT transform
			float* src = c.impulse;
			for (int k = 0; k < data->numpartitions; k++)
			{
				for (int n = 0; n < data->hopsize; n++)
				{
					c.h[k][n].re = *src++;
				}
				FFT::Forward(c.h[k], data->fftsize, false);
			}

			// integrate peak detection filtered impulse for later resampling via box-filtering when GUI requests preview waveform
			double sum = 0.0, peak = 0.0;
			for (int n = 0; n < impulsesamples; n++)
			{
				float a = fabsf(c.impulse[n]);
				if (a > peak)
					peak = a;
				else
					peak = peak * 0.99f + 1.0e-9f;
				sum += peak;
				c.impulse[n] = (float)sum;
			}
			double dc = -sum / (double)impulsesamples;
			sum = 0.0;
			for (int n = 0; n < impulsesamples; n++)
			{
				c.impulse[n] -= (float)sum;
				sum -= dc;
			}
		}
	}


	static void SetupImpulse(EffectData* data, int numchannels, int blocksize, int samplerate, bool async = true)
    {
        data->mutex->Lock();
		// fetch sample number
        int usesample = (int)data->p[P_USESAMPLE];
        // return if the impulse has not been updated
		if ((int)data->lastparams[P_USESAMPLE] == usesample
			&& GetIRSample(usesample).updatecount == 0
			&& data->channels != NULL)
		{
			data->mutex->Unlock();
			return;
		}
		else
		{
			data->mutex->Unlock();
			// call upload only once
			MutexScopeLock mutexScope2(GetIRSample(usesample).sampleMutex);
			GetIRSample(usesample).updatecount = 0;
		}
		
		// upload the impulse in a separate thread
		if (async)
		{
			std::thread(irUpload_thread, std::ref(data), numchannels, blocksize, samplerate);
		}
		else
		{
			irUpload_thread(data, numchannels, blocksize, samplerate);
		}
		
    }


    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK CreateCallback(UnityAudioEffectState* state)
    {
        EffectData* data = new EffectData;
        memset(data, 0, sizeof(EffectData));
        data->mutex = new Mutex();
        state->effectdata = data;
        InitParametersFromDefinitions(InternalRegisterEffectDefinition, data->p);
		// Assuming stereo and 1024 sample block size, no async update
        SetupImpulse(data, 2, 1024, state->samplerate, false);
        return UNITY_AUDIODSP_OK;
    }

    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK ReleaseCallback(UnityAudioEffectState* state)
    {
        EffectData* data = state->GetEffectData<EffectData>();
        delete data->mutex;
        delete data;
        return UNITY_AUDIODSP_OK;
    }

    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK ProcessCallback(UnityAudioEffectState* state, float* inbuffer, float* outbuffer, unsigned int length, int inchannels, int outchannels)
    {
        EffectData* data = state->GetEffectData<EffectData>();

        // this should be done on a separate thread to avoid cpu spikes
        SetupImpulse(data, outchannels, (int)length, state->samplerate, false);

        // Lock data here in case float parameters are changed in pause/stopped mode and cause further calls to SetupImpulse
        MutexScopeLock mutexScope1(*data->mutex);

        int writeoffset; // set inside loop

        for (int i = 0; i < inchannels; i++)
        {
            Channel& c = data->channels[i];

            // feed new data to input buffer s
            float* s = c.s;
            const int mask = data->fftsize - 1;
            writeoffset = data->writeoffset;
            for (int n = 0; n < data->hopsize; n++)
            {
                s[writeoffset] = inbuffer[n * inchannels + i];
                writeoffset = (writeoffset + 1) & mask;
            }

            // calculate X=FFT(s)
			writeoffset = data->writeoffset;
            UnityComplexNumber* x = c.x[data->bufferindex];
            for (int n = 0; n < data->fftsize; n++)
            {
                x[n].Set(s[writeoffset], 0.0f);
				writeoffset = (writeoffset + 1) & mask;
            }
            FFT::Forward(x, data->fftsize, false);

			writeoffset = (writeoffset + data->hopsize) & mask;

            // calculate y=IFFT(sum(convolve(H_k, X_k), k=1..numpartitions))
            UnityComplexNumber* y = data->tmpoutput;
            memset(y, 0, sizeof(UnityComplexNumber) * data->fftsize);
            for (int k = 0; k < data->numpartitions; k++)
            {
                UnityComplexNumber* h = c.h[k];
                UnityComplexNumber* x = c.x[(k + data->bufferindex) % data->numpartitions];
                for (int n = 0; n < data->fftsize; n++)
					UnityComplexNumber::MulAdd(h[n], x[n], y[n], y[n]);
            }
            FFT::Backward(y, data->fftsize, false);

            // overlap-save readout
            for (int n = 0; n < data->hopsize; n++)
            {
                outbuffer[n * outchannels + i] = y[n].re;
            }
        }

		if (--data->bufferindex < 0)
		{
            data->bufferindex = data->numpartitions - 1;
		}

        data->writeoffset = writeoffset;

        return UNITY_AUDIODSP_OK;
    }

    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK SetFloatParameterCallback(UnityAudioEffectState* state, int index, float value)
    {
        EffectData* data = state->GetEffectData<EffectData>();
        if (index >= P_NUM)
            return UNITY_AUDIODSP_ERR_UNSUPPORTED;
        data->p[index] = value;
        return UNITY_AUDIODSP_OK;
    }

    UNITY_AUDIODSP_RESULT UNITY_AUDIODSP_CALLBACK GetFloatParameterCallback(UnityAudioEffectState* state, int index, float* value, char *valuestr)
    {
        EffectData* data = state->GetEffectData<EffectData>();
        if (index >= P_NUM)
            return UNITY_AUDIODSP_ERR_UNSUPPORTED;
        if (value != NULL)
            *value = data->p[index];
        if (valuestr != NULL)
            valuestr[0] = 0;
        return UNITY_AUDIODSP_OK;
    }

    int UNITY_AUDIODSP_CALLBACK GetFloatBufferCallback(UnityAudioEffectState* state, const char* name, float* buffer, int numsamples)
    {
        EffectData* data = state->GetEffectData<EffectData>();
        if (strncmp(name, "Impulse", 7) == 0)
        {
            SetupImpulse(data, data->numchannels, data->hopsize, data->samplerate, false);
			MutexScopeLock mutexScope(*data->mutex);
            int index = name[7] - '0';
            if (index >= data->numchannels)
                return UNITY_AUDIODSP_OK;
            const float* src = data->channels[index].impulse;
            float scale = (float)(data->hopsize * data->numpartitions - 2) / (float)numsamples;
            float prev_val = 0.0f, time_scale = 1.0f / scale;
            for (int n = 0; n < numsamples; n++)
            {
                // resample pre-integrated curve via box-filtering: f(x) = (F(x+dx)-F(x)) / dx
                float next_time = n * scale;
                int i = FastFloor(next_time);
                float next_val = src[i] + (src[i + 1] - src[i]) * (next_time - i);
                buffer[n] = (next_val - prev_val) * time_scale;
                prev_val = next_val;
            }
        }
        return UNITY_AUDIODSP_OK;
    }
}


// Upload sample to the plugin
extern "C" UNITY_AUDIODSP_EXPORT_API bool ConvolutionReverb_UploadSample(int index, float* data, int numsamples, int numchannels, int samplerate, const char* name)
{
	// Check the validity of the given index
	if (index < 0 || index >= ConvolutionReverb::MAXSAMPLE)
	{
        return false;
	}
	// Lock and fetch the data
	//MutexScopeLock mutexScope(ConvolutionReverb::sampleMutex);
	MutexScopeLock mutexScope(ConvolutionReverb::GetIRSample(index).sampleMutex);
    ConvolutionReverb::IRSample& s = ConvolutionReverb::GetIRSample(index);
	// Fast track if no parameters have changed
	s.impulse_upload_only = s.allocated && (s.numsamples == numsamples) && (s.numchannels == numchannels) && (s.samplerate == samplerate);
	if (s.impulse_upload_only)
	{
		// Copy the input data
		strcpy_s(s.name, name);
		memcpy(s.data, data, numsamples * numchannels * sizeof(float));
		s.updatecount = 1;
		return true;
	}
	// Resize the data storage if needed
	int num = numsamples * numchannels;
	bool needs_resize = (num != s.numsamples * s.numchannels);
	if (s.allocated && needs_resize)
	{
        delete[] s.data;
	}
    if (num > 0)
    {
		if (needs_resize)
		{
			s.data = new float[num];
			s.allocated = 1;
		}
		// Copy the input data
        strcpy_s(s.name, name);
        memcpy(s.data, data, numsamples * numchannels * sizeof(float));
    }
    else
    {
		// No data has been given, use dummy
        s.data = NULL;
        s.allocated = 1;
    }
	// fill the rest of the fields
    s.numsamples = numsamples;
    s.numchannels = numchannels;
    s.samplerate = samplerate;
	// Mark the data as updated
	s.updatecount = 1;
    return true;
}

extern "C" UNITY_AUDIODSP_EXPORT_API const char* ConvolutionReverb_GetSampleName(int index)
{
    if (index < ConvolutionReverb::MAXSAMPLE)
    {
        MutexScopeLock mutexScope(ConvolutionReverb::GetIRSample(index).sampleMutex);
        ConvolutionReverb::IRSample& s = ConvolutionReverb::GetIRSample(index);
        if (!s.allocated)
            return "Not set";
        return s.name;
    }

    return "Not set";
}
