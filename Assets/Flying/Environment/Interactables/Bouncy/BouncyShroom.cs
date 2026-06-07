using Crease.Flying.Player;
using UnityEngine;

namespace Crease.Flying.Environment.Interactables.Bouncy
{
    /// <summary>
    /// Bounces the player upward when they collide with this mushroom.
    /// Requires a trigger collider on this GameObject.
    /// </summary>
    public class BouncyShroom : MonoBehaviour
    {
        [Header("Bounce Settings")]
        [Tooltip("Upward impulse force applied to the player on collision.")]
        [SerializeField] private float _bounceForce = 30f;

        [Header("Effects")]
        [Tooltip("Particle system to play when the player bounces on the mushroom.")]
        [SerializeField] private ParticleSystem _bounceEffect;

        private void OnTriggerEnter(Collider other)
        {
            KinematicBody body = other.GetComponent<KinematicBody>();
            if (body == null) return;

            body.AddImpulse(Vector3.up * _bounceForce);

            if (_bounceEffect != null)
            {
                _bounceEffect.Play();
            }
        }
    }
}
