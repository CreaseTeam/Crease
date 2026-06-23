using UnityEngine;

namespace Crease.Flying.Environment.Wind.SplineTube
{
    /// <summary>
    /// Thin relay component placed on each auto-generated segment child GameObject.
    /// Routes OnTriggerEnter/OnTriggerExit back to the parent SplineTubeTrigger's
    /// reference-counted enter/exit methods, so crossing between adjacent segments
    /// never produces a false exit/enter pair at the SplineWindZone level.
    /// 
    /// Also stores the ring index range this segment covers so SplineWindParticles
    /// can look up the correct tangent direction for per-segment particle orientation.
    /// 
    /// This script is created and managed entirely by SplineTubeTrigger at runtime.
    /// Do not add it manually.
    /// </summary>
    public class SegmentTriggerRelay : MonoBehaviour
    {
        private SplineTubeTrigger _parent;

        /// <summary>Index of the first ring this segment covers in SplineTubeTrigger.Rings.</summary>
        public int StartRingIndex { get; private set; }

        /// <summary>Index of the last ring this segment covers in SplineTubeTrigger.Rings.</summary>
        public int EndRingIndex { get; private set; }

        public void Initialize(SplineTubeTrigger parent, int startRingIndex, int endRingIndex)
        {
            _parent         = parent;
            StartRingIndex  = startRingIndex;
            EndRingIndex    = endRingIndex;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_parent != null)
                _parent.OnChildTriggerEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (_parent != null)
                _parent.OnChildTriggerExit(other);
        }
    }
}