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
    // === STRUCTS ===

    /// <summary>
    /// A container struct for image source data
    /// </summary>
    public struct ImageSource
    {
        /// <summary>
        /// Normal of the mirroring plane of the image source
        /// </summary>
        public Vector3 n;
        
        /// <summary>
        /// A constant value from the plane equation Ax + By + Cz + D = 0. Can
        /// also be thought as a dot product between the normal and the known 
        /// point on the plane, i.e. Vector3.Dot(p0, n)
        /// </summary>
        public float D;

        /// <summary>
        /// Position of the image source
        /// </summary>
        public Vector3 pos;

        /// <summary>
        /// Index of the parent image source
        /// </summary>
        public int i_parent;

        /// <summary>
        /// A main constructor, use this for image sources.
        /// </summary>
        /// <param name="pos_in">Position of the image source</param>
        /// <param name="p0_in">A point in the mirroring plane</param>
        /// <param name="n_in">Normal of the mirroring plane</param>
        /// <param name="i_parent_in">Index of the parent (-1 if root)</param>
        public ImageSource(Vector3 pos_in, Vector3 p0_in, Vector3 n_in, int i_parent_in = -1)
        {
            pos = pos_in;
            D = Vector3.Dot(p0_in, n_in);
            n = n_in;
            i_parent = i_parent_in;
        }


        /// <summary>
        /// A constructor for the real source.
        /// </summary>
        /// <param name="pos_in">Position of the real source</param>
        public ImageSource(Vector3 pos_in)
        {
            pos = pos_in;
            D = 0;
            n = Vector3.zero;
            i_parent = -1;
        }
    }


    /// <summary>
    /// A container struct for ray paths
    /// </summary>
    public struct RaycastHitPath
    {
        /// <summary>
        /// List of ray wall hit points in the path
        /// </summary>
        public List<Vector3> points;

        /// <summary>
        /// Total path length
        /// </summary>
        public float totalPathLength;


        /// <summary>
        /// A constructor.
        /// </summary>
        /// <param name="pathLength">Length of the path</param>
        public RaycastHitPath(float pathLength)
        {
            points = new List<Vector3>();
            totalPathLength = pathLength;
        }
    }


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
    /// Current image sources
    /// </summary>
    public List<ImageSource> imageSources = new List<ImageSource>();

    /// <summary>
    /// Valid ray hit paths between the source and the listener
    /// </summary>
    public List<RaycastHitPath> hitPaths = new List<RaycastHitPath>();


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


    // === PRIVATE ATTRIBUTES ===

    /// <summary>
    /// The full impulse response
    /// </summary>
    float[] ir;

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
        // Set up Convolution Reverb
        float fParamIdx;
        renderSettings.mixer.GetFloat(mixerGroupName, out fParamIdx);
        paramIdx = (int)fParamIdx;
        ConvolutionReverbInput.UploadSample(ir, paramIdx, name);
        nextSync = AudioSettings.dspTime;
        // Select random vectors as placeholders for the old positions
        oldSourcePosition = Random.insideUnitSphere * 1e3f;
    }


    // Update is called once per frame
    void Update()
    {
        // Check if either the source or the listener has moved or the 
        // simulation parameters have changed
        if (LocationsHaveChanged() || renderSettings.IRUpdateRequested)
        {
            // Calculate ISM impulse response
            ISMUpdate();
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
    /// Apply Image Source Method (ISM) to calculate specular reflections
    /// </summary>
    void ISMUpdate()
    {
        // Clear old data
        hitPaths.Clear();
        System.Array.Clear(ir, 0, ir.Length);
        // Check if the image source positions must be updated
        if (SourceHasMoved() || renderSettings.IRUpdateRequested)
        {
            // Clear old image sources
            imageSources.Clear();
            // === E1: Add direct sound ===
            // Add the original source to the image sources list
            // (E1) YOUR CODE HERE

            // retrieve position of the loudspeaker object
            imageSources.Add(new ImageSource(SourcePosition));

            // For each order of reflection
            int i_begin;
            int i_end = 0;

            for (var i_refl = 0; i_refl < renderSettings.NumberOfISMReflections; ++i_refl)
            {
                // === E4: Higher order reflections ===
                // (E4) YOUR CODE HERE: Update parent interval
                i_begin = i_end;
                i_end = imageSources.Count;
                // For each parent to reflect
                // for (var i_parent = i_begin)
                for (var i_parent = i_begin; i_parent < i_end; ++i_parent) // <-- (E4) YOUR CODE HERE
                {
                    // === E2: Calculate image source positions ===
                    // Parent source on this iteration
                    ImageSource parentSource = imageSources[i_parent];
                    // For each mirroring plane
                    for (var i_child = 0; 
                         i_child < renderSettings.PlaneCenters.Length; 
                         ++i_child)
                    {
                        // Get the current mirroring plane
                        Vector3 p_plane = renderSettings.PlaneCenters[i_child];
                        Vector3 n_plane = renderSettings.PlaneNormals[i_child];
                        // (E2) YOUR CODE HERE: calculate the distance from the plane to the source
                        float sourcePlaneDistance = Vector3.Dot(parentSource.pos-p_plane,n_plane);

                        // Is the parent source in front of the plane?
                        if (sourcePlaneDistance > 0.0) 
                        {
                            // Parent source is in front of the plane, calculate mirrored position
                            Vector3 N = transform.TransformDirection(n_plane);
                            //Vector3 mirroredPosition = parentSource.pos - 2 * transform.TransformDirection(n_plane) * sourcePlaneDistance; 
                            //Vector3 mirroredPosition = parentSource.pos - 2 * N * sourcePlaneDistance / Vector3.Dot(N,N);
                            Vector3 mirroredPosition = parentSource.pos - 2 * n_plane * sourcePlaneDistance;
                            // Add the image source
                            // (E2) YOUR CODE HERE
                            imageSources.Add(new ImageSource(mirroredPosition, p_plane, n_plane, i_parent));
                        }
                    }
                }
            }
        }
        // === E3: Cast rays ===
        // A mask for game objects using ISMCollider
        int ism_colliders_only = LayerMask.GetMask("ISM colliders");
        // For each image source
        for (var i = 0; i < imageSources.Count; ++i)
        {
            // Calculate path length
            float pathLength = Vector3.Distance(imageSources[i].pos, ListenerPosition);
                        
            // Check that the path can contribute to the impulse response
            if (pathLength < renderSettings.MaximumRayLength)
            {
                // Create a container for this path
                RaycastHitPath path = new RaycastHitPath(pathLength);
                // (E3) YOUR CODE HERE: Set the listener as the starting point for
                // the ray
                Vector3 origin = ListenerPosition;
                Vector3 originNormal = imageSources[i].pos - origin;
                int i_next = i;
                bool isValidPath = true;
                
                // Loop until we have either processed the original source or 
                // found the path invalid
                while (i_next != -1 && isValidPath)
                {
                    // Get the current source
                    ImageSource imageSource = imageSources[i_next];
                    // (E3) YOUR CODE HERE: Determine ray direction and length
                    Vector3 dir = originNormal;
                    float max_length = Vector3.Distance(origin, imageSource.pos);
                    // Trace the ray
                    RaycastHit hit;
                    // First, check that the outgoing ray is reflected from the wall
                    if (!Physics.Raycast(origin, dir, out hit, max_length, ism_colliders_only))
                    {
                        // The ray is sent in the wall, so the path is invalid
                        isValidPath = false;
                        Debug.Log("Invalid Path - no wall collision found");
                    }
                    else if (imageSource.i_parent == -1)
                    {
                        Debug.Log("Source hit distance: " + hit.distance);
                        // Handle the real source
                        // (E3) YOUR CODE HERE: check that the path is not obstructed                        
                        if (Mathf.Abs(max_length - hit.distance) < 0.2) {
                            isValidPath = true;
                            Debug.Log("Path to real source VALID");
                        } else {
                            isValidPath = false;
                            Debug.Log(max_length);
                            Debug.Log(hit.distance);
                        }
                        Debug.Log("Path to source");
                    }
                    else
                    {
                        // Handle image sources
                        // (E3) YOUR CODE HERE: check that the ray hits a wall on mirroring plane
                        isValidPath = ISMMath.PlaneEQ(hit, imageSource);
                        // Debug.Log("Path to wall");
                    }
                    // Add the traced path if it is still valid
                    if (isValidPath)
                    {
                        // Path is valid, add the hit point to the ray path
                        // (E3) YOUR CODE HERE
                        path.points.Add(hit.point);
                        // Prepare to send the ray towards the next image source
                        i_next = imageSource.i_parent;
                        origin = hit.point;
                        if (i_next != -1) originNormal = imageSources[i_next].pos-origin;
                    }
                }
                // Add the traced path if it is still valid
                if (isValidPath)
                {
                    // (E3) YOUR CODE HERE
                    hitPaths.Add(path);
                    Debug.Log("Path added");
                }
            }
        }
        // === E5: create image source impulse response ===
        foreach (var path in hitPaths)
        {
            // Calculate the sample that the ray path contributes to
            int i_path = Mathf.RoundToInt( // <-- (E5) YOUR CODE HERE
                AudioSettings.outputSampleRate * path.totalPathLength / ISMRenderSettings.speedOfSound
                ); 
            Debug.Log("impulse path " + i_path);
            if (i_path < ir.Length)
            {
                // (E5) YOUR CODE HERE: Determine the signal magnitude  w.r.t.
                // the amount of wall hits in the path
                ir[i_path] += Mathf.Pow(
                    (1 - renderSettings.Absorption) * (1 - renderSettings.DiffuseProportion), 
                    path.points.Count / 2
                ) / (path.totalPathLength + float.Epsilon)
                * Mathf.Pow(-1, path.points.Count); // flip phase on every reflection
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
