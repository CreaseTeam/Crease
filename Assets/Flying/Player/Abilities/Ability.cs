using UnityEngine;

namespace Crease.Flying.Player.Abilities
{
    /// <summary>
    /// Base for ability assets. To add a new ability, copy DashAbility.cs:
    /// tuning fields on the asset, behavior in a private nested Runtime class.
    /// </summary>
    public abstract class Ability : ScriptableObject
    {
        public string DisplayName;
        public Sprite Icon;

        internal abstract Runtime Begin(AbilityController controller);

        internal abstract class Runtime
        {
            protected readonly AbilityController C;

            protected Runtime(AbilityController controller)
            {
                C = controller;
            }

            public virtual void OnEquipped() { }
            public virtual void OnUnequipped() { }
            public virtual void Tick(float deltaTime) { }
            public virtual void FixedTick(float fixedDeltaTime) { }
            public virtual bool TryActivate() => false;
            public virtual void Refresh() { }
            public virtual bool IsActive => false;
            public virtual float RechargeNormalized => 1f;
            public virtual bool CanActivate => true;
        }
    }
}
