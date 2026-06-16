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

        private bool _hasBeenCollected;
        private Tween _spinTween;
        private MeshRenderer _meshRenderer;
        private Collider _collider;

        private bool _magnetized = false;
        private GameObject _magnetizedTarget;
        private float _magnetizedSpeed;

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

            _hasBeenCollected = true;
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

        public void RefreshDash()
        {
            if (HUDCanvas.Instance != null)
            {
                HUDCanvas.Instance.RefreshDash();
            }
        }

        public void Magnetize(GameObject player, float speed)
        {
            // it would be weird to keep spinning
            _spinTween?.Kill();

            _magnetized = true;
            _magnetizedTarget = player;
            _magnetizedSpeed = speed;
        }

        private void Update()
        {
            if (_magnetized)
            {

                float distance = Vector3.Distance(
                    transform.position,
                    _magnetizedTarget.transform.position
                );

                // ease-in
                float speed = Mathf.Lerp(1f, _magnetizedSpeed, distance / 10f);

                // trail plane
                Vector3 targetPos =
                    _magnetizedTarget.transform.position -
                    _magnetizedTarget.transform.forward * 1.5f;

                transform.position = Vector3.Lerp(
                    transform.position,
                    targetPos,
                    speed * Time.fixedDeltaTime);
            }
        }
    }
}
