using Crease.Flying.Player;
using Crease.Managers.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Crease.Flying.Player.Camera
{
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [FormerlySerializedAs("target")]
        public Transform Target;
        [FormerlySerializedAs("flightController")]
        public FlightController FlightController;
        [FormerlySerializedAs("settings")]
        [SerializeField]
        private CameraSettings _settings;

        private Vector3 _runtimeOffset;

        private float _currentYaw;
        private float _currentPitch;
        private float _prevTargetPitch;
        private float _smoothedPitchRate;
        private float _currentProfileOffset;
        private Vector3 _positionVelocity;
        private Quaternion _unpannedBaseRotation;
        private Vector3 _unpannedBasePosition;

        private float _panYaw;
        private float _panPitch;
        private bool _centerPanRequested;
        private float _mouseInactivityTimer;
        private Vector2 _lastMouseInput;

        public void RecenterPan()
        {
            _panYaw = 0f;
            _panPitch = 0f;
        }

        private void Start()
        {
            if (_settings == null)
            {
                Debug.LogError($"CameraController on '{name}' requires a CameraSettings asset assigned.");
                enabled = false;
                return;
            }

            if (Target == null) return;

            Vector3 euler = Target.rotation.eulerAngles;
            _currentYaw = euler.y;
            _currentPitch = NormalizeAngle(euler.x);
            _prevTargetPitch = _currentPitch;
            _unpannedBaseRotation = transform.rotation;
            _unpannedBasePosition = transform.position;
            _runtimeOffset = _settings.DefaultOffset;

            if (Mouse.current != null)
            {
                Mouse.current.WarpCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f));
            }
        }

        private void LateUpdate()
        {
            if (Target == null) return;

            float dt = Time.deltaTime;
            if (dt < 0.0001f) return;

            HandleZoom(dt);
            HandlePanning(dt);

            if (InputManager.Instance != null && InputManager.Instance.CenterCameraTriggered)
            {
                _centerPanRequested = true;
                if (Mouse.current != null)
                {
                    Mouse.current.WarpCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f));
                }
            }

            Vector3 targetEuler = Target.rotation.eulerAngles;
            float targetYaw = targetEuler.y;
            float targetPitch = NormalizeAngle(targetEuler.x);

            float rawPitchRate = Mathf.DeltaAngle(_prevTargetPitch, targetPitch) / dt;
            _prevTargetPitch = targetPitch;
            _smoothedPitchRate = Mathf.Lerp(_smoothedPitchRate, rawPitchRate, dt * _settings.PitchRateSmoothing);
            float desiredOffset = -_smoothedPitchRate * _settings.ProfileStrength;
            desiredOffset = Mathf.Clamp(desiredOffset, -_settings.MaxProfileOffset, _settings.MaxProfileOffset);
            _currentProfileOffset = Mathf.Lerp(_currentProfileOffset, desiredOffset, dt * _settings.ProfileDecay);

            _currentYaw = Mathf.LerpAngle(_currentYaw, targetYaw, dt * _settings.YawSpeed);
            _currentPitch = Mathf.LerpAngle(_currentPitch, targetPitch, dt * _settings.PitchSpeed);
            float finalPitch = _currentPitch + _currentProfileOffset;

            float targetRoll = (FlightController != null) ? -FlightController.Roll : NormalizeAngle(targetEuler.z);
            float rigRoll = Mathf.Lerp(targetRoll, 0f, _settings.HorizonRollStabilization);
            Quaternion baseRigRotation = Quaternion.Euler(finalPitch, _currentYaw, rigRoll);

            Vector3 baseDesiredPosition = Target.position + baseRigRotation * _runtimeOffset;
            if (_unpannedBasePosition == Vector3.zero) _unpannedBasePosition = transform.position;
            _unpannedBasePosition = Vector3.SmoothDamp(_unpannedBasePosition, baseDesiredPosition, ref _positionVelocity, 1f / _settings.PositionSmoothing);

            Vector3 baseLookTarget = Vector3.Lerp(
                Target.position,
                Target.position + Target.forward * _settings.LookAheadDistance,
                _settings.LookAheadBlend);

            Vector3 lookDir = (baseLookTarget - _unpannedBasePosition).normalized;
            Vector3 upVec = Vector3.Lerp(baseRigRotation * Vector3.up, Vector3.up, _settings.HorizonRollStabilization);

            Quaternion baseDesiredRotation = Quaternion.LookRotation(lookDir, upVec);

            if (_unpannedBaseRotation.w == 0f) _unpannedBaseRotation = transform.rotation;
            _unpannedBaseRotation = Quaternion.Slerp(_unpannedBaseRotation, baseDesiredRotation, dt * _settings.LookSmoothing);

            Quaternion localPan = Quaternion.Euler(-_panPitch, _panYaw, 0f);

            Quaternion finalRotation = _unpannedBaseRotation * localPan;

            Quaternion rOrbit = finalRotation * Quaternion.Inverse(_unpannedBaseRotation);
            Vector3 offsetFromTarget = _unpannedBasePosition - Target.position;

            transform.position = Target.position + rOrbit * offsetFromTarget;
            transform.rotation = finalRotation;
        }

        private void HandleZoom(float dt)
        {
            float scrollY = InputManager.Instance.CameraZoomInput.y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                _runtimeOffset.z += Mathf.Sign(scrollY) * _settings.ZoomSpeed * dt;
                _runtimeOffset.z = Mathf.Clamp(_runtimeOffset.z, _settings.MaxZoomOffset, _settings.MinZoomOffset);
            }
        }

        private void HandlePanning(float dt)
        {
            Vector2 input = InputManager.Instance.CameraPanInput;
            var activeControl = InputManager.Instance.Actions.Player.CameraPan.activeControl;

            bool isMouse = activeControl != null && activeControl.device is Mouse;

            Vector2 targetPan = Vector2.zero;

            if (_centerPanRequested)
            {
                targetPan = Vector2.zero;
                _centerPanRequested = false;
            }
            else if (isMouse)
            {
                if (Vector2.Distance(input, _lastMouseInput) < 0.1f)
                {
                    _mouseInactivityTimer += dt;

                    if (_mouseInactivityTimer >= _settings.MouseInactivityTimeout)
                    {
                        _centerPanRequested = true;
                        if (Mouse.current != null)
                        {
                            Mouse.current.WarpCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f));
                        }
                        _mouseInactivityTimer = 0f;
                    }
                }
                else
                {
                    _mouseInactivityTimer = 0f;
                }
                _lastMouseInput = input;

                float hw = Screen.width * 0.5f;
                float hh = Screen.height * 0.5f;
                float rawX = (input.x - hw) / hw;
                float rawY = (input.y - hh) / hh;
                Vector2 raw = new Vector2(rawX, rawY);
                float mag = raw.magnitude;

                if (mag <= _settings.MouseDeadzone)
                {
                    targetPan = Vector2.zero;
                }
                else
                {
                    float ratio = (mag - _settings.MouseDeadzone) / (1f - _settings.MouseDeadzone);
                    Vector2 scaled = raw.normalized * ratio;
                    targetPan.x = ApplyMouseSensitivity(scaled.x, _settings.MouseSensitivity);
                    targetPan.y = ApplyMouseSensitivity(scaled.y, _settings.MouseSensitivity);
                }
            }
            else
            {
                targetPan = input;
            }

            targetPan.x = Mathf.Clamp(targetPan.x, -1f, 1f);
            targetPan.y = Mathf.Clamp(targetPan.y, -1f, 1f);

            float targetYaw = targetPan.x * _settings.MaxPanAngleX;
            float targetPitch = targetPan.y * _settings.MaxPanAngleY;

            _panYaw = Mathf.MoveTowards(_panYaw, targetYaw, _settings.PanSpeed * dt);
            _panPitch = Mathf.MoveTowards(_panPitch, targetPitch, _settings.PanSpeed * dt);
        }

        private static float NormalizeAngle(float angle)
        {
            if (angle > 180f) angle -= 360f;
            return angle;
        }

        private static float ApplyMouseSensitivity(float normalized, float sensitivity)
        {
            float s = Mathf.Max(0.01f, sensitivity);
            double numerator = System.Math.Tanh(s * normalized);
            double denom = System.Math.Tanh(s);
            if (denom == 0.0) return normalized;
            return (float)(numerator / denom);
        }

        private void OnDrawGizmos()
        {
            if (Target == null) return;
            Quaternion rig = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 ghostPos = Target.position + rig * _runtimeOffset;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(ghostPos, 0.5f);
        }
    }
}
