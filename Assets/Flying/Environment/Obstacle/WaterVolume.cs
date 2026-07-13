using UnityEngine;
using Crease.Flying.Player.Health;

/// <summary>
/// Applies continuous Water damage to the player while they are inside this trigger volume.
/// Requires a Collider on this GameObject — isTrigger is forced on in Awake.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WaterVolume : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Water damage applied per second while the player is submerged.")]
    [SerializeField] private float _damagePerSecond = 15f;

    [Header("Tags")]
    [SerializeField] private string _playerTag = "Player";

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(_playerTag)) return;

        Health health = other.GetComponentInParent<Health>();
        if (health == null) return;

        health.TakeDamage(_damagePerSecond * Time.fixedDeltaTime, DamageType.Water);
    }
}
