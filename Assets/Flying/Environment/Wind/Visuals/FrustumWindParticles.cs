using UnityEngine;
using PhysicsHelpers;

[RequireComponent(typeof(ParticleSystem))]
[RequireComponent(typeof(FrustumTrigger))]
[ExecuteAlways]
public class FrustumWindParticles : MonoBehaviour
{
    [Tooltip("Optional secondary particle system whose shape is synced with the frustum and has its own speed/duration.")]
    [SerializeField] private ParticleSystem _secondaryParticleSystem;

    [Tooltip("Start speed for the secondary particle system. Duration is derived from height / speed.")]
    [SerializeField] private float _secondarySpeed = 5f;

    [Tooltip("Start lifetime for the secondary particle system. Speed is derived from height / duration.")]
    [SerializeField] private float _secondaryDuration = 2f;

    [Tooltip("Control whether the secondary system is driven by speed or duration.")]
    [SerializeField] private SecondaryDriveMode _secondaryDriveMode = SecondaryDriveMode.Speed;

    public enum SecondaryDriveMode { Speed, Duration }

    private ParticleSystem _particleSystem;
    private FrustumTrigger _frustumTrigger;
    
    // Cache to detect changes
    private float _lastTopRadius;
    private float _lastBottomRadius;
    private float _lastHeight;

    private void OnEnable()
    {
        _particleSystem = GetComponent<ParticleSystem>();
        _frustumTrigger = GetComponent<FrustumTrigger>();
        UpdateParticleShape();
    }

    private void OnValidate()
    {
        if (_particleSystem == null) _particleSystem = GetComponent<ParticleSystem>();
        if (_frustumTrigger == null) _frustumTrigger = GetComponent<FrustumTrigger>();
        UpdateParticleShape();
    }

    private void Update()
    {
        // Check if FrustumTrigger dimensions have changed
        if (_frustumTrigger != null && HasFrustumChanged())
        {
            UpdateParticleShape();
        }
    }

    private bool HasFrustumChanged()
    {
        return _frustumTrigger.topRadius != _lastTopRadius ||
               _frustumTrigger.bottomRadius != _lastBottomRadius ||
               _frustumTrigger.height != _lastHeight;
    }

    private void UpdateParticleShape()
    {
        if (_particleSystem == null || _frustumTrigger == null) return;

        // Update cache
        _lastTopRadius = _frustumTrigger.topRadius;
        _lastBottomRadius = _frustumTrigger.bottomRadius;
        _lastHeight = _frustumTrigger.height;

        // Ensure local-space simulation and hierarchy scaling for proper parent scaling
        var main = _particleSystem.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        // Configure shape module
        var shape = _particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        
        // Frustum has bottom (small radius) at Y=0 and top (large radius) at Y=height
        // We need a cone that starts with bottomRadius and expands to topRadius over the height
        
        float radiusDiff = _frustumTrigger.topRadius - _frustumTrigger.bottomRadius;
        
        if (Mathf.Abs(radiusDiff) < 0.001f)
        {
            // It's a cylinder - use minimal angle
            shape.angle = 0.1f;
            shape.radius = _frustumTrigger.bottomRadius;
            shape.length = _frustumTrigger.height;
            shape.position = Vector3.zero;
        }
        else
        {
            // Calculate cone angle: at distance=height from start, radius grows by radiusDiff
            // tan(angle) = radiusDiff / height
            float halfAngle = Mathf.Atan(radiusDiff / _frustumTrigger.height) * Mathf.Rad2Deg;
            
            shape.angle = halfAngle;
            shape.radius = _frustumTrigger.bottomRadius;
            shape.length = _frustumTrigger.height;
            shape.position = Vector3.zero;
        }

        // Configure start speed so particles reach the top exactly at end of lifetime
        float lifetime = main.startLifetime.constant;
        if (lifetime > 0)
        {
            main.startSpeed = _frustumTrigger.height / lifetime;
        }

        // Sync secondary particle system shape and compute its speed/duration
        UpdateSecondaryParticleSystem();
    }

    private void UpdateSecondaryParticleSystem()
    {
        if (_secondaryParticleSystem == null) return;

        float height = _frustumTrigger.height;

        // Ensure local-space simulation and hierarchy scaling for proper parent scaling
        var secondaryMain = _secondaryParticleSystem.main;
        secondaryMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        secondaryMain.scalingMode = ParticleSystemScalingMode.Hierarchy;

        // Mirror the same cone/cylinder shape onto the secondary system
        var shape = _secondaryParticleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;

        float radiusDiff = _frustumTrigger.topRadius - _frustumTrigger.bottomRadius;

        if (Mathf.Abs(radiusDiff) < 0.001f)
        {
            shape.angle = 0.1f;
            shape.radius = _frustumTrigger.bottomRadius;
            shape.length = height;
            shape.position = Vector3.zero;
        }
        else
        {
            float halfAngle = Mathf.Atan(radiusDiff / height) * Mathf.Rad2Deg;
            shape.angle = halfAngle;
            shape.radius = _frustumTrigger.bottomRadius;
            shape.length = height;
            shape.position = Vector3.zero;
        }

        // Derive speed and duration from each other based on drive mode

        if (_secondaryDriveMode == SecondaryDriveMode.Speed)
        {
            float speed = Mathf.Max(_secondarySpeed, 0.001f);
            secondaryMain.startSpeed = speed;
            secondaryMain.startLifetime = height / speed;
        }
        else // Duration
        {
            float duration = Mathf.Max(_secondaryDuration, 0.001f);
            secondaryMain.startLifetime = duration;
            secondaryMain.startSpeed = height / duration;
        }
    }
}
