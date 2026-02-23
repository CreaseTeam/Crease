using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Handles all player collisions. Obstacle hits apply knockback and trigger speed
/// recovery. Ground hits while crashed delegate to PlayerCrashHandler.Land().
/// All tuning values are exposed to the Inspector for rapid iteration.
/// </summary>
[RequireComponent(typeof(KinematicBody))]
public class FlightCollisionController : MonoBehaviour
{
    // ------------------------------------------------------------------ Refs
    [Header("References")]
    [SerializeField] private KinematicBody body;
    [SerializeField] private PlayerCrashHandler crashHandler;
    [SerializeField] private Collider playerCollider;

    // ------------------------------------------------------------------ Depenetration
    [Header("Depenetration")]
    [Tooltip("Extra margin to push player out of colliders to ensure no clipping.")]
    [SerializeField] private float depenetrationMargin = 0.1f;

    // ------------------------------------------------------------------ Tags
    [Header("Tags")]
    [SerializeField] private string obstacleTag = "Obstacle";
    [SerializeField] private string groundTag = "Ground";

    // ------------------------------------------------------------------ Knockback
    [Header("Knockback")]
    [Tooltip("Multiplier applied to the reflected collision normal to produce the knockback impulse.")]
    [SerializeField] private float knockbackForce = 20f;

    [Tooltip("How much of the pre-collision speed is added on top of the knockback direction. " +
             "0 = pure normal bounce, 1 = full reflection.")]
    [Range(0f, 1f)]
    [SerializeField] private float reflectionBlend = 0.3f;

    [Tooltip("Minimum knockback impulse magnitude (prevents weak glancing blows).")]
    [SerializeField] private float minKnockbackMagnitude = 5f;

    [Tooltip("Maximum knockback impulse magnitude.")]
    [SerializeField] private float maxKnockbackMagnitude = 60f;

    // ------------------------------------------------------------------ Recovery
    [Header("Speed Recovery")]
    [Tooltip("Fraction of pre-collision speed that the player will recover to (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float speedRetention = 0.5f;

    [Tooltip("Time in seconds to accelerate from knockback speed to the target recovery speed.")]
    [SerializeField] private float recoveryDuration = 2f;

    [Tooltip("Time in seconds after knockback before recovery acceleration begins.")]
    [SerializeField] private float recoveryDelay = 0.3f;

    [Tooltip("Maximum acceleration during recovery to prevent snapping on rapid collisions.")]
    [SerializeField] private float maxRecoveryAcceleration = 50f;

    // ------------------------------------------------------------------ Invulnerability
    [Header("Invulnerability")]
    [Tooltip("Seconds of invulnerability after a knockback hit (prevents rapid repeated hits).")]
    [SerializeField] private float invulnerabilityDuration = 0.5f;

    [Tooltip("Fraction of normal knockback force applied during invulnerability (prevents clipping).")]
    [Range(0f, 1f)]
    [SerializeField] private float invulnerableKnockbackMultiplier = 0.5f;

    // ------------------------------------------------------------------ Ground crash
    [Header("Ground Crash")]
    [Tooltip("If true, hitting the ground while in the crashed state triggers PlayerCrashHandler.Land().")]
    [SerializeField] private bool landOnGroundAfterCrash = true;

    // ------------------------------------------------------------------ Events
    [Header("Events")]
    public UnityEvent OnKnockback;
    public UnityEvent OnRecoveryStarted;
    public UnityEvent OnRecoveryComplete;

    // ------------------------------------------------------------------ State
    public bool IsRecovering => _isRecovering;
    public bool IsInvulnerable => Time.time < _invulnerableUntil;
    public float PreCollisionSpeed => _preCollisionSpeed;

    private bool _isRecovering;
    private float _preCollisionSpeed;
    private float _targetRecoverySpeed;
    private float _recoveryStartTime;
    private float _invulnerableUntil;

    private void Awake()
    {
        if (body == null) body = GetComponent<KinematicBody>();
        if (playerCollider == null) playerCollider = GetComponent<Collider>();
    }

    // ================================================================== Collision
    private void OnTriggerEnter(Collider other)
    {
        // --- Ground landing while crashed ---
        if (landOnGroundAfterCrash
            && crashHandler != null
            && crashHandler.IsCrashed
            && other.CompareTag(groundTag))
        {
            crashHandler.Land();
            return;
        }

        // Check if this object or its parent prevents knockback
        IPreventKnockback preventKnockbackComponent = other.GetComponentInParent<IPreventKnockback>();
        bool shouldPreventKnockback = preventKnockbackComponent != null 
            && preventKnockbackComponent.ShouldPreventKnockback(playerCollider);

        if (shouldPreventKnockback)
        {
            // Still depenetrate to prevent clipping, but no knockback
            DepenetrateFromCollider(other);
            return;
        }

        // --- Obstacle knockback ---
        if (other.CompareTag(obstacleTag))
        {
            ApplyKnockback(other, IsInvulnerable);
        }

        // temporarily treat ground as obstacle
        if (other.CompareTag(groundTag))
        {
            ApplyKnockback(other, IsInvulnerable);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Continuously depenetrate while overlapping obstacles/ground to prevent clipping at high speeds
        if (other.CompareTag(obstacleTag) || other.CompareTag(groundTag))
        {
            DepenetrateFromCollider(other);
        }
    }

    // ================================================================== Knockback
    private void ApplyKnockback(Collider obstacle, bool isInvulnerable)
    {
        if (HUDCanvas.Instance != null)
        {
            HUDCanvas.Instance.TakeDamage();
        }


        Vector3 velocity = body.Velocity;
        float preCollisionSpeed = velocity.magnitude;

        // CRITICAL: Depenetrate first to guarantee we're outside the collider
        Vector3 contactNormal = DepenetrateFromCollider(obstacle);
        
        // Fallback if depenetration failed: use velocity-based or bounds-based normal
        if (contactNormal.sqrMagnitude < 0.001f)
        {
            contactNormal = velocity.magnitude > 0.1f 
                ? -velocity.normalized 
                : (transform.position - obstacle.bounds.center).normalized;
        }

        // Build knockback direction: blend between pure normal and reflected velocity
        Vector3 reflected = Vector3.Reflect(velocity.normalized, contactNormal);
        Vector3 knockbackDir = Vector3.Lerp(contactNormal, reflected, reflectionBlend).normalized;

        // Compute impulse magnitude, scaled by incoming speed
        float impulseMagnitude = Mathf.Clamp(
            knockbackForce + preCollisionSpeed * reflectionBlend,
            minKnockbackMagnitude,
            maxKnockbackMagnitude);

        // During invulnerability, apply reduced knockback to prevent clipping but maintain control
        if (isInvulnerable)
        {
            body.SetVelocity(knockbackDir * impulseMagnitude * invulnerableKnockbackMultiplier);
            // Don't reset invulnerability timer, recovery state, or trigger events
            return;
        }

        // Apply full knockback
        body.SetVelocity(knockbackDir * impulseMagnitude);

        // Start recovery state
        _preCollisionSpeed = preCollisionSpeed;
        _targetRecoverySpeed = _preCollisionSpeed * speedRetention;
        _recoveryStartTime = Time.time + recoveryDelay;
        _isRecovering = true;
        _invulnerableUntil = Time.time + invulnerabilityDuration;

        OnKnockback?.Invoke();
    }

    /// <summary>
    /// Pushes the player out of the given collider using Physics.ComputePenetration.
    /// Returns the penetration normal (direction player was pushed).
    /// </summary>
    private Vector3 DepenetrateFromCollider(Collider obstacle)
    {
        if (playerCollider == null) return Vector3.zero;

        // Try to compute penetration between player and obstacle
        bool isPenetrating = Physics.ComputePenetration(
            playerCollider, transform.position, transform.rotation,
            obstacle, obstacle.transform.position, obstacle.transform.rotation,
            out Vector3 direction, out float distance);

        if (isPenetrating)
        {
            // Move player out of the collider plus a small margin
            Vector3 depenetrationVector = direction * (distance + depenetrationMargin);
            transform.position += depenetrationVector;
            return direction;
        }

        return Vector3.zero;
    }

    // ================================================================== Recovery
    private void FixedUpdate()
    {
        if (!_isRecovering) return;
        if (Time.time < _recoveryStartTime) return; // still in delay window

        // Fire event on the first recovery frame
        if (Time.time - _recoveryStartTime < Time.fixedDeltaTime * 1.5f)
        {
            OnRecoveryStarted?.Invoke();
        }

        float currentSpeed = body.Speed;
        float speedDelta = _targetRecoverySpeed - currentSpeed;

        // Check if we've reached or exceeded target speed
        if (speedDelta <= 0f)
        {
            _isRecovering = false;
            OnRecoveryComplete?.Invoke();
            return;
        }

        // Calculate time remaining in recovery window
        float elapsedRecoveryTime = Time.time - _recoveryStartTime;
        float remainingTime = recoveryDuration - elapsedRecoveryTime;

        // If we've exceeded recovery duration, clamp to target and complete
        if (remainingTime <= 0f)
        {
            Vector3 forward = body.Velocity.normalized;
            if (forward.sqrMagnitude < 0.001f)
                forward = transform.forward;

            body.SetVelocity(forward * _targetRecoverySpeed);
            _isRecovering = false;
            OnRecoveryComplete?.Invoke();
            return;
        }

        // Calculate required acceleration to reach target in remaining time
        float requiredAcceleration = speedDelta / remainingTime;
        
        // Cap acceleration to prevent huge snaps when remainingTime is very small
        requiredAcceleration = Mathf.Min(requiredAcceleration, maxRecoveryAcceleration);

        // Accelerate along current heading
        Vector3 heading = body.Velocity.normalized;
        if (heading.sqrMagnitude < 0.001f)
            heading = transform.forward;

        body.Velocity += heading * requiredAcceleration * Time.fixedDeltaTime;
    }
}
