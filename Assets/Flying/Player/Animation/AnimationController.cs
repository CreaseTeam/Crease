using UnityEngine;

namespace Crease.Flying.Player.Animation
{
    /// <summary>
    /// Central player animation driver. Spin rotation is handled by the Animator (Spin bool);
    /// procedural mesh offset for barrel rolls is blended here.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimationController : MonoBehaviour
    {
        private static readonly int SpinParam = Animator.StringToHash("Spin");

        [SerializeField] private Transform _meshTransform;
        [SerializeField] private float _defaultOffsetSize = 0.3f;
        [SerializeField] private float _defaultSpinSpeedMultiplier = 1.5f;
        [SerializeField] private float _offsetTransitionDuration = 0.16666667f;

        private Animator _animator;
        private Vector3 _baseMeshLocalPosition;
        private float _currentOffset;
        private float _targetOffset;
        private float _offsetVelocity;
        private float _durationRemaining;
        private RollMode _mode;
        private bool _barrelRollEnding;
        private bool _barrelRollEndSpinStarted;
        private float _barrelRollEndStartNormalizedTime;
        private float _barrelRollEndStartOffset;
        private bool _isSpinning;

        private enum RollMode
        {
            None,
            BarrelRoll,
            AileronRoll
        }

        public bool IsRolling => _mode != RollMode.None;

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            if (_meshTransform != null)
                _baseMeshLocalPosition = _meshTransform.localPosition;
        }

        private void Update()
        {
            UpdateDuration();
            UpdateOffset();
        }

        private void LateUpdate()
        {
            ApplyMeshOffset();

            // Animator updates before MonoBehaviour.Update, so rotation is reset here after
            // the spin state has fully released control of the transform.
            if (!_isSpinning)
                transform.localRotation = Quaternion.identity;
        }

        private void OnDisable()
        {
            ResetRollState();
            ApplyMeshOffset();
        }

        /// <param name="duration">Seconds before auto-stop. Negative values spin until stopped manually.</param>
        /// <param name="spinSpeed">Multiplier on the default spin rate. Null uses the configured default.</param>
        /// <param name="offsetSize">Downward mesh offset in local units. Null uses the configured default.</param>
        public void StartBarrelRoll(float duration = -1f, float? spinSpeed = null, float? offsetSize = null)
        {
            _barrelRollEnding = false;
            _barrelRollEndSpinStarted = false;
            _mode = RollMode.BarrelRoll;
            _targetOffset = offsetSize ?? _defaultOffsetSize;
            _durationRemaining = duration < 0f ? float.PositiveInfinity : duration;
            BeginSpin(spinSpeed);
        }

        public void StopBarrelRoll()
        {
            if (_mode != RollMode.BarrelRoll || _barrelRollEnding)
                return;

            _targetOffset = 0f;
            _durationRemaining = 0f;
            _barrelRollEnding = true;
            _barrelRollEndSpinStarted = false;
            _barrelRollEndStartOffset = _currentOffset;
            _offsetVelocity = 0f;
        }

        /// <param name="duration">Seconds before auto-stop. Negative values spin until stopped manually.</param>
        /// <param name="spinSpeed">Multiplier on the default spin rate. Null uses the configured default.</param>
        public void StartAileronRoll(float duration = -1f, float? spinSpeed = null)
        {
            _barrelRollEnding = false;
            _mode = RollMode.AileronRoll;
            _targetOffset = 0f;
            _currentOffset = 0f;
            _offsetVelocity = 0f;
            _durationRemaining = duration < 0f ? float.PositiveInfinity : duration;
            BeginSpin(spinSpeed);
        }

        public void StopAileronRoll()
        {
            if (_mode != RollMode.AileronRoll)
                return;

            _durationRemaining = 0f;
            _mode = RollMode.None;
            StopSpin();
        }

        private void BeginSpin(float? spinSpeed)
        {
            _isSpinning = true;
            _animator.speed = spinSpeed ?? _defaultSpinSpeedMultiplier;
            _animator.SetBool(SpinParam, true);
        }

        private void StopSpin()
        {
            _isSpinning = false;
            _animator.SetBool(SpinParam, false);
            _animator.speed = 1f;
        }

        private void ResetRollState()
        {
            _currentOffset = 0f;
            _targetOffset = 0f;
            _offsetVelocity = 0f;
            _durationRemaining = 0f;
            _barrelRollEnding = false;
            _barrelRollEndSpinStarted = false;
            _mode = RollMode.None;
            _isSpinning = false;

            if (_animator != null)
            {
                _animator.SetBool(SpinParam, false);
                _animator.speed = 1f;
            }
        }

        private void UpdateDuration()
        {
            if (_durationRemaining == float.PositiveInfinity || _durationRemaining <= 0f)
                return;

            _durationRemaining -= Time.deltaTime;

            if (_durationRemaining > 0f)
                return;

            if (_mode == RollMode.BarrelRoll)
                StopBarrelRoll();
            else if (_mode == RollMode.AileronRoll)
                StopAileronRoll();
        }

        private void UpdateOffset()
        {
            if (_meshTransform == null)
                return;

            if (_barrelRollEnding)
            {
                UpdateBarrelRollEnding();
                return;
            }

            if (_offsetTransitionDuration <= 0f)
                _currentOffset = _targetOffset;
            else
                _currentOffset = Mathf.SmoothDamp(
                    _currentOffset,
                    _targetOffset,
                    ref _offsetVelocity,
                    _offsetTransitionDuration);
        }

        private void UpdateBarrelRollEnding()
        {
            if (!_isSpinning)
                return;

            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName("Spin"))
                return;

            if (!_barrelRollEndSpinStarted)
            {
                _barrelRollEndStartNormalizedTime = state.normalizedTime;
                _barrelRollEndSpinStarted = true;
                return;
            }

            float rollProgress = state.normalizedTime - _barrelRollEndStartNormalizedTime;
            _currentOffset = Mathf.Lerp(_barrelRollEndStartOffset, 0f, Mathf.Clamp01(rollProgress));

            if (rollProgress < 1f)
                return;

            _currentOffset = 0f;
            _offsetVelocity = 0f;
            _barrelRollEnding = false;
            _barrelRollEndSpinStarted = false;
            _mode = RollMode.None;
            StopSpin();
        }

        private void ApplyMeshOffset()
        {
            if (_meshTransform == null)
                return;

            _meshTransform.localPosition = _baseMeshLocalPosition + Vector3.down * _currentOffset;
        }
    }
}
