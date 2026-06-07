using Crease.Flying.Player;
using Crease.Managers.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player.Dash
{
    public enum DashRechargeMode
    {
        Slipstream,
        SimpleTimer
    }

    public class DashController : MonoBehaviour
    {
        [FormerlySerializedAs("animator")]
        [SerializeField] private Animator _animator;
        [FormerlySerializedAs("kinematicBody")]
        [SerializeField] private KinematicBody _kinematicBody;
        [FormerlySerializedAs("boostStrength")]
        [SerializeField] private float _boostStrength = 50f;
        [FormerlySerializedAs("dashDuration")]
        [SerializeField] private float _dashDuration = 0.5f;
        [FormerlySerializedAs("invincibilityDuration")]
        [SerializeField] private float _invincibilityDuration = 0.6f;
        [Header("Trail Settings")]
        [FormerlySerializedAs("wingTrailController")]
        [SerializeField] private WingTrailController _wingTrailController;
        [FormerlySerializedAs("trailTime")]
        [SerializeField] private float _trailTime = 0.5f;
        [FormerlySerializedAs("dashBorder")]
        [SerializeField] private GameObject _dashBorder;

        [Header("Recharge Settings")]
        [FormerlySerializedAs("rechargeMode")]
        [SerializeField] private DashRechargeMode _rechargeMode = DashRechargeMode.Slipstream;
        [FormerlySerializedAs("rechargeRate")]
        [SerializeField] private float _rechargeRate = 20f;
        [FormerlySerializedAs("rechargeMax")]
        [SerializeField] private float _rechargeMax = 100f;

        private int _objectsInRange = 0;
        private MeshRenderer _dashBorderRenderer;

        private float _dashTimer = 0f;
        private float _invincibilityTimer = 0f;
        private float _trailTimer = 0f;
        private float _currentRecharge = 0f;
        private bool _canDash = true;

        private Vector3 _dashDirection;
        private float _dashSpeed;

        public bool IsDashing => _dashTimer > 0f;
        public bool IsInvincible => _invincibilityTimer > 0f;
        public float CurrentRecharge => _currentRecharge;
        public float MaxRecharge => _rechargeMax;
        public bool CanDash => _canDash;

        void Start()
        {
            _currentRecharge = _rechargeMax;

            if (_wingTrailController != null)
            {
                _wingTrailController.SetTrailEnabled(false);
            }

            if (_dashBorder != null)
            {
                _dashBorderRenderer = _dashBorder.GetComponent<MeshRenderer>();
            }
        }

        void Update()
        {
            if (InputManager.Instance.DashTriggered)
            {
                TriggerDash();
            }

            if (_dashTimer > 0f)
            {
                _dashTimer -= Time.deltaTime;
            }

            if (_invincibilityTimer > 0f)
            {
                _invincibilityTimer -= Time.deltaTime;
            }

            if (_trailTimer > 0f)
            {
                _trailTimer -= Time.deltaTime;
                if (_trailTimer <= 0f && _wingTrailController != null)
                {
                    _wingTrailController.SetTrailEnabled(false);
                }
            }

            bool isRecharging = (_rechargeMode == DashRechargeMode.SimpleTimer) || (_objectsInRange > 0);

            if (isRecharging && _currentRecharge < _rechargeMax)
            {
                _currentRecharge += _rechargeRate * Time.deltaTime;
                if (_currentRecharge >= _rechargeMax)
                {
                    _currentRecharge = _rechargeMax;
                    _canDash = true;
                }
            }

            bool shouldShowDashBorder = (_rechargeMode == DashRechargeMode.Slipstream) && _objectsInRange > 0;
            SetDashBorderVisible(shouldShowDashBorder);
        }

        void FixedUpdate()
        {
            if (IsDashing)
            {
                _kinematicBody.Velocity = _dashDirection * _dashSpeed;
            }
        }

        public void TriggerDash()
        {
            if (_canDash)
            {
                _canDash = false;
                _currentRecharge = 0f;
                _dashTimer = _dashDuration;
                _invincibilityTimer = _invincibilityDuration;

                if (_wingTrailController != null)
                {
                    _wingTrailController.SetTrailEnabled(true);
                    _trailTimer = _trailTime;
                }

                _dashDirection = transform.forward;
                float forwardSpeed = Vector3.Dot(_kinematicBody.Velocity, _dashDirection);
                _dashSpeed = Mathf.Max(forwardSpeed, 0f) + _boostStrength;

                if (_animator != null)
                {
                    _animator.SetTrigger("Dash");
                }
            }
        }

        public void ModifyObjectsInRange(int amount)
        {
            _objectsInRange += amount;
        }

        private void SetDashBorderVisible(bool visible)
        {
            if (_dashBorderRenderer != null)
            {
                _dashBorderRenderer.enabled = visible;
            }
        }

        public void RefreshDash()
        {
            _canDash = true;
            _currentRecharge = _rechargeMax;
        }
    }
}
