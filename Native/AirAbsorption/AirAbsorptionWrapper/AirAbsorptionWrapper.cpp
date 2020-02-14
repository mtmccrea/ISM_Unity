#include <string>
#include "airAbsorptionProxy.h"
#include "AirAbsorptionWrapper.h"
#include "airAbsorptionProxy_terminate.h"
#include "airAbsorptionProxy_emxAPI.h"
#include "airAbsorptionProxy_initialize.h"

namespace  // anynomous
{

	void initOpType(std::string op_type_in, char (&data_out)[8], int (&data_size_out)[2])
	{
		data_size_out[0] = 1;
		data_size_out[1] = op_type_in.length();
		for (int i = 0; i < data_size_out[1]; ++i)
		{
			// copy string contents
			data_out[i] = op_type_in[i];
		}
	}
	

	//emxArray_real32_T *initIRIn(const float*<float>& ir_in)
	emxArray_real32_T *initIRIn(const float* ir_in, int in_length)
	{
		emxArray_real32_T *result;
		//int iv0[1] = { ir_in.length() };
		int iv0[1] = { in_length };
		// Set the size of the array.
		result = emxCreateND_real32_T(1, *(int(*)[1])&iv0[0]);
		// Loop over the array to initialize each element.
		for (int i = 0; i < result->size[0U]; ++i) 
		{
			// Set the value of the array element.
			result->data[i] = ir_in[i];
		}
		return result;
	}


	emxArray_real32_T *initEmptyIR()
	{
		emxArray_real32_T *result;
		int iv0[1] = { 1 };
		// Set the size of the array.
		result = emxCreateND_real32_T(1, *(int(*)[1])&iv0[0]);
		// Set the value of the array element.
		result->data[0] = 0;
		return result;
	}

}  // anynomous namespace




int AirAbsorption_Initialize()
{
	int retval = 0;
	try
	{
		airAbsorptionProxy_initialize();
	}
	catch (int e)
	{
		retval = e;
	}
	return retval;
}


int AirAbsorption_SetFs(unsigned int fs)
{
	int retval = 0;
	try
	{
		std::string op_type = "setfs";

		// initialize MATLAB variables
		// Init output
		emxArray_real32_T *emx_ir_out;
		emxInitArray_real32_T(&emx_ir_out, 1);
		// Init op_type
		char op_type_data[8];
		int op_type_size[2];
		initOpType(op_type, op_type_data, op_type_size);
		// Init fs
		unsigned int fs_in = fs;
		// Init ir_in
		emxArray_real32_T *emx_ir_in;
		emx_ir_in = initEmptyIR();

		// Call the entry-point 'airAbsorptionProxy'.
		airAbsorptionProxy(op_type_data, op_type_size, fs_in, emx_ir_in, emx_ir_out);


		// destroy emx arrays
		emxDestroyArray_real32_T(emx_ir_out);
		emxDestroyArray_real32_T(emx_ir_in);
	}
	catch (int e)
	{
		retval = e;
	}
	return retval;
}


//int AirAbsorption_Apply(float*Raw* ir_in, float*Raw* ir_out)
// NO double-star (fails otherwise)
int AirAbsorption_Apply(float* ir_in, int in_count, float* ir_out, int* out_count)
{
	int retval = 0;
	try 
	{
		//float*<float> mono_ir_in = float*<float>(ir_in);
		//float*<float> mono_ir_out = float*<float>(ir_out);
		std::string op_type = "apply";

		// initialize MATLAB variables
		// Init output
		emxArray_real32_T *emx_ir_out;
		emxInitArray_real32_T(&emx_ir_out, 1);
		// Init op_type
		char op_type_data[8];
		int op_type_size[2];
		initOpType(op_type, op_type_data, op_type_size);
		// Init fs
		unsigned int fs_in = 0;
		// Init ir_in
		emxArray_real32_T *emx_ir_in;
		//emx_ir_in = initIRIn(mono_ir_in);
		emx_ir_in = initIRIn(ir_in, in_count);

		// Call the entry-point 'airAbsorptionProxy'.
		airAbsorptionProxy(op_type_data, op_type_size, fs_in, emx_ir_in, emx_ir_out);

		// Write output data back to the output
		for (int i = 0; i < emx_ir_out->size[0]; ++i)
		{
			ir_out[i] = emx_ir_out->data[i];
		}
		*out_count = emx_ir_out->size[0];

		// destroy emx arrays
		emxDestroyArray_real32_T(emx_ir_out);
		emxDestroyArray_real32_T(emx_ir_in);
	}
	catch (int e)
	{
		retval = e;
	}
	return retval;
}


int AirAbsorption_Terminate()
{
	int retval = 0;
	try
	{
		airAbsorptionProxy_terminate();
	}
	catch (int e)
	{
		retval = e;
	}
	return retval;
}
