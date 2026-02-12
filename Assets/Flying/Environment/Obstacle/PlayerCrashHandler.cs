using UnityEngine;

/// <summary>
/// Handles the crash state of the player — disabling flight and freezing the body.
/// Does NOT handle collision detection; that is done by FlightCollisionController,
/// which may call Crash() when appropriate.
/// </summary>
[RequireComponent(typeof(KinematicBody))]
public class PlayerCrashHandler : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MonoBehaviour flightController;
    [SerializeField] private KinematicBody body;

    [Header("Crash Tuning")]
    [SerializeField] private bool zeroVelocityOnCrash = true;
    [SerializeField] private bool stopCompletelyOnLand = true;

    [Header("Gravity While Crashed")]
    [Tooltip("Custom gravity applied after crash (body falls under its own sim).")]
    [SerializeField] private float crashGravity = 20f;

    [SerializeField] private CameraController cameraController;

    public bool IsCrashed => crashed;
    public bool IsLanded => landed;

    private bool crashed = false;
    private bool landed = false;

    private void Awake()
    {
        if (body == null) body = GetComponent<KinematicBody>();
    }

    private void FixedUpdate()
    {
        // Apply gravity while crashed but not yet landed
        if (crashed && !landed)
        {
            body.Velocity += Vector3.down * crashGravity * Time.fixedDeltaTime;
        }
    }

    /// <summary>
    /// Enters the crashed state — disables flight and lets the plane fall.
    /// </summary>
    public void Crash()
    {
        if (crashed) return;

        crashed = true;

        if (flightController != null)
            flightController.enabled = false;

        if (zeroVelocityOnCrash)
            body.SetVelocity(Vector3.zero);
    }

    /// <summary>
    /// Called when the crashed plane touches the ground.
    /// </summary>
    public void Land()
    {
        if (!crashed || landed) return;

        landed = true;

        if (stopCompletelyOnLand)
            body.SetVelocity(Vector3.zero);

        body.Frozen = true;
    }

    /// <summary>
    /// Resets the crash state (useful for scene reloads or respawns).
    /// </summary>
    public void ResetCrash()
    {
        crashed = false;
        landed = false;
        body.Frozen = false;

        if (flightController != null)
            flightController.enabled = true;
    }
}
