using Crease.Flying.Player;
using Crease.Flying.Player.Abilities;
using Crease.Folding.PaperGraph;
using UnityEngine;

namespace Crease.Flying.Player.Loadouts
{
    [CreateAssetMenu(fileName = "PlaneLoadout", menuName = "Crease/Plane Loadout")]
    public class PlaneLoadout : ScriptableObject
    {
        public FoldInstruction FoldInstruction;
        public FlightSettings FlightSettings;
        public Ability Ability;
    }
}
