using Crease.Flying.Player.FlightModifiers;
using Crease.Managers.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(KinematicBody))]
    public class FlightController : MonoBehaviour
    {
        private KinematicBody _body;
        private FlightModifiers.FlightModifiers _flightModifiers;

        [FormerlySerializedAs("pitch")]
        [SerializeField] private float _pitch = 0f;
        public float Pitch => _pitch;

        [FormerlySerializedAs("meshTransform")]
        [SerializeField] private Transform _meshTransform;
        public Transform MeshTransform => _meshTransform;

        private Vector3 _meshRotation;
        private float _yaw = 0f;

        private float _roll = 0f;
        public float Roll => _roll;

        [FormerlySerializedAs("rollOffset")]
        [SerializeField] private float _rollOffset = 0f;
        public float RollOffset
        {
            get => _rollOffset;
            set => _rollOffset = value;
        }

        [FormerlySerializedAs("stats")]
        [SerializeField] private FlightStats _stats;

        private float _inputMagnitude;

        private void Awake()
        {
            _body = GetComponent<KinematicBody>();
            _flightModifiers = GetComponent<FlightModifiers.FlightModifiers>();
        }

        void Start()
        {
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x;
            if (_pitch > 180f) _pitch -= 360f;

            if (_stats == null)
            {
                Debug.LogError($"FlightController on '{name}' requires a FlightStats component assigned.");
                enabled = false;
                return;
            }

            _body.Velocity = transform.forward * _stats.CurrentStats.InitialSpeed;

            _meshRotation = _meshTransform.localEulerAngles;
        }

        private bool IsFlightControlLocked =>
            _flightModifiers != null && _flightModifiers.IsActive(FlightModifierType.LockFlightControl);

        void FixedUpdate()
        {
            _body.SimulationSpeed = _flightModifiers != null ? _flightModifiers.SimulationSpeed : 1f;

            if (!IsFlightControlLocked)
            {
                ProcessInput();
                UpdateVelocity();
                ApplyStabilityTorque();
                ClampOrientation();
            }

            UpdateRotation();
        }

        private float ScaledDeltaTime =>
            _flightModifiers != null ? _flightModifiers.ScaledFixedDeltaTime : Time.fixedDeltaTime;

        private void UpdateVelocity()
        {
            Vector3 velocity = _body.Velocity;
            float scaledDt = ScaledDeltaTime;

            float pitchRadians = _pitch * Mathf.Deg2Rad;
            float cosPitch = Mathf.Cos(pitchRadians);
            float sinPitch = Mathf.Sin(pitchRadians);
            float safeCosPitch = Mathf.Max(Mathf.Abs(cosPitch), 0.2f);

            Vector3 lookDirection = GetLookDirection();
            float horizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

            float mp = _stats.CurrentStats.MaxPitch;
            float gravityT = Mathf.InverseLerp(-mp, mp, _pitch);
            float currentGravity = Mathf.Lerp(_stats.CurrentStats.ClimbingGravity, _stats.CurrentStats.DivingGravity, gravityT);
            velocity.y -= currentGravity * scaledDt;

            velocity.y += cosPitch * cosPitch * _stats.CurrentStats.Lift * scaledDt;

            if (velocity.y < 0 && cosPitch > 0)
            {
                float yAcc = velocity.y * -_stats.CurrentStats.DiveRate * cosPitch * cosPitch * scaledDt;
                velocity.y += yAcc;
                RedirectVerticalAcceleration(ref velocity, lookDirection, yAcc, safeCosPitch);
            }

            if (pitchRadians < 0)
            {
                float yAcc = horizontalSpeed * -sinPitch * -sinPitch * _stats.CurrentStats.ClimbRate * scaledDt;
                velocity.y += yAcc * _stats.CurrentStats.ClimbEfficiency;
                RedirectVerticalAcceleration(ref velocity, lookDirection, -yAcc, safeCosPitch);
            }

            float speed = velocity.magnitude;
            if (speed > 0.001f)
            {
                float tInterp = _stats.CurrentStats.TurnInterpolation * scaledDt;
                velocity = Vector3.Lerp(velocity, lookDirection * speed, tInterp);
            }

            float simSpeed = _flightModifiers != null ? _flightModifiers.SimulationSpeed : 1f;
            velocity.x *= Mathf.Pow(_stats.CurrentStats.XDrag, simSpeed);
            velocity.y *= Mathf.Pow(_stats.CurrentStats.YDrag, simSpeed);
            velocity.z *= Mathf.Pow(_stats.CurrentStats.ZDrag, simSpeed);

            float minVel = _stats.CurrentStats.MinimumVelocity;
            if (velocity.magnitude < minVel)
            {
                velocity = velocity.normalized * minVel;
            }

            _body.Velocity = velocity;
        }

        private Vector3 GetLookDirection()
        {
            return Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
        }

        private static void RedirectVerticalAcceleration(ref Vector3 velocity, Vector3 lookDirection, float yAcc, float safeCosPitch)
        {
            velocity.x += lookDirection.x * yAcc / safeCosPitch;
            velocity.z += lookDirection.z * yAcc / safeCosPitch;
        }

        private void ApplyStabilityTorque()
        {
            Vector3 velocity = _body.Velocity;
            float speed = velocity.magnitude;
            if (speed < 0.5f) return;

            float speedFactor = Mathf.Clamp01(speed / _stats.CurrentStats.StabilityReferenceSpeed);
            if (speedFactor <= 0f) return;

            Vector3 velocityDirection = velocity / speed;
            Vector3 forward = GetLookDirection();

            Vector3 misalignmentAxis = Vector3.Cross(forward, velocityDirection);
            float sinAngle = misalignmentAxis.magnitude;
            if (sinAngle < 0.001f) return;

            float angleDegrees = Mathf.Asin(Mathf.Clamp(sinAngle, 0f, 1f)) * Mathf.Rad2Deg;
            Vector3 correctionAxis = misalignmentAxis / sinAngle;

            float inputSuppression = _stats.CurrentStats.StabilityInputSuppression * _inputMagnitude;
            float stabilityScale = speedFactor * (1f - inputSuppression);
            if (stabilityScale <= 0f) return;

            float correctionRate = angleDegrees * _stats.CurrentStats.StabilityStrength * stabilityScale;
            Vector3 worldAngularVelocity = correctionAxis * correctionRate;

            float pitchRate = Vector3.Dot(worldAngularVelocity, transform.right);
            float yawRate = Vector3.Dot(worldAngularVelocity, Vector3.up);

            float dt = ScaledDeltaTime;
            _pitch += pitchRate * dt;
            _yaw += yawRate * dt;
        }

        private void UpdateRotation()
        {
            _body.MoveRotation(Quaternion.Euler(_pitch, _yaw, 0f));

            if (_meshTransform != null)
            {
                _meshTransform.localRotation = Quaternion.Euler(_roll + _rollOffset + _meshRotation.x, _meshRotation.y, _meshRotation.z);
            }
        }

        private void ProcessInput()
        {
            ProcessKeyboardInput();
        }

        private void ClampOrientation()
        {
            _pitch = Mathf.Clamp(_pitch, -_stats.CurrentStats.MaxPitch, _stats.CurrentStats.MaxPitch);
            _roll = Mathf.Clamp(_roll, -_stats.CurrentStats.MaxRoll, _stats.CurrentStats.MaxRoll);
        }

        private void ProcessKeyboardInput()
        {
            Vector2 move = InputManager.Instance.MoveInput;
            _inputMagnitude = move.magnitude;

            float scaledDt = ScaledDeltaTime;
            _pitch += move.y * _stats.CurrentStats.PitchSpeed * scaledDt;

            if (move.x != 0f)
            {
                _yaw += move.x * _stats.CurrentStats.YawSpeed * scaledDt;
                _roll += move.x * _stats.CurrentStats.RollSpeed * scaledDt;
            }

            if (InputManager.Instance.BoostPressed)
                Boost();

            if (move.x == 0f)
            {
                if (_roll > 0f)
                {
                    _roll -= _stats.CurrentStats.RollBackSpeed * scaledDt;
                    if (_roll < 0f) _roll = 0f;
                }
                else if (_roll < 0f)
                {
                    _roll += _stats.CurrentStats.RollBackSpeed * scaledDt;
                    if (_roll > 0f) _roll = 0f;
                }
            }
        }

        private void Boost()
        {
            _body.Velocity += transform.forward * _stats.CurrentStats.BoostSpeed;
        }
    }
}
