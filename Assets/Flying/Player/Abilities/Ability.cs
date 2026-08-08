using UnityEngine;

namespace Crease.Flying.Player.Abilities
{
    /// <summary>
    /// Base for ability assets. To add a new ability, duplicate DashAbility.cs or start from AbilityStub.
    /// </summary>
    public abstract class Ability : ScriptableObject
    {
        public string DisplayName;
        public Sprite Icon;

        internal abstract Runtime Begin(AbilityController controller);

        internal abstract class Runtime
        {
            protected readonly AbilityController C;

            /// <summary>
            /// Set by AbilityController each Update before Tick is called.
            /// True while the input button for this ability's slot is held.
            /// Use this instead of querying InputManager directly so hold abilities work in either slot.
            /// </summary>
            protected bool InputHeld { get; private set; }

            internal void SetInputHeld(bool held) => InputHeld = held;

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
