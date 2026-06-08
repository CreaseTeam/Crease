using Crease.Flying.Player.Dash;
using Crease.Flying.Player.Health;
using PlayerHealth = Crease.Flying.Player.Health.Health;
using Crease.Managers.Input;
using Crease.UI.Flying;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Crease.UI
{
    public class HUDCanvas : MonoBehaviour
    {
        [SerializeField]
        [FormerlySerializedAs("dashController")]
        private DashController _dashController;
        [SerializeField]
        [FormerlySerializedAs("rechargeBar")]
        private Image _rechargeBar;
        [SerializeField]
        [FormerlySerializedAs("dashBarBorder")]
        private GameObject _dashBarBorder;
        [SerializeField]
        [FormerlySerializedAs("collectibleText")]
        private TextMeshProUGUI _collectibleText;
        // [SerializeField] private List<Heart> hearts;
        [SerializeField]
        [FormerlySerializedAs("healthBar")]
        private HealthBar _healthBar;
        [SerializeField]
        [FormerlySerializedAs("playerHealth")]
        private PlayerHealth _playerHealth;

        [SerializeField]
        [FormerlySerializedAs("foldingUI")]
        private GameObject _foldingUI;
        [SerializeField]
        [FormerlySerializedAs("flyingUI")]
        private GameObject _flyingUI;
        [SerializeField]
        [FormerlySerializedAs("checkpointUI")]
        private GameObject _checkpointUI;

        [SerializeField]
        [FormerlySerializedAs("stickerUI")]
        private GameObject _stickerUI;

        [Header("Fold Accuracy")]
        [SerializeField]
        [FormerlySerializedAs("foldAccuracyText")]
        private TextMeshProUGUI _foldAccuracyText;
        [SerializeField]
        [FormerlySerializedAs("overallAccuracyText")]
        private TextMeshProUGUI _overallAccuracyText;
        [SerializeField]
        [FormerlySerializedAs("timerText")]
        private TextMeshProUGUI _timerText;

        private float _foldingTimer = 0f;
        private bool _isFoldingTimerRunning = false;

        [Header("Menus")]
        [SerializeField]
        [FormerlySerializedAs("pauseMenuUI")]
        private GameObject _pauseMenuUI;
        private bool _isPaused = false;


        public static HUDCanvas Instance { get; private set; }

        private int _collectibleCount = 0;

        public int Collect()
        {
            _collectibleCount++;
            _collectibleText.text = $"{_collectibleCount}";
            return _collectibleCount;
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
            _collectibleText.text = $"{_collectibleCount}";
            _rechargeBar.fillAmount = _dashController.CurrentRecharge / _dashController.MaxRecharge;

            // maxHealth = hearts.Count;
            // health = maxHealth;
            // Debug.Log($"HUDCanvas Start: collectible={_collectibleCount}");
        }

        // Update is called once per frame
        void Update()
        {
            if (_isFoldingTimerRunning && _timerText != null)
            {
                _foldingTimer += Time.deltaTime;
                UpdateTimerDisplay();
            }

            _rechargeBar.fillAmount = _dashController.CurrentRecharge / _dashController.MaxRecharge;

            if (_dashBarBorder != null)
            {
                bool isFullyCharged = _dashController.CurrentRecharge >= _dashController.MaxRecharge;
                if (isFullyCharged)
                {
                    bool flip = Mathf.PingPong(Time.time * 2f, 1f) > 0.5f;
                    _dashBarBorder.SetActive(flip);
                }
                else
                {
                    _dashBarBorder.SetActive(false);
                }
            }
        }

        public void ShowFoldingUI(bool show)
        {
            _foldingUI.SetActive(show);
            _flyingUI.SetActive(!show);
        }

        public void ShowFlyingUI(bool show)
        {
            _flyingUI.SetActive(show);
            _foldingUI.SetActive(!show);
        }

        public void ShowCheckpointUI(bool show)
        {
            _checkpointUI.SetActive(show);
        }

        public void ShowStickerUI(bool show)
        {
            if (_stickerUI != null)
                _stickerUI.SetActive(show);
        }

        /// <summary>
        /// Called by the health system to update the flying health bar segments visually.
        /// </summary>
        public void VisualDamage(DamageType type, float normalizedDamage)
        {
            if (_healthBar != null)
            {
                _healthBar.HandleDamage(type, normalizedDamage);
            }
        }

        /// <summary>
        /// Called by the health system to update the flying health bar segments down on heal.
        /// </summary>
        public void VisualHeal(float normalizedDamage, DamageType? type = null)
        {
            if (_healthBar != null)
            {
                _healthBar.HandleHeal(normalizedDamage, type);
            }
        }

        /// <summary>
        /// Called by UI / other systems to request damage on the player health (absolute amount).
        /// This delegates gameplay logic to the `Health` component.
        /// </summary>
        public void Damage(DamageType type, float absoluteDamage)
        {
            if (_playerHealth == null)
            {
                Debug.LogWarning("HUDCanvas.Damage: playerHealth is not assigned");
                return;
            }

            Debug.Log($"HUDCanvas.Damage delegating to playerHealth: type={type}, absolute={absoluteDamage}");
            _playerHealth.TakeDamage(absoluteDamage, type);
        }

        /// <summary>
        /// Called by UI / other systems to request healing on the player health.
        /// Delegates to the `Health` component.
        /// </summary>
        public void Heal(float absoluteAmount, DamageType? type = null)
        {
            if (_playerHealth == null)
            {
                Debug.LogWarning("HUDCanvas.Heal: playerHealth is not assigned");
                return;
            }

            Debug.Log($"HUDCanvas.Heal delegating to playerHealth: absolute={absoluteAmount}, type={type}");
            _playerHealth.Heal(absoluteAmount, type);
        }

        /// <summary>
        /// Updates the fold accuracy text to show the latest fold's accuracy.
        /// </summary>
        public void UpdateFoldAccuracy(float accuracy)
        {
            if (_foldAccuracyText != null)
                _foldAccuracyText.text = $"Fold: {accuracy:F0}%";
        }

        /// <summary>
        /// Updates the overall accuracy text to show the running average.
        /// </summary>
        public void UpdateOverallAccuracy(float accuracy)
        {
            if (_overallAccuracyText != null)
                _overallAccuracyText.text = $"Overall: {accuracy:F0}%";
        }

        /// <summary>
        /// Resets both accuracy displays to their default state.
        /// </summary>
        public void ResetAccuracyDisplay()
        {
            if (_foldAccuracyText != null)
                _foldAccuracyText.text = "Fold: --";
            if (_overallAccuracyText != null)
                _overallAccuracyText.text = "Overall: --";
        }

        private void UpdateTimerDisplay()
        {
            if (_timerText != null)
            {
                int minutes = Mathf.FloorToInt(_foldingTimer / 60F);
                int seconds = Mathf.FloorToInt(_foldingTimer % 60F);
                int milliseconds = Mathf.FloorToInt((_foldingTimer * 100F) % 100F);
                _timerText.text = $"{minutes:00}:{seconds:00}.{milliseconds:00}";
            }
        }

        public void StartFoldingTimer()
        {
            _foldingTimer = 0f;
            _isFoldingTimerRunning = true;
            UpdateTimerDisplay();
        }

        public void StopFoldingTimer()
        {
            _isFoldingTimerRunning = false;
        }

        /// <summary>
        /// Toggles the pause state, showing the pause menu and freezing time.
        /// </summary>
        public void TogglePause()
        {
            _isPaused = !_isPaused;

            if (_pauseMenuUI != null)
                _pauseMenuUI.SetActive(_isPaused);

            Time.timeScale = _isPaused ? 0f : 1f;

            if (_isPaused)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                // Restore cursor state based on active UI
                if (_foldingUI != null && _foldingUI.activeSelf)
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
            SceneManager.LoadScene("StartScene");
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
            if (_dashController != null)
            {
                _dashController.RefreshDash();
            }
        }
    }
}
