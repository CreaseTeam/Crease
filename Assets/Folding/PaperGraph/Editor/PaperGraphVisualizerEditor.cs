using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PaperGraphVisualizer))]
public class PaperGraphVisualizerEditor : Editor
{
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        PaperGraphVisualizer visualizer = (PaperGraphVisualizer)target;

        if (visualizer.graph == null || visualizer.graph.tags == null || visualizer.graph.tags.Count == 0) {
            EditorGUILayout.HelpBox("No tags available. Execute a fold with a tag name to populate tags.", MessageType.Info);
            visualizer.selectedTagIndex = 0;
            return;
        }

        // Build dropdown options: "None" + all tag keys
        List<string> tagKeys = new List<string>(visualizer.graph.tags.Keys);
        List<string> options = new List<string> { "(None)" };
        options.AddRange(tagKeys);

        // Clamp index to valid range
        if (visualizer.selectedTagIndex >= options.Count)
            visualizer.selectedTagIndex = 0;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Tag Highlight", EditorStyles.boldLabel);

        int newIndex = EditorGUILayout.Popup("Highlight Tag", visualizer.selectedTagIndex, options.ToArray());
        if (newIndex != visualizer.selectedTagIndex) {
            Undo.RecordObject(visualizer, "Change Highlight Tag");
            visualizer.selectedTagIndex = newIndex;
            EditorUtility.SetDirty(visualizer);
            SceneView.RepaintAll();
        }

        if (visualizer.selectedTagIndex > 0 && visualizer.selectedTagIndex <= tagKeys.Count) {
            string selectedTag = tagKeys[visualizer.selectedTagIndex - 1];
            int count = visualizer.graph.tags[selectedTag].Count;
            EditorGUILayout.HelpBox($"Tag \"{selectedTag}\" has {count} vertices.", MessageType.None);
        }
    }
}
