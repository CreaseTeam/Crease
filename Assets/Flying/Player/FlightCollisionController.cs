using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Handles all player collisions. Obstacle hits apply knockback and trigger speed
/// recovery. Ground hits while crashed delegate to PlayerCrashHandler.Land().
/// All tuning values are exposed to the Inspector for rapid iteration.
///
/// Uses OnCollisionEnter with a dynamic (non-kinematic) Rigidbody. Unity's physics
/// solver handles depenetration automatically — this script focuses purely on
/// knockback, damage, and recovery logic.
/// </summary>
[RequireComponent(typeof(KinematicBody))]
public class FlightCollisionController : MonoBehaviour
{
    // ------------------------------------------------------------------ Refs
    [Header("References")]
    [SerializeField] private KinematicBody body;
    [SerializeField] private PlayerCrashHandler crashHandler;
    [SerializeField] private Collider playerCollider;
    [SerializeField] private Health healthComponent;

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
    public bool IsInvulnerable => Time.time < _invulnerableUntil || (_dashController != null && _dashController.IsInvincible);
    public float PreCollisionSpeed => _preCollisionSpeed;

    private bool _isRecovering;
    private float _preCollisionSpeed;
    private float _targetRecoverySpeed;
    private float _recoveryStartTime;
    private float _invulnerableUntil;
    private DashController _dashController;

    private void Awake()
    {
        if (body == null) body = GetComponent<KinematicBody>();
        if (playerCollider == null) playerCollider = GetComponent<Collider>();
        _dashController = GetComponent<DashController>();
    }

    // ================================================================== Collision

    private void OnCollisionEnter(Collision collision)
    {
        Collider other = collision.collider;

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
            // Unity's solver already handles depenetration — nothing else needed
            return;
        }

        // --- Obstacle / Ground knockback ---
        if (other.CompareTag(obstacleTag) || other.CompareTag(groundTag))
        {
            ApplyKnockback(collision, IsInvulnerable);
        }
    }

    // ================================================================== Knockback

    private void ApplyKnockback(Collision collision, bool isInvulnerable)
    {
        Vector3 velocity = body.Velocity;
        float preCollisionSpeed = velocity.magnitude;

        // Get the contact normal from Unity's collision solver — reliable even
        // for non-convex mesh colliders and complex terrain geometry
        Vector3 contactNormal = GetContactNormal(collision, velocity);

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

        TakeDamage(collision.gameObject);

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
    /// Extracts a reliable contact normal from the Collision data.
    /// Falls back to velocity-based or bounds-based estimation if no contacts exist.
    /// </summary>
    private Vector3 GetContactNormal(Collision collision, Vector3 velocity)
    {
        if (collision.contactCount > 0)
        {
            // Average all contact normals for a more stable result
            // (multiple contact points occur when hitting edges/corners)
            Vector3 avgNormal = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++)
            {
                avgNormal += collision.GetContact(i).normal;
            }
            return avgNormal.normalized;
        }

        // Fallback: use velocity-based or bounds-based normal
        if (velocity.magnitude > 0.1f)
            return -velocity.normalized;

        return (transform.position - collision.collider.bounds.center).normalized;
    }

    private void TakeDamage(GameObject obstacle) {
        // Determine damage and type from obstacle if available, otherwise use defaults
        float damageAmount = 10f;
        DamageType damageType = DamageType.Impact;
        Obstacle obstacleComp = obstacle.GetComponentInParent<Obstacle>();
        if (obstacleComp != null)
        {
            damageAmount = obstacleComp._impactDamage;
            damageType = obstacleComp._damageType;
        }

        healthComponent.TakeDamage(damageAmount, damageType);
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
