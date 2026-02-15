using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A draggable handle that drives PaperGraphController fold values.
/// Attach this to a visible GameObject (e.g. a sphere) with a Collider.
/// Requires a Camera tagged "MainCamera" in the scene.
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

    private Camera mainCamera;
    private bool isDragging = false;

    private Mouse mouse;

    private void Start() {
        mainCamera = Camera.main;
        mouse = Mouse.current;

        // Initialize handle position from controller
        if (controller != null)
            transform.position = controller.dragHandlePosition;
    }

    private void Update() {
        if (mouse == null || mainCamera == null) return;

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
        Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());

        // Raycast against this object's collider
        if (Physics.Raycast(ray, out RaycastHit hit)) {
            if (hit.collider.gameObject == gameObject) {
                isDragging = true;
            }
        }
    }

    private void UpdateDrag() {
        if (controller == null) return;

        Vector3 planeNormal = controller.dragPlaneNormal.normalized;
        if (planeNormal.sqrMagnitude < 0.0001f) return;

        // Define the drag plane at the controller's configured handle position
        Vector3 startPos = controller.dragHandlePosition;
        Plane dragPlane = new Plane(planeNormal, startPos);

        Ray ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());

        if (dragPlane.Raycast(ray, out float enter)) {
            Vector3 hitPoint = ray.GetPoint(enter);

            // Move the handle to the hit point
            transform.position = hitPoint;

            // Update the controller fold values
            controller.UpdateFoldFromDrag(startPos, hitPoint);
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
            transform.position = controller.dragHandlePosition;
        }
    }

    private void OnDrawGizmos() {
        Gizmos.color = isDragging ? draggingColor : handleColor;
        Gizmos.DrawSphere(transform.position, handleRadius);

        if (controller != null) {
            // Draw line from start position to current handle position
            Vector3 startPos = controller.dragHandlePosition;
            Gizmos.color = Color.white;
            Gizmos.DrawLine(startPos, transform.position);

            // Draw a small indicator of the drag plane normal
            Vector3 normal = controller.dragPlaneNormal.normalized;
            if (normal.sqrMagnitude > 0.0001f) {
                Gizmos.color = new Color(handleColor.r, handleColor.g, handleColor.b, 0.3f);
                Gizmos.DrawRay(startPos, normal * 0.2f);
            }
        }
    }
}
