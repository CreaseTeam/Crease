using Crease.Flying.Player.Abilities;
using Crease.Flying.Player.FlightModifiers;
using UnityEngine;

namespace Crease.Flying.Player.Dash
{
    [CreateAssetMenu(fileName = "MultiDashAbility", menuName = "Crease/Abilities/Multi Dash")]
    public class MultiDashAbility : Ability
    {
        public float BoostStrength = 50f;
        public float DashDuration = 0.5f;
        public float SpinSpeed = 2.0f;
        public float InvincibilityDuration = 0.6f;
        public float TrailTime = 0.5f;

        [Header("Recharge")]
        public DashRechargeMode RechargeMode = DashRechargeMode.Slipstream;
        public float RechargeRate = 20f;
        public float RechargeMax = 100f;

        [Header("Multi Dash")]
        [Min(1)]
        public int DashCharges = 3;

        internal override Runtime Begin(AbilityController controller) => new MultiDashRuntime(controller, this);

        private float CostPerDash => RechargeMax / Mathf.Max(1, DashCharges);

        private class MultiDashRuntime : Runtime
        {
            private readonly MultiDashAbility _def;
            private MeshRenderer _proximityIndicatorRenderer;
            private float _dashTimer;
            private float _trailTimer;
            private float _recharge;
            private Vector3 _dashDirection;
            private float _dashSpeed;

            public override bool IsActive => _dashTimer > 0f;
            public override bool CanActivate => !IsActive && _recharge >= _def.CostPerDash;
            public override float RechargeNormalized =>
                _def.RechargeMax > 0f ? _recharge / _def.RechargeMax : 0f;

            public MultiDashRuntime(AbilityController controller, MultiDashAbility def) : base(controller)
            {
                _def = def;
            }

            public override void OnEquipped()
            {
                _recharge = _def.RechargeMax;

                if (C.WingTrail != null)
                    C.WingTrail.SetTrailEnabled(false);

                if (C.RechargeProximityIndicator != null)
                    _proximityIndicatorRenderer = C.RechargeProximityIndicator.GetComponent<MeshRenderer>();

                SetProximityIndicatorVisible(false);
            }

            public override void OnUnequipped()
            {
                _dashTimer = 0f;
                _trailTimer = 0f;

                if (C.AnimationController != null)
                    C.AnimationController.StopBarrelRoll();

                if (C.WingTrail != null)
                    C.WingTrail.SetTrailEnabled(false);

                SetProximityIndicatorVisible(false);
            }

            public override void Tick(float deltaTime)
            {
                if (_dashTimer > 0f)
                    _dashTimer -= deltaTime;

                if (_trailTimer > 0f)
                {
                    _trailTimer -= deltaTime;
                    if (_trailTimer <= 0f && C.WingTrail != null)
                        C.WingTrail.SetTrailEnabled(false);
                }

                bool recharging = _def.RechargeMode == DashRechargeMode.SimpleTimer || C.ProximitySourceCount > 0;
                if (recharging && _recharge < _def.RechargeMax)
                {
                    _recharge += _def.RechargeRate * deltaTime;
                    if (_recharge >= _def.RechargeMax)
                        _recharge = _def.RechargeMax;
                }

                bool showIndicator = _def.RechargeMode == DashRechargeMode.Slipstream && C.ProximitySourceCount > 0;
                SetProximityIndicatorVisible(showIndicator);
            }

            public override void FixedTick(float fixedDeltaTime)
            {
                if (IsActive)
                    C.Body.Velocity = _dashDirection * _dashSpeed;
            }

            public override bool TryActivate()
            {
                if (IsActive || _recharge < _def.CostPerDash)
                    return false;

                _recharge -= _def.CostPerDash;
                _dashTimer = _def.DashDuration;

                if (C.FlightModifiers != null)
                {
                    C.FlightModifiers.ApplyForDuration(FlightModifierType.Invulnerable, this, _def.InvincibilityDuration);
                    C.FlightModifiers.ApplyForDuration(FlightModifierType.LockFlightControl, this, _def.DashDuration);
                }

                if (C.WingTrail != null)
                {
                    C.WingTrail.SetTrailEnabled(true);
                    _trailTimer = _def.TrailTime;
                }

                _dashDirection = C.PlayerTransform.forward;
                float forwardSpeed = Vector3.Dot(C.Body.Velocity, _dashDirection);
                _dashSpeed = Mathf.Max(forwardSpeed, 0f) + _def.BoostStrength;

                if (C.AnimationController != null)
                    C.AnimationController.StartBarrelRoll(_def.DashDuration, _def.SpinSpeed);

                return true;
            }

            public override void Refresh()
            {
                _recharge = _def.RechargeMax;
            }

            private void SetProximityIndicatorVisible(bool visible)
            {
                if (_proximityIndicatorRenderer != null)
                    _proximityIndicatorRenderer.enabled = visible;
            }
        }
    }
}
