using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public FlightController flightController;

    [Header("Offset")]
    [Tooltip("Offset behind and above the plane (in rig-local space).")]
    public Vector3 defaultOffset = new Vector3(0f, 2f, -8f);

    [Header("Camera Zoom")]
    [Tooltip("How fast the camera zooms in/out per scroll tick.")]
    public float zoomSpeed = 2f;
    public float minZoomOffset = -3f;
    public float maxZoomOffset = -20f;

    [Header("Camera Panning (Rotation-Based)")]
    [Tooltip("Maximum angle in degrees the camera can pan.")]
    public float maxPanAngleX = 30f;
    public float maxPanAngleY = 20f;

    [Tooltip("How fast the camera pans with Mouse input.")]
    public float mousePanSensitivity = 0.1f;

    [Tooltip("How fast the camera pans with Gamepad input.")]
    public float gamepadPanSpeed = 100f;

    [Tooltip("How fast the camera snaps back to center (0 = no return).")]
    public float panReturnSpring = 5f;

    [Tooltip("Smoothing for the panning rotation.")]
    public float panSmoothing = 0.1f;

    [Header("Follow Speeds")]
    public float yawSpeed = 5f;
    public float pitchSpeed = 5f;
    public float positionSmoothing = 10f;

    [Header("Pitch Profile (Velocity-Driven)")]
    public float profileStrength = 0.25f;
    public float maxProfileOffset = 30f;
    public float pitchRateSmoothing = 8f;
    public float profileDecay = 3f;

    [Header("Look At")]
    public float lookAheadDistance = 5f;
    public float lookSmoothing = 8f;
    [Range(0f, 1f)]
    public float lookAheadBlend = 0.5f;

    [Header("Horizon Stabilization")]
    [Range(0f, 1f)]
    public float horizonRollStabilization = 0.85f;

    // ---- Internal State ----
    private float _currentYaw;
    private float _currentPitch;
    private float _prevTargetPitch;
    private float _smoothedPitchRate;
    private float _currentProfileOffset;
    private Vector3 _positionVelocity;
    private Quaternion _lookRotation;

    // Panning State
    private float _panYaw;
    private float _panPitch;
    private float _panYawVel;
    private float _panPitchVel;

    private void Start()
    {
        if (target == null) return;

        Vector3 euler = target.rotation.eulerAngles;
        _currentYaw = euler.y;
        _currentPitch = NormalizeAngle(euler.x);
        _prevTargetPitch = _currentPitch;
        _lookRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;
        if (dt < 0.0001f) return;

        HandleZoom(dt);
        HandlePanning(dt);

        // --- Target angles ---
        Vector3 targetEuler = target.rotation.eulerAngles;
        float targetYaw = targetEuler.y;
        float targetPitch = NormalizeAngle(targetEuler.x);

        // Pitch-rate & Profile logic
        float rawPitchRate = Mathf.DeltaAngle(_prevTargetPitch, targetPitch) / dt;
        _prevTargetPitch = targetPitch;
        _smoothedPitchRate = Mathf.Lerp(_smoothedPitchRate, rawPitchRate, dt * pitchRateSmoothing);
        float desiredOffset = -_smoothedPitchRate * profileStrength;
        desiredOffset = Mathf.Clamp(desiredOffset, -maxProfileOffset, maxProfileOffset);
        _currentProfileOffset = Mathf.Lerp(_currentProfileOffset, desiredOffset, dt * profileDecay);

        // Update orbital angles
        _currentYaw = Mathf.LerpAngle(_currentYaw, targetYaw, dt * yawSpeed);
        _currentPitch = Mathf.LerpAngle(_currentPitch, targetPitch, dt * pitchSpeed);
        float finalPitch = _currentPitch + _currentProfileOffset;

        // Rig rotation
        float targetRoll = (flightController != null) ? -flightController.Roll : NormalizeAngle(targetEuler.z);
        float rigRoll = Mathf.Lerp(targetRoll, 0f, horizonRollStabilization);
        Quaternion rigRotation = Quaternion.Euler(finalPitch, _currentYaw, rigRoll);

        // Position
        Vector3 desiredPosition = target.position + rigRotation * defaultOffset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _positionVelocity, 1f / positionSmoothing);

        // --- Look-at logic ---
        Vector3 baseLookTarget = Vector3.Lerp(
            target.position,
            target.position + target.forward * lookAheadDistance,
            lookAheadBlend);

        Vector3 lookDir = (baseLookTarget - transform.position).normalized;
        Vector3 upVec = Vector3.Lerp(rigRotation * Vector3.up, Vector3.up, horizonRollStabilization);
        
        // Base Rotation from Look-at
        Quaternion baseRotation = Quaternion.LookRotation(lookDir, upVec);

        // Apply Panning as a LOCAL rotation offset
        // This makes it feel much more natural as it follows the camera's orientation
        Quaternion panRotation = Quaternion.Euler(-_panPitch, _panYaw, 0f);
        Quaternion desiredRotation = baseRotation * panRotation;

        _lookRotation = Quaternion.Slerp(_lookRotation, desiredRotation, dt * lookSmoothing);
        transform.rotation = _lookRotation;
    }

    private void HandleZoom(float dt)
    {
        float scrollY = InputManager.Instance.CameraZoomInput.y;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            defaultOffset.z += Mathf.Sign(scrollY) * zoomSpeed * dt;
            defaultOffset.z = Mathf.Clamp(defaultOffset.z, maxZoomOffset, minZoomOffset);
        }
    }

    private void HandlePanning(float dt)
    {
        Vector2 input = InputManager.Instance.CameraPanInput;
        bool isGamepad = false;

        // Detect if input is from Gamepad (Value usually stays between -1 and 1)
        // Note: In a production environment, you'd check the InputDevice of the action.
        // For now, we'll use a simple heuristic or stick to cumulative for both but with return spring.
        
        // --- Improved Logic: Cumulative with high friction/return ---
        // Mouse uses Delta directly, Gamepad uses Value * speed * dt
        
        // Heuristic: Input System Mouse Delta is usually integer-like and large, Gamepad is 0-1.
        // But the best way is to accumulate and spring-back.

        if (input.sqrMagnitude > 0.001f)
        {
            // Horizontal (Yaw)
            _panYaw += input.x * mousePanSensitivity;
            // Vertical (Pitch)
            _panPitch += input.y * mousePanSensitivity;
        }

        // Clamp
        _panYaw = Mathf.Clamp(_panYaw, -maxPanAngleX, maxPanAngleX);
        _panPitch = Mathf.Clamp(_panPitch, -maxPanAngleY, maxPanAngleY);

        // Spring back to zero when no input
        if (input.sqrMagnitude < 0.001f)
        {
            _panYaw = Mathf.SmoothDamp(_panYaw, 0f, ref _panYawVel, panSmoothing, 1000f, dt * panReturnSpring);
            _panPitch = Mathf.SmoothDamp(_panPitch, 0f, ref _panPitchVel, panSmoothing, 1000f, dt * panReturnSpring);
        }
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    private void OnDrawGizmos()
    {
        if (target == null) return;
        Quaternion rig = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
        Vector3 ghostPos = target.position + rig * defaultOffset;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(ghostPos, 0.5f);
    }
}