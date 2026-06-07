using UnityEngine;
using Crease.Flying.Environment.Wind.Frustum;

namespace Crease.Flying.Environment.Wind.Visuals
{
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

        private float _lastTopRadius;
        private float _lastBottomRadius;
        private float _lastHeight;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
            _frustumTrigger = GetComponent<FrustumTrigger>();
        }

        private void OnEnable()
        {
            UpdateParticleShape();
        }

        private void OnValidate()
        {
            UpdateParticleShape();
        }

        private void Update()
        {
            if (_frustumTrigger != null && HasFrustumChanged())
            {
                UpdateParticleShape();
            }
        }

        private bool HasFrustumChanged()
        {
            return _frustumTrigger.TopRadius != _lastTopRadius ||
                   _frustumTrigger.BottomRadius != _lastBottomRadius ||
                   _frustumTrigger.Height != _lastHeight;
        }

        private void UpdateParticleShape()
        {
            if (_particleSystem == null || _frustumTrigger == null) return;

            _lastTopRadius = _frustumTrigger.TopRadius;
            _lastBottomRadius = _frustumTrigger.BottomRadius;
            _lastHeight = _frustumTrigger.Height;

            var main = _particleSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var shape = _particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;

            float radiusDiff = _frustumTrigger.TopRadius - _frustumTrigger.BottomRadius;

            if (Mathf.Abs(radiusDiff) < 0.001f)
            {
                shape.angle = 0.1f;
                shape.radius = _frustumTrigger.BottomRadius;
                shape.length = _frustumTrigger.Height;
                shape.position = Vector3.zero;
            }
            else
            {
                float halfAngle = Mathf.Atan(radiusDiff / _frustumTrigger.Height) * Mathf.Rad2Deg;

                shape.angle = halfAngle;
                shape.radius = _frustumTrigger.BottomRadius;
                shape.length = _frustumTrigger.Height;
                shape.position = Vector3.zero;
            }

            float lifetime = main.startLifetime.constant;
            if (lifetime > 0)
            {
                main.startSpeed = _frustumTrigger.Height / lifetime;
            }

            UpdateSecondaryParticleSystem();
        }

        private void UpdateSecondaryParticleSystem()
        {
            if (_secondaryParticleSystem == null) return;

            float height = _frustumTrigger.Height;

            var secondaryMain = _secondaryParticleSystem.main;
            secondaryMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            secondaryMain.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var shape = _secondaryParticleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;

            float radiusDiff = _frustumTrigger.TopRadius - _frustumTrigger.BottomRadius;

            if (Mathf.Abs(radiusDiff) < 0.001f)
            {
                shape.angle = 0.1f;
                shape.radius = _frustumTrigger.BottomRadius;
                shape.length = height;
                shape.position = Vector3.zero;
            }
            else
            {
                float halfAngle = Mathf.Atan(radiusDiff / height) * Mathf.Rad2Deg;
                shape.angle = halfAngle;
                shape.radius = _frustumTrigger.BottomRadius;
                shape.length = height;
                shape.position = Vector3.zero;
            }

            if (_secondaryDriveMode == SecondaryDriveMode.Speed)
            {
                float speed = Mathf.Max(_secondarySpeed, 0.001f);
                secondaryMain.startSpeed = speed;
                secondaryMain.startLifetime = height / speed;
            }
            else
            {
                float duration = Mathf.Max(_secondaryDuration, 0.001f);
                secondaryMain.startLifetime = duration;
                secondaryMain.startSpeed = height / duration;
            }
        }
    }
}
