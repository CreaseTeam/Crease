using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PaperGraphController))]
public class PaperGraphControllerEditor : Editor
{
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        PaperGraphController controller = (PaperGraphController)target;

        // Filter-by-tag dropdown
        PaperGraph graph = controller.GetComponent<PaperGraph>();
        if (graph != null && graph.tags != null && graph.tags.Count > 0) {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Filter by Tag", EditorStyles.boldLabel);

            List<string> tagKeys = new List<string>(graph.tags.Keys);
            List<string> options = new List<string> { "(None)" };
            options.AddRange(tagKeys);

            if (controller.selectedFilterTagIndex >= options.Count)
                controller.selectedFilterTagIndex = 0;

            int newIndex = EditorGUILayout.Popup("Filter Tag", controller.selectedFilterTagIndex, options.ToArray());
            if (newIndex != controller.selectedFilterTagIndex) {
                Undo.RecordObject(controller, "Change Filter Tag");
                controller.selectedFilterTagIndex = newIndex;
                EditorUtility.SetDirty(controller);
            }
        } else {
            controller.selectedFilterTagIndex = 0;
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

