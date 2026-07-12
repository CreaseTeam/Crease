using UnityEditor;
using UnityEngine;

namespace Crease.Folding.Paper.Editor
{
    [CustomEditor(typeof(PaperGraph))]
    public class PaperGraphEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            PaperGraph graph = (PaperGraph)target;
            if (graph.GetComponent<PaperGraphPreviewRoot>() != null)
            {
                EditorGUILayout.HelpBox(
                    "This graph is the fold preview and is driven by PaperGraphController on the parent. "
                    + "Edit the parent PaperGraph for sheet size, crease shading, and guide line settings.",
                    MessageType.Info);
                return;
            }

            DrawDefaultInspector();
        }
    }
}
