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
    [SerializeField] private TextMeshProUGUI timerText;

    private float foldingTimer = 0f;
    private bool isFoldingTimerRunning = false;

    [Header("Menus")]
    [SerializeField] private GameObject pauseMenuUI;
    private bool isPaused = false;


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
        if (isFoldingTimerRunning && timerText != null)
        {
            foldingTimer += Time.deltaTime;
            UpdateTimerDisplay();
        }

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

    private void UpdateTimerDisplay()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(foldingTimer / 60F);
            int seconds = Mathf.FloorToInt(foldingTimer % 60F);
            int milliseconds = Mathf.FloorToInt((foldingTimer * 100F) % 100F);
            timerText.text = $"{minutes:00}:{seconds:00}.{milliseconds:00}";
        }
    }

    public void StartFoldingTimer()
    {
        foldingTimer = 0f;
        isFoldingTimerRunning = true;
        UpdateTimerDisplay();
    }

    public void StopFoldingTimer()
    {
        isFoldingTimerRunning = false;
    }

    /// <summary>
    /// Toggles the pause state, showing the pause menu and freezing time.
    /// </summary>
    public void TogglePause()
    {
        isPaused = !isPaused;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(isPaused);

        Time.timeScale = isPaused ? 0f : 1f;

        if (isPaused)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            // Restore cursor state based on active UI
            if (foldingUI != null && foldingUI.activeSelf)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Confined;
            }
        }
    }

    /// <summary>
    /// Unpauses the game and returns to the main menu.
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("StartScene");
    }

    /// <summary>
    /// Instructs the InputManager to toggle the pilot controls setting (inverting Y for W/S flight).
    /// Used by Unity UI Dropdown events (0 = Pilot, 1 = Arcade).
    /// </summary>
    public void TogglePilotControls(int controlModeIndex)
    {
        if (InputManager.Instance != null)
        {
            // 0 is Pilot Controls, 1 is Arcade Controls
            InputManager.Instance.PilotControlsEnabled = (controlModeIndex == 0);
        }
    }

    public void RefreshDash()
    {
        if (dashController != null)
        {
            dashController.RefreshDash();
        }
    }
}
