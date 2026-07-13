using System.Collections.Generic;
using UnityEngine;

namespace Crease.Flying.Player.Loadouts
{
    [CreateAssetMenu(fileName = "PlaneLoadoutLibrary", menuName = "Crease/Plane Loadout Library")]
    public class PlaneLoadoutLibrary : ScriptableObject
    {
        public List<PlaneLoadout> Loadouts = new List<PlaneLoadout>();
    }
}
