using Crease.Flying.Player;
using Crease.Flying.Player.Camera;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Environment.Obstacle
{
    /// <summary>
    /// Handles the crash state of the player — disabling flight and letting the body fall.
    /// Does NOT handle collision detection; that is done by FlightCollisionController,
    /// which may call Land() when the crashed plane hits the ground.
    /// </summary>
    [RequireComponent(typeof(KinematicBody))]
    public class PlayerCrashHandler : MonoBehaviour
    {
        [Header("Refs")]
        [FormerlySerializedAs("flightController")]
        [SerializeField] private MonoBehaviour _flightController;
        [FormerlySerializedAs("body")]
        [SerializeField] private KinematicBody _body;

        [Header("Crash Tuning")]
        [FormerlySerializedAs("zeroVelocityOnCrash")]
        [SerializeField] private bool _zeroVelocityOnCrash = true;
        [FormerlySerializedAs("stopCompletelyOnLand")]
        [SerializeField] private bool _stopCompletelyOnLand = true;

        [Header("Gravity While Crashed")]
        [Tooltip("Custom gravity applied after crash (body falls under its own sim).")]
        [FormerlySerializedAs("crashGravity")]
        [SerializeField] private float _crashGravity = 20f;

        [FormerlySerializedAs("cameraController")]
        [SerializeField] private CameraController _cameraController;

        public bool IsCrashed => _crashed;
        public bool IsLanded => _landed;

        private bool _crashed;
        private bool _landed;
        private Rigidbody _rigidbody;
        private RigidbodyConstraints _flightConstraints;

        private void Awake()
        {
            if (_body == null) _body = GetComponent<KinematicBody>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (_crashed && !_landed)
            {
                _body.Velocity += Vector3.down * _crashGravity * Time.fixedDeltaTime;
            }
        }

        /// <summary>
        /// Enters the crashed state — disables flight and lets the plane fall.
        /// </summary>
        public void Crash()
        {
            if (_crashed) return;

            _crashed = true;
            _landed = false;

            if (_rigidbody != null)
                _flightConstraints = _rigidbody.constraints;

            if (_flightController != null)
                _flightController.enabled = false;

            if (_body != null)
                _body.Frozen = false;

            if (_zeroVelocityOnCrash && _body != null)
                _body.SetVelocity(Vector3.zero);

            if (_rigidbody != null)
            {
                _rigidbody.constraints = RigidbodyConstraints.None;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Called when the crashed plane touches the ground.
        /// </summary>
        public void Land()
        {
            if (!_crashed || _landed) return;

            _landed = true;

            if (_stopCompletelyOnLand && _body != null)
                _body.SetVelocity(Vector3.zero);

            if (_body != null)
                _body.Frozen = true;
        }

        /// <summary>
        /// Resets the crash state (useful for scene reloads or respawns).
        /// </summary>
        public void ResetCrash()
        {
            _crashed = false;
            _landed = false;

            if (_body != null)
                _body.Frozen = false;

            if (_rigidbody != null)
            {
                _rigidbody.constraints = _flightConstraints;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            if (_flightController != null)
                _flightController.enabled = true;
        }
    }
}
