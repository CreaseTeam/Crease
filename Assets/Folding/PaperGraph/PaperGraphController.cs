using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("selectedFilterTagIndex")]
        public int SelectedFilterTagIndex = 0;

        [Header("Drag Handle")]
        [FormerlySerializedAs("dragHandlePosition")]
        public Vector3 DragHandlePosition = Vector3.zero;
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

        // When set by FoldInstructionRunner, RecalculateFoldAxis will refuse to move
        // FoldPoint1/FoldPoint2 to a position whose axis line crosses this segment.
        [HideInInspector]
        [FormerlySerializedAs("hasFoldAxisLock")]
        public bool HasFoldAxisLock = false;
        [HideInInspector]
        [FormerlySerializedAs("foldAxisLockP1")]
        public Vector3 FoldAxisLockP1;
        [HideInInspector]
        [FormerlySerializedAs("foldAxisLockP2")]
        public Vector3 FoldAxisLockP2;
        [HideInInspector]
        [FormerlySerializedAs("lockedFoldPoint1")]
        public Vector3 LockedFoldPoint1;
        [HideInInspector]
        [FormerlySerializedAs("lockedFoldPoint2")]
        public Vector3 LockedFoldPoint2;

        private Vector3 _prevFoldPoint1;
        private Vector3 _prevFoldPoint2;
        private Vector3 _prevFoldPlaneVector;
        private float _prevFoldDegrees;

        public string SelectedFilterTag {
            get {
                if (_paperGraph == null || _paperGraph.Tags == null || _paperGraph.Tags.Count == 0)
                    return null;
                var tagKeys = new System.Collections.Generic.List<string>(_paperGraph.Tags.Keys);
                if (SelectedFilterTagIndex <= 0 || SelectedFilterTagIndex > tagKeys.Count)
                    return null;
                return tagKeys[SelectedFilterTagIndex - 1];
            }
        }

        private void Awake() {
            _paperGraph = GetComponent<PaperGraph>();
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
            bool valid = PreviewGraph.ExecuteFold(FoldPoint1, FoldPoint2, FoldPlaneVector, FoldDegrees, tag, SelectedFilterTag, FoldOffset);

            if (!valid) {
                FoldPoint1 = LockedFoldPoint1;
                FoldPoint2 = LockedFoldPoint2;
            } else {
                LockedFoldPoint1 = FoldPoint1;
                LockedFoldPoint2 = FoldPoint2;
            }

            CacheFoldValues();
            RefreshVisualizers();
        }

        public void ExecuteFoldAction() {
            if (_paperGraph == null) {
                Debug.LogError("No PaperGraph component found on this GameObject.");
                return;
            }

            string tag = string.IsNullOrEmpty(FoldTagName) ? null : FoldTagName;
            _paperGraph.ExecuteFold(FoldPoint1, FoldPoint2, FoldPlaneVector, FoldDegrees, tag, SelectedFilterTag, FoldOffset);
            RefreshVisualizers();
        }

        public void RecalculateFoldAxis() {
            Vector3 dragDelta = DragHandlePosition;
            if (dragDelta.sqrMagnitude < 0.00001f) return;

            Vector3 midpoint = DragHandlePosition * 0.5f;
            Vector3 dragDir = dragDelta.normalized;
            Vector3 foldAxisDir = Vector3.Cross(DragPlaneNormal, dragDir).normalized;
            if (foldAxisDir.sqrMagnitude < 0.0001f) return;

            Vector3 candidateP1 = midpoint + foldAxisDir * FoldLineHalfLength;
            Vector3 candidateP2 = midpoint - foldAxisDir * FoldLineHalfLength;

            if (HasFoldAxisLock) {
                bool crosses = FoldAxisCrossesLockSegment(candidateP1, candidateP2, FoldAxisLockP1, FoldAxisLockP2, DragPlaneNormal);
                if (crosses) {
                    FoldPoint1 = LockedFoldPoint1;
                    FoldPoint2 = LockedFoldPoint2;
                    FoldPlaneVector = DragPlaneNormal;
                    return;
                }
                LockedFoldPoint1 = candidateP1;
                LockedFoldPoint2 = candidateP2;
            }

            FoldPoint1 = candidateP1;
            FoldPoint2 = candidateP2;
            FoldPlaneVector = DragPlaneNormal;
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

        public void ClearPreview() {
            if (_paperGraph == null || PreviewGraph == null) return;

            PaperGraphSnapshot snapshot = _paperGraph.CreateSnapshot();
            PreviewGraph.RestoreSnapshot(snapshot);
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
            Vector3 dragDelta = dragCurrentLocal - dragStartLocal;
            if (dragDelta.sqrMagnitude < 0.00001f) return;

            Vector3 midpoint = (dragStartLocal + dragCurrentLocal) * 0.5f;
            Vector3 dragDir = dragDelta.normalized;
            Vector3 foldAxisDir = Vector3.Cross(DragPlaneNormal, dragDir).normalized;
            if (foldAxisDir.sqrMagnitude < 0.0001f) return;

            Vector3 candidateP1 = midpoint + foldAxisDir * FoldLineHalfLength;
            Vector3 candidateP2 = midpoint - foldAxisDir * FoldLineHalfLength;

            if (HasFoldAxisLock) {
                bool crosses = FoldAxisCrossesLockSegment(candidateP1, candidateP2, FoldAxisLockP1, FoldAxisLockP2, DragPlaneNormal);
                if (crosses) {
                    FoldPoint1 = LockedFoldPoint1;
                    FoldPoint2 = LockedFoldPoint2;
                    FoldPlaneVector = DragPlaneNormal;
                    UpdatePreview();
                    return;
                }
                LockedFoldPoint1 = candidateP1;
                LockedFoldPoint2 = candidateP2;
            }

            FoldPoint1 = candidateP1;
            FoldPoint2 = candidateP2;
            FoldPlaneVector = DragPlaneNormal;

            UpdatePreview();
        }

        private void RefreshVisualizers() {
            foreach (PaperGraphVisualizer vis in FindObjectsByType<PaperGraphVisualizer>(FindObjectsSortMode.None)) {
                vis.UpdateMesh();
            }
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

            if (HasFoldAxisLock) {
                Gizmos.matrix = localToWorld;

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(FoldAxisLockP1, FoldAxisLockP2);

                Gizmos.DrawSphere(FoldAxisLockP1, 0.018f);
                Gizmos.DrawSphere(FoldAxisLockP2, 0.018f);

                Vector3 lockMid = (FoldAxisLockP1 + FoldAxisLockP2) * 0.5f;
                Vector3 lockDir = (FoldAxisLockP2 - FoldAxisLockP1).normalized;
                Vector3 perpDir = Vector3.Cross(lockDir, FoldPlaneVector).normalized;
                float diamondSize = 0.03f;
                Gizmos.DrawLine(lockMid + perpDir * diamondSize, lockMid + lockDir * diamondSize);
                Gizmos.DrawLine(lockMid + lockDir * diamondSize, lockMid - perpDir * diamondSize);
                Gizmos.DrawLine(lockMid - perpDir * diamondSize, lockMid - lockDir * diamondSize);
                Gizmos.DrawLine(lockMid - lockDir * diamondSize, lockMid + perpDir * diamondSize);

                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}
