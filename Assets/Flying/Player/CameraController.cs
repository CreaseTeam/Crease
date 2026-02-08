using UnityEngine;

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

    [Tooltip("Minimum (closest) Z offset value (least negative / closest to zero).")]
    public float minZoomOffset = -3f;

    [Tooltip("Maximum (farthest) Z offset value (most negative / farthest away).")]
    public float maxZoomOffset = -20f;

    [Header("Follow Speeds")]
    [Tooltip("How fast the camera yaw catches up to the plane's heading.")]
    public float yawSpeed = 5f;

    [Tooltip("How fast the camera pitch catches up to the plane's pitch.")]
    public float pitchSpeed = 5f;

    [Tooltip("How fast the camera physically moves to the desired position.")]
    public float positionSmoothing = 10f;

    [Header("Pitch Profile (Velocity-Driven)")]
    [Tooltip("How many degrees of camera-pitch offset are applied per degree/sec of pitch rate. " +
             "Higher = more profile revealed when the player pitches.")]
    public float profileStrength = 0.25f;

    [Tooltip("Maximum pitch offset in degrees the profile effect can apply.")]
    public float maxProfileOffset = 30f;

    [Tooltip("How fast the raw pitch-rate reading is smoothed (higher = more responsive, lower = smoother).")]
    public float pitchRateSmoothing = 8f;

    [Tooltip("How fast the profile offset decays back to zero when no pitch input is applied.")]
    public float profileDecay = 3f;

    [Header("Look At")]
    [Tooltip("Distance ahead of the plane used as the look target.")]
    public float lookAheadDistance = 5f;

    [Tooltip("How fast the camera rotates to face the look target.")]
    public float lookSmoothing = 8f;

    [Tooltip("0 = look at the plane, 1 = look where the plane is heading.")]
    [Range(0f, 1f)]
    public float lookAheadBlend = 0.5f;

    [Header("Horizon Stabilization")]
    [Tooltip("0 = follow plane roll fully, 1 = keep camera level with the horizon.")]
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

    private void Start()
    {
        if (target == null) return;

        Vector3 euler = target.rotation.eulerAngles;
        _currentYaw = euler.y;
        _currentPitch = NormalizeAngle(euler.x);
        _prevTargetPitch = _currentPitch;
        _smoothedPitchRate = 0f;
        _currentProfileOffset = 0f;
        _lookRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;
        if (dt < 0.0001f) return; // guard against zero dt

        // --- Camera zoom via input system ---
        float scrollY = InputManager.Instance.CameraZoomInput.y;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            // Scroll up (positive y) → increase z (zoom in), scroll down → decrease z (zoom out)
            defaultOffset.z += Mathf.Sign(scrollY) * zoomSpeed * dt;
            defaultOffset.z = Mathf.Clamp(defaultOffset.z, maxZoomOffset, minZoomOffset);
        }

        // --- Target angles ---
        Vector3 targetEuler = target.rotation.eulerAngles;
        float targetYaw = targetEuler.y;
        float targetPitch = NormalizeAngle(targetEuler.x);

        // =============================================================
        // Pitch-rate detection
        // =============================================================
        // How fast the plane's pitch is changing (degrees/sec).
        float rawPitchRate = Mathf.DeltaAngle(_prevTargetPitch, targetPitch) / dt;
        _prevTargetPitch = targetPitch;

        // Smooth the rate so single-frame spikes don't jolt the camera.
        _smoothedPitchRate = Mathf.Lerp(_smoothedPitchRate, rawPitchRate, dt * pitchRateSmoothing);

        // =============================================================
        // Profile offset (the core effect)
        // =============================================================
        // Desired offset is opposite to the pitch direction:
        //   pitching down (positive rate) → negative offset → camera stays higher → sees top
        //   pitching up   (negative rate) → positive offset → camera stays lower  → sees bottom
        // (sign may need to flip depending on your conventions — see note below)
        float desiredOffset = -_smoothedPitchRate * profileStrength;
        desiredOffset = Mathf.Clamp(desiredOffset, -maxProfileOffset, maxProfileOffset);

        // Drive toward the desired offset, but decay back to zero when pitch rate is small.
        // This gives a snappy response on pitch-start and a smooth return on pitch-end.
        _currentProfileOffset = Mathf.Lerp(_currentProfileOffset, desiredOffset, dt * profileDecay);

        // =============================================================
        // Update orbital yaw & pitch
        // =============================================================
        _currentYaw  = Mathf.LerpAngle(_currentYaw,  targetYaw,   dt * yawSpeed);
        _currentPitch = Mathf.LerpAngle(_currentPitch, targetPitch, dt * pitchSpeed);

        // Apply the profile offset on top of the normal tracked pitch.
        float finalPitch = _currentPitch + _currentProfileOffset;

        // =============================================================
        // Rig rotation
        // =============================================================
        // Use the actual roll from FlightController (which is on the mesh child)
        float targetRoll = (flightController != null) ? -flightController.Roll : NormalizeAngle(targetEuler.z);
        float rigRoll = Mathf.Lerp(targetRoll, 0f, horizonRollStabilization);
        Quaternion rigRotation = Quaternion.Euler(finalPitch, _currentYaw, rigRoll);

        // =============================================================
        // Position
        // =============================================================
        Vector3 desiredPosition = target.position + rigRotation * defaultOffset;
        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref _positionVelocity, 1f / positionSmoothing);

        // =============================================================
        // Look-at
        // =============================================================
        Vector3 lookTarget = Vector3.Lerp(
            target.position,
            target.position + target.forward * lookAheadDistance,
            lookAheadBlend);

        Vector3 lookDir = lookTarget - transform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Vector3 upVec = Vector3.Lerp(rigRotation * Vector3.up, Vector3.up, horizonRollStabilization);
            Quaternion desiredLook = Quaternion.LookRotation(lookDir.normalized, upVec);
            _lookRotation = Quaternion.Slerp(_lookRotation, desiredLook, dt * lookSmoothing);
            transform.rotation = _lookRotation;
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

        Gizmos.color = Color.green;
        Gizmos.DrawLine(target.position, target.position + target.forward * lookAheadDistance);

        Quaternion rig = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
        Vector3 ghostPos = target.position + rig * defaultOffset;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(ghostPos, 0.5f);
        Gizmos.DrawLine(target.position, ghostPos);

        // Profile offset indicator — size shows how much offset is active
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.1f + Mathf.Abs(_currentProfileOffset) / maxProfileOffset * 0.4f);
    }
}