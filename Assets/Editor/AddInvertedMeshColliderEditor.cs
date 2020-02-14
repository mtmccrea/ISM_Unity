using UnityEngine;
using UnityEditor;
using System.Collections;


/// <summary>
/// A custom inspector interface for AddInvertedMeshCollider script
/// </summary>
[CustomEditor(typeof(AddInvertedMeshCollider))]
public class AddInvertedMeshColliderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        // Add a custom button to invert the mesh
        AddInvertedMeshCollider script = (AddInvertedMeshCollider)target;
        if (GUILayout.Button("Create Inverted Mesh Collider"))
            script.CreateInvertedMeshCollider();
    }
}