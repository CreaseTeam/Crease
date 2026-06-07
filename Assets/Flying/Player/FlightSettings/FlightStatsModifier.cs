using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player.FlightSettings
{
    /// <summary>
    /// Auxiliary script designed to be hooked up to Unity Events in the editor
    /// to easily apply or remove modifiers to the active FlightStats.
    /// </summary>
    public class FlightStatsModifier : MonoBehaviour
    {
        [Header("Single Stat Modifier Settings")]
        [FormerlySerializedAs("statType")]
        public FlightStatType StatType;
        [FormerlySerializedAs("modifierValue")]
        public float ModifierValue;

        private FlightSettings _appliedModifier;

        public void ApplyModifier()
        {
            if (FlightStats.Instance != null)
            {
                if (_appliedModifier != null)
                {
                    FlightStats.Instance.RemoveModifier(_appliedModifier);
                }

                _appliedModifier = FlightStats.Instance.AddModifierToValue(StatType, ModifierValue);
            }
            else
            {
                Debug.LogWarning("FlightStats.Instance is null. Cannot apply modifier.");
            }
        }

        public void RemoveModifier()
        {
            if (FlightStats.Instance != null && _appliedModifier != null)
            {
                FlightStats.Instance.RemoveModifier(_appliedModifier);
                _appliedModifier = null;
            }
        }

        public void SetSpecificValue()
        {
            if (FlightStats.Instance != null)
            {
                if (_appliedModifier != null)
                {
                    FlightStats.Instance.RemoveModifier(_appliedModifier);
                }

                _appliedModifier = FlightStats.Instance.SetSpecificValue(StatType, ModifierValue);
            }
            else
            {
                Debug.LogWarning("FlightStats.Instance is null. Cannot set specific value.");
            }
        }

        public void ApplySettingsModifier(FlightSettings settingsAsset)
        {
            if (FlightStats.Instance != null && settingsAsset != null)
            {
                if (_appliedModifier != null)
                {
                    FlightStats.Instance.RemoveModifier(_appliedModifier);
                }
                _appliedModifier = FlightStats.Instance.ApplySettingsAsModifier(settingsAsset);
            }
            else if (settingsAsset == null)
            {
                Debug.LogWarning("Cannot ApplySettingsModifier: provided settingsAsset is null.");
            }
        }

        public void MatchTargetSettings(FlightSettings targetSettingsAsset)
        {
            if (FlightStats.Instance != null && targetSettingsAsset != null)
            {
                if (_appliedModifier != null)
                {
                    FlightStats.Instance.RemoveModifier(_appliedModifier);
                }
                _appliedModifier = FlightStats.Instance.MatchSettings(targetSettingsAsset);
            }
            else if (targetSettingsAsset == null)
            {
                Debug.LogWarning("Cannot MatchTargetSettings: provided targetSettingsAsset is null.");
            }
        }

        public void SetBaseSettings(FlightSettings targetSettingsAsset)
        {
            if (FlightStats.Instance != null && targetSettingsAsset != null)
            {
                FlightStats.Instance.SetBaseSettings(targetSettingsAsset);
            }
            else if (targetSettingsAsset == null)
            {
                Debug.LogWarning("Cannot SetBaseSettings: provided targetSettingsAsset is null.");
            }
        }

        public void ClearAllModifications()
        {
            if (FlightStats.Instance != null)
            {
                FlightStats.Instance.ClearAllModifications();
                _appliedModifier = null;
            }
        }

        public void RevertToBaseStats()
        {
            if (FlightStats.Instance != null)
            {
                FlightStats.Instance.RevertToBaseStats();
                _appliedModifier = null;
            }
        }
    }
}
