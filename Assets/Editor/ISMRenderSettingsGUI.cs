using UnityEngine;
using UnityEngine.Audio;
using UnityEditor;


/// <summary>
/// A custom inspector interface for ISMRenderSettings script
/// </summary>
[CustomEditor(typeof(ISMRenderSettings))]
public class ISMRenderSettingsGUI : Editor
{
    public override void OnInspectorGUI()
    {
        // Get the object behind the GUI
        ISMRenderSettings targetScript = (ISMRenderSettings)target;
        // General settings
        EditorGUILayout.LabelField("General");
        targetScript.listener =
            (AudioListener)EditorGUILayout.ObjectField("Audio Listener",
                                                       targetScript.listener,
                                                       typeof(AudioListener),
                                                       true);
        targetScript.mixer =
            (AudioMixer)EditorGUILayout.ObjectField("Master Mixer",
                                                    targetScript.mixer,
                                                    typeof(AudioMixer),
                                                    false);
        // Simulation settings
        EditorGUILayout.LabelField("Simulation");
        targetScript.Absorption =
            EditorGUILayout.FloatField("Absorption of walls",
                                       targetScript.Absorption);
        targetScript.DiffuseProportion =
            EditorGUILayout.FloatField("Diffuse energy proportion",
                                       targetScript.DiffuseProportion);
        targetScript.IRLength =
            EditorGUILayout.FloatField("IR Length", targetScript.IRLength);
        targetScript.UseISM =
            EditorGUILayout.Toggle("Use ISM", targetScript.UseISM);
        if (targetScript.UseISM)
        {
            EditorGUI.indentLevel++;
            targetScript.NumberOfISMReflections =
                EditorGUILayout.IntField("num reflections",
                                         targetScript.NumberOfISMReflections);
            EditorGUI.indentLevel--;
        }
        targetScript.UseRaycast =
            EditorGUILayout.Toggle("Use ray tracing", targetScript.UseRaycast);
        if (targetScript.UseRaycast)
        {
            targetScript.TargetFPS =
                EditorGUILayout.DoubleField("Target FPS",
                                            targetScript.TargetFPS);
            EditorGUILayout.LabelField(
                "Raycast time budget",
                targetScript.RaycastTimeBudget.ToString("F3"));
        }
        targetScript.ApplyAirAbsorption =
            EditorGUILayout.Toggle("Apply air absorption",
                                   targetScript.ApplyAirAbsorption);
        targetScript.airAbsorption =
            (ISMAirAbsorption)EditorGUILayout.ObjectField(
                "Air Absorption (script)",
                targetScript.airAbsorption,
                typeof(ISMAirAbsorption),
                true);
    }
}
