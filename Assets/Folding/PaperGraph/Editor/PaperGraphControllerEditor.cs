using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Crease.Folding.PaperGraph.Editor
{
    [CustomEditor(typeof(PaperGraphController))]
    public class PaperGraphControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            PaperGraphController controller = (PaperGraphController)target;

            PaperGraph graph = controller.GetComponent<PaperGraph>();
            if (graph != null && graph.Tags != null && graph.Tags.Count > 0) {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Filter by Tag", EditorStyles.boldLabel);

                List<string> availableTags = FilterTagDropdown.CollectAvailableTags(graph);
                FilterTagDropdown.DrawLayout(
                    controller.SelectedFilterTags,
                    availableTags,
                    nextTags => {
                        Undo.RecordObject(controller, "Change Filter Tags");
                        controller.SelectedFilterTags = nextTags ?? new List<string>();
                        EditorUtility.SetDirty(controller);
                    });
            } else if (controller.SelectedFilterTags != null && controller.SelectedFilterTags.Count > 0) {
                controller.SelectedFilterTags.Clear();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Snap to Outside", GUILayout.Height(30))) {
                Undo.RecordObject(controller, "Snap to Outside");
                controller.SnapDragHandleToOutside();
                EditorUtility.SetDirty(controller);
            }

            if (GUILayout.Button("Execute Fold", GUILayout.Height(30))) {
                controller.ExecuteFoldAction();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Undo", GUILayout.Height(25))) {
                controller.UndoFold();
            }
            if (GUILayout.Button("Redo", GUILayout.Height(25))) {
                controller.RedoFold();
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Reset Sheet", GUILayout.Height(30))) {
                controller.ResetSheet();
            }
        }
    }
}
