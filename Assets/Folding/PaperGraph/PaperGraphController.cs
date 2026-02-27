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
        previewGraph.ExecuteFold(foldPoint1, foldPoint2, foldPlaneVector, foldDegrees, tag, filter, foldOffset);

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
    /// Call after changing dragHandlePosition to avoid stale fold axis values.
    /// </summary>
    public void RecalculateFoldAxis() {
        Vector3 dragDelta = dragHandlePosition - transform.position;
        if (dragDelta.sqrMagnitude < 0.00001f) return;

        Vector3 midpoint = (transform.position + dragHandlePosition) * 0.5f;
        Vector3 dragDir = dragDelta.normalized;
        Vector3 foldAxisDir = Vector3.Cross(dragPlaneNormal, dragDir).normalized;
        if (foldAxisDir.sqrMagnitude < 0.0001f) return;

        foldPoint1 = midpoint + foldAxisDir * foldLineHalfLength;
        foldPoint2 = midpoint - foldAxisDir * foldLineHalfLength;
        foldPlaneVector = dragPlaneNormal;
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
    /// dragStart is the position where the drag began (on the drag plane).
    /// dragCurrent is the current drag position (on the drag plane).
    /// </summary>
    public void UpdateFoldFromDrag(Vector3 dragStart, Vector3 dragCurrent) {
        Vector3 dragDelta = dragCurrent - dragStart;
        if (dragDelta.sqrMagnitude < 0.00001f) return;

        // Midpoint between start and current drag position
        Vector3 midpoint = (dragStart + dragCurrent) * 0.5f;

        // Fold axis direction: perpendicular to the drag direction, lying on the drag plane
        Vector3 dragDir = dragDelta.normalized;
        Vector3 foldAxisDir = Vector3.Cross(dragPlaneNormal, dragDir).normalized;

        if (foldAxisDir.sqrMagnitude < 0.0001f) return;

        // Set fold points along the fold axis, centered at the midpoint
        foldPoint1 = midpoint + foldAxisDir * foldLineHalfLength;
        foldPoint2 = midpoint - foldAxisDir * foldLineHalfLength;

        // Fold plane vector is the drag plane normal
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

        Vector3 foldAxis = (foldPoint2 - foldPoint1).normalized;
        Vector3 foldNormal = Vector3.Cross(foldAxis, foldPlaneVector).normalized;
        float gizmoWidth = Vector3.Distance(foldPoint1, foldPoint2);

            if (foldNormal != Vector3.zero) {
                // Draw the fold axis line
                Gizmos.color = Color.red;
                Gizmos.DrawLine(foldPoint1, foldPoint2);

                // Draw spheres at the two fold points
                Gizmos.DrawSphere(foldPoint1, 0.02f);
                Gizmos.DrawSphere(foldPoint2, 0.02f);

                // Draw the fold plane
                Vector3 foldCenter = (foldPoint1 + foldPoint2) * 0.5f;
                Quaternion foldRotation = Quaternion.LookRotation(foldNormal, foldAxis);

                Gizmos.color = gizmoColor;
                Gizmos.matrix = Matrix4x4.TRS(foldCenter, foldRotation, Vector3.one);
                Gizmos.DrawCube(Vector3.zero, new Vector3(gizmoHeight, gizmoWidth, 0.001f));

                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(gizmoHeight, gizmoWidth, 0.001f));

                // Draw the fold plane normal
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(foldCenter, foldNormal * gizmoHeight * 0.5f);
            }
    }
}
