using UnityEditor;
using UnityEngine;

namespace Crease.Flying.DeveloperTools.Editor
{
    [CustomEditor(typeof(SplinePathPlacer))]
    public class SplinePathPlacerEditor : UnityEditor.Editor
    {
        const int HighCountWarningThreshold = 500;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "1. Set the Prefab and Count below.\n" +
                "2. Select this object, pick the Spline tool in the Scene view toolbar, and drag the\n" +
                "    knots to shape the path. Copies redistribute live.\n" +
                "3. Ctrl/Cmd-click the path to add a knot; shape it into an arc, an S-snake, or a loop.\n" +
                "4. Happy with it? Press Bake to commit permanent objects, then delete this placer.",
                MessageType.Info);

            var placer = (SplinePathPlacer)target;

            if (placer.Prefab == null)
            {
                EditorGUILayout.HelpBox("Assign a Prefab to start placing copies.", MessageType.Warning);
            }

            if (placer.Count > HighCountWarningThreshold)
            {
                EditorGUILayout.HelpBox(
                    $"Count is {placer.Count}. Editing the path re-places every copy — very high counts " +
                    "with heavy prefabs can slow down live dragging.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Now"))
                    placer.Rebuild();

                if (GUILayout.Button("Reset Path To Default Arc"))
                    placer.ResetPathToDefaultArc();
            }

            using (new EditorGUI.DisabledScope(placer.Prefab == null))
            {
                if (GUILayout.Button("Bake To Permanent Objects"))
                    placer.Bake();
            }
        }
    }
}
