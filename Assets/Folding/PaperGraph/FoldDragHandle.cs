using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A draggable handle that drives PaperGraphController fold values.
/// Attach this to a visible GameObject (e.g. a sphere) with a Collider.
/// Assign the folding camera in the Inspector via trackedCamera.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FoldDragHandle : MonoBehaviour
{
    [Tooltip("The PaperGraphController whose fold values this handle drives.")]
    public PaperGraphController controller;

    [Tooltip("Visual size scale for the handle gizmo.")]
    public float handleRadius = 0.05f;
    public Color handleColor = Color.cyan;
    public Color draggingColor = Color.yellow;

    [SerializeField]
    [Tooltip("Camera used for raycasting. If not set, falls back to Camera.main.")]
    private Camera trackedCamera;

    private Camera activeCamera;

    private bool isDragging = false;

    private Mouse mouse;

    private void Start() {
        mouse = Mouse.current;

        // Initialize handle position from controller (local → world)
        if (controller != null)
            transform.position = controller.transform.TransformPoint(controller.dragHandlePosition);
    }

    private void Update() {
        activeCamera = trackedCamera != null ? trackedCamera : Camera.main;
        if (mouse == null || activeCamera == null) return;

        if (mouse.leftButton.wasPressedThisFrame) {
            TryBeginDrag();
        }

        if (isDragging) {
            UpdateDrag();

            if (mouse.leftButton.wasReleasedThisFrame) {
                EndDrag();
            }
        }
    }

    private void TryBeginDrag() {
        Ray ray = activeCamera.ScreenPointToRay(mouse.position.ReadValue());

        // Raycast against this object's collider
        if (Physics.Raycast(ray, out RaycastHit hit)) {
            if (hit.collider.gameObject == gameObject) {
                isDragging = true;
            }
        }
    }

    private void UpdateDrag() {
        if (controller == null) return;

        // dragPlaneNormal is in local-space; convert to world for the raycast plane
        Vector3 localNormal = controller.dragPlaneNormal.normalized;
        if (localNormal.sqrMagnitude < 0.0001f) return;
        Vector3 worldNormal = controller.transform.TransformDirection(localNormal);

        // dragHandlePosition is local; convert to world for the plane anchor
        Vector3 localStartPos = controller.dragHandlePosition;
        Vector3 worldStartPos = controller.transform.TransformPoint(localStartPos);
        Plane dragPlane = new Plane(worldNormal, worldStartPos);

        Ray ray = activeCamera.ScreenPointToRay(mouse.position.ReadValue());

        if (dragPlane.Raycast(ray, out float enter)) {
            Vector3 hitPoint = ray.GetPoint(enter);

            // Move the handle to the world-space hit point
            transform.position = hitPoint;

            // Convert both start and hit to local-space for the controller
            Vector3 localHit = controller.transform.InverseTransformPoint(hitPoint);
            controller.UpdateFoldFromDrag(localStartPos, localHit);
        }
    }

    private void EndDrag() {
        isDragging = false;
    }

    /// <summary>
    /// Resets the handle back to its configured starting position.
    /// </summary>
    public void ResetHandle() {
        if (controller != null) {
            transform.position = controller.transform.TransformPoint(controller.dragHandlePosition);
        }
    }

    private void OnDrawGizmos() {
        Gizmos.color = isDragging ? draggingColor : handleColor;
        Gizmos.DrawSphere(transform.position, handleRadius);

        if (controller != null) {
            // Controller values are local-space — convert to world for gizmos
            Vector3 worldStartPos = controller.transform.TransformPoint(controller.dragHandlePosition);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(worldStartPos, transform.position);

            // Draw a small indicator of the drag plane normal (local → world)
            Vector3 worldNormal = controller.transform.TransformDirection(controller.dragPlaneNormal.normalized);
            if (worldNormal.sqrMagnitude > 0.0001f) {
                Gizmos.color = new Color(handleColor.r, handleColor.g, handleColor.b, 0.3f);
                Gizmos.DrawRay(worldStartPos, worldNormal * 0.2f);
            }
        }
    }
}
