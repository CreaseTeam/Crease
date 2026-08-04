using Crease.Flying.Player;
using Crease.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace Crease.Flying.Environment.Collectibles
{
    /// <summary>
    /// Simple, reusable collectible that triggers an event when the player collides with it.
    /// Optionally destroys itself after collection.
    /// Requires a trigger collider on this GameObject.
    /// </summary>
    public class Collectible : MonoBehaviour
    {
        [Header("Collection Settings")]
        [Tooltip("If true, this GameObject will be destroyed after being collected.")]
        [SerializeField] private bool _destroyOnCollect = true;
        [Tooltip("Hide mesh and collider on collect instead of destroying immediately (allows effects to play).")]
        [SerializeField] private bool _hideOnCollect = false;

        [Header("Events")]
        [Tooltip("Event invoked when the player collects this item.")]
        public UnityEvent OnCollected;

        [Header("Effects")]
        [Tooltip("Should the collectible spin around y axis")]
        [SerializeField] private bool _spin = false;
        [Tooltip("Spin speed in degrees per second")]
        [SerializeField] private float _spinSpeed = 90f;
        [Tooltip("Particle system to play when the item is collected.")]
        [SerializeField] private ParticleSystem _collectEffect;
        [Tooltip("Should the collectible be attracted to the player?")]
        [SerializeField] private bool _magnetize = true;

        private bool _hasBeenCollected;
        private Tween _spinTween;
        private MeshRenderer _meshRenderer;
        private Collider _collider;

        private bool _magnetized = false;
        private GameObject _magnetizedTarget;
        private KinematicBody _magnetizedBody;
        private float _magnetizedElapsed;
        private float _magnetizedDuration;
        private AnimationCurve _magnetProgressCurve;
        private MagnetArcSettings _magnetizedArc;

        // Captured once when magnetization begins, so the arc plane stays stable even if the player turns.
        private Vector3 _magnetStart;         // coin position at capture (Bézier start point)
        private Vector3 _magnetLateral;       // horizontal axis pointing to the chosen swing side
        private float _magnetSide;            // +1 / -1 swing direction
        private Vector3 _magnetLastTargetPos; // previous player position, for velocity fallback

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<Collider>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasBeenCollected) return;

            KinematicBody body = other.GetComponent<KinematicBody>();
            if (body == null) return;

            Collect();
        }

        private void Collect()
        {
            if (_hasBeenCollected) return;

            _hasBeenCollected = true;
            _magnetized = false;
            OnCollected?.Invoke();

            if (_collectEffect != null)
            {
                _collectEffect.Play();
            }

            if (_destroyOnCollect)
            {
                Destroy(gameObject);
            }
            else if (_hideOnCollect)
            {
                if (_meshRenderer != null) _meshRenderer.enabled = false;
                if (_collider != null) _collider.enabled = false;
            }
        }

        private void Start()
        {
            if (_spin)
            {
                _spinTween = transform.DOLocalRotate(new Vector3(0, _spinSpeed, 0), 1)
                    .SetRelative()
                    .SetLoops(-1, LoopType.Incremental)
                    .SetEase(Ease.Linear);
            }
        }

        private void OnDestroy()
        {
            _spinTween?.Kill();
        }

        public void IncrementCollectibleCount()
        {
            if (HUDCanvas.Instance != null)
            {
                HUDCanvas.Instance.Collect();
            }
        }

        public void HealPlayer(float amount)
        {
            if (HUDCanvas.Instance != null)
            {
                HUDCanvas.Instance.Heal(amount);
            }
        }

        public void RefreshAbility()
        {
            if (HUDCanvas.Instance != null)
                HUDCanvas.Instance.RefreshAbility();
        }

        public void Magnetize(GameObject player, KinematicBody playerBody, float arcDuration, AnimationCurve progressCurve, MagnetArcSettings arc)
        {
            if (!_magnetize || _magnetized || _hasBeenCollected) return;
            // it would be weird to keep spinning
            _spinTween?.Kill();

            _magnetized = true;
            _magnetizedTarget = player;
            _magnetizedBody = playerBody;
            _magnetizedElapsed = 0f;
            _magnetizedDuration = arcDuration;
            _magnetProgressCurve = progressCurve;
            _magnetizedArc = arc;

            _magnetStart = transform.position;
            _magnetLastTargetPos = player.transform.position;
            CaptureArcFrame(player.transform.position, playerBody, arc);
        }

        /// <summary>
        /// Establishes the stable side/lateral axis for the arc from the player's flight direction at
        /// the moment of capture, so the swing side is chosen reliably and never flips mid-flight.
        /// </summary>
        private void CaptureArcFrame(Vector3 playerPos, KinematicBody playerBody, MagnetArcSettings arc)
        {
            Vector3 forward = playerBody != null ? playerBody.Velocity : Vector3.zero;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f && _magnetizedTarget != null)
            {
                forward = _magnetizedTarget.transform.forward;
                forward.y = 0f;
            }
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            forward.Normalize();

            _magnetLateral = Vector3.Cross(Vector3.up, forward).normalized;

            float sideDot = Vector3.Dot(_magnetStart - playerPos, _magnetLateral);
            float fallbackSide = arc != null && arc.DefaultSide < 0f ? -1f : 1f;
            _magnetSide = Mathf.Abs(sideDot) > 0.01f ? Mathf.Sign(sideDot) : fallbackSide;
        }

        private void Update()
        {
            if (!_magnetized) return;

            if (_magnetizedTarget == null)
            {
                _magnetized = false;
                return;
            }

            float dt = Time.deltaTime;
            _magnetizedElapsed += dt;

            // Progress is derived from elapsed time (framerate-independent), then eased by the curve.
            float normalizedTime = _magnetizedDuration > 0f
                ? Mathf.Clamp01(_magnetizedElapsed / _magnetizedDuration)
                : 1f;
            float progress = normalizedTime >= 1f
                ? 1f
                : (_magnetProgressCurve != null
                    ? Mathf.Clamp01(_magnetProgressCurve.Evaluate(normalizedTime))
                    : normalizedTime);

            Vector3 playerPos = _magnetizedTarget.transform.position;

            // Predicted interception point: lead the player by their velocity so the coin aims where
            // they are going, not where they are — this is what breaks the rear-chasing behavior.
            Vector3 velocity = _magnetizedBody != null
                ? (_magnetizedBody.Frozen
                    ? Vector3.zero
                    : _magnetizedBody.Velocity * _magnetizedBody.SimulationSpeed)
                : (dt > 1e-6f ? (playerPos - _magnetLastTargetPos) / dt : Vector3.zero);

            // Predict where the player will be when the arc completes. Using only a short, fixed lead
            // makes the endpoint move away throughout the arc and leaves the coin trailing behind.
            // Remaining time naturally reaches zero at arrival, so the coin still finishes on the player.
            float remainingTime = Mathf.Max(0f, _magnetizedDuration - _magnetizedElapsed);
            Vector3 end = playerPos + velocity * remainingTime;

            // Quadratic Bézier: fixed captured start, a side/height control point for the parabola,
            // and the moving interception point as the end. The offset lives only in the control
            // point, so it collapses to zero exactly at arrival — no time-based expiry back to homing.
            Vector3 control = (_magnetStart + end) * 0.5f;
            if (_magnetizedArc != null)
                // A quadratic Bézier applies half of its control-point offset at the midpoint.
                control += _magnetLateral * (_magnetSide * _magnetizedArc.LateralDistance * 2f)
                           + Vector3.up * (_magnetizedArc.VerticalOffset * 2f);

            transform.position = EvaluateQuadraticBezier(_magnetStart, control, end, progress);
            _magnetLastTargetPos = playerPos;

            // Transform-driven trigger movement can cross the player's collider between physics
            // steps. Completing the arc at the player's position is sufficient proof of collection.
            if (progress >= 1f && _magnetizedBody != null)
                Collect();
        }

        private static Vector3 EvaluateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            float u = 1f - t;
            return (u * u) * start + (2f * u * t) * control + (t * t) * end;
        }
    }
}
