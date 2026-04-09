using UnityEngine;

public enum DashRechargeMode
{
    Slipstream,
    SimpleTimer
}

public class DashController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private KinematicBody kinematicBody;
    [SerializeField] private float boostStrength = 50f;
    [SerializeField] private float dashDuration = 0.5f;
    [SerializeField] private float invincibilityDuration = 0.6f;
    [Header("Trail Settings")]
    [SerializeField] private WingTrailController wingTrailController;
    [SerializeField] private float trailTime = 0.5f;
    [SerializeField] private GameObject dashBorder;

    [Header("Recharge Settings")]
    [SerializeField] private DashRechargeMode rechargeMode = DashRechargeMode.Slipstream;
    [SerializeField] private float rechargeRate = 20f;
    [SerializeField] private float rechargeMax = 100f;

    private int objectsInRange = 0;
    private MeshRenderer dashBorderRenderer;
    
    private float dashTimer = 0f;
    private float invincibilityTimer = 0f;
    private float trailTimer = 0f;
    private float currentRecharge = 0f;
    private bool canDash = true;

    private Vector3 dashDirection;
    private float dashSpeed;

    public bool IsDashing => dashTimer > 0f;
    public bool IsInvincible => invincibilityTimer > 0f;
    public float CurrentRecharge => currentRecharge;
    public float MaxRecharge => rechargeMax;
    public bool CanDash => canDash;

    void Start()
    {
        currentRecharge = rechargeMax;

        if (wingTrailController != null)
        {
            wingTrailController.SetTrailEnabled(false);
        }
        
        if (dashBorder != null)
        {
            dashBorderRenderer = dashBorder.GetComponent<MeshRenderer>();
        }
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

        if (invincibilityTimer > 0f)
        {
            invincibilityTimer -= Time.deltaTime;
        }

        if (trailTimer > 0f)
        {
            trailTimer -= Time.deltaTime;
            if (trailTimer <= 0f && wingTrailController != null)
            {
                wingTrailController.SetTrailEnabled(false);
            }
        }

        bool isRecharging = (rechargeMode == DashRechargeMode.SimpleTimer) || (objectsInRange > 0);

        if (isRecharging && currentRecharge < rechargeMax)
        {
            currentRecharge += rechargeRate * Time.deltaTime;
            if (currentRecharge >= rechargeMax)
            {
                currentRecharge = rechargeMax;
                canDash = true;
            }
        }

        bool shouldShowDashBorder = (rechargeMode == DashRechargeMode.Slipstream) && objectsInRange > 0;
        SetDashBorderVisible(shouldShowDashBorder);
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
            invincibilityTimer = invincibilityDuration;

            if (wingTrailController != null)
            {
                wingTrailController.SetTrailEnabled(true);
                trailTimer = trailTime;
            }

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

    private void SetDashBorderVisible(bool visible)
    {
        if (dashBorderRenderer != null)
        {
            dashBorderRenderer.enabled = visible;
        }
    }

}
