using UnityEngine;

namespace Crease.Flying.Player
{
    /// <summary>
    /// Runtime handle for an active flight-stat modifier. Use <see cref="SetValue"/> to
    /// change contributed values; stats recalculate when read or after structural changes.
    /// </summary>
    public sealed class FlightStatModifier
    {
        private readonly FlightStats _owner;
        private readonly FlightSettings _settings;
        private bool _revoked;

        internal FlightStatModifier(FlightStats owner, FlightSettings settings)
        {
            _owner = owner;
            _settings = settings;
        }

        internal FlightSettings Settings => _settings;
        internal bool IsRevoked => _revoked;

        public void SetValue(FlightStatType statType, float value)
        {
            if (_revoked || _settings == null)
                return;

            FlightStatAccessor.Set(_settings, statType, value);
            _owner.MarkDirty();
        }

        public float GetValue(FlightStatType statType)
        {
            if (_revoked || _settings == null)
                return 0f;

            return FlightStatAccessor.Get(_settings, statType);
        }

        public void Revoke()
        {
            if (_revoked)
                return;

            _owner.RemoveModifier(this);
        }

        internal void RevokeInternal()
        {
            if (_revoked)
                return;

            _revoked = true;

            if (_settings != null)
                Object.Destroy(_settings);
        }
    }
}
