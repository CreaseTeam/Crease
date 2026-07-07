using Crease.Flying.Player;
using Crease.Flying.Player.Abilities;
using Crease.Folding.PaperGraph;
using UnityEngine;

namespace Crease.Flying.Player.Loadouts
{
    [CreateAssetMenu(fileName = "PlaneLoadout", menuName = "Crease/Plane Loadout")]
    public class PlaneLoadout : ScriptableObject
    {
        [Header("Gameplay")]
        public FoldInstruction FoldInstruction;
        public FlightSettings FlightSettings;
        public Ability Ability;

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
