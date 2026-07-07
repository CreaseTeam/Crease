using Crease.Flying.Player;
using Crease.Flying.Player.Abilities;
using Crease.Folding.PaperGraph;
using UnityEngine;

namespace Crease.Flying.Player.Loadouts
{
    public class PlaneLoadoutApplier : MonoBehaviour
    {
        [SerializeField] private FoldInstructionRunner _foldInstructionRunner;
        [SerializeField] private FlightStats _flightStats;
        [SerializeField] private AbilityController _abilityController;

        public void ApplyLoadout(PlaneLoadout loadout, bool preserveDecals = false)
        {
            if (loadout == null)
                return;

            if (loadout.FoldInstruction != null && _foldInstructionRunner != null)
                _foldInstructionRunner.LoadInstruction(loadout.FoldInstruction, clearDecals: !preserveDecals);

            if (loadout.FlightSettings != null && _flightStats != null)
                _flightStats.SetBaseSettings(loadout.FlightSettings);

            if (_abilityController != null)
                _abilityController.Equip(loadout.PrimaryAbility, loadout.SecondaryAbility);
        }
    }
}
