using System;
using DG.Tweening;
using UnityEngine;

namespace Crease.Flying.Environment
{
    /// <summary>
    /// Rotates a parent transform to roll between day and night sky spheres.
    /// Each hemisphere can use different relative scale multipliers and local offsets
    /// when active vs inactive. Child lights fade in and out with their sky's weight.
    /// Optionally follows a target transform's position.
    /// </summary>
    public class SkyTransition : MonoBehaviour
    {
        public static SkyTransition Instance { get; private set; }

        [Serializable]
        private class HemispherePresentation
        {
            public Transform Hemisphere;

            [Tooltip("Multiplies the authored local scale while this sky is active.")]
            public Vector3 ActiveScaleMultiplier = Vector3.one;

            [Tooltip("Multiplies the authored local scale while the other sky is active.")]
            public Vector3 InactiveScaleMultiplier = Vector3.one;

            [Tooltip("Added to the authored local position while this sky is active.")]
            public Vector3 ActiveLocalOffset;

            [Tooltip("Added to the authored local position while the other sky is active.")]
            public Vector3 InactiveLocalOffset;

            [Tooltip("Light parented under this sky (e.g. sun or moon). Fades with transition weight.")]
            public Light Light;
        }

        [Header("Skies")]
        [SerializeField] private HemispherePresentation _day;
        [SerializeField] private HemispherePresentation _night;

        [Header("Rotation")]
        [Tooltip("Local rotation added each transition (e.g. 180,0,0 rolls on local X).")]
        [SerializeField] private Vector3 _rotationStep = new Vector3(180f, 0f, 0f);

        [SerializeField] private float _duration = 8f;
        [SerializeField] private Ease _ease = Ease.InOutSine;
        [SerializeField] private float _delay;

        [Header("Follow")]
        [Tooltip("When set, this object follows the target's world position each frame.")]
        [SerializeField] private Transform _followTarget;

        [Tooltip("Added to the follow target's world position.")]
        [SerializeField] private Vector3 _followOffset;

        [Header("Debug")]
        [SerializeField] private bool _triggerOnStart;

        private Vector3 _dayBaseLocalScale;
        private Vector3 _dayBaseLocalPosition;
        private Vector3 _nightBaseLocalScale;
        private Vector3 _nightBaseLocalPosition;
        private float _dayLightBaseIntensity;
        private float _nightLightBaseIntensity;

        private Sequence _transitionSequence;
        private bool _isDay = true;

        public bool IsDay => _isDay;
        public bool IsTransitioning { get; private set; }

        public event Action OnTransitionStarted;
        public event Action<bool> OnTransitionComplete;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("SkyTransition: Multiple instances found. Destroying duplicate.");
                Destroy(this);
                return;
            }

            Instance = this;
            CacheBaseTransforms();
            ApplyPresentation(_isDay ? 1f : 0f, _isDay ? 0f : 1f);
            UpdateHemisphereVisibility();
        }

        private void Start()
        {
            if (_triggerOnStart)
                Toggle();
        }

        private void OnDestroy()
        {
            _transitionSequence?.Kill();

            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            if (_followTarget == null)
                return;

            transform.position = _followTarget.position + _followOffset;
        }

        public void TransitionToDay()
        {
            if (_isDay || IsTransitioning)
                return;

            BeginTransition();
        }

        public void TransitionToNight()
        {
            if (!_isDay || IsTransitioning)
                return;

            BeginTransition();
        }

        public void Toggle()
        {
            if (IsTransitioning)
                return;

            BeginTransition();
        }

        public void Stop()
        {
            _transitionSequence?.Kill();
            IsTransitioning = false;
            ApplyPresentation(_isDay ? 1f : 0f, _isDay ? 0f : 1f);
            UpdateHemisphereVisibility();
        }

        private void CacheBaseTransforms()
        {
            if (_day?.Hemisphere != null)
            {
                _dayBaseLocalScale = _day.Hemisphere.localScale;
                _dayBaseLocalPosition = _day.Hemisphere.localPosition;
            }

            if (_night?.Hemisphere != null)
            {
                _nightBaseLocalScale = _night.Hemisphere.localScale;
                _nightBaseLocalPosition = _night.Hemisphere.localPosition;
            }

            if (_day?.Light != null)
                _dayLightBaseIntensity = _day.Light.intensity;

            if (_night?.Light != null)
                _nightLightBaseIntensity = _night.Light.intensity;
        }

        private void BeginTransition()
        {
            _transitionSequence?.Kill();

            float progress = 0f;
            float startDayWeight = _isDay ? 1f : 0f;
            float endDayWeight = 1f - startDayWeight;

            IsTransitioning = true;
            SetHemisphereEnabled(_day, true);
            SetHemisphereEnabled(_night, true);
            OnTransitionStarted?.Invoke();

            _transitionSequence = DOTween.Sequence()
                .Append(transform
                    .DOLocalRotate(_rotationStep, _duration, RotateMode.LocalAxisAdd)
                    .SetEase(_ease))
                .Join(DOTween.To(
                    () => progress,
                    value =>
                    {
                        progress = value;
                        float dayWeight = Mathf.Lerp(startDayWeight, endDayWeight, progress);
                        ApplyPresentation(dayWeight, 1f - dayWeight);
                    },
                    1f,
                    _duration)
                    .SetEase(_ease))
                .SetDelay(_delay)
                .SetAutoKill(true)
                .OnComplete(FinishTransition);
        }

        private void ApplyPresentation(float dayWeight, float nightWeight)
        {
            ApplyHemisphere(_day, _dayBaseLocalScale, _dayBaseLocalPosition, dayWeight);
            ApplyHemisphere(_night, _nightBaseLocalScale, _nightBaseLocalPosition, nightWeight);
            ApplyLight(_day?.Light, _dayLightBaseIntensity, dayWeight);
            ApplyLight(_night?.Light, _nightLightBaseIntensity, nightWeight);
        }

        private static void ApplyHemisphere(
            HemispherePresentation presentation,
            Vector3 baseLocalScale,
            Vector3 baseLocalPosition,
            float activeWeight)
        {
            if (presentation?.Hemisphere == null)
                return;

            Vector3 scaleMultiplier = Vector3.Lerp(
                presentation.InactiveScaleMultiplier,
                presentation.ActiveScaleMultiplier,
                activeWeight);

            Vector3 localOffset = Vector3.Lerp(
                presentation.InactiveLocalOffset,
                presentation.ActiveLocalOffset,
                activeWeight);

            presentation.Hemisphere.localScale = Vector3.Scale(baseLocalScale, scaleMultiplier);
            presentation.Hemisphere.localPosition = baseLocalPosition + localOffset;
        }

        private static void ApplyLight(Light light, float baseIntensity, float activeWeight)
        {
            if (light == null)
                return;

            light.intensity = baseIntensity * activeWeight;
            light.enabled = activeWeight > 0f;
        }

        private void FinishTransition()
        {
            _isDay = !_isDay;
            ApplyPresentation(_isDay ? 1f : 0f, _isDay ? 0f : 1f);
            UpdateHemisphereVisibility();
            IsTransitioning = false;
            OnTransitionComplete?.Invoke(_isDay);
        }

        private void UpdateHemisphereVisibility()
        {
            SetHemisphereEnabled(_day, _isDay);
            SetHemisphereEnabled(_night, !_isDay);
        }

        private static void SetHemisphereEnabled(HemispherePresentation presentation, bool enabled)
        {
            if (presentation?.Hemisphere == null)
                return;

            presentation.Hemisphere.gameObject.SetActive(enabled);
        }
    }
}
