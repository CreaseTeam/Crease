using Crease.Flying.Player;
using Crease.Flying.Player.Abilities;
using Crease.Folding.Paper;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player.Loadouts
{
    [CreateAssetMenu(fileName = "PlaneLoadout", menuName = "Crease/Plane Loadout")]
    public class PlaneLoadout : ScriptableObject
    {
        [Header("Gameplay")]
        public FoldInstruction FoldInstruction;
        public FlightSettings FlightSettings;
        [FormerlySerializedAs("Ability")]
        public Ability PrimaryAbility;
        public Ability SecondaryAbility;

        [Header("UI")]
        public string DisplayName;
        public Sprite Image;
        [TextArea]
        public string Description;
        public int Speed;
        public int Control;
        public int Durability;
        public int WindResistance;
        [TextArea]
        public string AbilityDescription;
    }
}
