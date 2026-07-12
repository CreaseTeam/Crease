using Crease.Folding.PaperSurface.Decals;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

namespace Crease.Folding.PaperGraph
{
    public class PaperGraphController : MonoBehaviour
    {
        [Header("Fold")]
        [FormerlySerializedAs("foldPoint1")]
        public Vector3 FoldPoint1 = new Vector3(-0.5f, 0, 0);
        [FormerlySerializedAs("foldPoint2")]
        public Vector3 FoldPoint2 = new Vector3(0.5f, 0, 0);
        [FormerlySerializedAs("foldPlaneVector")]
        public Vector3 FoldPlaneVector = Vector3.forward;
        [FormerlySerializedAs("foldDegrees")]
        public float FoldDegrees = 180f;
        [FormerlySerializedAs("foldOffset")]
        public float FoldOffset = 0f;
        [FormerlySerializedAs("foldTagName")]
        public string FoldTagName = "";
        [HideInInspector]
        public List<string> SelectedFilterTags = new List<string>();

        [Header("Drag Handle")]
        [FormerlySerializedAs("dragHandlePosition")]
        public Vector3 DragHandlePosition = Vector3.zero;
        [HideInInspector]
        public Vector3 IdealDragPosition = Vector3.zero;
        [FormerlySerializedAs("dragPlaneNormal")]
        public Vector3 DragPlaneNormal = Vector3.up;
        [FormerlySerializedAs("foldLineHalfLength")]
        public float FoldLineHalfLength = 1f;

        [Header("Preview")]
        [FormerlySerializedAs("previewGraph")]
        public PaperGraph PreviewGraph;

        [Header("Gizmo")]
        [FormerlySerializedAs("showFoldGizmo")]
        public bool ShowFoldGizmo = true;
        [FormerlySerializedAs("gizmoHeight")]
        public float GizmoHeight = 2f;
        [FormerlySerializedAs("gizmoColor")]
        public Color GizmoColor = new Color(1f, 0.5f, 0f, 0.25f);

        private PaperGraph _paperGraph;
        private PaperGraphVisualizer _authoringVisualizer;
        private PaperGraphVisualizer _previewVisualizer;

        // When set by FoldInstructionRunner, RecalculateFoldAxis will refuse to move
        // FoldPoint1/FoldPoint2 to a position whose axis line crosses any frozen segment.
        [System.Serializable]
        public struct FoldAxisLockSegment
        {
            public Vector3 P1;
            public Vector3 P2;
        }

        [HideInInspector]
        public List<FoldAxisLockSegment> FoldAxisLocks = new List<FoldAxisLockSegment>();
        [HideInInspector]
        [FormerlySerializedAs("lockedFoldPoint1")]
        public Vector3 LockedFoldPoint1;
        [HideInInspector]
        [FormerlySerializedAs("lockedFoldPoint2")]
        public Vector3 LockedFoldPoint2;

        private readonly List<float> _lockStartMidpointSide = new List<float>();
        private Vector3 _foldDragStart;
        private Vector3 _lastValidDragEnd;

        private const int DragLockSampleCount = 10;
        private const float ParallelAxisDotThreshold = 0.92f;

        public bool HasFoldAxisLock => FoldAxisLocks != null && FoldAxisLocks.Count > 0;

        public void ClearFoldAxisLocks() {
            if (FoldAxisLocks == null)
                FoldAxisLocks = new List<FoldAxisLockSegment>();
            else
                FoldAxisLocks.Clear();

            _lockStartMidpointSide.Clear();
        }

        public void SetFoldAxisLocks(IReadOnlyList<SavedCrease> axes) {
            ClearFoldAxisLocks();
            if (axes == null)
                return;

            foreach (SavedCrease axis in axes) {
                FoldAxisLocks.Add(new FoldAxisLockSegment {
                    P1 = axis.P1,
                    P2 = axis.P2
                });
            }
        }

        public void BeginFoldStepDrag(Vector3 foldP1, Vector3 foldP2) {
            _foldDragStart = DragHandlePosition;
            _lastValidDragEnd = DragHandlePosition;

            _lockStartMidpointSide.Clear();
            if (!HasFoldAxisLock)
                return;

            Vector3 midpoint = (foldP1 + foldP2) * 0.5f;
            foreach (FoldAxisLockSegment lockSegment in FoldAxisLocks) {
                _lockStartMidpointSide.Add(GetSideOfInfiniteLine(midpoint, lockSegment.P1, lockSegment.P2, DragPlaneNormal));
            }
        }

        public bool IsAccordionDragStep { get; private set; }
        public Vector3 AccordionDragStart { get; private set; }
        public Vector3 AccordionDragEnd { get; private set; }
        public float AccordionCollapseT { get; private set; }

        public bool IsAccordionDragComplete(float minProgress = 0.99f) {
            return IsAccordionDragStep && AccordionCollapseT >= minProgress;
        }

        private Vector3 _prevFoldPoint1;
        private Vector3 _prevFoldPoint2;
        private Vector3 _prevFoldPlaneVector;
        private float _prevFoldDegrees;

        private void Awake() {
            _paperGraph = GetComponent<PaperGraph>();
            _authoringVisualizer = GetComponent<PaperGraphVisualizer>();
            if (PreviewGraph != null)
                _previewVisualizer = PreviewGraph.GetComponent<PaperGraphVisualizer>();
            CacheFoldValues();
        }

        private void OnValidate() {
            UpdatePreview();
        }

        private void CacheFoldValues() {
            _prevFoldPoint1 = FoldPoint1;
            _prevFoldPoint2 = FoldPoint2;
            _prevFoldPlaneVector = FoldPlaneVector;
            _prevFoldDegrees = FoldDegrees;
        }

        public void UpdatePreview() {
            if (_paperGraph == null || PreviewGraph == null) return;

            PaperGraphSnapshot snapshot = _paperGraph.CreateSnapshot();
            PreviewGraph.RestoreSnapshot(snapshot);
            string tag = string.IsNullOrEmpty(FoldTagName) ? null : FoldTagName;
            bool valid = PreviewGraph.ExecuteFold(FoldPoint1, FoldPoint2, FoldPlaneVector, FoldDegrees, tag, SelectedFilterTags, FoldOffset);

            if (!valid) {
                FoldPoint1 = LockedFoldPoint1;
                FoldPoint2 = LockedFoldPoint2;
                PreviewGraph.RestoreSnapshot(snapshot);
                PreviewGraph.ExecuteFold(LockedFoldPoint1, LockedFoldPoint2, FoldPlaneVector, FoldDegrees, tag, SelectedFilterTags, FoldOffset);
            } else {
                LockedFoldPoint1 = FoldPoint1;
                LockedFoldPoint2 = FoldPoint2;
            }

            CacheFoldValues();
            RefreshVisualizers();
        }

        private void FreezeAtLastValidFold() {
            FoldPoint1 = LockedFoldPoint1;
            FoldPoint2 = LockedFoldPoint2;
            FoldPlaneVector = DragPlaneNormal;
            UpdatePreview();
        }

        /// <summary>
        /// Invalid when the drag handle and the instruction's ideal fold axis lie on opposite
        /// sides of the start boundary (the ideal fold axis duplicated through drag start).
        /// </summary>
        private bool IsPastStartBoundary(Vector3 dragStart, Vector3 dragCurrent) {
            Vector3 idealDrag = IdealDragPosition - dragStart;
            if (idealDrag.sqrMagnitude < 0.00001f) return false;

            Vector3 idealDir = idealDrag.normalized;
            Vector3 idealFoldCenter = dragStart + idealDrag * 0.5f;
            float handleT = Vector3.Dot(dragCurrent - dragStart, idealDir);
            float idealFoldT = Vector3.Dot(idealFoldCenter - dragStart, idealDir);
            return handleT * idealFoldT < -0.0001f;
        }

        private bool IsFoldCandidateInvalid(Vector3 dragStart, Vector3 dragCurrent, Vector3 candidateP1, Vector3 candidateP2) {
            if (IsPastStartBoundary(dragStart, dragCurrent)) return true;

            return IsFoldAxisBlockedByLocks(dragCurrent, candidateP1, candidateP2);
        }

        private bool IsFoldAxisBlockedByLocks(Vector3 dragEnd, Vector3 foldP1, Vector3 foldP2) {
            if (!HasFoldAxisLock)
                return false;

            if (IsFoldAxisBlockedAtPosition(foldP1, foldP2))
                return true;

            for (int i = 1; i < DragLockSampleCount; i++) {
                float t = i / (float)DragLockSampleCount;
                Vector3 sampledDragEnd = Vector3.Lerp(_lastValidDragEnd, dragEnd, t);
                if (!TryComputeFoldAxisFromDrag(_foldDragStart, sampledDragEnd, out Vector3 sampleP1, out Vector3 sampleP2))
                    continue;

                if (IsFoldAxisBlockedAtPosition(sampleP1, sampleP2))
                    return true;
            }

            return false;
        }

        private bool IsFoldAxisBlockedAtPosition(Vector3 foldP1, Vector3 foldP2) {
            for (int i = 0; i < FoldAxisLocks.Count; i++) {
                FoldAxisLockSegment lockSegment = FoldAxisLocks[i];
                if (FoldAxisCrossesLockSegment(foldP1, foldP2, lockSegment.P1, lockSegment.P2, DragPlaneNormal))
                    return true;

                if (i < _lockStartMidpointSide.Count
                    && IsParallelSlidePastLock(foldP1, foldP2, _lockStartMidpointSide[i], lockSegment, DragPlaneNormal))
                    return true;
            }

            return false;
        }

        private static bool IsParallelSlidePastLock(
            Vector3 foldP1,
            Vector3 foldP2,
            float startMidpointSide,
            FoldAxisLockSegment lockSegment,
            Vector3 normal) {
            if (Mathf.Abs(startMidpointSide) < 0.0001f)
                return false;

            Vector3 foldDir = Vector3.ProjectOnPlane(foldP2 - foldP1, normal);
            Vector3 lockDir = Vector3.ProjectOnPlane(lockSegment.P2 - lockSegment.P1, normal);
            if (foldDir.sqrMagnitude < 0.00001f || lockDir.sqrMagnitude < 0.00001f)
                return false;

            if (Mathf.Abs(Vector3.Dot(foldDir.normalized, lockDir.normalized)) < ParallelAxisDotThreshold)
                return false;

            Vector3 midpoint = (foldP1 + foldP2) * 0.5f;
            float midpointSide = GetSideOfInfiniteLine(midpoint, lockSegment.P1, lockSegment.P2, normal);
            if (Mathf.Abs(midpointSide) < 0.0001f)
                return false;

            return midpointSide * startMidpointSide < -0.0001f;
        }

        private static float GetSideOfInfiniteLine(Vector3 point, Vector3 lineP1, Vector3 lineP2, Vector3 normal) {
            Vector3 lineDir = Vector3.ProjectOnPlane(lineP2 - lineP1, normal);
            if (lineDir.sqrMagnitude < 0.00001f)
                return 0f;

            Vector3 u = lineDir.normalized;
            Vector3 v = Vector3.Cross(normal, u).normalized;

            Vector2 a = new Vector2(Vector3.Dot(lineP1, u), Vector3.Dot(lineP1, v));
            Vector2 b = new Vector2(Vector3.Dot(lineP2, u), Vector3.Dot(lineP2, v));
            Vector2 p = new Vector2(Vector3.Dot(point, u), Vector3.Dot(point, v));

            return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        }

        public void ExecuteFoldAction() {
            if (_paperGraph == null) {
                Debug.LogError("No PaperGraph component found on this GameObject.");
                return;
            }

            string tag = string.IsNullOrEmpty(FoldTagName) ? null : FoldTagName;
            _paperGraph.ExecuteFold(FoldPoint1, FoldPoint2, FoldPlaneVector, FoldDegrees, tag, SelectedFilterTags, FoldOffset);
            RefreshVisualizers();
        }

        public bool ExecuteCreaseAction(bool refreshVisuals = true) {
            if (_paperGraph == null) {
                Debug.LogError("No PaperGraph component found on this GameObject.");
                return false;
            }

            string tag = string.IsNullOrEmpty(FoldTagName) ? null : FoldTagName;
            bool valid = _paperGraph.ExecuteCrease(FoldPoint1, FoldPoint2, FoldPlaneVector, tag, SelectedFilterTags, FoldDegrees);
            if (valid && refreshVisuals)
                RefreshVisualizers();
            return valid;
        }

        public bool BeginVertexRotationAnimation(Vector3 pivot, Vector3 axis, float degrees) {
            if (_paperGraph == null) {
                Debug.LogError("No PaperGraph component found on this GameObject.");
                return false;
            }

            if (!_paperGraph.BeginVertexRotationAnimation(pivot, axis, degrees))
                return false;

            ApplyVertexRotationProgress(0f);
            return true;
        }

        public void ApplyVertexRotationProgress(float t) {
            if (_paperGraph == null)
                return;

            _paperGraph.SetVertexRotationProgress(t);
            SyncPreviewFromAuthoring();
            RefreshVisualizers();
        }

        public void CommitVertexRotationAnimation() {
            if (_paperGraph == null)
                return;

            _paperGraph.CommitVertexRotationAnimation();
            SyncPreviewFromAuthoring();
            RefreshVisualizers();
        }

        private void SyncPreviewFromAuthoring() {
            if (_paperGraph == null || PreviewGraph == null)
                return;

            PreviewGraph.RestoreSnapshot(_paperGraph.CreateSnapshot());
        }

        public bool PrepareAccordionAction(
            string creaseTagA,
            string creaseTagB,
            Vector3 creaseAxisA1,
            Vector3 creaseAxisA2,
            Vector3 creaseAxisB1,
            Vector3 creaseAxisB2,
            float foldDegrees,
            Vector3 planeVector,
            float foldOffset) {
            if (_paperGraph == null) {
                Debug.LogError("No PaperGraph component found on this GameObject.");
                return false;
            }

            string tag = string.IsNullOrEmpty(FoldTagName) ? null : FoldTagName;
            bool valid = _paperGraph.PrepareAccordionCollapse(
                creaseTagA, creaseTagB, tag,
                creaseAxisA1, creaseAxisA2, creaseAxisB1, creaseAxisB2,
                foldDegrees, planeVector, foldOffset);
            if (valid)
                RefreshVisualizers();
            return valid;
        }

        public void UpdateAccordionPreview(float collapseT) {
            if (_paperGraph == null || PreviewGraph == null || !_paperGraph.HasAccordionData) return;

            if (_previewVisualizer != null)
                _previewVisualizer.SkipColliderUpdate = true;

            AccordionCollapseData data = _paperGraph.GetAccordionData();
            PaperGraphSnapshot snapshot = _paperGraph.CreateSnapshot();
            PreviewGraph.RestoreSnapshot(snapshot);
            AccordionCollapse.ApplyPose(PreviewGraph, data, collapseT, FoldOffset);
            RefreshVisualizers();
        }

        public bool CommitAccordionAction() {
            if (_paperGraph == null) {
                Debug.LogError("No PaperGraph component found on this GameObject.");
                return false;
            }

            float t = IsAccordionDragStep ? AccordionCollapseT : 1f;
            bool valid = _paperGraph.CommitAccordionCollapse(FoldOffset, t);
            if (valid)
                RefreshVisualizers();
            return valid;
        }

        public void BeginAccordionDragStep(Vector3 dragStart, Vector3 dragEnd) {
            IsAccordionDragStep = true;
            AccordionDragStart = dragStart;
            AccordionDragEnd = dragEnd;
            AccordionCollapseT = 0f;
            DragHandlePosition = dragStart;
            IdealDragPosition = dragEnd;
            UpdateAccordionPreview(0f);
        }

        public void EndAccordionDragStep() {
            IsAccordionDragStep = false;
            AccordionCollapseT = 0f;
        }

        public void UpdateAccordionFromDrag(Vector3 dragCurrentLocal) {
            if (!IsAccordionDragStep) return;

            Vector3 line = AccordionDragEnd - AccordionDragStart;
            float lineLengthSq = line.sqrMagnitude;
            if (lineLengthSq < 0.00001f) return;

            float t = Vector3.Dot(dragCurrentLocal - AccordionDragStart, line) / lineLengthSq;
            t = Mathf.Clamp01(t);

            DragHandlePosition = AccordionDragStart + line * t;
            AccordionCollapseT = t;
            UpdateAccordionPreview(t);
        }

        public void SetAccordionPreviewProgress(float t) {
            if (!IsAccordionDragStep) return;

            t = Mathf.Clamp01(t);
            AccordionCollapseT = t;
            DragHandlePosition = Vector3.Lerp(AccordionDragStart, AccordionDragEnd, t);
            UpdateAccordionPreview(t);
        }

        public void RecalculateFoldAxis() {
            if (!TryComputeFoldAxisFromDrag(DragHandlePosition, IdealDragPosition, out Vector3 candidateP1, out Vector3 candidateP2))
                return;

            if (IsFoldAxisBlockedAtPosition(candidateP1, candidateP2)) {
                FoldPoint1 = LockedFoldPoint1;
                FoldPoint2 = LockedFoldPoint2;
                FoldPlaneVector = DragPlaneNormal;
                return;
            }

            FoldPoint1 = candidateP1;
            FoldPoint2 = candidateP2;
            FoldPlaneVector = DragPlaneNormal;
        }

        private bool TryComputeFoldAxisFromDrag(
            Vector3 dragStart,
            Vector3 dragEnd,
            out Vector3 foldP1,
            out Vector3 foldP2) {
            foldP1 = default;
            foldP2 = default;

            Vector3 dragDelta = dragEnd - dragStart;
            if (dragDelta.sqrMagnitude < 0.00001f)
                return false;

            Vector3 midpoint = (dragStart + dragEnd) * 0.5f;
            Vector3 dragDir = dragDelta.normalized;
            Vector3 foldAxisDir = Vector3.Cross(DragPlaneNormal, dragDir).normalized;
            if (foldAxisDir.sqrMagnitude < 0.0001f)
                return false;

            foldP1 = midpoint + foldAxisDir * FoldLineHalfLength;
            foldP2 = midpoint - foldAxisDir * FoldLineHalfLength;
            return true;
        }

        private bool FoldAxisCrossesLockSegment(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal) {
            Vector3 dir = Vector3.ProjectOnPlane(b - a, normal);
            if (dir.sqrMagnitude < 0.00001f) return false;
            Vector3 u = dir.normalized;
            Vector3 v = Vector3.Cross(normal, u).normalized;

            Vector2 A = new Vector2(Vector3.Dot(a, u), Vector3.Dot(a, v));
            Vector2 B = new Vector2(Vector3.Dot(b, u), Vector3.Dot(b, v));
            Vector2 C = new Vector2(Vector3.Dot(c, u), Vector3.Dot(c, v));
            Vector2 D = new Vector2(Vector3.Dot(d, u), Vector3.Dot(d, v));

            float sideC = (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x);
            float sideD = (B.x - A.x) * (D.y - A.y) - (B.y - A.y) * (D.x - A.x);

            return (sideC > 0.0001f && sideD < -0.0001f) || (sideC < -0.0001f && sideD > 0.0001f);
        }

        public void SnapDragHandleToOutside() {
            if (_paperGraph == null || _paperGraph.Faces == null || _paperGraph.Faces.Count == 0) return;

            Vector3 N = DragPlaneNormal.normalized;
            if (N.sqrMagnitude < 0.0001f) return;

            Vector3 H0 = DragHandlePosition;
            float maxT = float.MinValue;
            bool found = false;

            foreach (Face face in _paperGraph.Faces) {
                if (face.Vertices.Count < 3) continue;

                Vector3 faceNormal = Vector3.zero;
                Vector3 p0 = face.Vertices[0].Position;
                for (int i = 1; i < face.Vertices.Count - 1; i++) {
                    Vector3 p1 = face.Vertices[i].Position;
                    Vector3 p2 = face.Vertices[i + 1].Position;
                    faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
                    if (faceNormal.sqrMagnitude > 0.000001f) {
                        faceNormal = faceNormal.normalized;
                        break;
                    }
                }

                if (faceNormal.sqrMagnitude < 0.000001f) continue;

                float dotDirNormal = Vector3.Dot(N, faceNormal);
                if (Mathf.Abs(dotDirNormal) < 0.0001f) continue;

                float t = Vector3.Dot(p0 - H0, faceNormal) / dotDirNormal;
                Vector3 P = H0 + t * N;

                bool inside = true;
                for (int i = 0; i < face.Vertices.Count; i++) {
                    Vector3 vA = face.Vertices[i].Position;
                    Vector3 vB = face.Vertices[(i + 1) % face.Vertices.Count].Position;
                    Vector3 edge = vB - vA;
                    Vector3 toP = P - vA;
                    Vector3 cross = Vector3.Cross(edge, toP);
                    float signedArea = Vector3.Dot(cross, faceNormal);
                    if (signedArea < -0.0001f * edge.magnitude) {
                        inside = false;
                        break;
                    }
                }

                if (inside) {
                    if (t > maxT) {
                        maxT = t;
                        found = true;
                    }
                }
            }

            if (found) {
                DragHandlePosition = H0 + maxT * N;
                RecalculateFoldAxis();
                UpdatePreview();

                foreach (FoldDragHandle handle in FindObjectsByType<FoldDragHandle>(FindObjectsSortMode.None)) {
                    if (handle.Controller == this) {
                        handle.ResetHandle();
                    }
                }
            }
        }

        /// <summary>
        /// Swaps the front material on the preview paper (the mesh seen during folding).
        /// Used by the level-end flow to reveal the clear letter in place of the blurry material.
        /// </summary>
        public void SetPreviewFrontMaterial(Material material) {
            if (_previewVisualizer != null)
                _previewVisualizer.SetFrontMaterial(material);
        }

        public void ClearPreview() {
            if (_paperGraph == null || PreviewGraph == null) return;

            if (_previewVisualizer != null)
                _previewVisualizer.SkipColliderUpdate = false;

            PaperGraphSnapshot snapshot = _paperGraph.CreateSnapshot();
            PreviewGraph.RestoreSnapshot(snapshot);

            if (IsAccordionDragStep && _paperGraph.HasAccordionData)
                AccordionCollapse.ApplyPose(PreviewGraph, _paperGraph.GetAccordionData(), AccordionCollapseT, FoldOffset);

            RefreshVisualizers();
        }

        public void UndoFold() {
            if (_paperGraph == null) {
                Debug.LogError("No PaperGraph component found on this GameObject.");
                return;
            }

            _paperGraph.Undo();
            RefreshVisualizers();
        }

        /// <summary>
        /// Reverts one committed fold on the authoring graph without moving decals.
        /// Use before unfold preview animation so stickers can track the preview mesh.
        /// </summary>
        public void UndoAuthoringFold() {
            if (_paperGraph == null) {
                Debug.LogError("No PaperGraph component found on this GameObject.");
                return;
            }

            _paperGraph.Undo();
            RefreshMeshesOnly();
        }

        private void RefreshMeshesOnly() {
            _authoringVisualizer?.UpdateMesh();
            _previewVisualizer?.UpdateMesh();
        }

        public void RedoFold() {
            if (_paperGraph == null) {
                Debug.LogError("No PaperGraph component found on this GameObject.");
                return;
            }

            _paperGraph.Redo();
            RefreshVisualizers();
        }

        public void ResetSheet() {
            if (_paperGraph == null) {
                Debug.LogError("No PaperGraph component found on this GameObject.");
                return;
            }

            _paperGraph.Vertices.Clear();
            _paperGraph.Edges.Clear();
            _paperGraph.Faces.Clear();
            _paperGraph.Tags.Clear();
            _paperGraph.CreateSheet(_paperGraph.Width, _paperGraph.Height);
            RefreshVisualizers();
        }

        public void UpdateFoldFromDrag(Vector3 dragStartLocal, Vector3 dragCurrentLocal) {
            if (!TryComputeFoldAxisFromDrag(dragStartLocal, dragCurrentLocal, out Vector3 candidateP1, out Vector3 candidateP2))
                return;

            if (IsFoldCandidateInvalid(dragStartLocal, dragCurrentLocal, candidateP1, candidateP2)) {
                FreezeAtLastValidFold();
                return;
            }

            FoldPoint1 = candidateP1;
            FoldPoint2 = candidateP2;
            FoldPlaneVector = DragPlaneNormal;
            _lastValidDragEnd = dragCurrentLocal;
            UpdatePreview();
        }

        private void RefreshVisualizers() {
            _authoringVisualizer?.UpdateMesh();
            _previewVisualizer?.UpdateMesh();
            DecalController.Instance?.OnMeshUpdated();
        }

        private void OnDrawGizmos() {
            if (!ShowFoldGizmo) return;

            Matrix4x4 localToWorld = transform.localToWorldMatrix;

            Vector3 foldAxis = (FoldPoint2 - FoldPoint1).normalized;
            Vector3 foldNormal = Vector3.Cross(foldAxis, FoldPlaneVector).normalized;
            float gizmoWidth = Vector3.Distance(FoldPoint1, FoldPoint2);

            if (foldNormal != Vector3.zero) {
                Gizmos.matrix = localToWorld;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(FoldPoint1, FoldPoint2);

                Gizmos.DrawSphere(FoldPoint1, 0.02f);
                Gizmos.DrawSphere(FoldPoint2, 0.02f);

                Vector3 foldCenter = (FoldPoint1 + FoldPoint2) * 0.5f;
                Quaternion foldRotation = Quaternion.LookRotation(foldNormal, foldAxis);

                Gizmos.color = GizmoColor;
                Gizmos.matrix = localToWorld * Matrix4x4.TRS(foldCenter, foldRotation, Vector3.one);
                Gizmos.DrawCube(Vector3.zero, new Vector3(GizmoHeight, gizmoWidth, 0.001f));

                Gizmos.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, 1f);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(GizmoHeight, gizmoWidth, 0.001f));

                Gizmos.matrix = localToWorld;
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(foldCenter, foldNormal * GizmoHeight * 0.5f);

                Gizmos.matrix = Matrix4x4.identity;
            }

            Vector3 idealDrag = IdealDragPosition - DragHandlePosition;
            if (idealDrag.sqrMagnitude > 0.00001f) {
                Vector3 idealFoldAxisDir = Vector3.Cross(DragPlaneNormal, idealDrag.normalized).normalized;
                if (idealFoldAxisDir.sqrMagnitude > 0.0001f) {
                    Gizmos.matrix = localToWorld;
                    Gizmos.color = Color.yellow;
                    Vector3 startP1 = DragHandlePosition + idealFoldAxisDir * FoldLineHalfLength;
                    Vector3 startP2 = DragHandlePosition - idealFoldAxisDir * FoldLineHalfLength;
                    Gizmos.DrawLine(startP1, startP2);
                    Gizmos.DrawSphere(startP1, 0.015f);
                    Gizmos.DrawSphere(startP2, 0.015f);
                }
            }

            if (HasFoldAxisLock) {
                Gizmos.matrix = localToWorld;

                foreach (FoldAxisLockSegment lockSegment in FoldAxisLocks) {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(lockSegment.P1, lockSegment.P2);

                    Gizmos.DrawSphere(lockSegment.P1, 0.018f);
                    Gizmos.DrawSphere(lockSegment.P2, 0.018f);

                    Vector3 lockMid = (lockSegment.P1 + lockSegment.P2) * 0.5f;
                    Vector3 lockDir = (lockSegment.P2 - lockSegment.P1).normalized;
                    Vector3 perpDir = Vector3.Cross(lockDir, FoldPlaneVector).normalized;
                    float diamondSize = 0.03f;
                    Gizmos.DrawLine(lockMid + perpDir * diamondSize, lockMid + lockDir * diamondSize);
                    Gizmos.DrawLine(lockMid + lockDir * diamondSize, lockMid - perpDir * diamondSize);
                    Gizmos.DrawLine(lockMid - perpDir * diamondSize, lockMid - lockDir * diamondSize);
                    Gizmos.DrawLine(lockMid - lockDir * diamondSize, lockMid + perpDir * diamondSize);
                }

                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}
