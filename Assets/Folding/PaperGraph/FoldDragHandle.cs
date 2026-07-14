using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Crease.Folding.Paper
{
    /// <summary>
    /// A draggable handle that drives PaperGraphController fold values.
    /// Attach this to a visible GameObject (e.g. a sphere) with a Collider.
    /// Assign the folding camera in the Inspector via trackedCamera.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FoldDragHandle : MonoBehaviour
    {
        [Tooltip("The PaperGraphController whose fold values this handle drives.")]
        [FormerlySerializedAs("controller")]
        public PaperGraphController Controller;

        [Tooltip("Visual size scale for the handle gizmo.")]
        [FormerlySerializedAs("handleRadius")]
        public float HandleRadius = 0.05f;
        [FormerlySerializedAs("handleColor")]
        public Color HandleColor = Color.cyan;
        [FormerlySerializedAs("draggingColor")]
        public Color DraggingColor = Color.yellow;

        [SerializeField]
        [Tooltip("Camera used for raycasting. If not set, falls back to Camera.main.")]
        private Camera _trackedCamera;

        private Camera _activeCamera;
        private bool _isDragging;
        private Mouse _mouse;

        private void Start() {
            _mouse = Mouse.current;

            if (Controller != null)
                transform.position = Controller.transform.TransformPoint(Controller.DragHandlePosition);
        }

        private void Update() {
            _activeCamera = _trackedCamera != null ? _trackedCamera : Camera.main;
            if (_mouse == null || _activeCamera == null) return;

            if (_mouse.leftButton.wasPressedThisFrame) {
                TryBeginDrag();
            }

            if (_isDragging) {
                UpdateDrag();

                if (_mouse.leftButton.wasReleasedThisFrame) {
                    EndDrag();
                }
            }
        }

        private void TryBeginDrag() {
            Ray ray = _activeCamera.ScreenPointToRay(_mouse.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit)) {
                if (hit.collider.gameObject == gameObject) {
                    _isDragging = true;
                }
            }
        }

        private void UpdateDrag() {
            if (Controller == null) return;

            Vector3 localNormal = Controller.DragPlaneNormal.normalized;
            if (localNormal.sqrMagnitude < 0.0001f) return;
            Vector3 worldNormal = Controller.transform.TransformDirection(localNormal);

            Vector3 localStartPos = Controller.IsAccordionDragStep
                ? Controller.AccordionDragStart
                : Controller.DragHandlePosition;
            Vector3 worldStartPos = Controller.transform.TransformPoint(localStartPos);
            Plane dragPlane = new Plane(worldNormal, worldStartPos);

            Ray ray = _activeCamera.ScreenPointToRay(_mouse.position.ReadValue());

            if (dragPlane.Raycast(ray, out float enter)) {
                Vector3 hitPoint = ray.GetPoint(enter);
                Vector3 localHit = Controller.transform.InverseTransformPoint(hitPoint);

                if (Controller.IsAccordionDragStep) {
                    Vector3 worldEnd = Controller.transform.TransformPoint(Controller.AccordionDragEnd);
                    Vector3 worldLine = worldEnd - worldStartPos;
                    if (worldLine.sqrMagnitude > 0.00001f) {
                        float t = Vector3.Dot(hitPoint - worldStartPos, worldLine) / worldLine.sqrMagnitude;
                        t = Mathf.Clamp01(t);
                        hitPoint = worldStartPos + worldLine * t;
                        localHit = Controller.transform.InverseTransformPoint(hitPoint);
                    }

                    transform.position = hitPoint;
                    Controller.UpdateAccordionFromDrag(localHit);
                    return;
                }

                transform.position = hitPoint;
                Controller.UpdateFoldFromDrag(localStartPos, localHit);
            }
        }

        private void EndDrag() {
            _isDragging = false;
        }

        public void ResetHandle() {
            if (Controller != null) {
                Vector3 localPos = Controller.IsAccordionDragStep
                    ? Controller.AccordionDragStart
                    : Controller.DragHandlePosition;
                transform.position = Controller.transform.TransformPoint(localPos);
            }
        }

        private void OnDrawGizmos() {
            Gizmos.color = _isDragging ? DraggingColor : HandleColor;
            Gizmos.DrawSphere(transform.position, HandleRadius);

            if (Controller != null) {
                Vector3 worldStartPos = Controller.transform.TransformPoint(Controller.DragHandlePosition);
                Gizmos.color = Color.white;
                Gizmos.DrawLine(worldStartPos, transform.position);

                Vector3 worldNormal = Controller.transform.TransformDirection(Controller.DragPlaneNormal.normalized);
                if (worldNormal.sqrMagnitude > 0.0001f) {
                    Gizmos.color = new Color(HandleColor.r, HandleColor.g, HandleColor.b, 0.3f);
                    Gizmos.DrawRay(worldStartPos, worldNormal * 0.2f);
                }
            }
        }
    }
}
