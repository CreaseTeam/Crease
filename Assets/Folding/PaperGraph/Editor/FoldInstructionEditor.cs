using UnityEditor;
using UnityEngine;

namespace Crease.Folding.PaperGraph.Editor
{
    [CustomEditor(typeof(FoldInstruction))]
    public class FoldInstructionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() {
            FoldInstruction instruction = (FoldInstruction)target;
            if (instruction.Steps != null) {
                foreach (FoldStep step in instruction.Steps)
                    step.MigrateLegacyFilterTag();
            }

            DrawDefaultInspector();
        }
    }
}
