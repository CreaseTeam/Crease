using UnityEngine;
using UnityEngine.Events;

public class Obstacle : MonoBehaviour
{
    public float _impactDamage = 10f;
    public DamageType _damageType = DamageType.Impact;
    public float knockbackMultiplier = 1f;

    [Header("Collision")]
    [Tooltip("If false, this obstacle will still apply damage but will not impart knockback to the hitter.")]
    public bool applyKnockback = true;

    [Tooltip("Invoked when this obstacle is hit. The GameObject parameter is the hitter.")]
    public UnityEvent<GameObject> OnHit = new UnityEvent<GameObject>();

    public void TriggerHit(GameObject hitter)
    {
        OnHit?.Invoke(hitter);
    }
}
