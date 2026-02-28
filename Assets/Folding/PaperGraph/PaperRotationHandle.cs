using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rotates the attached GameObject by right-click dragging.
/// Rotation is strictly relative to the Camera's point of view, 
/// making it consistent regardless of the object's current orientation.
/// </summary>
public class PaperRotationHandle : MonoBehaviour
{
    [Tooltip("Degrees of rotation per pixel of mouse movement.")]
    public float rotationSpeed = 0.3f;

    private Mouse mouse;
    private bool isRotating = false;
    
    [SerializeField] private Camera trackedCamera; 

    private void Start() 
    {
        mouse = Mouse.current;
    }

    private void Update() 
    {
        if (mouse == null || trackedCamera == null) return;

        if (mouse.rightButton.wasPressedThisFrame) 
        {
            isRotating = true;
        }
        else if (mouse.rightButton.wasReleasedThisFrame) 
        {
            isRotating = false;
        }

        if (isRotating) 
        {
            Vector2 deltaPos = mouse.delta.ReadValue();

            // Horizontal mouse drag rotates the object around the Camera's Up axis
            transform.Rotate(trackedCamera.transform.up, -deltaPos.x * rotationSpeed, Space.World);

            // Vertical mouse drag rotates the object around the Camera's Right axis
            transform.Rotate(trackedCamera.transform.right, deltaPos.y * rotationSpeed, Space.World);
        }
    }
}