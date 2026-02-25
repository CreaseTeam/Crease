using UnityEngine;

public class DashController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private KinematicBody kinematicBody;
    [SerializeField] private float boostStrength = 50f;
    [SerializeField] private float dashDuration = 0.5f;
    [Header("Recharge Settings")]
    [SerializeField] private float rechargeRate = 20f;
    [SerializeField] private float rechargeMax = 100f;

    private int objectsInRange = 0;
    
    private float dashTimer = 0f;
    private float currentRecharge = 0f;
    private bool canDash = true;

    private Vector3 dashDirection;
    private float dashSpeed;

    public bool IsDashing => dashTimer > 0f;
    public float CurrentRecharge => currentRecharge;
    public float MaxRecharge => rechargeMax;
    public bool CanDash => canDash;

    void Start()
    {
        currentRecharge = rechargeMax;
    }

    void Update()
    {
        if (InputManager.Instance.DashTriggered)
        {
            TriggerDash();
        }

        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
        }

        if (objectsInRange > 0 && currentRecharge < rechargeMax)
        {
            currentRecharge += rechargeRate * Time.deltaTime;
            if (currentRecharge >= rechargeMax)
            {
                currentRecharge = rechargeMax;
                canDash = true;
            }
        }
    }

    void FixedUpdate()
    {
        if (IsDashing)
        {
            kinematicBody.Velocity = dashDirection * dashSpeed;
        }
    }

    public void TriggerDash()
    {
        if (canDash)
        {
            canDash = false;
            currentRecharge = 0f;
            dashTimer = dashDuration;

            // Lock direction to current facing; speed = forward component of current velocity + boost
            dashDirection = transform.forward;
            float forwardSpeed = Vector3.Dot(kinematicBody.Velocity, dashDirection);
            dashSpeed = Mathf.Max(forwardSpeed, 0f) + boostStrength;

            // Trigger dash animation
            if (animator != null)
            {
                animator.SetTrigger("Dash");
            }
        }
    }

    public void ModifyObjectsInRange(int amount)
    {
        objectsInRange += amount;
    }
}
