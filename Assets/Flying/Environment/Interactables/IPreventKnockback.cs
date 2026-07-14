using UnityEngine;

namespace Crease.Flying.Environment.Interactables
{
    /// <summary>
    /// Interface for objects that should prevent knockback when the player collides with them.
    /// Allows objects to conditionally prevent knockback based on collision circumstances.
    /// </summary>
    public interface IPreventKnockback
    {
        /// <summary>
        /// Determines whether knockback should be prevented for this collision.
        /// </summary>
        /// <param name="playerCollider">The collider of the player that triggered the collision.</param>
        /// <returns>True if knockback should be prevented, false otherwise.</returns>
        bool ShouldPreventKnockback(Collider playerCollider);
    }
}
