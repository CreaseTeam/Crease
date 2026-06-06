using UnityEngine;
using DG.Tweening;

namespace Crease.Flying.BlockoutContent
{
    public class ClockworkRotator : MonoBehaviour
    {
        public enum PivotMode { Self, AroundPivotTransform, AroundPoint }
        public enum AxisMode { X, Y, Z, Custom }

        [Header("Trigger")]
        [SerializeField] private bool _triggerOnStart = true;

        [Header("Timing")]
        [Min(0.01f)]
        [SerializeField] private float _duration = 2f;

        [Min(0f)]
        [SerializeField] private float _delay = 0f;

        [Header("Rotation")]
        [Tooltip("Total degrees to rotate over the duration.")]
        [SerializeField] private float _degrees = 360f;

        [SerializeField] private bool _clockwise = true;

        [Tooltip("World: axis is world space | Local: axis follows the object.")]
        [SerializeField] private Space _axisSpace = Space.Self;

        [SerializeField] private AxisMode _axisMode = AxisMode.Y;

        [Tooltip("Used when AxisMode = Custom.")]
        [SerializeField] private Vector3 _customAxis = Vector3.up;

        [Header("Pivot")]
        [SerializeField] private PivotMode _pivotMode = PivotMode.Self;
        [SerializeField] private Transform _pivotTransform;
        [SerializeField] private Vector3 _pivotWorldPoint;

        [Header("DOTween Settings")]
        [SerializeField] private Ease _easeType = Ease.InOutQuad;
        [SerializeField] private LoopType _loopType = LoopType.Restart;
        [SerializeField] private int _loops = 1;
        [SerializeField] private bool _resetOnComplete = false;

        private Quaternion _startLocalRotation;
        private Vector3 _startLocalPosition;
        private Tweener _rotateTween;

        private void Awake()
        {
            _startLocalRotation = transform.localRotation;
            _startLocalPosition = transform.localPosition;
        }

        private void Start()
        {
            if (_triggerOnStart)
                TriggerRotation();
        }

        private void OnDestroy()
        {
            _rotateTween?.Kill();
        }

        public void TriggerRotation()
        {
            DOTween.Init();
            _rotateTween?.Kill();

            transform.localRotation = _startLocalRotation;
            transform.localPosition = _startLocalPosition;

            Vector3 axisLocalOrWorld = GetAxisNormalized();
            float signedDegrees = _clockwise ? -_degrees : _degrees;
            float degPerSec = signedDegrees / _duration;

            if (_pivotMode == PivotMode.Self)
            {
                _rotateTween = DOVirtual.Float(0f, 1f, _duration, _ =>
                    {
                        float dt = Time.deltaTime;
                        Vector3 axis = AxisInWorldIfNeeded(axisLocalOrWorld);
                        if (_axisSpace == Space.Self)
                            transform.Rotate(axisLocalOrWorld, degPerSec * dt, Space.Self);
                        else
                            transform.Rotate(axis, degPerSec * dt, Space.World);
                    })
                    .SetEase(_easeType)
                    .SetLoops(_loops, _loopType)
                    .SetDelay(_delay)
                    .SetAutoKill(true);

                if (_resetOnComplete && _loops != -1)
                    _rotateTween.OnComplete(ResetTransform);

                return;
            }

            Vector3 pivot = GetPivotWorldPoint();
            _rotateTween = DOVirtual.Float(0f, 1f, _duration, _ =>
                {
                    float dt = Time.deltaTime;
                    Vector3 axis = AxisInWorldIfNeeded(axisLocalOrWorld);
                    transform.RotateAround(pivot, axis, degPerSec * dt);
                })
                .SetEase(_easeType)
                .SetLoops(_loops, _loopType)
                .SetDelay(_delay)
                .SetAutoKill(true);

            if (_resetOnComplete && _loops != -1)
                _rotateTween.OnComplete(ResetTransform);
        }

        public void StopRotation() => _rotateTween?.Kill();
        public void PauseRotation() => _rotateTween?.Pause();
        public void ResumeRotation() => _rotateTween?.Play();

        public void ResetTransform()
        {
            _rotateTween?.Kill();
            transform.localRotation = _startLocalRotation;
            transform.localPosition = _startLocalPosition;
        }

        private Vector3 GetAxisNormalized()
        {
            Vector3 axis = _axisMode switch
            {
                AxisMode.X => Vector3.right,
                AxisMode.Y => Vector3.up,
                AxisMode.Z => Vector3.forward,
                AxisMode.Custom => _customAxis,
                _ => Vector3.up
            };

            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.up;
            return axis.normalized;
        }

        private Vector3 AxisInWorldIfNeeded(Vector3 axis)
        {
            return _axisSpace == Space.Self ? transform.TransformDirection(axis).normalized : axis.normalized;
        }

        private Vector3 GetPivotWorldPoint()
        {
            return _pivotMode switch
            {
                PivotMode.AroundPivotTransform => _pivotTransform != null ? _pivotTransform.position : transform.position,
                PivotMode.AroundPoint => _pivotWorldPoint,
                _ => transform.position
            };
        }
    }
}
