using DG.Tweening;
using UnityEngine;

namespace Crease.Flying.Player.Animation
{
    /// <summary>
    /// Drives procedural barrel-roll rotation on this transform and optional mesh offset on a child.
    /// </summary>
    public class AnimationController : MonoBehaviour
    {
        [SerializeField] private Transform _meshTransform;
        [SerializeField] private float _defaultOffsetSize = 0.3f;
        [SerializeField] private float _defaultSpinSpeedMultiplier = 1.5f;
        [SerializeField] private float _rollEaseDuration = 0.15f;

        private Vector3 _baseMeshLocalPosition;
        private float _currentOffset;
        private float _targetOffset;
        private float _rollAngle;
        private float _degreesPerSecond;
        private float _spinEase = 1f;
        private float _durationRemaining;
        private RollMode _mode;
        private Tween _rollTween;
        private Tween _offsetTween;
        private Tween _spinEaseTween;

        private enum RollMode
        {
            None,
            BarrelRoll,
            BarrelRollEnding,
            AileronRoll
        }

        public bool IsRolling => _mode != RollMode.None;

        private void Awake()
        {
            if (_meshTransform != null)
                _baseMeshLocalPosition = _meshTransform.localPosition;
        }

        private void Update()
        {
            UpdateDuration();
            UpdateRoll();
            ApplyMeshOffset();
        }

        private void OnDisable()
        {
            KillTweens();
            ResetRollState();
            ApplyMeshOffset();
        }

        /// <param name="duration">Seconds before auto-stop. Negative values spin until stopped manually.</param>
        /// <param name="spinSpeed">Multiplier on the default spin rate. Null uses the configured default.</param>
        /// <param name="offsetSize">Downward mesh offset in local units. Null uses the configured default.</param>
        public void StartBarrelRoll(float duration = -1f, float? spinSpeed = null, float? offsetSize = null)
        {
            KillTweens();
            ResetVisuals();

            _mode = RollMode.BarrelRoll;
            _targetOffset = offsetSize ?? _defaultOffsetSize;
            _degreesPerSecond = 360f * (spinSpeed ?? _defaultSpinSpeedMultiplier);
            _durationRemaining = duration < 0f ? float.PositiveInfinity : duration;

            if (_rollEaseDuration <= 0f)
            {
                _currentOffset = _targetOffset;
                _spinEase = 1f;
                return;
            }

            _spinEase = 0f;
            _offsetTween = DOTween.To(() => _currentOffset, value => _currentOffset = value, _targetOffset, _rollEaseDuration)
                .SetEase(Ease.InOutSine);
            _spinEaseTween = DOTween.To(() => _spinEase, value => _spinEase = value, 1f, _rollEaseDuration)
                .SetEase(Ease.InOutSine);
        }

        public void StopBarrelRoll()
        {
            if (_mode != RollMode.BarrelRoll)
                return;

            KillTweens();
            _spinEase = 1f;
            BeginBarrelRollEnding();
        }

        /// <param name="duration">Seconds before auto-stop. Negative values spin until stopped manually.</param>
        /// <param name="spinSpeed">Multiplier on the default spin rate. Null uses the configured default.</param>
        public void StartAileronRoll(float duration = -1f, float? spinSpeed = null)
        {
            KillTweens();
            ResetVisuals();

            _mode = RollMode.AileronRoll;
            _targetOffset = 0f;
            _currentOffset = 0f;
            _degreesPerSecond = 360f * (spinSpeed ?? _defaultSpinSpeedMultiplier);
            _spinEase = 1f;
            _durationRemaining = duration < 0f ? float.PositiveInfinity : duration;
        }

        public void StopAileronRoll()
        {
            if (_mode != RollMode.AileronRoll)
                return;

            KillTweens();
            ResetRollState();
            ApplyMeshOffset();
        }

        /// <summary>
        /// Updates the spin rate of an in-progress AileronRoll without resetting the roll angle.
        /// Has no effect if not currently in AileronRoll mode.
        /// </summary>
        public void SetAileronRollSpeed(float spinSpeed)
        {
            if (_mode != RollMode.AileronRoll) return;
            _degreesPerSecond = 360f * spinSpeed;
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

        private void UpdateRoll()
        {
            if (_mode != RollMode.BarrelRoll && _mode != RollMode.AileronRoll)
                return;

            _rollAngle += _degreesPerSecond * _spinEase * Time.deltaTime;
            ApplyRotation();
        }

        private void BeginBarrelRollEnding()
        {
            _mode = RollMode.BarrelRollEnding;
            _durationRemaining = 0f;

            float remainder = 360f - Mathf.Repeat(_rollAngle, 360f);
            if (remainder < 0.001f)
                remainder = 360f;

            float endAngle = _rollAngle + remainder;
            float duration = remainder / _degreesPerSecond;

            _rollTween = DOTween.To(() => _rollAngle, value =>
                {
                    _rollAngle = value;
                    ApplyRotation();
                }, endAngle, duration)
                .SetEase(Ease.InOutSine)
                .OnComplete(CompleteBarrelRoll);

            _offsetTween = DOTween.To(() => _currentOffset, value => _currentOffset = value, 0f, duration)
                .SetEase(Ease.InOutSine);
        }

        private void CompleteBarrelRoll()
        {
            KillTweens();
            ResetRollState();
            ApplyMeshOffset();
        }

        private void ApplyRotation()
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Repeat(_rollAngle, 360f));
        }

        private void ApplyMeshOffset()
        {
            if (_meshTransform == null)
                return;

            _meshTransform.localPosition = _baseMeshLocalPosition + Vector3.down * _currentOffset;
        }

        private void KillTweens()
        {
            _rollTween?.Kill();
            _offsetTween?.Kill();
            _spinEaseTween?.Kill();
            _rollTween = null;
            _offsetTween = null;
            _spinEaseTween = null;
        }

        private void ResetVisuals()
        {
            _rollAngle = 0f;
            _currentOffset = 0f;
            _spinEase = 1f;
            transform.localRotation = Quaternion.identity;
        }

        private void ResetRollState()
        {
            _mode = RollMode.None;
            _rollAngle = 0f;
            _currentOffset = 0f;
            _targetOffset = 0f;
            _spinEase = 1f;
            _durationRemaining = 0f;
            transform.localRotation = Quaternion.identity;
        }
    }
}
