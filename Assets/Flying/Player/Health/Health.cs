using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Health : MonoBehaviour
{
    public float MaxHealth = 100f;

    public float CurrentHealth { get; private set; }
    public float NormalizedDamage(float amount) => amount / MaxHealth;

    private readonly Dictionary<DamageType, float> _damageMap = new();

    public event Action<DamageType, float> OnDamaged;

    void Start() => CurrentHealth = MaxHealth;

    public void TakeDamage(float amount, DamageType type)
    {
        amount = Mathf.Min(amount, CurrentHealth);
        Debug.Log($"Health.TakeDamage called: amount={amount}, type={type}, CurrentHealth={CurrentHealth}");

        if (amount <= 0f) return;

        CurrentHealth -= amount;
        _damageMap.TryGetValue(type, out float existing);
        _damageMap[type] = existing + amount;

        float prevHealth = CurrentHealth;
        Debug.Log($"Health applied: prev={prevHealth} -> current={CurrentHealth} (damage={amount})");

        Debug.Log("Invoking OnDamaged event");
        OnDamaged?.Invoke(type, _damageMap[type]);

        float normalizedTotal = _damageMap[type] / MaxHealth;
        if (HUDCanvas.Instance != null)
        {
            Debug.Log($"Notifying HUDCanvas of damage: type={type}, normalizedTotal={normalizedTotal}");
            HUDCanvas.Instance.OnHealthDamaged(type, normalizedTotal);
        }
    }
}
