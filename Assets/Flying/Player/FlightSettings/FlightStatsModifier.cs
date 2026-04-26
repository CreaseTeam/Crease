using UnityEngine;

/// <summary>
/// Auxiliary script designed to be hooked up to Unity Events in the editor 
/// to easily apply or remove modifiers to the active FlightStats.
/// </summary>
public class FlightStatsModifier : MonoBehaviour
{
    [Header("Single Stat Modifier Settings")]
    public FlightStatType statType;
    public float modifierValue;

    private FlightSettings appliedModifier;

    /// <summary>
    /// Applies the specified modifier value additively to the selected stat type.
    /// Stores a reference to the modifier so it can be removed later.
    /// </summary>
    public void ApplyModifier()
    {
        if (FlightStats.Instance != null)
        {
            // If we already applied a modifier, we might want to remove it first before reapplying
            if (appliedModifier != null)
            {
                FlightStats.Instance.RemoveModifier(appliedModifier);
            }
            
            appliedModifier = FlightStats.Instance.AddModifierToValue(statType, modifierValue);
        }
        else
        {
            Debug.LogWarning("FlightStats.Instance is null. Cannot apply modifier.");
        }
    }

    /// <summary>
    /// Removes the modifier applied by this script, if any.
    /// </summary>
    public void RemoveModifier()
    {
        if (FlightStats.Instance != null && appliedModifier != null)
        {
            FlightStats.Instance.RemoveModifier(appliedModifier);
            appliedModifier = null;
        }
    }

    /// <summary>
    /// Computes the difference required to set the target stat precisely to modifierValue, 
    /// and applies it additively. Stores a reference so it can be removed later.
    /// </summary>
    public void SetSpecificValue()
    {
        if (FlightStats.Instance != null)
        {
            if (appliedModifier != null)
            {
                FlightStats.Instance.RemoveModifier(appliedModifier);
            }

            appliedModifier = FlightStats.Instance.SetSpecificValue(statType, modifierValue);
        }
        else
        {
            Debug.LogWarning("FlightStats.Instance is null. Cannot set specific value.");
        }
    }

    /// <summary>
    /// Applies an entire FlightSettings object as an additive modifier.
    /// Stores a reference so it can be removed later.
    /// </summary>
    public void ApplySettingsModifier(FlightSettings settingsAsset)
    {
        if (FlightStats.Instance != null && settingsAsset != null)
        {
            if (appliedModifier != null)
            {
                FlightStats.Instance.RemoveModifier(appliedModifier);
            }
            appliedModifier = FlightStats.Instance.ApplySettingsAsModifier(settingsAsset);
        }
        else if (settingsAsset == null)
        {
            Debug.LogWarning("Cannot ApplySettingsModifier: provided settingsAsset is null.");
        }
    }

    /// <summary>
    /// Computes the delta required across all stats to make the current FlightStats 
    /// match the targetSettingsAsset perfectly, and applies it additively.
    /// Stores a reference so it can be removed later.
    /// </summary>
    public void MatchTargetSettings(FlightSettings targetSettingsAsset)
    {
        if (FlightStats.Instance != null && targetSettingsAsset != null)
        {
            if (appliedModifier != null)
            {
                FlightStats.Instance.RemoveModifier(appliedModifier);
            }
            appliedModifier = FlightStats.Instance.MatchSettings(targetSettingsAsset);
        }
        else if (targetSettingsAsset == null)
        {
            Debug.LogWarning("Cannot MatchTargetSettings: provided targetSettingsAsset is null.");
        }
    }

    /// <summary>
    /// Directly sets the active base settings of the FlightStats to the targetSettingsAsset.
    /// </summary>
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

    /// <summary>
    /// Clears all modifications across the FlightStats system.
    /// Note: This clears ALL active modifiers, not just the one from this script.
    /// </summary>
    public void ClearAllModifications()
    {
        if (FlightStats.Instance != null)
        {
            FlightStats.Instance.ClearAllModifications();
            appliedModifier = null;
        }
    }

    /// <summary>
    /// Reverts the FlightStats system completely to its original base stats and clears all modifiers.
    /// </summary>
    public void RevertToBaseStats()
    {
        if (FlightStats.Instance != null)
        {
            FlightStats.Instance.RevertToBaseStats();
            appliedModifier = null;
        }
    }
}
