using UnityEngine;

public class PaperGraphController : MonoBehaviour
{
    [Header("Fold")]
    public Vector3 foldPoint1 = new Vector3(-0.5f, 0, 0);
    public Vector3 foldPoint2 = new Vector3(0.5f, 0, 0);
    public Vector3 foldPlaneVector = Vector3.forward;
    public float foldDegrees = 180f;

    [Header("Preview")]
    public PaperGraph previewGraph;

    [Header("Gizmo")]
    public bool showFoldGizmo = true;
    public float gizmoSize = 2f;
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
        previewGraph.ExecuteFold(foldPoint1, foldPoint2, foldPlaneVector, foldDegrees);

        CacheFoldValues();
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

        paperGraph.ExecuteFold(foldPoint1, foldPoint2, foldPlaneVector, foldDegrees);
    }

    public void UndoFold() {
        if (paperGraph == null)
            paperGraph = GetComponent<PaperGraph>();

        if (paperGraph == null) {
            Debug.LogError("No PaperGraph component found on this GameObject.");
            return;
        }

        paperGraph.Undo();
    }

    public void RedoFold() {
        if (paperGraph == null)
            paperGraph = GetComponent<PaperGraph>();

        if (paperGraph == null) {
            Debug.LogError("No PaperGraph component found on this GameObject.");
            return;
        }

        paperGraph.Redo();
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
        paperGraph.CreateSheet(paperGraph.width, paperGraph.height);
    }

    private void OnDrawGizmos() {
        if (!showFoldGizmo) return;

        Vector3 foldAxis = (foldPoint2 - foldPoint1).normalized;
        Vector3 foldNormal = Vector3.Cross(foldAxis, foldPlaneVector).normalized;

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
                Gizmos.DrawCube(Vector3.zero, new Vector3(gizmoSize, gizmoSize, 0.001f));

                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(gizmoSize, gizmoSize, 0.001f));

                // Draw the fold plane normal
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(foldCenter, foldNormal * gizmoSize * 0.5f);
            }
    }
}
