using UnityEngine;

public class PaperGraphController : MonoBehaviour
{
    [Header("Fold")]
    public Vector3 foldPoint1 = new Vector3(-0.5f, 0, 0);
    public Vector3 foldPoint2 = new Vector3(0.5f, 0, 0);
    public Vector3 foldPlaneVector = Vector3.forward;
    public float foldDegrees = 180f;
    public float foldOffset = 0f;
    public string foldTagName = "";
    [HideInInspector] public int selectedFilterTagIndex = 0;

    [Header("Drag Handle")]
    public Vector3 dragHandlePosition = Vector3.zero;
    public Vector3 dragPlaneNormal = Vector3.up;
    public float foldLineHalfLength = 1f;

    [Header("Preview")]
    public PaperGraph previewGraph;

    [Header("Gizmo")]
    public bool showFoldGizmo = true;
    public float gizmoHeight = 2f;
    public Color gizmoColor = new Color(1f, 0.5f, 0f, 0.25f);

    private PaperGraph paperGraph;

    // ── Locked fold axis ──────────────────────────────────────────────────────
    // When set by FoldInstructionRunner, RecalculateFoldAxis will refuse to move
    // foldPoint1/foldPoint2 to a position whose axis line crosses this segment.
    [HideInInspector] public bool hasFoldAxisLock = false;
    [HideInInspector] public Vector3 foldAxisLockP1;
    [HideInInspector] public Vector3 foldAxisLockP2;
    // The last axis that was accepted while the lock is active:
    [HideInInspector] public Vector3 lockedFoldPoint1;
    [HideInInspector] public Vector3 lockedFoldPoint2;

    // Cache previous values to detect changes
    private Vector3 prevFoldPoint1;
    private Vector3 prevFoldPoint2;
    private Vector3 prevFoldPlaneVector;
    private float prevFoldDegrees;

    private void Awake() {
        paperGraph = GetComponent<PaperGraph>();
        CacheFoldValues();
    }

    private void OnValidate() {
        UpdatePreview();
    }

    private void CacheFoldValues() {
        prevFoldPoint1 = foldPoint1;
        prevFoldPoint2 = foldPoint2;
        prevFoldPlaneVector = foldPlaneVector;
        prevFoldDegrees = foldDegrees;
    }

    private bool FoldValuesChanged() {
        return foldPoint1 != prevFoldPoint1 ||
               foldPoint2 != prevFoldPoint2 ||
               foldPlaneVector != prevFoldPlaneVector ||
               !Mathf.Approximately(foldDegrees, prevFoldDegrees);
    }

    /// <summary>
    /// Copies the current PaperGraph state into the preview graph and applies the pending fold.
    /// </summary>
    public void UpdatePreview() {
        if (paperGraph == null)
            paperGraph = GetComponent<PaperGraph>();
        if (paperGraph == null || previewGraph == null) return;

        PaperGraphSnapshot snapshot = paperGraph.CreateSnapshot();
        previewGraph.RestoreSnapshot(snapshot);
        string tag = string.IsNullOrEmpty(foldTagName) ? null : foldTagName;
        string filter = GetSelectedFilterTag();
        bool valid = previewGraph.ExecuteFold(foldPoint1, foldPoint2, foldPlaneVector, foldDegrees, tag, filter, foldOffset);

        if (!valid) {
            // The current fold position produces invalid geometry — freeze the axis
            // so the next accepted position can restore it naturally.
            foldPoint1 = lockedFoldPoint1;
            foldPoint2 = lockedFoldPoint2;
        } else {
            lockedFoldPoint1 = foldPoint1;
            lockedFoldPoint2 = foldPoint2;
        }

        CacheFoldValues();
        RefreshVisualizers();
    }

    /// <summary>
    /// Called by the custom inspector button.
    /// </summary>
    public void ExecuteFoldAction() {
        if (paperGraph == null)
            paperGraph = GetComponent<PaperGraph>();

        if (paperGraph == null) {
            Debug.LogError("No PaperGraph component found on this GameObject.");
            return;
        }

        string tag = string.IsNullOrEmpty(foldTagName) ? null : foldTagName;
        string filter = GetSelectedFilterTag();
        paperGraph.ExecuteFold(foldPoint1, foldPoint2, foldPlaneVector, foldDegrees, tag, filter, foldOffset);
        RefreshVisualizers();
    }

    /// <summary>
    /// Recomputes foldPoint1/foldPoint2 from the current dragHandlePosition.
    /// All positions are in local-space relative to this transform.
    /// Call after changing dragHandlePosition to avoid stale fold axis values.
    /// </summary>
    public void RecalculateFoldAxis() {
        Vector3 dragDelta = dragHandlePosition;
        if (dragDelta.sqrMagnitude < 0.00001f) return;

        Vector3 midpoint = dragHandlePosition * 0.5f;
        Vector3 dragDir = dragDelta.normalized;
        Vector3 foldAxisDir = Vector3.Cross(dragPlaneNormal, dragDir).normalized;
        if (foldAxisDir.sqrMagnitude < 0.0001f) return;

        Vector3 candidateP1 = midpoint + foldAxisDir * foldLineHalfLength;
        Vector3 candidateP2 = midpoint - foldAxisDir * foldLineHalfLength;

        if (hasFoldAxisLock) {
            bool crosses = FoldAxisCrossesLockSegment(candidateP1, candidateP2, foldAxisLockP1, foldAxisLockP2, dragPlaneNormal);
            if (crosses) {
                foldPoint1 = lockedFoldPoint1;
                foldPoint2 = lockedFoldPoint2;
                foldPlaneVector = dragPlaneNormal;
                return;
            }
            lockedFoldPoint1 = candidateP1;
            lockedFoldPoint2 = candidateP2;
        }

        foldPoint1 = candidateP1;
        foldPoint2 = candidateP2;
        foldPlaneVector = dragPlaneNormal;
    }

    /// <summary>
    /// Returns true when the lock segment CD straddles (is crossed by) the infinite
    /// fold-axis line passing through A and B, when projected onto the plane
    /// perpendicular to <paramref name="normal"/>.
    /// We intentionally treat the fold axis as an infinite line — only the lock
    /// segment endpoints (C, D) need to be on opposite sides.
    /// </summary>
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

    /// <summary>
    /// Moves the drag handle position to the outermost point on the mesh in the direction of the drag plane normal.
    /// The drag handle's position along the drag plane remains constant.
    /// </summary>
    public void SnapDragHandleToOutside() {
        if (paperGraph == null)
            paperGraph = GetComponent<PaperGraph>();
        if (paperGraph == null || paperGraph.faces == null || paperGraph.faces.Count == 0) return;

        Vector3 N = dragPlaneNormal.normalized;
        if (N.sqrMagnitude < 0.0001f) return;

        Vector3 H0 = dragHandlePosition;
        float maxT = float.MinValue;
        bool found = false;

        foreach (Face face in paperGraph.faces) {
            if (face.vertices.Count < 3) continue;

            Vector3 faceNormal = Vector3.zero;
            Vector3 p0 = face.vertices[0].position;
            for (int i = 1; i < face.vertices.Count - 1; i++) {
                Vector3 p1 = face.vertices[i].position;
                Vector3 p2 = face.vertices[i + 1].position;
                faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
                if (faceNormal.sqrMagnitude > 0.000001f) {
                    faceNormal = faceNormal.normalized;
                    break;
                }
            }

            if (faceNormal.sqrMagnitude < 0.000001f) continue;

            float dotDirNormal = Vector3.Dot(N, faceNormal);
            // If the ray is parallel to the face, it doesn't cleanly intersect at a point
            if (Mathf.Abs(dotDirNormal) < 0.0001f) continue;

            float t = Vector3.Dot(p0 - H0, faceNormal) / dotDirNormal;
            Vector3 P = H0 + t * N;

            // Inside test (convex polygon)
            bool inside = true;
            for (int i = 0; i < face.vertices.Count; i++) {
                Vector3 vA = face.vertices[i].position;
                Vector3 vB = face.vertices[(i + 1) % face.vertices.Count].position;
                Vector3 edge = vB - vA;
                Vector3 toP = P - vA;
                Vector3 cross = Vector3.Cross(edge, toP);
                float signedArea = Vector3.Dot(cross, faceNormal);
                // Allow a small tolerance for floating point errors
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
            dragHandlePosition = H0 + maxT * N;
            RecalculateFoldAxis();
            UpdatePreview();

            foreach (FoldDragHandle handle in FindObjectsByType<FoldDragHandle>(FindObjectsSortMode.None)) {
                if (handle.controller == this) {
                    handle.ResetHandle();
                }
            }
        }
    }

    /// <summary>
    /// Clears the preview graph so no ghost fold is displayed.
    /// </summary>
    public void ClearPreview() {
        if (paperGraph == null)
            paperGraph = GetComponent<PaperGraph>();
        if (paperGraph == null || previewGraph == null) return;

        PaperGraphSnapshot snapshot = paperGraph.CreateSnapshot();
        previewGraph.RestoreSnapshot(snapshot);
        RefreshVisualizers();
    }

    /// <summary>
    /// Resolves the selected filter tag index into the actual tag name string.
    /// Returns null if "(None)" is selected or the graph has no tags.
    /// </summary>
    public string GetSelectedFilterTag() {
        if (paperGraph == null)
            paperGraph = GetComponent<PaperGraph>();
        if (paperGraph == null || paperGraph.tags == null || paperGraph.tags.Count == 0)
            return null;
        var tagKeys = new System.Collections.Generic.List<string>(paperGraph.tags.Keys);
        if (selectedFilterTagIndex <= 0 || selectedFilterTagIndex > tagKeys.Count)
            return null;
        return tagKeys[selectedFilterTagIndex - 1];
    }

    public void UndoFold() {
        if (paperGraph == null)
            paperGraph = GetComponent<PaperGraph>();

        if (paperGraph == null) {
            Debug.LogError("No PaperGraph component found on this GameObject.");
            return;
        }

        paperGraph.Undo();
        RefreshVisualizers();
    }

    public void RedoFold() {
        if (paperGraph == null)
            paperGraph = GetComponent<PaperGraph>();

        if (paperGraph == null) {
            Debug.LogError("No PaperGraph component found on this GameObject.");
            return;
        }

        paperGraph.Redo();
        RefreshVisualizers();
    }

    /// <summary>
    /// Clears all graph data and recreates a fresh sheet.
    /// </summary>
    public void ResetSheet() {
        if (paperGraph == null)
            paperGraph = GetComponent<PaperGraph>();

        if (paperGraph == null) {
            Debug.LogError("No PaperGraph component found on this GameObject.");
            return;
        }

        paperGraph.vertices.Clear();
        paperGraph.edges.Clear();
        paperGraph.faces.Clear();
        paperGraph.tags.Clear();
        paperGraph.CreateSheet(paperGraph.width, paperGraph.height);
        RefreshVisualizers();
    }

    /// <summary>
    /// Called by the drag handle script to update fold values based on drag movement.
    /// Both positions must be in local-space relative to this transform.
    /// </summary>
    public void UpdateFoldFromDrag(Vector3 dragStartLocal, Vector3 dragCurrentLocal) {
        Vector3 dragDelta = dragCurrentLocal - dragStartLocal;
        if (dragDelta.sqrMagnitude < 0.00001f) return;

        Vector3 midpoint = (dragStartLocal + dragCurrentLocal) * 0.5f;
        Vector3 dragDir = dragDelta.normalized;
        Vector3 foldAxisDir = Vector3.Cross(dragPlaneNormal, dragDir).normalized;
        if (foldAxisDir.sqrMagnitude < 0.0001f) return;

        Vector3 candidateP1 = midpoint + foldAxisDir * foldLineHalfLength;
        Vector3 candidateP2 = midpoint - foldAxisDir * foldLineHalfLength;

        if (hasFoldAxisLock) {
            bool crosses = FoldAxisCrossesLockSegment(candidateP1, candidateP2, foldAxisLockP1, foldAxisLockP2, dragPlaneNormal);
            if (crosses) {
                foldPoint1 = lockedFoldPoint1;
                foldPoint2 = lockedFoldPoint2;
                foldPlaneVector = dragPlaneNormal;
                UpdatePreview();
                return;
            }
            lockedFoldPoint1 = candidateP1;
            lockedFoldPoint2 = candidateP2;
        }

        foldPoint1 = candidateP1;
        foldPoint2 = candidateP2;
        foldPlaneVector = dragPlaneNormal;

        UpdatePreview();
    }

    /// <summary>
    /// Finds all PaperGraphVisualizers that reference this paper and refreshes their meshes.
    /// </summary>
    private void RefreshVisualizers() {
        foreach (PaperGraphVisualizer vis in FindObjectsByType<PaperGraphVisualizer>(FindObjectsSortMode.None)) {
            vis.UpdateMesh();
        }
    }

    private void OnDrawGizmos() {
        if (!showFoldGizmo) return;

        // All fold values are in local-space; use localToWorldMatrix so gizmos render correctly.
        Matrix4x4 localToWorld = transform.localToWorldMatrix;

        Vector3 foldAxis = (foldPoint2 - foldPoint1).normalized;
        Vector3 foldNormal = Vector3.Cross(foldAxis, foldPlaneVector).normalized;
        float gizmoWidth = Vector3.Distance(foldPoint1, foldPoint2);

            if (foldNormal != Vector3.zero) {
                // Draw the fold axis line
                Gizmos.matrix = localToWorld;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(foldPoint1, foldPoint2);

                // Draw spheres at the two fold points
                Gizmos.DrawSphere(foldPoint1, 0.02f);
                Gizmos.DrawSphere(foldPoint2, 0.02f);

                // Draw the fold plane
                Vector3 foldCenter = (foldPoint1 + foldPoint2) * 0.5f;
                Quaternion foldRotation = Quaternion.LookRotation(foldNormal, foldAxis);

                Gizmos.color = gizmoColor;
                Gizmos.matrix = localToWorld * Matrix4x4.TRS(foldCenter, foldRotation, Vector3.one);
                Gizmos.DrawCube(Vector3.zero, new Vector3(gizmoHeight, gizmoWidth, 0.001f));

                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(gizmoHeight, gizmoWidth, 0.001f));

                // Draw the fold plane normal
                Gizmos.matrix = localToWorld;
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(foldCenter, foldNormal * gizmoHeight * 0.5f);

                Gizmos.matrix = Matrix4x4.identity;
            }

        // ── Locked fold-axis gizmo ────────────────────────────────────────
        if (hasFoldAxisLock) {
            Gizmos.matrix = localToWorld;

            // Main lock segment in cyan
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(foldAxisLockP1, foldAxisLockP2);

            // Endpoint spheres
            Gizmos.DrawSphere(foldAxisLockP1, 0.018f);
            Gizmos.DrawSphere(foldAxisLockP2, 0.018f);

            // Diamond marker at midpoint to distinguish it from the active fold axis
            Vector3 lockMid = (foldAxisLockP1 + foldAxisLockP2) * 0.5f;
            Vector3 lockDir = (foldAxisLockP2 - foldAxisLockP1).normalized;
            Vector3 perpDir = Vector3.Cross(lockDir, foldPlaneVector).normalized;
            float diamondSize = 0.03f;
            Gizmos.DrawLine(lockMid + perpDir * diamondSize, lockMid + lockDir * diamondSize);
            Gizmos.DrawLine(lockMid + lockDir * diamondSize, lockMid - perpDir * diamondSize);
            Gizmos.DrawLine(lockMid - perpDir * diamondSize, lockMid - lockDir * diamondSize);
            Gizmos.DrawLine(lockMid - lockDir * diamondSize, lockMid + perpDir * diamondSize);

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
