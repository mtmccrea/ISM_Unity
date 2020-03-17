using System.Runtime.InteropServices;
using UnityEngine;


/// <summary>
/// An interface class to the AirAbsorptionWrapper library
/// </summary>
[RequireComponent(typeof(ISMRenderSettings))]
public class ISMAirAbsorption : MonoBehaviour {

    /// <summary>
    /// Initialize the library
    /// </summary>
    /// <returns>0 on a success, nonzero otherwise.</returns>
    [DllImport("libAirAbsorptionWrapper")]
    private static extern int AirAbsorption_Initialize();


    /// <summary>
    /// Set the sampling frequency of the library
    /// </summary>
    /// <param name="fs">sampling frequency</param>
    /// <returns>0 on a success, nonzero otherwise.</returns>
    [DllImport("libAirAbsorptionWrapper")]
    private static extern int AirAbsorption_SetFs(uint fs);


    /// <summary>
    /// Apply the air absorption to the given impulse response
    /// </summary>
    /// <param name="ir_in">The impulse response being filtered</param>
    /// <param name="in_count">The length of ir_in</param>
    /// <param name="ir_out">The filtered impulse response</param>
    /// <param name="out_count">
    /// The actual length of the filtered response
    /// </param>
    /// <returns>0 on a success, nonzero otherwise.</returns>
    [DllImport("libAirAbsorptionWrapper")]
    private static extern int AirAbsorption_Apply([In] float[] ir_in, 
                                                  int in_count, 
                                                  [Out] float[] ir_out, 
                                                  ref int out_count);


    /// <summary>
    /// Close the library and free the resources it uses
    /// </summary>
    /// <returns>An int to indicate whether the operation succeeded</returns>
    [DllImport("libAirAbsorptionWrapper")]
    private static extern int AirAbsorption_Terminate();


    /// <summary>
    /// Indicates whether the library has been initialized
    /// </summary>
    bool is_initialized = false;

    /// <summary>
    /// Indicates whether the sampling frequency has been set for the library
    /// </summary>
    bool fs_is_set = false;

    /// <summary>
    /// Indicates whether the effect should be bypassed due to a missing library
    /// </summary>
    bool bypass = false;


    /// <summary>
    /// Display a bypass message and deactivate
    /// </summary>
    void Bypass()
    {
        bypass = true;
        Debug.LogWarning("Air Absorption library not found! Effect is being bypassed.");
        // Disable air absorption
        ISMRenderSettings settings = GetComponent<ISMRenderSettings>();
        settings.ApplyAirAbsorption = false;
    }


    // Called before any Start functions
    private void Awake()
    {
        // Initialize Air Absorption library
        if (!is_initialized)
        {
            int retval = 0;
            try
            {
                retval = AirAbsorption_Initialize();
            }
            catch (System.DllNotFoundException)
            {
                Bypass();
                return;
            }
            Debug.Assert(retval == 0);
            is_initialized = true;
            SetFs((uint)AudioSettings.outputSampleRate);
        }
    }


    // Called when the object is disabled
    private void OnDisable()
    {
        // Close Air Absorption library
        if (is_initialized)
        {
            int retval = AirAbsorption_Terminate();
            Debug.Assert(retval == 0);
            is_initialized = false;
            fs_is_set = false;
        }
    }


    /// <summary>
    /// Set the sampling frequency of the Air Absorption library.
    /// </summary>
    /// <param name="fs"></param>
    public void SetFs(uint fs)
    {
        // Set the sampling frequency of the Air Absorption library
        if (!is_initialized)
        {
            Debug.LogError("SetFs was called before initialization!");
        }
        int retval = AirAbsorption_SetFs(fs);
        Debug.Assert(retval == 0);
        fs_is_set = true;
    }


    /// <summary>
    /// Apply air absorption on the given impulse response
    /// </summary>
    /// <param name="ir">An impulse response to be filtered</param>
    /// <returns>The filtered impulse response.</returns>
    public float[] Apply(float[] ir)
    {
        if (bypass)
        {
            Bypass();
            return ir;
        }
        // Check that the library has been initialized
        if (!fs_is_set)
        {
            Debug.LogError("Apply was called before fs was set!");
        }
        //// Create an array for the output
        //float[] ir_out = new float[ir.Length];
        // Call the library
        int out_count = 0;
        int retval = AirAbsorption_Apply(ir, ir.Length, ir, ref out_count);
        float[] ir_out = ir;
        // Check the output validity
        if (out_count != ir_out.Length)
        {
            Debug.LogWarning(
                "Output buffer size is different (" + ir_out.Length.ToString()
                + " set, " + out_count.ToString() + "received, difference " 
                + (ir_out.Length - out_count).ToString() + ")");
        }
        Debug.Assert(retval == 0);
        return ir_out;
    }

}
