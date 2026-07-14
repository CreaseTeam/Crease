using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Crease.Folding.PaperGraph.Editor
{
    [CustomEditor(typeof(PaperGraphVisualizer))]
    public class PaperGraphVisualizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            PaperGraphVisualizer visualizer = (PaperGraphVisualizer)target;

            if (GUI.changed)
                SceneView.RepaintAll();

            if (visualizer.Graph == null || visualizer.Graph.Tags == null || visualizer.Graph.Tags.Count == 0) {
                EditorGUILayout.HelpBox("No tags available. Execute a fold with a tag name to populate tags.", MessageType.Info);
                visualizer.SelectedTagIndex = 0;
                return;
            }

            List<string> tagKeys = new List<string>(visualizer.Graph.Tags.Keys);
            List<string> options = new List<string> { "(None)" };
            options.AddRange(tagKeys);

            if (visualizer.SelectedTagIndex >= options.Count)
                visualizer.SelectedTagIndex = 0;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Tag Highlight", EditorStyles.boldLabel);

            int newIndex = EditorGUILayout.Popup("Highlight Tag", visualizer.SelectedTagIndex, options.ToArray());
            if (newIndex != visualizer.SelectedTagIndex) {
                Undo.RecordObject(visualizer, "Change Highlight Tag");
                visualizer.SelectedTagIndex = newIndex;
                EditorUtility.SetDirty(visualizer);
                SceneView.RepaintAll();
            }

            if (visualizer.SelectedTagIndex > 0 && visualizer.SelectedTagIndex <= tagKeys.Count) {
                string selectedTag = tagKeys[visualizer.SelectedTagIndex - 1];
                int count = visualizer.Graph.Tags[selectedTag].Count;
                EditorGUILayout.HelpBox($"Tag \"{selectedTag}\" has {count} vertices.", MessageType.None);
            }
        }
    }
}
