using UnityEngine;
using System.Collections.Generic;

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
    // ------------------------------------------------------------------ Config
    [Header("Configuration")]
    [Tooltip("Mass used for force calculations. Does not use Rigidbody mass.")]
    [SerializeField] private float mass = 1f;

    [Tooltip("If true, the Rigidbody will be auto-configured on Awake.")]
    [SerializeField] private bool autoConfigureRigidbody = true;

    // ------------------------------------------------------------------ State
    /// <summary>Current velocity in world space.</summary>
    public Vector3 Velocity { get; set; }

    /// <summary>Speed (magnitude of Velocity).</summary>
    public float Speed => Velocity.magnitude;

    /// <summary>Current mass used for force calculations.</summary>
    public float Mass
    {
        get => mass;
        set => mass = Mathf.Max(0.001f, value);
    }

    /// <summary>Whether physics integration is paused (e.g. during crash).</summary>
    public bool Frozen { get; set; }

    // ------------------------------------------------------------------ Internal
    private Rigidbody _rb;
    private Vector3 _accumulatedForce;

    // ================================================================== Lifecycle

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (autoConfigureRigidbody)
        {
            // Dynamic rigidbody — Unity handles collision response
            _rb.isKinematic = false;
            _rb.useGravity = false;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Prevent tunneling at high flight speeds
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Lock rotation so the physics solver doesn't torque us —
            // rotation is fully controlled by MoveRotation()
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            // Zero-friction, zero-bounce material so the solver doesn't
            // add drag or energy changes — our scripts are the sole authority
            var frictionlessMat = new PhysicsMaterial("KinematicBody_Frictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            // Apply to all colliders on this gameobject
            foreach (var col in GetComponents<Collider>())
            {
                col.material = frictionlessMat;

                // Ensure colliders are NOT triggers — we want real collision response
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

        // Integrate accumulated forces → velocity
        if (_accumulatedForce.sqrMagnitude > 0f)
        {
            Velocity += (_accumulatedForce / mass) * Time.fixedDeltaTime;
            _accumulatedForce = Vector3.zero;
        }

        // Drive the rigidbody with our computed velocity.
        // Unity's collision solver may modify rb.linearVelocity after this
        // (e.g. depenetration pushes), which we pick up next frame.
        _rb.linearVelocity = Velocity;
    }



    // ================================================================== Public API

    /// <summary>
    /// Apply a continuous force (like gravity or wind). Accumulated over the frame,
    /// integrated in FixedUpdate. Equivalent to Rigidbody.AddForce(ForceMode.Force).
    /// </summary>
    public void AddForce(Vector3 force)
    {
        _accumulatedForce += force;
    }

    /// <summary>
    /// Apply an instant velocity change scaled by mass.
    /// Equivalent to Rigidbody.AddForce(ForceMode.Impulse).
    /// </summary>
    public void AddImpulse(Vector3 impulse)
    {
        Velocity += impulse / mass;
    }

    /// <summary>
    /// Apply a direct velocity change (ignores mass).
    /// Equivalent to Rigidbody.AddForce(ForceMode.VelocityChange).
    /// </summary>
    public void AddVelocityChange(Vector3 delta)
    {
        Velocity += delta;
    }

    /// <summary>
    /// Apply a direct acceleration (ignores mass), integrated over dt.
    /// Equivalent to Rigidbody.AddForce(ForceMode.Acceleration).
    /// </summary>
    public void AddAcceleration(Vector3 acceleration)
    {
        Velocity += acceleration * Time.fixedDeltaTime;
    }

    /// <summary>
    /// Hard-set the velocity. Prefer AddForce/AddImpulse for gameplay behaviour.
    /// </summary>
    public void SetVelocity(Vector3 velocity)
    {
        Velocity = velocity;
    }

    /// <summary>
    /// Rotate the rigidbody.
    /// </summary>
    public void MoveRotation(Quaternion rotation)
    {
        _rb.MoveRotation(rotation);
    }

    /// <summary>
    /// Helper: apply a force using Unity's ForceMode enum for compatibility.
    /// </summary>
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
