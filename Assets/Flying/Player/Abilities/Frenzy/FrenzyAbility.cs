using Crease.Flying.Player.Abilities;
using UnityEngine;

namespace Crease.Flying.Player.Frenzy
{
    [CreateAssetMenu(fileName = "FrenzyAbility", menuName = "Crease/Abilities/Frenzy")]
    public class FrenzyAbility : Ability
    {
        [Header("Bar")]
        [Tooltip("Rate the frenzy bar fills while input is held (0–1 per second). Reaches max in 1/ChargeRate seconds.")]
        public float ChargeRate = 0.25f;

        [Tooltip("Rate the frenzy bar drains after releasing input (0–1 per second).")]
        public float DrainRate = 0.15f;

        [Header("Speed")]
        [Tooltip("Forward acceleration (units/s²) applied to the plane while the bar is charging.")]
        public float ForwardAcceleration = 20f;

        [Tooltip("Braking acceleration (units/s²) applied while the bar is draining back down.")]
        public float BrakeAcceleration = 10f;

        [Tooltip("Do not apply forward acceleration while speed is at or above this (units/s).")]
        public float MaxVelocity = 50f;

        [Tooltip("Do not apply braking while speed is at or below this (units/s).")]
        public float MinVelocity = 5f;

        [Header("Control Reduction")]
        [Tooltip("Maximum PitchSpeed reduction at full bar. Applied as an additive negative modifier to FlightSettings.PitchSpeed.")]
        public float MaxPitchSpeedReduction = 40f;

        [Tooltip("Maximum YawSpeed reduction at full bar. Applied as an additive negative modifier to FlightSettings.YawSpeed.")]
        public float MaxYawSpeedReduction = 40f;

        [Header("Cooldown")]
        [Tooltip("Seconds before the player can hold the button again after releasing.")]
        public float Cooldown = 2f;

        [Header("Animation")]
        [Tooltip("AileronRoll spin speed multiplier at full bar. Scales linearly from MinSpinSpeed with the bar.")]
        public float MaxSpinSpeed = 3f;

        [Tooltip("Minimum AileronRoll spin speed multiplier so the roll never fully stops while the bar is active.")]
        public float MinSpinSpeed = 0.05f;

        internal override Runtime Begin(AbilityController controller) => new FrenzyRuntime(controller, this);

        private class FrenzyRuntime : Runtime
        {
            private readonly FrenzyAbility _def;

            private float _bar;           // 0–1: current frenzy level
            private float _cooldownTimer; // counts down after releasing input
            private bool _isCharging;     // input held and cooldown expired
            private FlightStatModifier _controlModifier;

            // Bar fills 0→1 while charging, drains 1→0 while not. Show as the ability progress bar.
            public override float RechargeNormalized => _bar;

            // Active whenever frenzy is having any effect (charging or bar still draining).
            public override bool IsActive => _isCharging || _bar > 0f;

            // Ready to charge once the cooldown has expired.
            public override bool CanActivate => _cooldownTimer <= 0f;

            public FrenzyRuntime(AbilityController controller, FrenzyAbility def) : base(controller)
            {
                _def = def;
            }

            public override void OnEquipped()
            {
                _bar = 0f;
                _cooldownTimer = 0f;
                _isCharging = false;

                // Create a persistent modifier for pitch/yaw reduction. Starts at zero effect.
                // SetValue is called each Tick to update both PitchSpeed and YawSpeed deltas.
                if (C.FlightStats != null)
                    _controlModifier = C.FlightStats.AddModifierToValue(FlightStatType.PitchSpeed, 0f);
            }

            public override void OnUnequipped()
            {
                _isCharging = false;
                _bar = 0f;
                _cooldownTimer = 0f;
                _controlModifier?.Revoke();
                _controlModifier = null;
                C.AnimationController?.StopAileronRoll();
            }

            public override void Tick(float deltaTime)
            {
                bool wasCharging = _isCharging;

                if (InputHeld && _cooldownTimer <= 0f)
                {
                    _isCharging = true;
                    _bar = Mathf.Min(1f, _bar + _def.ChargeRate * deltaTime);
                }
                else
                {
                    // Transition: was charging, now released — start cooldown.
                    if (_isCharging && !InputHeld)
                        _cooldownTimer = _def.Cooldown;

                    _isCharging = false;
                    _bar = Mathf.Max(0f, _bar - _def.DrainRate * deltaTime);
                }

                if (_cooldownTimer > 0f)
                    _cooldownTimer = Mathf.Max(0f, _cooldownTimer - deltaTime);

                UpdateControlModifier();
                UpdateAnimation(wasCharging);
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                if (_isCharging)
                {
                    if (C.Body.Speed < _def.MaxVelocity)
                        C.Body.AddAcceleration(C.PlayerTransform.forward * _def.ForwardAcceleration);
                }
                else if (_bar > 0f)
                {
                    if (C.Body.Speed > _def.MinVelocity)
                        C.Body.AddAcceleration(-C.Body.Velocity.normalized * _def.BrakeAcceleration);
                }
            }

            // Clears the cooldown so the player can charge again immediately.
            public override void Refresh() => _cooldownTimer = 0f;

            private void UpdateControlModifier()
            {
                if (_controlModifier == null) return;

                // Negative deltas reduce PitchSpeed and YawSpeed proportional to bar level.
                _controlModifier.SetValue(FlightStatType.PitchSpeed, -_def.MaxPitchSpeedReduction * _bar);
                _controlModifier.SetValue(FlightStatType.YawSpeed,   -_def.MaxYawSpeedReduction  * _bar);
            }

            private void UpdateAnimation(bool wasCharging)
            {
                if (C.AnimationController == null) return;

                float spinSpeed = Mathf.Max(_def.MinSpinSpeed, _def.MaxSpinSpeed * _bar);

                if (_isCharging)
                {
                    if (!wasCharging)
                        C.AnimationController.StartAileronRoll(-1f, spinSpeed);
                    else
                        C.AnimationController.SetAileronRollSpeed(spinSpeed);
                }
                else if (_bar > 0f)
                {
                    if (!C.AnimationController.IsRolling)
                        C.AnimationController.StartAileronRoll(-1f, spinSpeed);
                    else
                        C.AnimationController.SetAileronRollSpeed(spinSpeed);
                }
                else
                {
                    if (wasCharging || C.AnimationController.IsRolling)
                        C.AnimationController.StopAileronRoll();
                }
            }
        }
    }
}
