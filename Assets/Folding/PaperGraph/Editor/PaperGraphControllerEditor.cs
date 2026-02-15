using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PaperGraphController))]
public class PaperGraphControllerEditor : Editor
{
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        PaperGraphController controller = (PaperGraphController)target;

        EditorGUILayout.Space(10);

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
