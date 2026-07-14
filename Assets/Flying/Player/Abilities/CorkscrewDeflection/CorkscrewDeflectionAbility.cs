using Crease.Flying.Player;
using Crease.Flying.Player.Abilities;
using UnityEngine;

namespace Crease.Flying.Player.CorkscrewDeflection
{
    [CreateAssetMenu(fileName = "CorkscrewDeflectionAbility", menuName = "Crease/Abilities/Corkscrew Deflection")]
    public class CorkscrewDeflectionAbility : Ability
    {
        [Header("Corkscrew Deflection")]
        [Tooltip("How long the aileron roll and damage reduction last.")]
        public float Duration = 1.5f;

        [Tooltip("Multiplier on the default aileron roll spin rate.")]
        public float SpinSpeed = 2f;

        [Tooltip("Amount subtracted from DamageTaken while active. 0.5 on a base of 1 halves damage taken.")]
        public float DamageTakenReduction = 0.5f;

        [Header("Recharge")]
        public float RechargeRate = 10f;
        public float RechargeMax = 100f;

        internal override Runtime Begin(AbilityController controller) => new CorkscrewDeflectionRuntime(controller, this);

        private class CorkscrewDeflectionRuntime : Runtime
        {
            private readonly CorkscrewDeflectionAbility _def;
            private FlightStatModifier _damageTakenModifier;
            private float _activeTimer;
            private float _recharge;
            private bool _canUse = true;

            public override bool IsActive => _activeTimer > 0f;
            public override bool CanActivate => _canUse;
            public override float RechargeNormalized =>
                _def.RechargeMax > 0f ? _recharge / _def.RechargeMax : 0f;

            public CorkscrewDeflectionRuntime(AbilityController controller, CorkscrewDeflectionAbility def) : base(controller)
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
                EndActive();
            }

            public override void Tick(float deltaTime)
            {
                if (_activeTimer > 0f)
                {
                    _activeTimer -= deltaTime;
                    if (_activeTimer <= 0f)
                        EndActive();
                }

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

                if (C.AnimationController != null)
                    C.AnimationController.StartAileronRoll(_def.Duration, _def.SpinSpeed);

                ApplyDamageReduction();

                return true;
            }

            public override void Refresh()
            {
                _canUse = true;
                _recharge = _def.RechargeMax;
            }

            private void ApplyDamageReduction()
            {
                if (C.FlightStats == null || _def.DamageTakenReduction <= 0f)
                    return;

                _damageTakenModifier = C.FlightStats.AddModifierToValue(
                    FlightStatType.DamageTaken,
                    -_def.DamageTakenReduction);
            }

            private void RevokeDamageReduction()
            {
                if (_damageTakenModifier == null)
                    return;

                _damageTakenModifier.Revoke();
                _damageTakenModifier = null;
            }

            private void EndActive()
            {
                _activeTimer = 0f;

                if (C.AnimationController != null)
                    C.AnimationController.StopAileronRoll();

                RevokeDamageReduction();
            }
        }
    }
}
