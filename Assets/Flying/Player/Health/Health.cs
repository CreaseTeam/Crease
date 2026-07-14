using System;
using System.Collections.Generic;
using Crease.Flying.Player;
using Crease.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Crease.Flying.Player.Health
{
    public class Health : MonoBehaviour
    {
        public static event Action<float, DamageType> OnDamageTaken;
        public static event Action<float, DamageType> OnDamageHealed;

        public float MaxHealth = 100f;

        public float CurrentHealth { get; private set; }
        public float NormalizedDamage(float amount) => amount / MaxHealth;

        [Serializable]
        private class DamageRecord
        {
            public DamageType Type;
            public float Amount;
        }

        private readonly List<DamageRecord> _damageLog = new();
        private readonly int[] _damageDecalCountByType = new int[5];

        void Start() => CurrentHealth = MaxHealth;

        public void TakeDamage(float amount, DamageType type)
        {
            if (FlightStats.Instance != null && FlightStats.Instance.CurrentStats != null)
                amount *= FlightStats.Instance.CurrentStats.DamageTaken;

            amount = Mathf.Min(amount, CurrentHealth);
            if (amount <= 0f) return;

            CurrentHealth -= amount;

            int index = _damageLog.FindIndex(r => r.Type == type);
            DamageRecord record = null;
            if (index >= 0)
            {
                record = _damageLog[index];
                if (record.Amount <= 0f)
                {
                    _damageLog.RemoveAt(index);
                    record = null;
                }
            }

            if (record != null)
            {
                record.Amount += amount;
            }
            else
            {
                record = new DamageRecord { Type = type, Amount = amount };
                _damageLog.Add(record);
            }

            if (HUDCanvas.Instance != null)
            {
                HUDCanvas.Instance.VisualDamage(type, record.Amount / MaxHealth);
            }

            OnDamageTaken?.Invoke(amount, type);
        }

        public int GetDamageDecalCount(DamageType type)
        {
            int typeIndex = (int)type;
            if (typeIndex < 0 || typeIndex >= _damageDecalCountByType.Length)
                return 0;

            return _damageDecalCountByType[typeIndex];
        }

        public void RegisterDamageDecal(DamageType type)
        {
            int typeIndex = (int)type;
            if (typeIndex < 0 || typeIndex >= _damageDecalCountByType.Length)
                return;

            _damageDecalCountByType[typeIndex]++;
        }

        public void UnregisterDamageDecal(DamageType type)
        {
            int typeIndex = (int)type;
            if (typeIndex < 0 || typeIndex >= _damageDecalCountByType.Length)
                return;

            if (_damageDecalCountByType[typeIndex] > 0)
                _damageDecalCountByType[typeIndex]--;
        }

        public void ClearDamageDecalTracking()
        {
            for (int i = 0; i < _damageDecalCountByType.Length; i++)
                _damageDecalCountByType[i] = 0;
        }

        private void NotifyDamageHealed(float healAmount, DamageType type)
        {
            if (healAmount <= 0f)
                return;

            OnDamageHealed?.Invoke(healAmount, type);
        }

        public void Heal(float amount, DamageType? targetType = null)
        {
            if (amount <= 0f) return;

            float remainingHeal = amount;

            if (targetType.HasValue)
            {
                DamageType target = targetType.Value;
                int index = _damageLog.FindIndex(r => r.Type == target);
                if (index >= 0)
                {
                    DamageRecord record = _damageLog[index];
                    float healAmount = Mathf.Min(record.Amount, remainingHeal);
                    record.Amount -= healAmount;
                    CurrentHealth += healAmount;

                    if (HUDCanvas.Instance != null)
                    {
                        HUDCanvas.Instance.VisualHeal(record.Amount / MaxHealth, record.Type);
                    }

                    NotifyDamageHealed(healAmount, record.Type);

                    if (record.Amount <= 0f)
                    {
                        _damageLog.RemoveAt(index);
                    }
                }
            }
            else
            {
                for (int i = 0; i < _damageLog.Count && remainingHeal > 0;)
                {
                    DamageRecord record = _damageLog[i];
                    float healAmount = Mathf.Min(record.Amount, remainingHeal);

                    record.Amount -= healAmount;
                    CurrentHealth += healAmount;
                    remainingHeal -= healAmount;

                    if (HUDCanvas.Instance != null)
                    {
                        HUDCanvas.Instance.VisualHeal(record.Amount / MaxHealth, record.Type);
                    }

                    NotifyDamageHealed(healAmount, record.Type);

                    if (record.Amount <= 0f)
                    {
                        _damageLog.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }
    }
}
