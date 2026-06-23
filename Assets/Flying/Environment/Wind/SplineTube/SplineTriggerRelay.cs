using UnityEngine;

namespace Crease.Flying.Environment.Wind.SplineTube
{
    /// <summary>
    /// Thin relay component placed on each auto-generated segment child GameObject.
    /// Routes OnTriggerEnter/OnTriggerExit back to the parent SplineTubeTrigger's
    /// reference-counted enter/exit methods, so crossing between adjacent segments
    /// never produces a false exit/enter pair at the SplineWindZone level.
    /// 
    /// This script is created and managed entirely by SplineTubeTrigger at runtime.
    /// Do not add it manually.
    /// </summary>
    public class SegmentTriggerRelay : MonoBehaviour
    {
        private SplineTubeTrigger _parent;

        public void Initialize(SplineTubeTrigger parent)
        {
            _parent = parent;
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