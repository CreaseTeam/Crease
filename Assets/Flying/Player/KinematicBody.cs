using UnityEngine;

namespace Crease.Flying.Player
{
    /// <summary>
    /// Custom physics body that drives a non-kinematic Rigidbody via direct velocity control.
    /// Unity's physics solver handles collision response and depenetration automatically,
    /// while this script maintains full authority over the velocity each FixedUpdate.
    ///
    /// The Rigidbody is configured as dynamic (non-kinematic) with:
    ///   - No gravity (custom gravity is applied by gameplay scripts)
    ///   - Frozen rotation (rotation is controlled manually via MoveRotation)
    ///   - Continuous dynamic collision detection (prevents tunneling)
    ///   - A zero-friction, zero-bounce PhysicMaterial (prevents solver energy changes)
    ///
    /// Usage:
    ///   body.Velocity          — get/set the current velocity
    ///   body.Speed             — shorthand for velocity magnitude
    ///   body.AddForce(v)       — continuous force (scaled by dt and mass internally)
    ///   body.AddImpulse(v)     — instant velocity change (scaled by mass)
    ///   body.SetVelocity(v)    — hard override (use sparingly)
    ///   body.MoveRotation(q)   — rotate the rigidbody
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KinematicBody : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Mass used for force calculations. Does not use Rigidbody mass.")]
        [SerializeField] private float mass = 1f;

        [Tooltip("If true, the Rigidbody will be auto-configured on Awake.")]
        [SerializeField] private bool autoConfigureRigidbody = true;

        public Vector3 Velocity { get; set; }

        public float Speed => Velocity.magnitude;

        public float Mass
        {
            get => mass;
            set => mass = Mathf.Max(0.001f, value);
        }

        public bool Frozen { get; set; }

        private Rigidbody _rb;
        private Vector3 _accumulatedForce;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (autoConfigureRigidbody)
            {
                _rb.isKinematic = false;
                _rb.useGravity = false;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _rb.constraints = RigidbodyConstraints.FreezeRotation;

                var frictionlessMat = new PhysicsMaterial("KinematicBody_Frictionless")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };

                _rb.mass = mass;

                foreach (var col in GetComponents<Collider>())
                {
                    col.material = frictionlessMat;

                    if (col.isTrigger)
                    {
                        Debug.LogWarning($"[KinematicBody] Collider '{col.name}' was set as trigger — disabling trigger mode for physics collision support.");
                        col.isTrigger = false;
                    }
                }
            }
        }

        private void FixedUpdate()
        {
            if (Frozen)
            {
                _rb.linearVelocity = Vector3.zero;
                return;
            }

            if (_accumulatedForce.sqrMagnitude > 0f)
            {
                Velocity += (_accumulatedForce / mass) * Time.fixedDeltaTime;
                _accumulatedForce = Vector3.zero;
            }

            _rb.linearVelocity = Velocity;
        }

        public void AddForce(Vector3 force)
        {
            _accumulatedForce += force;
        }

        public void AddImpulse(Vector3 impulse)
        {
            Velocity += impulse / mass;
        }

        public void AddVelocityChange(Vector3 delta)
        {
            Velocity += delta;
        }

        public void AddAcceleration(Vector3 acceleration)
        {
            Velocity += acceleration * Time.fixedDeltaTime;
        }

        public void SetVelocity(Vector3 velocity)
        {
            Velocity = velocity;
        }

        public void MoveRotation(Quaternion rotation)
        {
            _rb.MoveRotation(rotation);
        }

        public void AddForce(Vector3 force, ForceMode mode)
        {
            switch (mode)
            {
                case ForceMode.Force:
                    AddForce(force);
                    break;
                case ForceMode.Impulse:
                    AddImpulse(force);
                    break;
                case ForceMode.VelocityChange:
                    AddVelocityChange(force);
                    break;
                case ForceMode.Acceleration:
                    AddAcceleration(force);
                    break;
            }
        }
    }
}
