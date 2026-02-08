using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(KinematicBody))]
public class FlightController : MonoBehaviour
{
    private KinematicBody body;

    [Header("Control Mode")]
    [SerializeField] private bool useMouseControl = false;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float mouseSmoothing = 0.1f;

    [SerializeField] private float pitch = 0f;
    public float Pitch => pitch;

    [SerializeField] private Transform meshTransform;
    private Vector3 meshRotation;
    private float yaw = 0f;
    private float targetPitch = 0f;
    private float targetYaw = 0f;

    private float roll = 0f;
    public float Roll => roll;
    // private float targetRoll = 0f;


    [Header("Flight Physics")]
    [SerializeField] private float gravity = 0.08f;
    [SerializeField] private float lift = 0.06f;
    [SerializeField] private float diveRate = 0.1f;
    [SerializeField] private float climbRate = 0.04f;
    [SerializeField] private float climbEfficiency = 3.5f;
    [SerializeField] private float turnInterpolation = 0.1f;
    [SerializeField] private float xDrag = 0.99f;
    [SerializeField] private float yDrag = 0.98f;
    [SerializeField] private float zDrag = 0.99f;

    [Header("Input Tuning")]
    [SerializeField] private float pitchSpeed = 45f;
    [SerializeField] private float maxPitch = 90f;
    [SerializeField] private float yawSpeed = 45f;
    [SerializeField] private float rollSpeed = 45f;
    [SerializeField] private float rollBackSpeed = 45f;

    [SerializeField] private float maxRoll = 90f;

    [SerializeField] private float boostSpeed = 150f;

    [Header("Initial Speed")]
    [SerializeField] private float initialSpeed = 10f;


    void Start()
    {
        body = GetComponent<KinematicBody>();
        body.Velocity = transform.forward * initialSpeed;
        
        if (useMouseControl)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        meshRotation = meshTransform.eulerAngles;
    }

    void FixedUpdate()
    {
        ProcessInput();
        UpdateVelocity();
        UpdateRotation();
    }

    private void UpdateVelocity()
    {
        Vector3 velocity = body.Velocity;

        float pitchRadians = pitch * Mathf.Deg2Rad;
        float cosPitch = Mathf.Cos(pitchRadians);
        float sinPitch = Mathf.Sin(pitchRadians);

        Vector3 lookDirection = transform.forward;
        float horizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

        // Gravity
        velocity.y -= gravity;

        // Lift
        velocity.y += cosPitch * cosPitch * lift;

        // Convert dive speed into forward speed
        if (velocity.y < 0 && cosPitch > 0)
        {
            float yAcc = velocity.y * -diveRate * cosPitch * cosPitch;
            velocity.y += yAcc;
            velocity.x += lookDirection.x * yAcc / cosPitch;
            velocity.z += lookDirection.z * yAcc / cosPitch;
        }

        // Climbing
        if (pitchRadians < 0)
        {
            float yAcc = horizontalSpeed * -sinPitch * climbRate;
            velocity.y += yAcc * climbEfficiency;
            velocity.x -= lookDirection.x * yAcc / cosPitch;
            velocity.z -= lookDirection.z * yAcc / cosPitch;
        }

        // Redirect horizontal speed toward look direction
        if (cosPitch > 0)
        {
            velocity.x += (lookDirection.x / cosPitch * horizontalSpeed - velocity.x) * turnInterpolation;
            velocity.z += (lookDirection.z / cosPitch * horizontalSpeed - velocity.z) * turnInterpolation;
        }

        // Drag
        velocity.x *= xDrag;
        velocity.y *= yDrag;
        velocity.z *= zDrag;

        body.Velocity = velocity;
    }

    private void UpdateRotation()
    {
        body.MoveRotation(Quaternion.Euler(pitch, yaw, 0f));

        if (meshTransform != null)
        {
            meshTransform.localRotation = Quaternion.Euler(roll + meshRotation.x, meshRotation.y, meshRotation.z);
        }
    }

    private void ProcessInput()
    {
        if (useMouseControl)
            ProcessMouseInput();
        else
            ProcessKeyboardInput();
        
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
        roll = Mathf.Clamp(roll, -maxRoll, maxRoll);
    }

    private void ProcessKeyboardInput()
    {
        Vector2 move = InputManager.Instance.MoveInput;

        pitch += move.y * pitchSpeed * Time.fixedDeltaTime;

        if (move.x != 0f)
        {
            yaw += move.x * yawSpeed * Time.fixedDeltaTime;
            roll += move.x * rollSpeed * Time.fixedDeltaTime;
        }

        if (InputManager.Instance.BoostPressed)
            Boost();

        // Roll back to level when no lateral input
        if (move.x == 0f)
        {
            if (roll > 0f)
            {
                roll -= rollBackSpeed * Time.fixedDeltaTime;
                if (roll < 0f) roll = 0f;
            }
            else if (roll < 0f)
            {
                roll += rollBackSpeed * Time.fixedDeltaTime;
                if (roll > 0f) roll = 0f;
            }
        }
    }

    private void ProcessMouseInput()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        
        targetYaw += mouseDelta.x * mouseSensitivity * Time.fixedDeltaTime;
        targetPitch -= mouseDelta.y * mouseSensitivity * Time.fixedDeltaTime;
        targetPitch = Mathf.Clamp(targetPitch, -90f, 90f);
        
        pitch = Mathf.Lerp(pitch, targetPitch, mouseSmoothing);
        yaw = Mathf.Lerp(yaw, targetYaw, mouseSmoothing);

        if (InputManager.Instance.BoostPressed)
            Boost();
        
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void Boost()
    {
        body.Velocity += transform.forward * boostSpeed;
    }
}