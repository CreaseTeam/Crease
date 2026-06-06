using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player.FlightSettings
{
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
        [FormerlySerializedAs("baseSettings")]
        [SerializeField] private FlightSettings _baseSettings;

        [Header("Active Stats (Read-Only)")]
        [SerializeField, Tooltip("Current active stats including modifiers.")]
        [FormerlySerializedAs("currentStats")]
        private FlightSettings _currentStats;

        private List<FlightSettings> _activeModifiers = new List<FlightSettings>();
        private FlightSettings _initialBaseSettings;

        public FlightSettings CurrentStats => _currentStats;

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

            _initialBaseSettings = _baseSettings;

            if (_currentStats == null)
            {
                _currentStats = ScriptableObject.CreateInstance<FlightSettings>();
                _currentStats.name = "CurrentStats_Runtime";
                _currentStats.hideFlags = HideFlags.DontSave;
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
            if (_currentStats == null)
            {
                _currentStats = ScriptableObject.CreateInstance<FlightSettings>();
                _currentStats.name = "CurrentStats_Runtime";
                _currentStats.hideFlags = HideFlags.DontSave;
            }
            RecalculateStats();
        }

        private void OnValidate()
        {
            if (_currentStats == null)
            {
                _currentStats = ScriptableObject.CreateInstance<FlightSettings>();
                _currentStats.name = "CurrentStats_Runtime";
                _currentStats.hideFlags = HideFlags.DontSave;
            }
            RecalculateStats();
        }

        public void SetBaseSettings(FlightSettings newSettings)
        {
            _baseSettings = newSettings;
            RecalculateStats();
        }

        public void RevertToBaseStats()
        {
            _baseSettings = _initialBaseSettings;
            ClearAllModifications();
        }

        public void ClearAllModifications()
        {
            foreach (var mod in _activeModifiers)
            {
                if (mod != null) Destroy(mod);
            }
            _activeModifiers.Clear();
            RecalculateStats();
        }

        public void AddModifier(FlightSettings modifier)
        {
            if (modifier != null && !_activeModifiers.Contains(modifier))
            {
                _activeModifiers.Add(modifier);
                RecalculateStats();
            }
        }

        public void RemoveModifier(FlightSettings modifier)
        {
            if (modifier != null && _activeModifiers.Contains(modifier))
            {
                _activeModifiers.Remove(modifier);
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

            mod.DivingGravity = targetSettings.DivingGravity - _currentStats.DivingGravity;
            mod.ClimbingGravity = targetSettings.ClimbingGravity - _currentStats.ClimbingGravity;
            mod.Lift = targetSettings.Lift - _currentStats.Lift;
            mod.DiveRate = targetSettings.DiveRate - _currentStats.DiveRate;
            mod.ClimbRate = targetSettings.ClimbRate - _currentStats.ClimbRate;
            mod.ClimbEfficiency = targetSettings.ClimbEfficiency - _currentStats.ClimbEfficiency;
            mod.TurnInterpolation = targetSettings.TurnInterpolation - _currentStats.TurnInterpolation;
            mod.XDrag = targetSettings.XDrag - _currentStats.XDrag;
            mod.YDrag = targetSettings.YDrag - _currentStats.YDrag;
            mod.ZDrag = targetSettings.ZDrag - _currentStats.ZDrag;
            mod.PitchSpeed = targetSettings.PitchSpeed - _currentStats.PitchSpeed;
            mod.MaxPitch = targetSettings.MaxPitch - _currentStats.MaxPitch;
            mod.YawSpeed = targetSettings.YawSpeed - _currentStats.YawSpeed;
            mod.RollSpeed = targetSettings.RollSpeed - _currentStats.RollSpeed;
            mod.RollBackSpeed = targetSettings.RollBackSpeed - _currentStats.RollBackSpeed;
            mod.MaxRoll = targetSettings.MaxRoll - _currentStats.MaxRoll;
            mod.BoostSpeed = targetSettings.BoostSpeed - _currentStats.BoostSpeed;
            mod.InitialSpeed = targetSettings.InitialSpeed - _currentStats.InitialSpeed;
            mod.MinimumVelocity = targetSettings.MinimumVelocity - _currentStats.MinimumVelocity;

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
                case FlightStatType.DivingGravity: mod.DivingGravity = modifierValue; break;
                case FlightStatType.ClimbingGravity: mod.ClimbingGravity = modifierValue; break;
                case FlightStatType.Lift: mod.Lift = modifierValue; break;
                case FlightStatType.DiveRate: mod.DiveRate = modifierValue; break;
                case FlightStatType.ClimbRate: mod.ClimbRate = modifierValue; break;
                case FlightStatType.ClimbEfficiency: mod.ClimbEfficiency = modifierValue; break;
                case FlightStatType.TurnInterpolation: mod.TurnInterpolation = modifierValue; break;
                case FlightStatType.XDrag: mod.XDrag = modifierValue; break;
                case FlightStatType.YDrag: mod.YDrag = modifierValue; break;
                case FlightStatType.ZDrag: mod.ZDrag = modifierValue; break;
                case FlightStatType.PitchSpeed: mod.PitchSpeed = modifierValue; break;
                case FlightStatType.MaxPitch: mod.MaxPitch = modifierValue; break;
                case FlightStatType.YawSpeed: mod.YawSpeed = modifierValue; break;
                case FlightStatType.RollSpeed: mod.RollSpeed = modifierValue; break;
                case FlightStatType.RollBackSpeed: mod.RollBackSpeed = modifierValue; break;
                case FlightStatType.MaxRoll: mod.MaxRoll = modifierValue; break;
                case FlightStatType.BoostSpeed: mod.BoostSpeed = modifierValue; break;
                case FlightStatType.InitialSpeed: mod.InitialSpeed = modifierValue; break;
                case FlightStatType.MinimumVelocity: mod.MinimumVelocity = modifierValue; break;
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
            mod.DivingGravity = 0f;
            mod.ClimbingGravity = 0f;
            mod.Lift = 0f;
            mod.DiveRate = 0f;
            mod.ClimbRate = 0f;
            mod.ClimbEfficiency = 0f;
            mod.TurnInterpolation = 0f;
            mod.XDrag = 0f;
            mod.YDrag = 0f;
            mod.ZDrag = 0f;
            mod.PitchSpeed = 0f;
            mod.MaxPitch = 0f;
            mod.YawSpeed = 0f;
            mod.RollSpeed = 0f;
            mod.RollBackSpeed = 0f;
            mod.MaxRoll = 0f;
            mod.BoostSpeed = 0f;
            mod.InitialSpeed = 0f;
            mod.MinimumVelocity = 0f;
        }

        private float GetCurrentValue(FlightStatType statType)
        {
            if (_currentStats == null) return 0f;

            switch (statType)
            {
                case FlightStatType.DivingGravity: return _currentStats.DivingGravity;
                case FlightStatType.ClimbingGravity: return _currentStats.ClimbingGravity;
                case FlightStatType.Lift: return _currentStats.Lift;
                case FlightStatType.DiveRate: return _currentStats.DiveRate;
                case FlightStatType.ClimbRate: return _currentStats.ClimbRate;
                case FlightStatType.ClimbEfficiency: return _currentStats.ClimbEfficiency;
                case FlightStatType.TurnInterpolation: return _currentStats.TurnInterpolation;
                case FlightStatType.XDrag: return _currentStats.XDrag;
                case FlightStatType.YDrag: return _currentStats.YDrag;
                case FlightStatType.ZDrag: return _currentStats.ZDrag;
                case FlightStatType.PitchSpeed: return _currentStats.PitchSpeed;
                case FlightStatType.MaxPitch: return _currentStats.MaxPitch;
                case FlightStatType.YawSpeed: return _currentStats.YawSpeed;
                case FlightStatType.RollSpeed: return _currentStats.RollSpeed;
                case FlightStatType.RollBackSpeed: return _currentStats.RollBackSpeed;
                case FlightStatType.MaxRoll: return _currentStats.MaxRoll;
                case FlightStatType.BoostSpeed: return _currentStats.BoostSpeed;
                case FlightStatType.InitialSpeed: return _currentStats.InitialSpeed;
                case FlightStatType.MinimumVelocity: return _currentStats.MinimumVelocity;
                default: return 0f;
            }
        }

        private void RecalculateStats()
        {
            if (_baseSettings == null || _currentStats == null) return;

            _currentStats.DivingGravity = _baseSettings.DivingGravity;
            _currentStats.ClimbingGravity = _baseSettings.ClimbingGravity;
            _currentStats.Lift = _baseSettings.Lift;
            _currentStats.DiveRate = _baseSettings.DiveRate;
            _currentStats.ClimbRate = _baseSettings.ClimbRate;
            _currentStats.ClimbEfficiency = _baseSettings.ClimbEfficiency;
            _currentStats.TurnInterpolation = _baseSettings.TurnInterpolation;
            _currentStats.XDrag = _baseSettings.XDrag;
            _currentStats.YDrag = _baseSettings.YDrag;
            _currentStats.ZDrag = _baseSettings.ZDrag;
            _currentStats.PitchSpeed = _baseSettings.PitchSpeed;
            _currentStats.MaxPitch = _baseSettings.MaxPitch;
            _currentStats.YawSpeed = _baseSettings.YawSpeed;
            _currentStats.RollSpeed = _baseSettings.RollSpeed;
            _currentStats.RollBackSpeed = _baseSettings.RollBackSpeed;
            _currentStats.MaxRoll = _baseSettings.MaxRoll;
            _currentStats.BoostSpeed = _baseSettings.BoostSpeed;
            _currentStats.InitialSpeed = _baseSettings.InitialSpeed;
            _currentStats.MinimumVelocity = _baseSettings.MinimumVelocity;

            foreach (var mod in _activeModifiers)
            {
                if (mod == null) continue;

                _currentStats.DivingGravity += mod.DivingGravity;
                _currentStats.ClimbingGravity += mod.ClimbingGravity;
                _currentStats.Lift += mod.Lift;
                _currentStats.DiveRate += mod.DiveRate;
                _currentStats.ClimbRate += mod.ClimbRate;
                _currentStats.ClimbEfficiency += mod.ClimbEfficiency;
                _currentStats.TurnInterpolation += mod.TurnInterpolation;
                _currentStats.XDrag += mod.XDrag;
                _currentStats.YDrag += mod.YDrag;
                _currentStats.ZDrag += mod.ZDrag;
                _currentStats.PitchSpeed += mod.PitchSpeed;
                _currentStats.MaxPitch += mod.MaxPitch;
                _currentStats.YawSpeed += mod.YawSpeed;
                _currentStats.RollSpeed += mod.RollSpeed;
                _currentStats.RollBackSpeed += mod.RollBackSpeed;
                _currentStats.MaxRoll += mod.MaxRoll;
                _currentStats.BoostSpeed += mod.BoostSpeed;
                _currentStats.InitialSpeed += mod.InitialSpeed;
                _currentStats.MinimumVelocity += mod.MinimumVelocity;
            }
        }
    }
}
