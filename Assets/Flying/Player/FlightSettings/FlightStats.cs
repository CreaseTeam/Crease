using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player
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

        private readonly List<FlightStatModifier> _activeModifiers = new List<FlightStatModifier>();
        private FlightSettings _initialBaseSettings;
        private KinematicBody _body;
        private bool _isDirty;

        public FlightSettings CurrentStats
        {
            get
            {
                FlushIfDirty();
                return _currentStats;
            }
        }

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

        internal void MarkDirty()
        {
            _isDirty = true;
        }

        internal void FlushIfDirty()
        {
            if (!_isDirty)
                return;

            _isDirty = false;
            RecalculateStats();
        }

        public void SetBaseSettings(FlightSettings newSettings)
        {
            _baseSettings = newSettings;
            MarkDirty();
            FlushIfDirty();
        }

        public void RevertToBaseStats()
        {
            _baseSettings = _initialBaseSettings;
            ClearAllModifications();
        }

        public void ClearAllModifications()
        {
            foreach (FlightStatModifier modifier in _activeModifiers)
                modifier.RevokeInternal();

            _activeModifiers.Clear();
            MarkDirty();
            FlushIfDirty();
        }

        public void RemoveModifier(FlightStatModifier modifier)
        {
            if (modifier == null || modifier.IsRevoked || !_activeModifiers.Contains(modifier))
                return;

            _activeModifiers.Remove(modifier);
            modifier.RevokeInternal();
            MarkDirty();
            FlushIfDirty();
        }

        public FlightStatModifier ApplySettingsAsModifier(FlightSettings settingsAsset)
        {
            if (settingsAsset == null)
                return null;

            FlightSettings mod = ScriptableObject.Instantiate(settingsAsset);
            mod.name = $"Modifier_From_{settingsAsset.name}";
            mod.hideFlags = HideFlags.DontSave;

            FlightStatModifier handle = RegisterModifier(mod);
            FlushIfDirty();
            return handle;
        }

        public FlightStatModifier MatchSettings(FlightSettings targetSettings)
        {
            if (targetSettings == null)
                return null;

            FlushIfDirty();

            FlightSettings mod = ScriptableObject.CreateInstance<FlightSettings>();
            mod.name = $"Modifier_Match_{targetSettings.name}";
            mod.hideFlags = HideFlags.DontSave;

            FlightStatAccessor.ComputeDelta(targetSettings, _currentStats, mod);

            FlightStatModifier handle = RegisterModifier(mod);
            FlushIfDirty();
            return handle;
        }

        public FlightStatModifier AddModifierToValue(FlightStatType statType, float modifierValue)
        {
            FlightSettings mod = ScriptableObject.CreateInstance<FlightSettings>();
            mod.name = $"Modifier_{statType}";
            mod.hideFlags = HideFlags.DontSave;
            FlightStatAccessor.SetAllZero(mod);
            FlightStatAccessor.Set(mod, statType, modifierValue);

            FlightStatModifier handle = RegisterModifier(mod);
            FlushIfDirty();
            return handle;
        }

        public FlightStatModifier SetSpecificValue(FlightStatType statType, float targetValue)
        {
            FlushIfDirty();

            float currentValue = FlightStatAccessor.Get(_currentStats, statType);
            float delta = targetValue - currentValue;
            return AddModifierToValue(statType, delta);
        }

        private FlightStatModifier RegisterModifier(FlightSettings settings)
        {
            FlightStatModifier handle = new FlightStatModifier(this, settings);
            _activeModifiers.Add(handle);
            MarkDirty();
            return handle;
        }

        private void EnsureCurrentStatsInstance()
        {
            if (_currentStats != null)
                return;

            _currentStats = ScriptableObject.CreateInstance<FlightSettings>();
            _currentStats.name = "CurrentStats_Runtime";
            _currentStats.hideFlags = HideFlags.DontSave;
        }

        private void RecalculateStats()
        {
            if (_baseSettings == null || _currentStats == null)
                return;

            FlightStatAccessor.CopyFrom(_baseSettings, _currentStats);

            foreach (FlightStatModifier modifier in _activeModifiers)
            {
                FlightSettings mod = modifier.Settings;
                if (mod == null)
                    continue;

                FlightStatAccessor.AddInto(mod, _currentStats);
            }

            ApplyMassToBody();
        }

        private void ApplyMassToBody()
        {
            if (_body == null || _currentStats == null)
                return;

            _body.Mass = _currentStats.Mass;
        }
    }
}
