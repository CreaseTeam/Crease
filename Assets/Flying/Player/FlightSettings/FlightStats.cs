using System.Collections.Generic;
using Crease.Flying.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player.FlightSettings
{
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
        private KinematicBody _body;

        public FlightSettings CurrentStats => _currentStats;
        public float ScaledFixedDeltaTime => Time.fixedDeltaTime * CurrentStats.SimulationSpeed;

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
            _body = GetComponent<KinematicBody>();

            EnsureCurrentStatsInstance();
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
            EnsureCurrentStatsInstance();
            RecalculateStats();
        }

        private void OnValidate()
        {
            EnsureCurrentStatsInstance();
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

            FlightStatAccessor.ComputeDelta(targetSettings, _currentStats, mod);

            AddModifier(mod);
            return mod;
        }

        public FlightSettings AddModifierToValue(FlightStatType statType, float modifierValue)
        {
            FlightSettings mod = ScriptableObject.CreateInstance<FlightSettings>();
            mod.name = $"Modifier_{statType}_{modifierValue}";
            mod.hideFlags = HideFlags.DontSave;
            FlightStatAccessor.SetAllZero(mod);
            FlightStatAccessor.Set(mod, statType, modifierValue);

            AddModifier(mod);
            return mod;
        }

        public FlightSettings SetSpecificValue(FlightStatType statType, float targetValue)
        {
            float currentValue = FlightStatAccessor.Get(_currentStats, statType);
            float delta = targetValue - currentValue;
            return AddModifierToValue(statType, delta);
        }

        private void EnsureCurrentStatsInstance()
        {
            if (_currentStats != null) return;

            _currentStats = ScriptableObject.CreateInstance<FlightSettings>();
            _currentStats.name = "CurrentStats_Runtime";
            _currentStats.hideFlags = HideFlags.DontSave;
        }

        private void RecalculateStats()
        {
            if (_baseSettings == null || _currentStats == null) return;

            FlightStatAccessor.CopyFrom(_baseSettings, _currentStats);

            foreach (FlightSettings mod in _activeModifiers)
            {
                if (mod == null) continue;
                FlightStatAccessor.AddInto(mod, _currentStats);
            }

            _currentStats.SimulationSpeed = Mathf.Clamp(_currentStats.SimulationSpeed, 0.01f, 5f);

            ApplyMassToBody();
        }

        private void ApplyMassToBody()
        {
            if (_body == null || _currentStats == null) return;

            _body.Mass = _currentStats.Mass;
        }
    }
}
