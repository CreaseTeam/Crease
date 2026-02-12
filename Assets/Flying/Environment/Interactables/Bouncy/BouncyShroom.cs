using UnityEngine;

/// <summary>
/// Bounces the player upward when they collide with this mushroom.
/// Requires a trigger collider on this GameObject.
/// </summary>
public class BouncyShroom : MonoBehaviour
{
    [Header("Bounce Settings")]
    [Tooltip("Upward impulse force applied to the player on collision.")]
    [SerializeField] private float bounceForce = 30f;

    [Header("Effects")]
    [Tooltip("Particle system to play when the player bounces on the mushroom.")]
    [SerializeField] private ParticleSystem bounceEffect;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the colliding object has a KinematicBody (i.e., is the player)
        KinematicBody body = other.GetComponent<KinematicBody>();
        if (body == null) return;

        // Apply upward bounce impulse
        body.AddImpulse(Vector3.up * bounceForce);

        // Play bounce effect if assigned
        if (bounceEffect != null)
        {
            bounceEffect.Play();
        }
    }
}
