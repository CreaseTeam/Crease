using UnityEngine;

namespace Crease.Flying.Environment.Collectibles
{
    /// <summary>
    /// Tuning for the parabolic interception arc a magnetized collectible follows toward the player.
    /// The coin is launched on a quadratic Bézier from where it was captured, bowing out to the side,
    /// toward a predicted interception point ahead of the moving player — so it arrives from the side
    /// instead of chasing directly behind the plane (which obscures the rear camera).
    /// Set <see cref="LateralDistance"/> and <see cref="VerticalOffset"/> both to 0 to fall back to a
    /// straight lead-homing line.
    /// </summary>
    [System.Serializable]
    public class MagnetArcSettings
    {
        [Tooltip("How far to the side (world units) the coin bows out at the middle of the arc. Larger = wider, more obvious side approach.")]
        public float LateralDistance = 6f;

        [Tooltip("Arc apex height (world units) at the middle of the path. Positive lobs over the top; negative comes in from below. Keep modest so it stays clear of the rear camera.")]
        public float VerticalOffset = 1.5f;

        [Tooltip("Which way to swing when the coin sits (nearly) directly behind the player. 1 = right, -1 = left.")]
        public float DefaultSide = 1f;
    }
}
