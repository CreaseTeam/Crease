using Crease.Flying.Player.Health;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Crease.Flying.Environment.Obstacle
{
    public class Obstacle : MonoBehaviour
    {
        [FormerlySerializedAs("_impactDamage")]
        public float ImpactDamage = 10f;

        [FormerlySerializedAs("_damageType")]
        public DamageType DamageType = DamageType.Impact;

        [FormerlySerializedAs("knockbackMultiplier")]
        public float KnockbackMultiplier = 1f;

        [Header("Collision")]
        [Tooltip("If false, this obstacle will still apply damage but will not impart knockback to the hitter.")]
        [FormerlySerializedAs("applyKnockback")]
        public bool ApplyKnockback = true;

        [Tooltip("Invoked when this obstacle is hit. The GameObject parameter is the hitter.")]
        public UnityEvent<GameObject> OnHit = new UnityEvent<GameObject>();

        public void TriggerHit(GameObject hitter)
        {
            OnHit?.Invoke(hitter);
        }
    }
}
