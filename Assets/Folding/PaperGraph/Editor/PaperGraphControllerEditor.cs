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

                List<string> tagKeys = new List<string>(graph.Tags.Keys);
                List<string> options = new List<string> { "(None)" };
                options.AddRange(tagKeys);

                if (controller.SelectedFilterTagIndex >= options.Count)
                    controller.SelectedFilterTagIndex = 0;

                int newIndex = EditorGUILayout.Popup("Filter Tag", controller.SelectedFilterTagIndex, options.ToArray());
                if (newIndex != controller.SelectedFilterTagIndex) {
                    Undo.RecordObject(controller, "Change Filter Tag");
                    controller.SelectedFilterTagIndex = newIndex;
                    EditorUtility.SetDirty(controller);
                }
            } else {
                controller.SelectedFilterTagIndex = 0;
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
