using UnityEngine;
using UnityEngine.Audio;
using UnityEditor;


/// <summary>
/// A custom inspector interface for ISMRenderSettings script
/// </summary>
[CustomEditor(typeof(ISMRenderSettings))]
public class ISMRenderSettingsGUI : Editor
{

    SerializedProperty listener;
    SerializedProperty mixer;


    private void OnEnable()
    {
        listener = serializedObject.FindProperty("listener");
        mixer = serializedObject.FindProperty("mixer");
    }


    public override void OnInspectorGUI()
    {
        // Get the object behind the GUI
        ISMRenderSettings targetScript = (ISMRenderSettings)target;
        // General settings
        EditorGUILayout.LabelField("General");
        EditorGUILayout.PropertyField(listener);
        EditorGUILayout.PropertyField(mixer);
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
        targetScript.NumberOfISMReflections = 
            EditorGUILayout.IntField("num reflections", 
                                        targetScript.NumberOfISMReflections);
        serializedObject.ApplyModifiedProperties();
    }
}
