using UnityEngine;

namespace Crease.Flying.Environment.Wind.SplineTube
{
    /// <summary>
    /// Thin relay component placed on each of the two end-cap trigger GameObjects generated
    /// by SplineTubeTrigger when Reversible is enabled. Routes OnTriggerEnter back to the
    /// parent's OnEndCapTriggerEnter, tagged with which end (start or end) this cap represents,
    /// so SplineWindZone can determine which direction to blow the wind for that traversal.
    ///
    /// This script is created and managed entirely by SplineTubeTrigger at runtime.
    /// Do not add it manually.
    /// </summary>
    public class TubeEndCapRelay : MonoBehaviour
    {
        private SplineTubeTrigger _parent;

        /// <summary>True if this cap sits at the "end" of the spline (t = 1), false if at the "start" (t = 0).</summary>
        public bool IsEndSide { get; private set; }

        public void Initialize(SplineTubeTrigger parent, bool isEndSide)
        {
            _parent = parent;
            IsEndSide = isEndSide;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_parent != null)
                _parent.OnEndCapTriggerEnter(other, IsEndSide);
        }
    }
}