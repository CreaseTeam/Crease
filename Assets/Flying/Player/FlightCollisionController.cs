using Crease.Flying.Environment.Interactables;
using Crease.Flying.Environment.Obstacle;
using Crease.Flying.Player.FlightModifiers;
using Crease.Flying.Player.Health;
using PlayerHealth = Crease.Flying.Player.Health.Health;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Crease.Flying.Player
{
    /// <summary>
    /// Handles all player collisions. Obstacle hits apply knockback and trigger speed
    /// recovery. Ground hits while crashed delegate to PlayerCrashHandler.Land().
    /// All tuning values are exposed to the Inspector for rapid iteration.
    ///
    /// Uses OnCollisionEnter with a dynamic (non-kinematic) Rigidbody. Unity's physics
    /// solver handles depenetration automatically — this script focuses purely on
    /// knockback, damage, and recovery logic.
    ///
    /// Component lookups on the player are cached in Awake. Collision handlers must still
    /// resolve components on the other collider (IPreventKnockback, Obstacle) via
    /// GetComponentInParent because those objects are not known until impact.
    /// </summary>
    [RequireComponent(typeof(KinematicBody))]
    public class FlightCollisionController : MonoBehaviour
    {
        [Header("References")]
        [FormerlySerializedAs("body")]
        [SerializeField] private KinematicBody _body;
        [FormerlySerializedAs("crashHandler")]
        [SerializeField] private PlayerCrashHandler _crashHandler;
        [FormerlySerializedAs("playerCollider")]
        [SerializeField] private Collider _playerCollider;
        [FormerlySerializedAs("healthComponent")]
        [SerializeField] private PlayerHealth _healthComponent;

        [Header("Tags")]
        [FormerlySerializedAs("obstacleTag")]
        [SerializeField] private string _obstacleTag = "Obstacle";
        [FormerlySerializedAs("groundTag")]
        [SerializeField] private string _groundTag = "Ground";

        [Header("Knockback")]
        [Tooltip("Multiplier applied to the reflected collision normal to produce the knockback impulse.")]
        [FormerlySerializedAs("knockbackForce")]
        [SerializeField] private float _knockbackForce = 20f;

        [Tooltip("How much of the pre-collision speed is added on top of the knockback direction. " +
                 "0 = pure normal bounce, 1 = full reflection.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("reflectionBlend")]
        [SerializeField] private float _reflectionBlend = 0.3f;

        [Tooltip("Minimum knockback impulse magnitude (prevents weak glancing blows).")]
        [FormerlySerializedAs("minKnockbackMagnitude")]
        [SerializeField] private float _minKnockbackMagnitude = 5f;

        [Tooltip("Maximum knockback impulse magnitude.")]
        [FormerlySerializedAs("maxKnockbackMagnitude")]
        [SerializeField] private float _maxKnockbackMagnitude = 60f;

        [Header("Speed Recovery")]
        [Tooltip("Fraction of pre-collision speed that the player will recover to (0-1).")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("speedRetention")]
        [SerializeField] private float _speedRetention = 0.5f;

        [Tooltip("Time in seconds to accelerate from knockback speed to the target recovery speed.")]
        [FormerlySerializedAs("recoveryDuration")]
        [SerializeField] private float _recoveryDuration = 2f;

        [Tooltip("Time in seconds after knockback before recovery acceleration begins.")]
        [FormerlySerializedAs("recoveryDelay")]
        [SerializeField] private float _recoveryDelay = 0.3f;

        [Tooltip("Maximum acceleration during recovery to prevent snapping on rapid collisions.")]
        [FormerlySerializedAs("maxRecoveryAcceleration")]
        [SerializeField] private float _maxRecoveryAcceleration = 50f;

        [Header("Invulnerability")]
        [Tooltip("Seconds of invulnerability after a knockback hit (prevents rapid repeated hits).")]
        [FormerlySerializedAs("invulnerabilityDuration")]
        [SerializeField] private float _invulnerabilityDuration = 0.5f;

        [Tooltip("Fraction of normal knockback force applied during invulnerability (prevents clipping).")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("invulnerableKnockbackMultiplier")]
        [SerializeField] private float _invulnerableKnockbackMultiplier = 0.5f;

        [Header("Ground Crash")]
        [Tooltip("If true, hitting the ground while in the crashed state triggers PlayerCrashHandler.Land().")]
        [FormerlySerializedAs("landOnGroundAfterCrash")]
        [SerializeField] private bool _landOnGroundAfterCrash = true;

        [Header("Events")]
        public UnityEvent OnKnockback;
        public UnityEvent OnRecoveryStarted;
        public UnityEvent OnRecoveryComplete;

        public bool IsRecovering => _isRecovering;
        public bool IsInvulnerable => _flightModifiers != null && _flightModifiers.IsActive(FlightModifierType.Invulnerable);
        public float PreCollisionSpeed => _preCollisionSpeed;

        private bool _isRecovering;
        private float _preCollisionSpeed;
        private float _targetRecoverySpeed;
        private float _recoveryStartTime;
        private float _scaledRecoveryDuration;
        private FlightModifiers.FlightModifiers _flightModifiers;

        private void Awake()
        {
            if (_body == null) _body = GetComponent<KinematicBody>();
            if (_playerCollider == null) _playerCollider = GetComponent<Collider>();
            if (_healthComponent == null) _healthComponent = GetComponent<PlayerHealth>();
            _flightModifiers = GetComponent<FlightModifiers.FlightModifiers>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            Collider other = collision.collider;

            if (_landOnGroundAfterCrash
                && _crashHandler != null
                && _crashHandler.IsCrashed
                && other.CompareTag(_groundTag))
            {
                _crashHandler.Land();
                return;
            }

            // Other-object lookups: cannot be cached — resolved per collision via GetComponentInParent.
            IPreventKnockback preventKnockbackComponent = other.GetComponentInParent<IPreventKnockback>();
            if (preventKnockbackComponent != null
                && preventKnockbackComponent.ShouldPreventKnockback(_playerCollider))
            {
                return;
            }

            if (other.CompareTag(_obstacleTag) || other.CompareTag(_groundTag))
            {
                ApplyKnockback(collision, IsInvulnerable);
            }
        }

        private void ApplyKnockback(Collision collision, bool isInvulnerable)
        {
            Vector3 velocity = _body.Velocity;
            float preCollisionSpeed = velocity.magnitude;

            Vector3 contactNormal = GetContactNormal(collision, velocity);

            Vector3 reflected = Vector3.Reflect(velocity.normalized, contactNormal);
            Vector3 knockbackDir = Vector3.Lerp(contactNormal, reflected, _reflectionBlend).normalized;

            float impulseMagnitude = Mathf.Clamp(
                _knockbackForce + preCollisionSpeed * _reflectionBlend,
                _minKnockbackMagnitude,
                _maxKnockbackMagnitude);

            Rigidbody otherRigidbody = collision.rigidbody ?? collision.collider.attachedRigidbody;
            bool otherHasRigidbody = otherRigidbody != null && !otherRigidbody.isKinematic;

            Obstacle obstacleComp = collision.gameObject.GetComponentInParent<Obstacle>();
            float obstacleKnockbackMultiplier = obstacleComp != null ? obstacleComp.KnockbackMultiplier : 1f;

            float appliedImpulse = impulseMagnitude * obstacleKnockbackMultiplier;

            bool obstacleAppliesKnockback = obstacleComp == null ? true : obstacleComp.ApplyKnockback;
            if (!obstacleAppliesKnockback)
            {
                TakeDamage(collision.gameObject);
                return;
            }

            if (isInvulnerable)
            {
                float invApplied = appliedImpulse * _invulnerableKnockbackMultiplier;
                _body.SetVelocity(knockbackDir * invApplied);

                if (otherHasRigidbody)
                {
                    otherRigidbody.AddForce(-knockbackDir * invApplied, ForceMode.Impulse);
                }

                return;
            }

            TakeDamage(collision.gameObject);

            _body.SetVelocity(knockbackDir * appliedImpulse);

            if (otherHasRigidbody)
            {
                otherRigidbody.AddForce(-knockbackDir * appliedImpulse, ForceMode.Impulse);
            }

            _preCollisionSpeed = preCollisionSpeed;
            _targetRecoverySpeed = _preCollisionSpeed * _speedRetention;
            float simSpeed = _flightModifiers != null ? _flightModifiers.SimulationSpeed : 1f;
            _recoveryStartTime = Time.time + _recoveryDelay / simSpeed;
            _scaledRecoveryDuration = _recoveryDuration / simSpeed;
            _isRecovering = true;

            if (_flightModifiers != null)
                _flightModifiers.ApplyForDuration(FlightModifierType.Invulnerable, this, _invulnerabilityDuration);

            OnKnockback?.Invoke();
        }

        private Vector3 GetContactNormal(Collision collision, Vector3 velocity)
        {
            if (collision.contactCount > 0)
            {
                Vector3 avgNormal = Vector3.zero;
                for (int i = 0; i < collision.contactCount; i++)
                {
                    avgNormal += collision.GetContact(i).normal;
                }
                return avgNormal.normalized;
            }

            if (velocity.magnitude > 0.1f)
                return -velocity.normalized;

            return (transform.position - collision.collider.bounds.center).normalized;
        }

        private void TakeDamage(GameObject obstacle)
        {
            float damageAmount = 10f;
            DamageType damageType = DamageType.Impact;
            Obstacle obstacleComp = obstacle.GetComponentInParent<Obstacle>();
            if (obstacleComp != null)
            {
                damageAmount = obstacleComp.ImpactDamage;
                damageType = obstacleComp.DamageType;
                obstacleComp.TriggerHit(gameObject);
            }

            _healthComponent.TakeDamage(damageAmount, damageType);
        }

        private void FixedUpdate()
        {
            if (!_isRecovering) return;
            if (Time.time < _recoveryStartTime) return;

            if (Time.time - _recoveryStartTime < Time.fixedDeltaTime * 1.5f)
            {
                OnRecoveryStarted?.Invoke();
            }

            float currentSpeed = _body.Speed;
            float speedDelta = _targetRecoverySpeed - currentSpeed;

            if (speedDelta <= 0f)
            {
                _isRecovering = false;
                OnRecoveryComplete?.Invoke();
                return;
            }

            float elapsedRecoveryTime = Time.time - _recoveryStartTime;
            float remainingTime = _scaledRecoveryDuration - elapsedRecoveryTime;

            if (remainingTime <= 0f)
            {
                Vector3 forward = _body.Velocity.normalized;
                if (forward.sqrMagnitude < 0.001f)
                    forward = transform.forward;

                _body.SetVelocity(forward * _targetRecoverySpeed);
                _isRecovering = false;
                OnRecoveryComplete?.Invoke();
                return;
            }

            float requiredAcceleration = speedDelta / remainingTime;
            requiredAcceleration = Mathf.Min(requiredAcceleration, _maxRecoveryAcceleration);

            Vector3 heading = _body.Velocity.normalized;
            if (heading.sqrMagnitude < 0.001f)
                heading = transform.forward;

            _body.Velocity += heading * requiredAcceleration * Time.fixedDeltaTime;
        }
    }
}
