using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Health : MonoBehaviour
{
    public float MaxHealth = 100f;

    public float CurrentHealth { get; private set; }
    public float NormalizedDamage(float amount) => amount / MaxHealth;

    [Serializable]
    private class DamageRecord
    {
        public DamageType type;
        public float amount;
    }

    private readonly List<DamageRecord> _damageLog = new();

    void Start() => CurrentHealth = MaxHealth;

    public void TakeDamage(float amount, DamageType type)
    {
        amount = Mathf.Min(amount, CurrentHealth);
        if (amount <= 0f) return;

        CurrentHealth -= amount;

        // Either append to existing or add to end of list to preserve accrual order
        int index = _damageLog.FindIndex(r => r.type == type);
        DamageRecord record = null;
        if (index >= 0)
        {
            record = _damageLog[index];
            // If the record existed but was fully healed and remained with zero
            // amount for some reason, remove it so a new accrual places this
            // damage at the end of the log (preserve chronological order).
            if (record.amount <= 0f)
            {
                _damageLog.RemoveAt(index);
                record = null;
            }
        }

        if (record != null)
        {
            record.amount += amount;
        }
        else
        {
            record = new DamageRecord { type = type, amount = amount };
            _damageLog.Add(record);
        }

        if (HUDCanvas.Instance != null)
        {
            HUDCanvas.Instance.VisualDamage(type, record.amount / MaxHealth);
        }
    }

    public void Heal(float amount, DamageType? targetType = null)
    {
        if (amount <= 0f) return;
        
        float remainingHeal = amount;

        if (targetType.HasValue)
        {
            DamageType target = targetType.Value;
            int index = _damageLog.FindIndex(r => r.type == target);
            if (index >= 0)
            {
                DamageRecord record = _damageLog[index];
                float healAmount = Mathf.Min(record.amount, remainingHeal);
                record.amount -= healAmount;
                CurrentHealth += healAmount;
                
                if (HUDCanvas.Instance != null)
                {
                    HUDCanvas.Instance.VisualHeal(record.amount / MaxHealth, record.type);
                }

                if (record.amount <= 0f)
                {
                    _damageLog.RemoveAt(index);
                }
            }
        }
        else
        {
            // Iterate forwards: "first chunk of damage... oldest first" -> Index 0 forwards
            for (int i = 0; i < _damageLog.Count && remainingHeal > 0;)
            {
                DamageRecord record = _damageLog[i];
                float healAmount = Mathf.Min(record.amount, remainingHeal);

                record.amount -= healAmount;
                CurrentHealth += healAmount;
                remainingHeal -= healAmount;

                if (HUDCanvas.Instance != null)
                {
                    HUDCanvas.Instance.VisualHeal(record.amount / MaxHealth, record.type);
                }

                if (record.amount <= 0f)
                {
                    _damageLog.RemoveAt(i);
                    // Do not increment 'i' because the list just shifted left
                }
                else
                {
                    i++; // Only move to the next if we didn't destroy this one
                }
            }
        }
    }
}
