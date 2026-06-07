using Crease.Flying.Player.Dash;
using Crease.Flying.Player.FlightSettings;
using Crease.Managers.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player
{
    [RequireComponent(typeof(KinematicBody))]
    public class FlightController : MonoBehaviour
    {
        private KinematicBody _body;
        private DashController _dashController;

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

        private void Awake()
        {
            _body = GetComponent<KinematicBody>();
            _dashController = GetComponent<DashController>();
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

        void FixedUpdate()
        {
            ProcessInput();
            if (_dashController == null || !_dashController.IsDashing)
                UpdateVelocity();
            UpdateRotation();
        }

        private void UpdateVelocity()
        {
            Vector3 velocity = _body.Velocity;

            float pitchRadians = _pitch * Mathf.Deg2Rad;
            float cosPitch = Mathf.Cos(pitchRadians);
            float sinPitch = Mathf.Sin(pitchRadians);

            Vector3 lookDirection = transform.forward;
            float horizontalSpeed = new Vector3(velocity.x, 0, velocity.z).magnitude;

            float mp = _stats.CurrentStats.MaxPitch;
            float gravityT = Mathf.InverseLerp(-mp, mp, _pitch);
            float currentGravity = Mathf.Lerp(_stats.CurrentStats.ClimbingGravity, _stats.CurrentStats.DivingGravity, gravityT);
            velocity.y -= currentGravity * Time.fixedDeltaTime;

            velocity.y += cosPitch * cosPitch * _stats.CurrentStats.Lift * Time.fixedDeltaTime;

            if (velocity.y < 0 && cosPitch > 0)
            {
                float yAcc = velocity.y * -_stats.CurrentStats.DiveRate * cosPitch * cosPitch * Time.fixedDeltaTime;
                velocity.y += yAcc;
                velocity.x += lookDirection.x * yAcc / cosPitch;
                velocity.z += lookDirection.z * yAcc / cosPitch;
            }

            if (pitchRadians < 0)
            {
                float yAcc = horizontalSpeed * -sinPitch * -sinPitch * _stats.CurrentStats.ClimbRate * Time.fixedDeltaTime;
                velocity.y += yAcc * _stats.CurrentStats.ClimbEfficiency;
                velocity.x -= lookDirection.x * yAcc / cosPitch;
                velocity.z -= lookDirection.z * yAcc / cosPitch;
            }

            if (cosPitch > 0)
            {
                float tInterp = _stats.CurrentStats.TurnInterpolation;
                velocity.x += (lookDirection.x / cosPitch * horizontalSpeed - velocity.x) * tInterp * Time.fixedDeltaTime;
                velocity.z += (lookDirection.z / cosPitch * horizontalSpeed - velocity.z) * tInterp * Time.fixedDeltaTime;
                velocity.y += (lookDirection.y * velocity.magnitude - velocity.y) * tInterp * Time.fixedDeltaTime;
            }

            velocity.x *= _stats.CurrentStats.XDrag;
            velocity.y *= _stats.CurrentStats.YDrag;
            velocity.z *= _stats.CurrentStats.ZDrag;

            float minVel = _stats.CurrentStats.MinimumVelocity;
            if (velocity.magnitude < minVel)
            {
                velocity = velocity.normalized * minVel;
            }

            _body.Velocity = velocity;
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

            _pitch = Mathf.Clamp(_pitch, -_stats.CurrentStats.MaxPitch, _stats.CurrentStats.MaxPitch);
            _roll = Mathf.Clamp(_roll, -_stats.CurrentStats.MaxRoll, _stats.CurrentStats.MaxRoll);
        }

        private void ProcessKeyboardInput()
        {
            Vector2 move = InputManager.Instance.MoveInput;

            _pitch += move.y * _stats.CurrentStats.PitchSpeed * Time.fixedDeltaTime;

            if (move.x != 0f)
            {
                _yaw += move.x * _stats.CurrentStats.YawSpeed * Time.fixedDeltaTime;
                _roll += move.x * _stats.CurrentStats.RollSpeed * Time.fixedDeltaTime;
            }

            if (InputManager.Instance.BoostPressed)
                Boost();

            if (move.x == 0f)
            {
                if (_roll > 0f)
                {
                    _roll -= _stats.CurrentStats.RollBackSpeed * Time.fixedDeltaTime;
                    if (_roll < 0f) _roll = 0f;
                }
                else if (_roll < 0f)
                {
                    _roll += _stats.CurrentStats.RollBackSpeed * Time.fixedDeltaTime;
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
