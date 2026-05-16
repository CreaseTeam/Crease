using System.Collections.Generic;
using UnityEngine;

public enum FlightStatType
{
    DivingGravity,
    ClimbingGravity,
    Lift,
    DiveRate,
    ClimbRate,
    ClimbEfficiency,
    TurnInterpolation,
    XDrag,
    YDrag,
    ZDrag,
    PitchSpeed,
    MaxPitch,
    YawSpeed,
    RollSpeed,
    RollBackSpeed,
    MaxRoll,
    BoostSpeed,
    InitialSpeed,
    MinimumVelocity
}

public class FlightStats : MonoBehaviour
{
    public static FlightStats Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private FlightSettings baseSettings;
    
    [Header("Active Stats (Read-Only)")]
    [SerializeField, Tooltip("Current active stats including modifiers.")]
    private FlightSettings currentStats;

    private List<FlightSettings> activeModifiers = new List<FlightSettings>();
    private FlightSettings initialBaseSettings;

    public FlightSettings CurrentStats => currentStats;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("Multiple instances of FlightStats found. Destroying duplicate.");
            Destroy(this);
            return;
        }

        initialBaseSettings = baseSettings;

        if (currentStats == null)
        {
            currentStats = ScriptableObject.CreateInstance<FlightSettings>();
            currentStats.name = "CurrentStats_Runtime";
            currentStats.hideFlags = HideFlags.DontSave;
        }

        RecalculateStats();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        if (currentStats == null)
        {
            currentStats = ScriptableObject.CreateInstance<FlightSettings>();
            currentStats.name = "CurrentStats_Runtime";
            currentStats.hideFlags = HideFlags.DontSave;
        }
        RecalculateStats();
    }

    private void OnValidate()
    {
        // OnValidate can run in editor before awake, ensure currentStats is allocated so we can view it
        if (currentStats == null)
        {
            currentStats = ScriptableObject.CreateInstance<FlightSettings>();
            currentStats.name = "CurrentStats_Runtime";
            currentStats.hideFlags = HideFlags.DontSave;
        }
        RecalculateStats();
    }

    public void SetBaseSettings(FlightSettings newSettings)
    {
        baseSettings = newSettings;
        RecalculateStats();
    }

    public void RevertToBaseStats()
    {
        baseSettings = initialBaseSettings;
        ClearAllModifications();
    }

    public void ClearAllModifications()
    {
        // Clean up memory if needed, although they are mostly just runtime scriptable objects
        foreach (var mod in activeModifiers)
        {
            if (mod != null) Destroy(mod);
        }
        activeModifiers.Clear();
        RecalculateStats();
    }

    public void AddModifier(FlightSettings modifier)
    {
        if (modifier != null && !activeModifiers.Contains(modifier))
        {
            activeModifiers.Add(modifier);
            RecalculateStats();
        }
    }

    public void RemoveModifier(FlightSettings modifier)
    {
        if (modifier != null && activeModifiers.Contains(modifier))
        {
            activeModifiers.Remove(modifier);
            RecalculateStats();
        }
    }

    public FlightSettings ApplySettingsAsModifier(FlightSettings settingsAsset)
    {
        if (settingsAsset == null) return null;
        
        FlightSettings mod = ScriptableObject.Instantiate(settingsAsset);
        mod.name = $"Modifier_From_{settingsAsset.name}";
        mod.hideFlags = HideFlags.DontSave;
        
        AddModifier(mod);
        return mod;
    }

    public FlightSettings MatchSettings(FlightSettings targetSettings)
    {
        if (targetSettings == null) return null;

        FlightSettings mod = ScriptableObject.CreateInstance<FlightSettings>();
        mod.name = $"Modifier_Match_{targetSettings.name}";
        mod.hideFlags = HideFlags.DontSave;

        mod.divingGravity = targetSettings.divingGravity - currentStats.divingGravity;
        mod.climbingGravity = targetSettings.climbingGravity - currentStats.climbingGravity;
        mod.lift = targetSettings.lift - currentStats.lift;
        mod.diveRate = targetSettings.diveRate - currentStats.diveRate;
        mod.climbRate = targetSettings.climbRate - currentStats.climbRate;
        mod.climbEfficiency = targetSettings.climbEfficiency - currentStats.climbEfficiency;
        mod.turnInterpolation = targetSettings.turnInterpolation - currentStats.turnInterpolation;
        mod.xDrag = targetSettings.xDrag - currentStats.xDrag;
        mod.yDrag = targetSettings.yDrag - currentStats.yDrag;
        mod.zDrag = targetSettings.zDrag - currentStats.zDrag;
        mod.pitchSpeed = targetSettings.pitchSpeed - currentStats.pitchSpeed;
        mod.maxPitch = targetSettings.maxPitch - currentStats.maxPitch;
        mod.yawSpeed = targetSettings.yawSpeed - currentStats.yawSpeed;
        mod.rollSpeed = targetSettings.rollSpeed - currentStats.rollSpeed;
        mod.rollBackSpeed = targetSettings.rollBackSpeed - currentStats.rollBackSpeed;
        mod.maxRoll = targetSettings.maxRoll - currentStats.maxRoll;
        mod.boostSpeed = targetSettings.boostSpeed - currentStats.boostSpeed;
        mod.initialSpeed = targetSettings.initialSpeed - currentStats.initialSpeed;
        mod.minimumVelocity = targetSettings.minimumVelocity - currentStats.minimumVelocity;

        AddModifier(mod);
        return mod;
    }

    public FlightSettings AddModifierToValue(FlightStatType statType, float modifierValue)
    {
        FlightSettings mod = ScriptableObject.CreateInstance<FlightSettings>();
        mod.name = $"Modifier_{statType}_{modifierValue}";
        mod.hideFlags = HideFlags.DontSave;
        SetZero(mod);

        switch (statType)
        {
            case FlightStatType.DivingGravity: mod.divingGravity = modifierValue; break;
            case FlightStatType.ClimbingGravity: mod.climbingGravity = modifierValue; break;
            case FlightStatType.Lift: mod.lift = modifierValue; break;
            case FlightStatType.DiveRate: mod.diveRate = modifierValue; break;
            case FlightStatType.ClimbRate: mod.climbRate = modifierValue; break;
            case FlightStatType.ClimbEfficiency: mod.climbEfficiency = modifierValue; break;
            case FlightStatType.TurnInterpolation: mod.turnInterpolation = modifierValue; break;
            case FlightStatType.XDrag: mod.xDrag = modifierValue; break;
            case FlightStatType.YDrag: mod.yDrag = modifierValue; break;
            case FlightStatType.ZDrag: mod.zDrag = modifierValue; break;
            case FlightStatType.PitchSpeed: mod.pitchSpeed = modifierValue; break;
            case FlightStatType.MaxPitch: mod.maxPitch = modifierValue; break;
            case FlightStatType.YawSpeed: mod.yawSpeed = modifierValue; break;
            case FlightStatType.RollSpeed: mod.rollSpeed = modifierValue; break;
            case FlightStatType.RollBackSpeed: mod.rollBackSpeed = modifierValue; break;
            case FlightStatType.MaxRoll: mod.maxRoll = modifierValue; break;
            case FlightStatType.BoostSpeed: mod.boostSpeed = modifierValue; break;
            case FlightStatType.InitialSpeed: mod.initialSpeed = modifierValue; break;
            case FlightStatType.MinimumVelocity: mod.minimumVelocity = modifierValue; break;
        }

        AddModifier(mod);
        return mod;
    }

    public FlightSettings SetSpecificValue(FlightStatType statType, float targetValue)
    {
        float currentValue = GetCurrentValue(statType);
        float delta = targetValue - currentValue;
        return AddModifierToValue(statType, delta);
    }

    private void SetZero(FlightSettings mod)
    {
        mod.divingGravity = 0f;
        mod.climbingGravity = 0f;
        mod.lift = 0f;
        mod.diveRate = 0f;
        mod.climbRate = 0f;
        mod.climbEfficiency = 0f;
        mod.turnInterpolation = 0f;
        mod.xDrag = 0f;
        mod.yDrag = 0f;
        mod.zDrag = 0f;
        mod.pitchSpeed = 0f;
        mod.maxPitch = 0f;
        mod.yawSpeed = 0f;
        mod.rollSpeed = 0f;
        mod.rollBackSpeed = 0f;
        mod.maxRoll = 0f;
        mod.boostSpeed = 0f;
        mod.initialSpeed = 0f;
        mod.minimumVelocity = 0f;
    }

    private float GetCurrentValue(FlightStatType statType)
    {
        if (currentStats == null) return 0f;

        switch (statType)
        {
            case FlightStatType.DivingGravity: return currentStats.divingGravity;
            case FlightStatType.ClimbingGravity: return currentStats.climbingGravity;
            case FlightStatType.Lift: return currentStats.lift;
            case FlightStatType.DiveRate: return currentStats.diveRate;
            case FlightStatType.ClimbRate: return currentStats.climbRate;
            case FlightStatType.ClimbEfficiency: return currentStats.climbEfficiency;
            case FlightStatType.TurnInterpolation: return currentStats.turnInterpolation;
            case FlightStatType.XDrag: return currentStats.xDrag;
            case FlightStatType.YDrag: return currentStats.yDrag;
            case FlightStatType.ZDrag: return currentStats.zDrag;
            case FlightStatType.PitchSpeed: return currentStats.pitchSpeed;
            case FlightStatType.MaxPitch: return currentStats.maxPitch;
            case FlightStatType.YawSpeed: return currentStats.yawSpeed;
            case FlightStatType.RollSpeed: return currentStats.rollSpeed;
            case FlightStatType.RollBackSpeed: return currentStats.rollBackSpeed;
            case FlightStatType.MaxRoll: return currentStats.maxRoll;
            case FlightStatType.BoostSpeed: return currentStats.boostSpeed;
            case FlightStatType.InitialSpeed: return currentStats.initialSpeed;
            case FlightStatType.MinimumVelocity: return currentStats.minimumVelocity;
            default: return 0f;
        }
    }

    private void RecalculateStats()
    {
        if (baseSettings == null || currentStats == null) return;

        currentStats.divingGravity = baseSettings.divingGravity;
        currentStats.climbingGravity = baseSettings.climbingGravity;
        currentStats.lift = baseSettings.lift;
        currentStats.diveRate = baseSettings.diveRate;
        currentStats.climbRate = baseSettings.climbRate;
        currentStats.climbEfficiency = baseSettings.climbEfficiency;
        currentStats.turnInterpolation = baseSettings.turnInterpolation;
        currentStats.xDrag = baseSettings.xDrag;
        currentStats.yDrag = baseSettings.yDrag;
        currentStats.zDrag = baseSettings.zDrag;
        currentStats.pitchSpeed = baseSettings.pitchSpeed;
        currentStats.maxPitch = baseSettings.maxPitch;
        currentStats.yawSpeed = baseSettings.yawSpeed;
        currentStats.rollSpeed = baseSettings.rollSpeed;
        currentStats.rollBackSpeed = baseSettings.rollBackSpeed;
        currentStats.maxRoll = baseSettings.maxRoll;
        currentStats.boostSpeed = baseSettings.boostSpeed;
        currentStats.initialSpeed = baseSettings.initialSpeed;
        currentStats.minimumVelocity = baseSettings.minimumVelocity;

        foreach (var mod in activeModifiers)
        {
            if (mod == null) continue;
            
            currentStats.divingGravity += mod.divingGravity;
            currentStats.climbingGravity += mod.climbingGravity;
            currentStats.lift += mod.lift;
            currentStats.diveRate += mod.diveRate;
            currentStats.climbRate += mod.climbRate;
            currentStats.climbEfficiency += mod.climbEfficiency;
            currentStats.turnInterpolation += mod.turnInterpolation;
            currentStats.xDrag += mod.xDrag;
            currentStats.yDrag += mod.yDrag;
            currentStats.zDrag += mod.zDrag;
            currentStats.pitchSpeed += mod.pitchSpeed;
            currentStats.maxPitch += mod.maxPitch;
            currentStats.yawSpeed += mod.yawSpeed;
            currentStats.rollSpeed += mod.rollSpeed;
            currentStats.rollBackSpeed += mod.rollBackSpeed;
            currentStats.maxRoll += mod.maxRoll;
            currentStats.boostSpeed += mod.boostSpeed;
            currentStats.initialSpeed += mod.initialSpeed;
            currentStats.minimumVelocity += mod.minimumVelocity;
        }
    }
}
