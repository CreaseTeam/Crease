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
                for (int i = 0; i < instruction.Steps.Count; i++) {
                    FoldStep step = instruction.Steps[i];
                    step.MigrateLegacyFilterTag();
                    FoldStep nextStep = i + 1 < instruction.Steps.Count ? instruction.Steps[i + 1] : null;
                    step.MigrateLegacyLockFoldAxis(nextStep);
                }
            }

            DrawDefaultInspector();
        }
    }
}
