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
    [SerializeField] private List<Heart> hearts;
    [SerializeField] private HealthBar healthBar;

    [SerializeField] private GameObject foldingUI;
    [SerializeField] private GameObject flyingUI;
    [SerializeField] private GameObject checkpointUI;

    [Header("Fold Accuracy")]
    [SerializeField] private TextMeshProUGUI foldAccuracyText;
    [SerializeField] private TextMeshProUGUI overallAccuracyText;


    public static HUDCanvas Instance { get; private set; }

    private int collectibleCount = 0;
    private int maxHealth = 5;
    private int health = 5;

    public int Collect()
    {
        collectibleCount++;
        collectibleText.text = $"{collectibleCount}";
        return collectibleCount;
    }

    public void TakeDamage()
    {
        if (health > 0)
        {
            health--;
            UpdateHearts();
        }
    }

    public void Heal()
    {
        if (health < maxHealth)
        {
            health++;
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

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("HUDCanvas Awake: Instance set");
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

        maxHealth = hearts.Count;
        health = maxHealth;
        Debug.Log($"HUDCanvas Start: collectible={collectibleCount}, maxHealth={maxHealth}, health={health}");
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
    /// Called by the health system to update the flying health bar segments.
    /// </summary>
    public void OnHealthDamaged(DamageType type, float normalizedDamage)
    {
        Debug.Log($"HUDCanvas.OnHealthDamaged received: type={type}, normalizedDamage={normalizedDamage}");
        if (healthBar != null)
        {
            Debug.Log("HUDCanvas forwarding damage to HealthBar");
            healthBar.HandleDamaged(type, normalizedDamage);
        }
        else
        {
            Debug.LogWarning("HUDCanvas.OnHealthDamaged: healthBar reference is null");
        }
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
