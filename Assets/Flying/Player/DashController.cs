using UnityEngine;

public class DashController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float boostStrength = 50f;
    [SerializeField] private float dashCooldown = 2f;
    [SerializeField] private float dashDuration = 0.5f;
    
    private KinematicBody kinematicBody;
    private float cooldownTimer = 0f;
    private float dashTimer = 0f;

    public bool IsDashing => dashTimer > 0f;

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
        
        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
        }
    }

    public void TriggerDash()
    {
        if (cooldownTimer <= 0f)
        {
            cooldownTimer = dashCooldown;
            dashTimer = dashDuration;
            
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
