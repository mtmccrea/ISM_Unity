using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Audio;


/// <summary>
/// A container for the ISM render data shared between different sources
/// </summary>
[RequireComponent(typeof(ISMAirAbsorption))]
public class ISMRenderSettings : MonoBehaviour {

    /// <summary>
    /// Audio listener object
    /// </summary>
    public AudioListener listener;

    /// <summary>
    /// The location of the listener in the previous frame
    /// </summary>
    Vector3 oldListenerPos;

    /// <summary>
    /// An interface to the air absorption library
    /// </summary>
    public ISMAirAbsorption airAbsorption;

    /// <summary>
    /// Mixer containing the convolution reverb mixer groups
    /// </summary>
    public AudioMixer mixer;

    /// <summary>
    /// Do we use Image Source Method or not
    /// </summary>
    [SerializeField]
    private bool useISM = true;

    /// <summary>
    /// Do we use raycasting or not
    /// </summary>
    [SerializeField]
    private bool useRaycast = true;

    /// <summary>
    /// Should we apply air absorption to the impulse response or not
    /// </summary>
    [SerializeField]
    private bool applyAirAbsorption = true;

    /// <summary>
    /// The absorption of the walls in the room being simulated
    /// </summary>
    [SerializeField]
    private float absorption = 0.8f;

    /// <summary>
    /// The proportion of energy that scatters diffusely from the surface
    /// </summary>
    [SerializeField]
    private float diffuseProportion = 0.2f;

    /// <summary>
    /// number of reflections simulated with Image Source Method
    /// </summary>
    [SerializeField]
    private int nISMReflections = 2;

    /// <summary>
    /// The length of the impulse response (in seconds)
    /// </summary>
    [SerializeField]
    private float irLength = 1.0f;

    /// <summary>
    /// The desired time-per-frame in seconds
    /// </summary>
    [SerializeField]
    private double targetTime = 0.016;

    /// <summary>
    /// The amount of time that has been reserved for the raycaster
    /// </summary>
    double raycastTimeBudget;

    struct MirroringPlaneArray
    {

        public Vector3[] pos;

        public Vector3[] n;

        /// <summary>
        /// The colliders that are used for reverberation
        /// </summary>
        ISMCollider[] roomColliders;

        int[] startIdx;

        public void Construct(ISMCollider[] roomColliders_in)
        {
            roomColliders = roomColliders_in;
            startIdx = new int[roomColliders.Length];
            int count = 0;
            for (var i = 0; i < roomColliders.Length; ++i)
            {
                roomColliders[i].CalculateMirrorPlanes();
                startIdx[i] = count;
                count += roomColliders[i].planeCenters.Count;
            }
            pos = new Vector3[count];
            n = new Vector3[count];
            for (var i = 0; i < roomColliders.Length; ++i)
            {
                Array.Copy(roomColliders[i].planeCenters.ToArray(), 
                           0, 
                           pos, 
                           startIdx[i], 
                           roomColliders[i].planeCenters.Count);
                Array.Copy(roomColliders[i].planeNormals.ToArray(),
                           0,
                           n,
                           startIdx[i],
                           roomColliders[i].planeNormals.Count);
            }
        }


        public bool Refresh()
        {
            bool updateRequested = false;
            for (var i = 0; i < roomColliders.Length; ++i)
            {
                if (roomColliders[i].transform.hasChanged)
                {
                    roomColliders[i].CalculateMirrorPlanes();
                    Array.Copy(roomColliders[i].planeCenters.ToArray(),
                               0,
                               pos,
                               startIdx[i],
                               roomColliders[i].planeCenters.Count);
                    Array.Copy(roomColliders[i].planeNormals.ToArray(),
                               0,
                               n,
                               startIdx[i],
                               roomColliders[i].planeNormals.Count);
                    updateRequested = true;
                    roomColliders[i].transform.hasChanged = false;
                }
            }
            return updateRequested;
        }

    };

    MirroringPlaneArray mirroringPlaneArray = new MirroringPlaneArray();

    /// <summary>
    /// The number of reverberated sound sources in the scene
    /// </summary>
    int nReverbs;

    /// <summary>
    /// Indicates if any simulation critical value has been changed during the
    /// frame
    /// </summary>
    bool simulationValueChanged = false;

    /// <summary>
    /// Indicates if the impulse responses of the reverberators should be
    /// recalculated
    /// </summary>
    bool updateRequested = true;

    /// <summary>
    /// Speed of sound (m/s)
    /// </summary>
    public const float speedOfSound = 343.15f;

    /// <summary>
    /// Pi divided by two
    /// </summary>
    public const float kPIdiv2 = Mathf.PI / 2;


    /// <summary>
    /// Indicates whether the impulse responses should be updated
    /// </summary>
    public bool IRUpdateRequested
    {
        get { return updateRequested; }
    }
    

    /// <summary>
    /// Get listener position in world space
    /// </summary>
    public Vector3 ListenerPosition
    {
        get { return listener.transform.position; }
    }


    /// <summary>
    /// Maximum length of the ray that still contributes to the impulse
    /// response
    /// </summary>
    public float MaximumRayLength
    {
        get { return IRLength * speedOfSound; }
    }


    /// <summary>
    /// The total time one raycaster is allowed to spend in one frame
    /// </summary>
    public double RaycastTimeBudget
    {
        get { return raycastTimeBudget; }
    }


    /// <summary>
    /// Indicates whether Image Source Method should be used in the simulation 
    /// or not
    /// </summary>
    public bool UseISM
    {
        get { return useISM; }

        set
        {
            if (useISM != value)
            {
                simulationValueChanged = true;
            }
            useISM = value;
        }
    }


    /// <summary>
    /// Indicates whether raycasting should be used in the simulation or not
    /// </summary>
    public bool UseRaycast
    {
        get { return useRaycast; }

        set
        {
            if (useRaycast != value)
            {
                simulationValueChanged = true;
            }
            useRaycast = value;
        }
    }


    /// <summary>
    /// Indicates whether air absorption should be used in the simulation or
    /// not
    /// </summary>
    public bool ApplyAirAbsorption
    {
        get { return applyAirAbsorption; }

        set
        {
            if (applyAirAbsorption != value)
            {
                simulationValueChanged = true;
            }
            applyAirAbsorption = value;
        }
    }


    /// <summary>
    /// The amount of absorption of the walls
    /// </summary>
    public float Absorption
    {
        get { return absorption; }

        set
        {
            if (absorption != value)
            {
                simulationValueChanged = true;
            }
            absorption = Mathf.Clamp01(value);
        }
    }


    public float DiffuseProportion
    {
        get { return diffuseProportion; }

        set
        {
            if (absorption != value)
            {
                simulationValueChanged = true;
            }
            diffuseProportion = Mathf.Clamp01(value);
        }
    }


    /// <summary>
    /// The number of reflections simulated with Image Source Method
    /// </summary>
    public int NumberOfISMReflections
    {
        get { return nISMReflections; }

        set
        {
            if (nISMReflections != value)
            {
                simulationValueChanged = true;
            }
            nISMReflections = Mathf.Abs(value);
        }
    }


    /// <summary>
    /// The length of the impulse response in seconds
    /// </summary>
    public float IRLength
    {
        get { return irLength; }

        set
        {
            if (irLength != value)
            {
                simulationValueChanged = true;
            }
            irLength = value;
        }
    }


    /// <summary>
    /// The desired frame rate for the simulation
    /// </summary>
    public double TargetFPS
    {
        get
        {
            return 1.0 / targetTime;
        }

        set
        {
            targetTime = 1.0 / value;
        }
    }


    public Vector3[] PlaneCenters
    {
        get { return mirroringPlaneArray.pos; }
    }


    public Vector3[] PlaneNormals
    {
        get { return mirroringPlaneArray.n; }
    }


    // Called before any Start functions
    void Awake()
    {
        nReverbs = FindObjectsOfType<ISMReverb>().Length;
        // Enable application running in background (hangs otherwise!)
        Application.runInBackground = true;
        // set old position different from the current listener position
        oldListenerPos = listener.transform.position + Vector3.up;
        mirroringPlaneArray.Construct(FindObjectsOfType<ISMCollider>());
        // set initial time budget for raycasting
        raycastTimeBudget = targetTime / nReverbs;
    }


    private void FixedUpdate()
    {
        // Check if the room has changed
        updateRequested |= mirroringPlaneArray.Refresh();
    }


    // Called after all Update calls have been finished
    void LateUpdate()
    {
        // Reset update request flag
        updateRequested = false;
        // Update old listener position
        oldListenerPos = ListenerPosition;
        // Check if the user has changed any render parameters
        if (simulationValueChanged)
        {
            updateRequested = true;
            simulationValueChanged = false;
        }
        // Update raycast time budget
        double t = Time.deltaTime - (raycastTimeBudget * nReverbs);
        raycastTimeBudget = (double)Mathf.Max((float)(targetTime - t) / nReverbs, 0.001f);
    }


    /// <summary>
    /// Check whether the listener has moved after the last update
    /// </summary>
    /// <returns>True if the listener has moved, false otherwise.</returns>
    public bool ListenerHasMoved()
    {
        return !ISMMath.PositionEQ(ListenerPosition, oldListenerPos);
    }
}
