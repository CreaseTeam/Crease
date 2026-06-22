using System.Collections.Generic;
using Crease.Flying.Player.Abilities;
using Crease.Flying.Player.Health;
using PlayerHealth = Crease.Flying.Player.Health.Health;
using Crease.Folding.PaperGraph;
using Crease.Managers.Input;
using Crease.UI.Flying;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Crease.UI
{
    public class HUDCanvas : MonoBehaviour
    {
        [SerializeField]
        private AbilityController _abilityController;
        [SerializeField]
        private Image _abilityRechargeBar;
        [SerializeField]
        private GameObject _abilityReadyBorder;
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

        [Header("Plane Type")]
        [SerializeField]
        private TMP_Dropdown _planeTypeDropdown;
        [SerializeField]
        private FoldInstructionRunner _foldInstructionRunner;

        [Header("Folding Debug")]
        [SerializeField]
        private Toggle _debugModeToggle;
        [SerializeField]
        private PaperGraphVisualizer[] _paperGraphVisualizers;

        public static HUDCanvas Instance { get; private set; }

        public bool DebugMode { get; private set; }

        private int _collectibleCount = 0;
        private readonly List<FoldInstruction> _foldInstructions = new();
        private bool _isUpdatingPlaneTypeDropdown;
        private PaperGraphVisualizer[] _resolvedPaperGraphVisualizers = System.Array.Empty<PaperGraphVisualizer>();

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
            if (_abilityRechargeBar != null && _abilityController != null)
                _abilityRechargeBar.fillAmount = _abilityController.RechargeNormalized;

            PopulatePlaneTypeDropdown();
            if (_planeTypeDropdown != null)
                _planeTypeDropdown.onValueChanged.AddListener(OnPlaneTypeDropdownChanged);

            _resolvedPaperGraphVisualizers = ResolvePaperGraphVisualizers();
            if (_debugModeToggle != null)
            {
                _debugModeToggle.onValueChanged.AddListener(OnDebugModeToggleChanged);
                OnDebugModeToggleChanged(_debugModeToggle.isOn);
            }

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

            if (_abilityRechargeBar != null && _abilityController != null)
                _abilityRechargeBar.fillAmount = _abilityController.RechargeNormalized;

            if (_abilityReadyBorder != null && _abilityController != null)
            {
                bool canUse = _abilityController.CanActivate;
                if (canUse)
                {
                    bool flip = Mathf.PingPong(Time.time * 2f, 1f) > 0.5f;
                    _abilityReadyBorder.SetActive(flip);
                }
                else
                {
                    _abilityReadyBorder.SetActive(false);
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

        public void RefreshAbility()
        {
            if (_abilityController != null)
                _abilityController.Refresh();
        }

        /// <summary>
        /// Fills the plane type dropdown with every FoldInstruction in the project.
        /// </summary>
        public void PopulatePlaneTypeDropdown()
        {
            if (_planeTypeDropdown == null)
                return;

            _foldInstructions.Clear();
            _foldInstructions.AddRange(FindAllFoldInstructions());

            _planeTypeDropdown.ClearOptions();
            var options = new List<string>(_foldInstructions.Count);
            foreach (FoldInstruction instruction in _foldInstructions)
                options.Add(instruction != null ? instruction.name : "Missing");

            if (options.Count == 0)
                options.Add("No plane types");

            _isUpdatingPlaneTypeDropdown = true;
            _planeTypeDropdown.AddOptions(options);
            SyncPlaneTypeDropdownToRunner();
            _isUpdatingPlaneTypeDropdown = false;
        }

        /// <summary>
        /// Called when the plane type dropdown selection changes.
        /// Can also be wired directly from the dropdown's OnValueChanged event.
        /// </summary>
        /// <summary>
        /// Called when the debug mode toggle changes. Can also be wired directly from the toggle's OnValueChanged event.
        /// </summary>
        public void OnDebugModeToggleChanged(bool enabled)
        {
            DebugMode = enabled;

            foreach (PaperGraphVisualizer visualizer in _resolvedPaperGraphVisualizers)
            {
                if (visualizer != null)
                    visualizer.SetDebugMode(enabled);
            }
        }

        public void OnPlaneTypeDropdownChanged(int index)
        {
            if (_isUpdatingPlaneTypeDropdown)
                return;

            if (_foldInstructionRunner == null)
            {
                Debug.LogWarning("HUDCanvas.OnPlaneTypeDropdownChanged: FoldInstructionRunner is not assigned.");
                return;
            }

            if (index < 0 || index >= _foldInstructions.Count)
                return;

            FoldInstruction selectedInstruction = _foldInstructions[index];
            if (selectedInstruction == null)
                return;

            _foldInstructionRunner.LoadInstruction(selectedInstruction);
        }

        private void SyncPlaneTypeDropdownToRunner()
        {
            if (_planeTypeDropdown == null || _foldInstructions.Count == 0)
                return;

            int index = 0;
            FoldInstruction currentInstruction = _foldInstructionRunner != null
                ? _foldInstructionRunner.Instruction
                : null;

            if (currentInstruction != null)
            {
                int currentIndex = _foldInstructions.IndexOf(currentInstruction);
                if (currentIndex >= 0)
                    index = currentIndex;
            }

            _planeTypeDropdown.SetValueWithoutNotify(index);
            _planeTypeDropdown.RefreshShownValue();
        }

        private PaperGraphVisualizer[] ResolvePaperGraphVisualizers()
        {
            if (_paperGraphVisualizers != null && _paperGraphVisualizers.Length > 0)
                return _paperGraphVisualizers;

            if (_foldInstructionRunner == null || _foldInstructionRunner.Controller == null)
                return System.Array.Empty<PaperGraphVisualizer>();

            PaperGraphController controller = _foldInstructionRunner.Controller;
            var visualizers = new List<PaperGraphVisualizer>(2);

            PaperGraphVisualizer authoringVisualizer = controller.GetComponent<PaperGraphVisualizer>();
            if (authoringVisualizer != null)
                visualizers.Add(authoringVisualizer);

            if (controller.PreviewGraph != null)
            {
                PaperGraphVisualizer previewVisualizer = controller.PreviewGraph.GetComponent<PaperGraphVisualizer>();
                if (previewVisualizer != null)
                    visualizers.Add(previewVisualizer);
            }

            return visualizers.ToArray();
        }

        private static FoldInstruction[] FindAllFoldInstructions()
        {
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:FoldInstruction");
            var instructions = new List<FoldInstruction>(guids.Length);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var instruction = AssetDatabase.LoadAssetAtPath<FoldInstruction>(path);
                if (instruction != null)
                    instructions.Add(instruction);
            }

            instructions.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            return instructions.ToArray();
#else
            return Resources.LoadAll<FoldInstruction>("FoldInstructions");
#endif
        }
    }
}
