using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;


/// <summary>
/// Apply reverberation on AudioSource by using Image Source Method and ray 
/// tracing
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ISMReverb : MonoBehaviour
{
    // === PUBLIC ATTRIBUTES ===

    /// <summary>
    /// Name of the audio mixer group on which the audio source connects to
    /// </summary>
    public string mixerGroupName;

    /// <summary>
    /// The time interval between two consecutive impulse response updates 
    /// (in seconds)
    /// </summary>
    public double updateInterval = 1.0;

    /// <summary>
    /// Should we accumulate the raycast result over time
    /// </summary>
    public bool accumulateResults = true;


    // === PUBLIC PROPERTIES ===

    /// <summary>
    /// Position of the audio source
    /// </summary>
    public Vector3 SourcePosition
    {
        get { return source.transform.position; }
        set { source.transform.position = value; }
    }


    /// <summary>
    /// Position of the audio listener
    /// </summary>
    public Vector3 ListenerPosition
    {
        get { return renderSettings.ListenerPosition; }
    }


    /// <summary>
    /// Access to the generated impulse response
    /// </summary>
    public float[] IR
    {
        get { return ir; }
    }
    

    /// <summary>
    /// Access to the global render settings
    /// </summary>
    public ISMRenderSettings RenderSettings
    {
        get { return renderSettings; }
    }


    /// <summary>
    /// An ISM renderer of this reverb
    /// </summary>
    public ISMEngine.ISMEngine ism;


    // === PRIVATE ATTRIBUTES ===

    /// <summary>
    /// The full impulse response
    /// </summary>
    float[] ir;

    /// <summary>
    /// Impulse response created by raycasting
    /// </summary>
    float[] ir_raycast;

    /// <summary>
    /// The number of rays on a particular sample
    /// </summary>
    uint[] raycast_counts;

    /// <summary>
    /// A piece of white noise to be modulated with the raycast result
    /// </summary>
    float[] ir_noise;

    /// <summary>
    /// The source on which the reverb is applied
    /// </summary>
    AudioSource source;

    /// <summary>
    /// Position of the source in the previous frame
    /// </summary>
    Vector3 oldSourcePosition;

    /// <summary>
    /// A reference to the renderSettings script
    /// </summary>
    ISMRenderSettings renderSettings;


    /// <summary>
    /// Index of the impulse response slot in the convolution reverb plugin
    /// </summary>
    int paramIdx;

    /// <summary>
    /// The time when the impulse response is synced next time
    /// </summary>
    double nextSync;


    // === METHODS ===
    
    // Use this for initialization
    void Start()
    {
        renderSettings = FindObjectOfType<ISMRenderSettings>();
        // Plug the associated audio source to the given mixer group
        source = GetComponent<AudioSource>();
        AudioMixerGroup mixerGroup = 
            renderSettings.mixer.FindMatchingGroups(mixerGroupName)[0];
        source.outputAudioMixerGroup = mixerGroup;
        // initialize impulse responses
        var irNumSamples = Mathf.CeilToInt(
            renderSettings.IRLength * AudioSettings.outputSampleRate);
        ir = new float[irNumSamples];
        ir[0] = 1.0f;
        ir_raycast = new float[irNumSamples];
        raycast_counts = new uint[irNumSamples];
        ir_noise = new float[irNumSamples];
        for (int i = 0; i < ir_noise.Length; ++i)
        {
            ir_noise[i] = Random.Range(-1.0f, 1.0f);
        }
        // Set up Convolution Reverb
        float fParamIdx;
        renderSettings.mixer.GetFloat(mixerGroupName, out fParamIdx);
        paramIdx = (int)fParamIdx;
        ConvolutionReverbInput.UploadSample(ir, paramIdx, name);
        nextSync = AudioSettings.dspTime;
        // Select random vectors as placeholders for the old positions
        oldSourcePosition = Random.insideUnitSphere * 1e3f;
        ism = new ISMEngine.ISMEngine(renderSettings.IRLength);
    }


    // Update is called once per frame
    void Update()
    {
        // Check if either the source or the listener has moved or the 
        // simulation parameters have changed
        if (LocationsHaveChanged() || renderSettings.IRUpdateRequested)
        {
            // Calculate ISM impulse response
            ism.Update(SourcePosition, 
                       renderSettings.ListenerPosition,
                       renderSettings.PlaneCenters,
                       renderSettings.PlaneNormals,
                       renderSettings.NumberOfISMReflections,
                       renderSettings.MaximumRayLength,
                       ISMRenderSettings.speedOfSound,
                       renderSettings.Absorption,
                       renderSettings.DiffuseProportion,
                       renderSettings.UseISM,
                       SourceHasMoved() || renderSettings.IRUpdateRequested);
        }
        // Update raycast impulse response
        RaycastUpdate(renderSettings.RaycastTimeBudget);
        // Combine the ISM and raycast impulse responses
        for (var i = 0; i < ir.Length; ++i)
        {
            ir[i] = ism.IR[i];  // <-- (E5) YOUR CODE HERE: add the contribution of the ray tracer here
        }
        // Check if it is time to update the impulse response
        if (AudioSettings.dspTime > nextSync)
        {
            // Upload the final impulse response
            ConvolutionReverbInput.UploadSample(ir, paramIdx, name);
            nextSync = nextSync + updateInterval;
        }
        // Update old source position
        oldSourcePosition = SourcePosition;
    }


    


    /// <summary>
    /// Cast rays from source to the listener in order to simulate diffuse 
    /// field
    /// </summary>
    /// <param name="timeLimit">
    /// The amount of time allocated for raycasting in this frame
    /// </param>
    void RaycastUpdate(double timeLimit)
    {
        // Clear the raycast result if changes have made (or accumulation is 
        // not used)
        if (!accumulateResults
            || LocationsHaveChanged()
            || renderSettings.IRUpdateRequested)
        {
            System.Array.Clear(ir_raycast, 0, ir_raycast.Length);
            System.Array.Clear(raycast_counts, 0, raycast_counts.Length);
        }
        // Skip if not in use
        if (!renderSettings.UseRaycast)
        {
            return;
        }
        // A mask for game objects using ISMCollider
        int ism_colliders_only = LayerMask.GetMask("ISM colliders");
        // Start the raytracer loop
        double stopTime = AudioSettings.dspTime + timeLimit;
        Vector3 listenerPosition = renderSettings.ListenerPosition;
        while (AudioSettings.dspTime < stopTime)
        {
            // === E1: Cast a new ray towards random direction ===
            // (E1) YOUR CODE HERE: initialize ray starting position and direction
            //Vector3 pos = ...
            //Vector3 dir = ...
            // Intensity of the current ray
            float energy = 1.0f;
            // Current and remaining ray path lengths
            float pathLength = 0.0f;
            float remainingPathLength = renderSettings.MaximumRayLength;
            // The number of times that the ray has hit a surface
            int n_hit = 0;
            // Result of the raycast
            RaycastHit hit;
            // Cast a ray
            while (AudioSettings.dspTime < stopTime
                   && true /* <-- (E1) YOUR CODE HERE */)
            {
                // (E1) YOUR CODE HERE: Gather hit info
                //pos = ...
                //Vector3 surfaceNormal = ...
                //pathLength += ...
                //remainingPathLength -= ...

                // === E2: Calculate current energy ===
                // If the ISM reflections are still considered, take only the 
                // diffuse part
                if (n_hit++ < renderSettings.NumberOfISMReflections)
                {
                    energy *= 1; // <-- (E2) YOUR CODE HERE
                }
                // Calculate absorption
                energy *= 1;  // <-- (E2) YOUR CODE HERE
                // === E3: Add the contribution of the ray ===
                // Is there anything between the hit position and the listener
                if (true /* <-- (E3) YOUR CODE HERE */)
                {
                    // (E3) YOUR CODE HERE: Calculate the index of the ray in the impulse response
                    //float ray_length = ...
                    int i_ir = 0;
                    if (i_ir < ir.Length)
                    {
                        // (E3) YOUR CODE HERE: Add the contribution
                        //ir_raycast[i_ir] = ...
                        // Increment the corresponding ray counter
                        ++raycast_counts[i_ir];
                    }
                }
                // === E4: select new direction of propagation ===
                // (E4) YOUR CODE HERE
                //dir = ...
            }
        }
    }


    /// <summary>
    /// Check whether either the source or the listener has moved
    /// </summary>
    /// <returns>True if either the listener or this source has moved, false otherwise.</returns>
    bool LocationsHaveChanged()
    {
        return renderSettings.ListenerHasMoved() || SourceHasMoved();
    }


    /// <summary>
    /// Check whether the source has moved after the last update
    /// </summary>
    /// <returns>True if the source has moved, false otherwise.</returns>
    public bool SourceHasMoved()
    {
        return !ISMMath.PositionEQ(SourcePosition, oldSourcePosition);
    }

}
