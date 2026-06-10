using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Folding.PaperGraph
{
    [System.Serializable]
    public class FoldStep
    {
        [Tooltip("Position for the drag handle at this step (start of drag).")]
        [FormerlySerializedAs("dragHandlePosition")]
        public Vector3 DragHandlePosition;

        [Tooltip("Ideal end position the player should drag toward. Used to show the guide line.")]
        [FormerlySerializedAs("idealDragPosition")]
        public Vector3 IdealDragPosition;

        [Tooltip("Normal of the plane the drag handle moves on.")]
        [FormerlySerializedAs("dragPlaneNormal")]
        public Vector3 DragPlaneNormal = Vector3.up;

        [Tooltip("Fold angle in degrees.")]
        [FormerlySerializedAs("foldDegrees")]
        public float FoldDegrees = 180f;

        [Tooltip("Offset for flat folds (hinge thickness). 0 for no offset.")]
        [FormerlySerializedAs("foldOffset")]
        public float FoldOffset = 0f;

        [Tooltip("Tag to apply to vertices affected by this fold (leave empty for none).")]
        [FormerlySerializedAs("applyTag")]
        public string ApplyTag = "";

        [Tooltip("Tag to filter by — only vertices with this tag are folded (leave empty for none).")]
        [FormerlySerializedAs("filterTag")]
        public string FilterTag = "";

        [Tooltip("If true, the fold axis produced by this step is locked. Any subsequent fold whose proposed axis would cross this segment will be held at the last valid value until it no longer crosses.")]
        [FormerlySerializedAs("lockFoldAxis")]
        public bool LockFoldAxis = false;

        [Header("Paper Rotation (Optional)")]
        [Tooltip("If true, the paper will lerp to the specified rotation when this step is loaded.")]
        [FormerlySerializedAs("rotatePaper")]
        public bool RotatePaper = false;

        [Tooltip("Euler angles defining the paper's target rotation.")]
        [FormerlySerializedAs("paperRotation")]
        public Vector3 PaperRotation;
    }

    [CreateAssetMenu(fileName = "NewFoldInstruction", menuName = "Crease/Fold Instruction")]
    public class FoldInstruction : ScriptableObject
    {
        [Tooltip("Offset applied to every step position when this instruction is run (x → local X, y → local Z).")]
        public Vector2 Offset;

        [FormerlySerializedAs("steps")]
        public List<FoldStep> Steps = new List<FoldStep>();

        public Vector3 ApplyOffset(Vector3 position) {
            return new Vector3(position.x + Offset.x, position.y, position.z + Offset.y);
        }
    }
}
