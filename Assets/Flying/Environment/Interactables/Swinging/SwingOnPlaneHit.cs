using System.Collections;
using Crease.Flying.Environment.Interactables;
using Crease.Flying.Player.Abilities;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Crease.Flying.Environment.Interactables.Swinging
{
    public class SwingOnPlaneHit : MonoBehaviour, IPreventKnockback
    {
        [Header("Detection")]
        [SerializeField] private string _planeTag = "Player";

        [Tooltip("If true, only activates while the player's ability is active.")]
        [SerializeField] private bool _onlyWhileAbilityActive;

        [Tooltip("If true, prevents knockback when the swing is triggered.")]
        [SerializeField] private bool _preventKnockback = true;

        [Header("Rotation")]
        [SerializeField] private Transform _pivot;

        [Tooltip("Rotation offset from starting rotation")]
        [SerializeField] private Vector3 _endLocalEuler = new Vector3(0f, 90f, 0f);

        [SerializeField] private float _swingTime = 0.2f;

        [Tooltip("If true, it only triggers once.")]
        [SerializeField] private bool _oneShot = true;

        [Header("Events")]
        [FormerlySerializedAs("onDoorOpened")]
        public UnityEvent OnDoorOpened;

        [Header("Gizmos")]
        [SerializeField] private bool _showGizmos = true;
        [SerializeField] private Color _startColor = new Color(0f, 1f, 0f, 0.5f);
        [SerializeField] private Color _endColor = new Color(1f, 0f, 0f, 0.5f);
        [SerializeField] private Color _swingPathColor = new Color(1f, 1f, 0f, 0.3f);
        [SerializeField] private int _arcSegments = 20;
        [SerializeField] private float _gizmoSize = 5f;

        private bool _triggered;
        private Coroutine _swingRoutine;

        private Quaternion _initialRot;
        private Quaternion _endRot;

        private void Reset()
        {
            _pivot = transform;
        }

        private void Awake()
        {
            if (_pivot == null) _pivot = transform;

            _initialRot = _pivot.localRotation;
            _endRot = _initialRot * Quaternion.Euler(_endLocalEuler);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_oneShot && _triggered) return;

            if (!string.IsNullOrEmpty(_planeTag) && !other.CompareTag(_planeTag))
                return;

            if (_onlyWhileAbilityActive)
            {
                AbilityController abilityController = other.GetComponentInParent<AbilityController>();
                if (abilityController == null || !abilityController.IsActive)
                    return;
            }

            Trigger();
        }

        public void Trigger()
        {
            if (_oneShot && _triggered) return;
            _triggered = true;

            OnDoorOpened?.Invoke();

            if (_swingRoutine != null)
                StopCoroutine(_swingRoutine);

            if (_swingTime <= 0f)
            {
                _pivot.localRotation = _endRot;
            }
            else
            {
                _swingRoutine = StartCoroutine(SwingTo(_endRot, _swingTime));
            }
        }

        public void ResetToStart()
        {
            _triggered = false;

            if (_swingRoutine != null)
                StopCoroutine(_swingRoutine);

            _pivot.localRotation = _initialRot;
        }

        private IEnumerator SwingTo(Quaternion targetRot, float duration)
        {
            Quaternion startRot = _pivot.localRotation;
            Quaternion endRot = targetRot;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float alpha = Mathf.Clamp01(t / duration);
                _pivot.localRotation = Quaternion.Slerp(startRot, endRot, alpha);
                yield return null;
            }

            _pivot.localRotation = endRot;
            _swingRoutine = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showGizmos) return;

            Transform gizmoPivot = _pivot != null ? _pivot : transform;

            Quaternion baseRot = Application.isPlaying ? _initialRot : gizmoPivot.localRotation;
            Quaternion gizmoStartRot = baseRot;
            Quaternion gizmoEndRot = baseRot * Quaternion.Euler(_endLocalEuler);

            Vector3 pivotPos = gizmoPivot.position;

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(pivotPos, 0.1f * _gizmoSize);

            Vector3 startDir = gizmoPivot.parent != null
                ? gizmoPivot.parent.rotation * gizmoStartRot * Vector3.forward
                : gizmoStartRot * Vector3.forward;
            Gizmos.color = _startColor;
            Gizmos.DrawLine(pivotPos, pivotPos + startDir * _gizmoSize);
            Gizmos.DrawWireSphere(pivotPos + startDir * _gizmoSize, 0.15f * _gizmoSize);

            Vector3 endDir = gizmoPivot.parent != null
                ? gizmoPivot.parent.rotation * gizmoEndRot * Vector3.forward
                : gizmoEndRot * Vector3.forward;
            Gizmos.color = _endColor;
            Gizmos.DrawLine(pivotPos, pivotPos + endDir * _gizmoSize);
            Gizmos.DrawWireSphere(pivotPos + endDir * _gizmoSize, 0.15f * _gizmoSize);

            DrawSwingArc(pivotPos, gizmoPivot, gizmoStartRot, gizmoEndRot);

            Vector3 rotationAxis = Vector3.Cross(startDir, endDir).normalized;
            if (rotationAxis.sqrMagnitude > 0.01f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(pivotPos - rotationAxis * 0.3f * _gizmoSize,
                               pivotPos + rotationAxis * 0.3f * _gizmoSize);
            }
        }

        private void DrawSwingArc(Vector3 pivotPos, Transform gizmoPivot, Quaternion startRot, Quaternion endRot)
        {
            Gizmos.color = _swingPathColor;

            Vector3 previousPoint = Vector3.zero;
            for (int i = 0; i <= _arcSegments; i++)
            {
                float t = i / (float)_arcSegments;
                Quaternion currentRot = Quaternion.Slerp(startRot, endRot, t);

                Vector3 direction = gizmoPivot.parent != null
                    ? gizmoPivot.parent.rotation * currentRot * Vector3.forward
                    : currentRot * Vector3.forward;

                Vector3 currentPoint = pivotPos + direction * _gizmoSize;

                if (i > 0)
                {
                    Gizmos.DrawLine(previousPoint, currentPoint);
                }

                previousPoint = currentPoint;
            }
        }

        public bool ShouldPreventKnockback(Collider playerCollider)
        {
            if (!_preventKnockback) return false;
            if (_oneShot && _triggered) return false;
            if (!string.IsNullOrEmpty(_planeTag) && !playerCollider.CompareTag(_planeTag)) return false;

            if (_onlyWhileAbilityActive)
            {
                AbilityController abilityController = playerCollider.GetComponentInParent<AbilityController>();
                if (abilityController == null || !abilityController.IsActive)
                    return false;
            }

            return true;
        }
    }
}
