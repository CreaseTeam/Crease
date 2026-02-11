using UnityEngine;

public class DashController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float boostStrength = 50f;
    [SerializeField] private float dashCooldown = 2f;
    
    private KinematicBody kinematicBody;
    private float cooldownTimer = 0f;

    void Start()
    {
        kinematicBody = GetComponent<KinematicBody>();
    }

    void Update()
    {
        if (InputManager.Instance.DashTriggered)
        {
            TriggerDash();
        }
        
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }
    }

    public void TriggerDash()
    {
        if (cooldownTimer <= 0f)
        {
            cooldownTimer = dashCooldown;
            
            // Trigger dash animation
            if (animator != null)
            {
                animator.SetTrigger("Dash");
            }
            
            // Apply forward boost
            kinematicBody.Velocity += transform.forward * boostStrength;
        }
    }
}
