using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class HUDCanvas : MonoBehaviour
{
    [SerializeField] private DashController dashController;
    [SerializeField] private Image rechargeBar;
    [SerializeField] private GameObject dashBarBorder;
    [SerializeField] private TextMeshProUGUI collectibleText;
    // [SerializeField] private List<Heart> hearts;
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private Health playerHealth;

    [SerializeField] private GameObject foldingUI;
    [SerializeField] private GameObject flyingUI;
    [SerializeField] private GameObject checkpointUI;

    [Header("Fold Accuracy")]
    [SerializeField] private TextMeshProUGUI foldAccuracyText;
    [SerializeField] private TextMeshProUGUI overallAccuracyText;


    public static HUDCanvas Instance { get; private set; }

    private int collectibleCount = 0;

    public int Collect()
    {
        collectibleCount++;
        collectibleText.text = $"{collectibleCount}";
        return collectibleCount;
    }

    /* 
    Legacy Heart System 
    private int maxHealth = 5;
    private int health = 5;

    public void TakeDamage()
    {
        if (health > 0)
        {
            health--;
            UpdateHearts();
        }
    }

    private void UpdateHearts()
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            hearts[i].SetHealth(i < health);
        }
    }
    */

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("HUDCanvas Awake: Instance already exists, destroying duplicate");
            Destroy(gameObject);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        collectibleText.text = $"{collectibleCount}";
        rechargeBar.fillAmount = dashController.CurrentRecharge / dashController.MaxRecharge;

        // maxHealth = hearts.Count;
        // health = maxHealth;
        // Debug.Log($"HUDCanvas Start: collectible={collectibleCount}");
    }

    // Update is called once per frame
    void Update()
    {
        rechargeBar.fillAmount = dashController.CurrentRecharge / dashController.MaxRecharge;

        if (dashBarBorder != null)
        {
            bool isFullyCharged = dashController.CurrentRecharge >= dashController.MaxRecharge;
            if (isFullyCharged)
            {
                bool flip = Mathf.PingPong(Time.time * 2f, 1f) > 0.5f;
                dashBarBorder.SetActive(flip);
            }
            else
            {
                dashBarBorder.SetActive(false);
            }
        }
    }

    public void ShowFoldingUI(bool show)
    {
        foldingUI.SetActive(show);
        flyingUI.SetActive(!show);
    }

    public void ShowFlyingUI(bool show)
    {
        flyingUI.SetActive(show);
        foldingUI.SetActive(!show);
    }

    public void ShowCheckpointUI(bool show)
    {
        checkpointUI.SetActive(show);
    }

    /// <summary>
    /// Called by the health system to update the flying health bar segments visually.
    /// </summary>
    public void VisualDamage(DamageType type, float normalizedDamage)
    {
        if (healthBar != null)
        {
            healthBar.HandleDamage(type, normalizedDamage);
        }
    }

    /// <summary>
    /// Called by the health system to update the flying health bar segments down on heal.
    /// </summary>
    public void VisualHeal(float normalizedDamage, DamageType? type = null)
    {
        if (healthBar != null)
        {
            healthBar.HandleHeal(normalizedDamage, type);
        }
    }

    /// <summary>
    /// Called by UI / other systems to request damage on the player health (absolute amount).
    /// This delegates gameplay logic to the `Health` component.
    /// </summary>
    public void Damage(DamageType type, float absoluteDamage)
    {
        if (playerHealth == null)
        {
            Debug.LogWarning("HUDCanvas.Damage: playerHealth is not assigned");
            return;
        }

        Debug.Log($"HUDCanvas.Damage delegating to playerHealth: type={type}, absolute={absoluteDamage}");
        playerHealth.TakeDamage(absoluteDamage, type);
    }

    /// <summary>
    /// Called by UI / other systems to request healing on the player health.
    /// Delegates to the `Health` component.
    /// </summary>
    public void Heal(float absoluteAmount, DamageType? type = null)
    {
        if (playerHealth == null)
        {
            Debug.LogWarning("HUDCanvas.Heal: playerHealth is not assigned");
            return;
        }

        Debug.Log($"HUDCanvas.Heal delegating to playerHealth: absolute={absoluteAmount}, type={type}");
        playerHealth.Heal(absoluteAmount, type);
    }

    /// <summary>
    /// Updates the fold accuracy text to show the latest fold's accuracy.
    /// </summary>
    public void UpdateFoldAccuracy(float accuracy)
    {
        if (foldAccuracyText != null)
            foldAccuracyText.text = $"Fold: {accuracy:F0}%";
    }

    /// <summary>
    /// Updates the overall accuracy text to show the running average.
    /// </summary>
    public void UpdateOverallAccuracy(float accuracy)
    {
        if (overallAccuracyText != null)
            overallAccuracyText.text = $"Overall: {accuracy:F0}%";
    }

    /// <summary>
    /// Resets both accuracy displays to their default state.
    /// </summary>
    public void ResetAccuracyDisplay()
    {
        if (foldAccuracyText != null)
            foldAccuracyText.text = "Fold: --";
        if (overallAccuracyText != null)
            overallAccuracyText.text = "Overall: --";
    }
}
