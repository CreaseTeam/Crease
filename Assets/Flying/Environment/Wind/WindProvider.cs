using UnityEngine;

namespace Crease.Flying.Environment.Wind
{
    /// <summary>
    /// Base class for any system that provides wind data.
    /// Inherit from this and implement GetWindForceAtPoint with your own logic.
    /// </summary>
    public abstract class WindProvider : MonoBehaviour
    {
        /// <summary>
        /// Calculates the wind force vector at a specific world position.
        /// </summary>
        public abstract Vector3 GetWindForceAtPoint(Vector3 worldPosition);
    }
}
