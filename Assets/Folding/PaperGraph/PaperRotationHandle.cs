using Crease.Managers.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Folding.PaperGraph
{
    /// <summary>
    /// Rotates the attached GameObject using the abstract InputManager actions.
    /// Rotation is strictly relative to the Camera's point of view,
    /// making it consistent regardless of the object's current orientation.
    /// </summary>
    public class PaperRotationHandle : MonoBehaviour
    {
        [Tooltip("Degrees of rotation per pixel of input movement.")]
        [FormerlySerializedAs("rotationSpeed")]
        public float RotationSpeed = 0.3f;

        [SerializeField] private Camera _trackedCamera;

        private void Update()
        {
            if (_trackedCamera == null || InputManager.Instance == null) return;

            if (InputManager.Instance.RotatePaperPressed)
            {
                Vector2 deltaPos = InputManager.Instance.RotatePaperDelta;

                // TEMP diagnostic, remove with the other [VirtualCursor] logs.
                if (deltaPos.sqrMagnitude > 0.01f)
                    Debug.Log($"[VirtualCursor] PaperRotationHandle rotating, delta={deltaPos}");

                transform.Rotate(_trackedCamera.transform.up, -deltaPos.x * RotationSpeed, Space.World);
                transform.Rotate(_trackedCamera.transform.right, deltaPos.y * RotationSpeed, Space.World);
            }
        }
    }
}
