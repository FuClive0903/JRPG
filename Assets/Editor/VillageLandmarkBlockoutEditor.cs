using Game.Exploration;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VillageLandmarkBlockout))]
public sealed class VillageLandmarkBlockoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        if (!DrawDefaultInspector())
            return;

        serializedObject.ApplyModifiedProperties();
        if (Application.isPlaying)
            return;
        ((VillageLandmarkBlockout)target).Rebuild();
        SceneView.RepaintAll();
    }
}
