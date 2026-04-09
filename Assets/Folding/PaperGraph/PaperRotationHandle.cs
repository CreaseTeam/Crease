using UnityEngine;

/// <summary>
/// Rotates the attached GameObject using the abstract InputManager actions.
/// Rotation is strictly relative to the Camera's point of view, 
/// making it consistent regardless of the object's current orientation.
/// </summary>
public class PaperRotationHandle : MonoBehaviour
{
    [Tooltip("Degrees of rotation per pixel of input movement.")]
    public float rotationSpeed = 0.3f;
    
    [SerializeField] private Camera trackedCamera; 

    private void Update() 
    {
        if (trackedCamera == null || InputManager.Instance == null) return;

        // Check if the rotation action toggle is currently held
        if (InputManager.Instance.RotatePaperPressed) 
        {
            // Fetch the continuously evaluating delta (pointer delta / stick value)
            Vector2 deltaPos = InputManager.Instance.RotatePaperDelta;

            // Horizontal abstract drag rotates the object around the Camera's Up axis
            transform.Rotate(trackedCamera.transform.up, -deltaPos.x * rotationSpeed, Space.World);

            // Vertical abstract drag rotates the object around the Camera's Right axis
            transform.Rotate(trackedCamera.transform.right, deltaPos.y * rotationSpeed, Space.World);
        }
    }
}