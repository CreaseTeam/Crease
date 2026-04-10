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
    [SerializeField]
    private FlightSettings settings;


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

        if (settings == null)
        {
            Debug.LogError($"FlightController on '{name}' requires a FlightSettings asset assigned.");
            enabled = false;
            return;
        }

        body.Velocity = transform.forward * settings.initialSpeed;
        
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
        float mp = settings.maxPitch;
        float gravityT = Mathf.InverseLerp(-mp, mp, pitch);
        float currentGravity = Mathf.Lerp(settings.climbingGravity, settings.divingGravity, gravityT);
        velocity.y -= currentGravity * Time.fixedDeltaTime;

        // Lift
        // velocity.y += cosPitch * cosPitch * lift;
        velocity.y += cosPitch * cosPitch * settings.lift * Time.fixedDeltaTime;

        // Convert dive speed into forward speed
        if (velocity.y < 0 && cosPitch > 0)
        {
            // float yAcc = velocity.y * -diveRate * cosPitch * cosPitch;
            float yAcc = velocity.y * -settings.diveRate * cosPitch * cosPitch * Time.fixedDeltaTime;
            velocity.y += yAcc;
            velocity.x += lookDirection.x * yAcc / cosPitch;
            velocity.z += lookDirection.z * yAcc / cosPitch;
        }

        // Climbing
        if (pitchRadians < 0)
        {
            // float yAcc = horizontalSpeed * -sinPitch * -sinPitch * climbRate;
            float yAcc = horizontalSpeed * -sinPitch * -sinPitch * settings.climbRate * Time.fixedDeltaTime;
            velocity.y += yAcc * settings.climbEfficiency;
            velocity.x -= lookDirection.x * yAcc / cosPitch;
            velocity.z -= lookDirection.z * yAcc / cosPitch;
        }

        // Redirect horizontal speed toward look direction
        if (cosPitch > 0)
        {
            float tInterp = settings.turnInterpolation;
            velocity.x += (lookDirection.x / cosPitch * horizontalSpeed - velocity.x) * tInterp * Time.fixedDeltaTime;
            velocity.z += (lookDirection.z / cosPitch * horizontalSpeed - velocity.z) * tInterp * Time.fixedDeltaTime;
            velocity.y += (lookDirection.y * velocity.magnitude - velocity.y) * tInterp * Time.fixedDeltaTime;
        }

        // Drag
        velocity.x *= settings.xDrag;
        velocity.y *= settings.yDrag;
        velocity.z *= settings.zDrag;

        float minVel = settings.minimumVelocity;
        if (velocity.magnitude < minVel)
        {
            velocity = velocity.normalized * minVel;
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
        
        pitch = Mathf.Clamp(pitch, -settings.maxPitch, settings.maxPitch);
        roll = Mathf.Clamp(roll, -settings.maxRoll, settings.maxRoll);
    }

    private void ProcessKeyboardInput()
    {
        Vector2 move = InputManager.Instance.MoveInput;

        pitch += move.y * settings.pitchSpeed * Time.fixedDeltaTime;

        if (move.x != 0f)
        {
            yaw += move.x * settings.yawSpeed * Time.fixedDeltaTime;
            roll += move.x * settings.rollSpeed * Time.fixedDeltaTime;
        }

        if (InputManager.Instance.BoostPressed)
            Boost();

        // Roll back to level when no lateral input
        if (move.x == 0f)
        {
            if (roll > 0f)
            {
                roll -= settings.rollBackSpeed * Time.fixedDeltaTime;
                if (roll < 0f) roll = 0f;
            }
            else if (roll < 0f)
            {
                roll += settings.rollBackSpeed * Time.fixedDeltaTime;
                if (roll > 0f) roll = 0f;
            }
        }
    }

    private void Boost()
    {
        body.Velocity += transform.forward * settings.boostSpeed;
    }
}