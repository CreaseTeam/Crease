using UnityEngine;

namespace Crease.Flying.Player.Abilities
{
    /// <summary>
    /// Empty ability template. Duplicate this asset or copy DashAbility.cs to implement a new ability.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbility", menuName = "Crease/Ability")]
    public class AbilityStub : Ability
    {
        internal override Runtime Begin(AbilityController controller) => new StubRuntime(controller);

        private class StubRuntime : Runtime
        {
            public StubRuntime(AbilityController controller) : base(controller) { }

            public override bool TryActivate()
            {
                Debug.LogWarning("AbilityStub has no behavior. Replace this asset or implement a real ability.", C);
                return false;
            }
        }
    }
}
