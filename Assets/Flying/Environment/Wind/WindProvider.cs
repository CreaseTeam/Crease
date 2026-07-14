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

        /// <summary>
        /// If true, wind force from this provider also rotates the plane toward the force direction.
        /// </summary>
        public virtual bool AppliesTorqueFromForce => false;

        /// <summary>
        /// How strongly wind force rotates the plane when <see cref="AppliesTorqueFromForce"/> is enabled.
        /// </summary>
        public virtual float WindTorqueStrength => 1f;

        /// <summary>
        /// Whether torque should be applied at a specific world position this frame.
        /// </summary>
        public virtual bool ShouldApplyTorqueAtPoint(Vector3 worldPosition) => AppliesTorqueFromForce;

        /// <summary>
        /// If true, this provider drives the receiver by directly controlling its velocity.
        /// </summary>
        public virtual bool OverridesVelocity => false;

        /// <summary>
        /// Returns the world-space velocity the receiver should be set to this step.
        /// </summary>
        public virtual Vector3 GetVelocityOverride(Vector3 worldPosition, Vector3 currentVelocity)
        {
            return currentVelocity;
        }
    }
}
