using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Folding.Paper
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

        [Tooltip("Only vertices with all of these tags are folded. Empty means everything.")]
        [FilterTags]
        public FilterTagSet FilterTags = new FilterTagSet();

        [SerializeField, HideInInspector, FormerlySerializedAs("filterTag"), FormerlySerializedAs("FilterTag")]
        private string _legacyFilterTag = "";

        public void MigrateLegacyFilterTag() {
            List<string> tags = FilterTags.GetMutableTags();

            if (string.IsNullOrEmpty(_legacyFilterTag)) return;
            if (!tags.Contains(_legacyFilterTag))
                tags.Add(_legacyFilterTag);
            _legacyFilterTag = "";
        }

        [Tooltip("ApplyTag names of prior fold axes that cannot be crossed during this step. The proposed fold axis is held at the last valid value while it would cross any frozen axis.")]
        [FreezeAxisTags]
        public FilterTagSet FreezeAxisTags = new FilterTagSet();

        [SerializeField, HideInInspector, FormerlySerializedAs("LockFoldAxis"), FormerlySerializedAs("lockFoldAxis")]
        private bool _legacyLockFoldAxis;

        public void MigrateLegacyLockFoldAxis(FoldStep nextStep) {
            if (!_legacyLockFoldAxis)
                return;

            string axisTag = GetAxisTagName();
            if (!string.IsNullOrEmpty(axisTag) && nextStep != null) {
                List<string> tags = nextStep.FreezeAxisTags.GetMutableTags();
                if (!tags.Contains(axisTag))
                    tags.Add(axisTag);
            }

            _legacyLockFoldAxis = false;
        }

        public string GetAxisTagName() {
            if (string.IsNullOrEmpty(ApplyTag) || ApplyTag == "none")
                return null;
            return ApplyTag;
        }

        [Tooltip("If true, only cut topology along the fold axis (crease) without rotating geometry. ApplyTag writes _edge, _moved, and _static tags as if the fold had been committed.")]
        [FormerlySerializedAs("isCrease")]
        public bool IsCrease = false;

        [Tooltip("If true, performs a water-bomb style accordion collapse using two existing creases. Drag progress along the computed path drives the collapse.")]
        [FormerlySerializedAs("isAccordionFold")]
        public bool IsAccordionFold = false;

        [Tooltip("Tag of the first crease used by this accordion fold.")]
        public string AccordionCreaseTagA = "";

        [Tooltip("Tag of the second crease used by this accordion fold.")]
        public string AccordionCreaseTagB = "";

        [Header("Paper Rotation (Optional)")]
        [Tooltip("If true, the paper will lerp to the specified rotation when this step is loaded.")]
        [FormerlySerializedAs("rotatePaper")]
        public bool RotatePaper = false;

        [Tooltip("Euler angles defining the paper's target rotation.")]
        [FormerlySerializedAs("paperRotation")]
        public Vector3 PaperRotation;

        [Header("Vertex Rotation (Optional)")]
        [Tooltip("If true, rotates every vertex in the mesh around an axis after this step's fold is executed.")]
        public bool RotateVertices = false;

        [Tooltip("Point on the rotation axis (paper local space).")]
        public Vector3 VertexRotationPivot;

        [Tooltip("Direction of the rotation axis (paper local space).")]
        public Vector3 VertexRotationAxis = Vector3.up;

        [Tooltip("Rotation amount in degrees around VertexRotationAxis.")]
        public float VertexRotationDegrees;
    }

    [CreateAssetMenu(fileName = "NewFoldInstruction", menuName = "Crease/Fold Instruction")]
    public class FoldInstruction : ScriptableObject
    {
        [Tooltip("Offset applied to every step position when this instruction is run (x → local X, y → local Z).")]
        public Vector2 Offset;

        [Header("Flight Orientation")]
        [Tooltip("Euler rotation (graph space → flight mesh space) baked into the saved mesh so the model's front aligns with +Z.")]
        public Vector3 FlightMeshRotation;

        [FormerlySerializedAs("steps")]
        public List<FoldStep> Steps = new List<FoldStep>();

        public Vector3 ApplyOffset(Vector3 position) {
            return new Vector3(position.x + Offset.x, position.y, position.z + Offset.y);
        }

        private void OnValidate() {
            if (Steps == null) return;

            for (int i = 0; i < Steps.Count; i++) {
                FoldStep step = Steps[i];
                step.MigrateLegacyFilterTag();
                FoldStep nextStep = i + 1 < Steps.Count ? Steps[i + 1] : null;
                step.MigrateLegacyLockFoldAxis(nextStep);
            }
        }
    }
}
