using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Simple, reusable collectible that triggers an event when the player collides with it.
/// Optionally destroys itself after collection.
/// Requires a trigger collider on this GameObject.
/// </summary>
public class Collectible : MonoBehaviour
{
    [Header("Collection Settings")]
    [Tooltip("If true, this GameObject will be destroyed after being collected.")]
    [SerializeField] private bool destroyOnCollect = true;
    [Tooltip("Hide mesh and collider on collect instead of destroying immediately (allows effects to play).")]
    [SerializeField] private bool hideOnCollect = false;

    [Header("Events")]
    [Tooltip("Event invoked when the player collects this item.")]
    public UnityEvent OnCollected;

    [Header("Effects")]
    [Tooltip("Should the collectible spin around y axis")]
    [SerializeField] private bool spin = false;
    [Tooltip("Spin speed in degrees per second")]
    [SerializeField] private float spinSpeed = 90f;
    [Tooltip("Particle system to play when the item is collected.")]
    [SerializeField] private ParticleSystem collectEffect;
    
    private bool _hasBeenCollected = false;
    private Tween _spinTween;

    private void OnTriggerEnter(Collider other)
    {
        // Prevent multiple collections
        if (_hasBeenCollected) return;

        // Check if the colliding object is the player (has KinematicBody)
        KinematicBody body = other.GetComponent<KinematicBody>();
        if (body == null) return;

        // Mark as collected and invoke event
        _hasBeenCollected = true;
        OnCollected?.Invoke();

        // Play collection effect if assigned
        if (collectEffect != null)
        {
            collectEffect.Play();
        }

        // Optionally destroy this GameObject
        if (destroyOnCollect)
        {
            Destroy(gameObject);
        }
        else if (hideOnCollect)
        {
            // Hide mesh and collider instead of destroying
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.enabled = false;

            Collider collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }
    }

    private void Start() 
    {
        if (spin) {
            _spinTween = transform.DOLocalRotate(new Vector3(0, spinSpeed, 0), 1).SetRelative().SetLoops(-1, LoopType.Incremental).SetEase(Ease.Linear);
        }
    }

    private void OnDestroy()
    {
        // Clean up DOTween animation to prevent memory leaks
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
}
