using Crease.Flying.Player.Abilities;
using Crease.Flying.Player.FlightModifiers;
using UnityEngine;

namespace Crease.Flying.Player.TimeStop
{
    [CreateAssetMenu(fileName = "TimeStopAbility", menuName = "Crease/Abilities/Time Stop")]
    public class TimeStopAbility : Ability
    {
        [Header("Time Stop")]
        [Tooltip("How long the simulation speed override lasts.")]
        public float Duration = 2f;

        [Tooltip("Simulation speed while active. 0 freezes flight physics, 1 is normal speed.")]
        public float SimulationSpeed = 0f;

        [Header("Recharge")]
        public float RechargeRate = 10f;
        public float RechargeMax = 100f;

        internal override Runtime Begin(AbilityController controller) => new TimeStopRuntime(controller, this);

        private class TimeStopRuntime : Runtime
        {
            private readonly TimeStopAbility _def;
            private float _activeTimer;
            private float _recharge;
            private bool _canUse = true;

            public override bool IsActive => _activeTimer > 0f;
            public override bool CanActivate => _canUse;
            public override float RechargeNormalized =>
                _def.RechargeMax > 0f ? _recharge / _def.RechargeMax : 0f;

            public TimeStopRuntime(AbilityController controller, TimeStopAbility def) : base(controller)
            {
                _def = def;
            }

            public override void OnEquipped()
            {
                _recharge = _def.RechargeMax;
                _canUse = true;
                _activeTimer = 0f;
            }

            public override void OnUnequipped()
            {
                _activeTimer = 0f;
                RevokeSimulationSpeed();
            }

            public override void Tick(float deltaTime)
            {
                if (_activeTimer > 0f)
                    _activeTimer -= deltaTime;

                if (_recharge < _def.RechargeMax)
                {
                    _recharge += _def.RechargeRate * deltaTime;
                    if (_recharge >= _def.RechargeMax)
                    {
                        _recharge = _def.RechargeMax;
                        _canUse = true;
                    }
                }
            }

            public override bool TryActivate()
            {
                if (!_canUse)
                    return false;

                _canUse = false;
                _recharge = 0f;
                _activeTimer = _def.Duration;

                if (C.FlightModifiers != null)
                {
                    C.FlightModifiers.ApplyForDuration(
                        FlightModifierType.SimulationSpeed,
                        this,
                        _def.Duration,
                        _def.SimulationSpeed);
                }

                return true;
            }

            public override void Refresh()
            {
                _canUse = true;
                _recharge = _def.RechargeMax;
            }

            private void RevokeSimulationSpeed()
            {
                if (C.FlightModifiers != null)
                    C.FlightModifiers.Revoke(FlightModifierType.SimulationSpeed, this);
            }
        }
    }
}
