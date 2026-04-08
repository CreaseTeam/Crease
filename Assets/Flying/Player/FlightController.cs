using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(KinematicBody))]
public class FlightController : MonoBehaviour
{
    private KinematicBody body;
    private DashController dashController;

    [SerializeField] private float pitch = 0f;
    public float Pitch => pitch;

    [SerializeField] private Transform meshTransform;
    public Transform MeshTransform => meshTransform;
    private Vector3 meshRotation;
    private float yaw = 0f;

    private float roll = 0f;
    public float Roll => roll;
    // private float targetRoll = 0f;

    private float rollOffset = 0f;
    public float RollOffset
    {
        get => rollOffset;
        set => rollOffset = value;
    }


    [Header("Flight Physics")]
    [SerializeField] private float divingGravity = 0.12f;
    [SerializeField] private float climbingGravity = 0.04f;
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
    [SerializeField] private float minimumVelocity = 5f;


    void Start()
    {
        body = GetComponent<KinematicBody>();
        dashController = GetComponent<DashController>();

        // Initialize pitch and yaw from the current transform rotation
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
        // Normalize pitch to [-180, 180] range
        if (pitch > 180f) pitch -= 360f;

        body.Velocity = transform.forward * initialSpeed;
        
        meshRotation = meshTransform.localEulerAngles;
    }

    void FixedUpdate()
    {
        ProcessInput();
        if (dashController == null || !dashController.IsDashing)
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

        // Kind Gravity Lerp (negative pitch = climbing, positive pitch = diving)
        float gravityT = Mathf.InverseLerp(-maxPitch, maxPitch, pitch);
        float currentGravity = Mathf.Lerp(climbingGravity, divingGravity, gravityT);
        velocity.y -= currentGravity * Time.fixedDeltaTime;

        // Lift
        // velocity.y += cosPitch * cosPitch * lift;
        velocity.y += cosPitch * cosPitch * lift * Time.fixedDeltaTime;

        // Convert dive speed into forward speed
        if (velocity.y < 0 && cosPitch > 0)
        {
            // float yAcc = velocity.y * -diveRate * cosPitch * cosPitch;
            float yAcc = velocity.y * -diveRate * cosPitch * cosPitch * Time.fixedDeltaTime;
            velocity.y += yAcc;
            velocity.x += lookDirection.x * yAcc / cosPitch;
            velocity.z += lookDirection.z * yAcc / cosPitch;
        }

        // Climbing
        if (pitchRadians < 0)
        {
            // float yAcc = horizontalSpeed * -sinPitch * -sinPitch * climbRate;
            float yAcc = horizontalSpeed * -sinPitch * -sinPitch * climbRate * Time.fixedDeltaTime;
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

        if (velocity.magnitude < minimumVelocity)
        {
            velocity = velocity.normalized * minimumVelocity;
        }

        body.Velocity = velocity;
    }

    private void UpdateRotation()
    {
        body.MoveRotation(Quaternion.Euler(pitch, yaw, 0f));

        if (meshTransform != null)
        {
            meshTransform.localRotation = Quaternion.Euler(roll + rollOffset + meshRotation.x, meshRotation.y, meshRotation.z);
        }
    }

    private void ProcessInput()
    {
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

    private void Boost()
    {
        body.Velocity += transform.forward * boostSpeed;
    }
}