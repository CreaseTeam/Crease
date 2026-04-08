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

    [Tooltip("Sensitivity multiplier for camera panning (applies to both Mouse and Gamepad).")]
    public float panSensitivity = 1.0f;

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
    private Quaternion _unpannedBaseRotation;
    private Vector3 _unpannedBasePosition;

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
        _unpannedBaseRotation = transform.rotation;
        _unpannedBasePosition = transform.position;
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

        // Update unpanned orbital angles
        _currentYaw = Mathf.LerpAngle(_currentYaw, targetYaw, dt * yawSpeed);
        _currentPitch = Mathf.LerpAngle(_currentPitch, targetPitch, dt * pitchSpeed);
        float finalPitch = _currentPitch + _currentProfileOffset;

        // Base Rig rotation (pure follow, no panning here!)
        float targetRoll = (flightController != null) ? -flightController.Roll : NormalizeAngle(targetEuler.z);
        float rigRoll = Mathf.Lerp(targetRoll, 0f, horizonRollStabilization);
        Quaternion baseRigRotation = Quaternion.Euler(finalPitch, _currentYaw, rigRoll);

        // Smooth damp the strictly unpanned base position
        Vector3 baseDesiredPosition = target.position + baseRigRotation * defaultOffset;
        if (_unpannedBasePosition == Vector3.zero) _unpannedBasePosition = transform.position;
        _unpannedBasePosition = Vector3.SmoothDamp(_unpannedBasePosition, baseDesiredPosition, ref _positionVelocity, 1f / positionSmoothing);

        // --- Look-at logic ---
        Vector3 baseLookTarget = Vector3.Lerp(
            target.position,
            target.position + target.forward * lookAheadDistance,
            lookAheadBlend);

        Vector3 lookDir = (baseLookTarget - _unpannedBasePosition).normalized;
        Vector3 upVec = Vector3.Lerp(baseRigRotation * Vector3.up, Vector3.up, horizonRollStabilization);
        
        // Find desired unpanned rotation
        Quaternion baseDesiredRotation = Quaternion.LookRotation(lookDir, upVec);

        // Slerp the strictly unpanned base rotation
        if (_unpannedBaseRotation.w == 0f) _unpannedBaseRotation = transform.rotation;
        _unpannedBaseRotation = Quaternion.Slerp(_unpannedBaseRotation, baseDesiredRotation, dt * lookSmoothing);

        // --- Apply Rigid Orbital Panning ---
        // Generates the panning rotation in the camera's local space
        Quaternion localPan = Quaternion.Euler(-_panPitch, _panYaw, 0f);

        // The final rotation we want the camera to have
        Quaternion finalRotation = _unpannedBaseRotation * localPan;

        // Apply exactly this global rotational difference to the camera's offset from the target 
        // to rigidly sweep the camera's position opposite to its look direction,
        // locking the relative plane position on the screen!
        Quaternion rOrbit = finalRotation * Quaternion.Inverse(_unpannedBaseRotation);
        Vector3 offsetFromTarget = _unpannedBasePosition - target.position;
        
        transform.position = target.position + rOrbit * offsetFromTarget;
        transform.rotation = finalRotation;
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
        var activeControl = InputManager.Instance.Actions.Player.CameraPan.activeControl;
        
        bool isMouse = activeControl != null && activeControl.device is Mouse;

        Vector2 targetPan = Vector2.zero;

        if (isMouse)
        {
            // Normalize screen coordinates to -1 -> 1 range
            float hw = Screen.width * 0.5f;
            float hh = Screen.height * 0.5f;
            targetPan.x = (input.x - hw) / hw;
            targetPan.y = (input.y - hh) / hh;
        }
        else
        {
            // Gamepad stick naturally operates in -1 -> 1 range
            targetPan = input;
        }

        // For absolute bounded inputs, apply sensitivity as an exponential response curve.
        // This preserves the exact bounds so pushing the stick fully ALWAYS reaches exactly maxPanAngle.
        // panSensitivity = 1 -> Linear (1:1 curve)
        // panSensitivity > 1 -> more sensitive near center (x^(1/sens))
        // panSensitivity < 1 -> finer control near center (x^sens)
        float curve = 1f / Mathf.Max(0.01f, panSensitivity);
        targetPan.x = Mathf.Sign(targetPan.x) * Mathf.Pow(Mathf.Abs(targetPan.x), curve);
        targetPan.y = Mathf.Sign(targetPan.y) * Mathf.Pow(Mathf.Abs(targetPan.y), curve);

        // Clamp the evaluated input to mathematically guarantee strict bounds
        targetPan.x = Mathf.Clamp(targetPan.x, -1f, 1f);
        targetPan.y = Mathf.Clamp(targetPan.y, -1f, 1f);

        // Map to exact angle targets
        float targetYaw = targetPan.x * maxPanAngleX;
        float targetPitch = targetPan.y * maxPanAngleY;

        // Smoothly interpolate to the target orientation
        _panYaw = Mathf.SmoothDamp(_panYaw, targetYaw, ref _panYawVel, panSmoothing);
        _panPitch = Mathf.SmoothDamp(_panPitch, targetPitch, ref _panPitchVel, panSmoothing);
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