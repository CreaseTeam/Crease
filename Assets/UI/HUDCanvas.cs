using System.Collections;
using System.Collections.Generic;
using Crease.Flying.Player.Abilities;
using Crease.Flying.Player.Loadouts;
using Crease.Flying.Player.Health;
using PlayerHealth = Crease.Flying.Player.Health.Health;
using Crease.Folding.PaperGraph;
using Crease.Folding.PaperSurface.Writing;
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
        [Header("Ability UI")]
        [SerializeField]
        private AbilityController _abilityController;
        [SerializeField]
        private GameObject _primaryAbilityUI;
        [FormerlySerializedAs("_abilityRechargeBar")]
        [SerializeField]
        private Image _primaryAbilityRechargeBar;
        [FormerlySerializedAs("_abilityReadyBorder")]
        [SerializeField]
        private GameObject _primaryAbilityReadyBorder;
        [SerializeField]
        private GameObject _secondaryAbilityUI;
        [SerializeField]
        private Image _secondaryAbilityRechargeBar;
        [SerializeField]
        private GameObject _secondaryAbilityReadyBorder;
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
        private GameObject _flyCurrentButton;
        [SerializeField]
        [FormerlySerializedAs("flyingUI")]
        private GameObject _flyingUI;
        [SerializeField]
        private GameObject _levelEndUI;

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

        [Header("Folding")]
        [SerializeField]
        [FormerlySerializedAs("planeTypeDropdown")]
        private TMP_Dropdown _planeTypeDropdown;
        [SerializeField]
        private PlaneLoadoutLibrary _loadoutLibrary;
        [SerializeField]
        private PlaneLoadoutApplier _loadoutApplier;
        [SerializeField]
        private FoldInstructionRunner _foldInstructionRunner;
        [SerializeField]
        private PlaneSelectionScreen _planeSelectionScreen;
        [SerializeField]
        private GameObject _refoldButton;

        [Header("Folding Debug Paper Texture")]
        [SerializeField]
        private Toggle _debugToggle;
        [SerializeField]
        [FormerlySerializedAs("_debugModeToggle")]
        private Toggle _debugPaperTextureToggle;
        [SerializeField]
        private PaperGraphVisualizer[] _paperGraphVisualizers;

        public static HUDCanvas Instance { get; private set; }

        public PlaneSelectionScreen PlaneSelectionScreen => _planeSelectionScreen;

        private const string DebugPrefsKey = "Debug";

        public bool Debug { get; private set; }
        public bool DebugPaperTexture { get; private set; }

        private int _collectibleCount = 0;
        private int _selectedLoadoutIndex;
        private bool _isUpdatingLoadoutDropdown;
        private bool _hasStartedFolding;
        private bool _refoldAvailable;
        private bool _flyCurrentRequestedVisible;
        private bool _stickerUiRequestedVisible;
        private bool _preserveDecalsForNextLoadout;
        private PaperGraphVisualizer[] _resolvedPaperGraphVisualizers = System.Array.Empty<PaperGraphVisualizer>();

        public bool RequiresPlaneSelection => _planeSelectionScreen != null;
        public bool HasStartedFolding => _hasStartedFolding;
        public bool RefoldAvailable => _refoldAvailable;

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
                _hasStartedFolding = _planeSelectionScreen == null;
            }
            else
            {
                UnityEngine.Debug.LogWarning("HUDCanvas Awake: Instance already exists, destroying duplicate");
                Destroy(gameObject);
            }
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _collectibleText.text = $"{_collectibleCount}";
            UpdateAbilityUI();

            PopulateLoadoutDropdown();

            if (_hasStartedFolding)
                ApplySelectedLoadout();

            _resolvedPaperGraphVisualizers = ResolvePaperGraphVisualizers();
            if (_debugPaperTextureToggle != null)
                SetDebugPaperTexture(_debugPaperTextureToggle.isOn);

            Debug = PlayerPrefs.GetInt(DebugPrefsKey, 0) == 1;
            if (_debugToggle != null)
                _debugToggle.SetIsOnWithoutNotify(Debug);
            ApplyDebugVisibility();
            if (InputManager.Instance != null)
                InputManager.Instance.SyncDebugControls();
            UpdateRefoldUi();

            SetFlyCurrentVisible(false);

            // maxHealth = hearts.Count;
            // health = maxHealth;
            // Debug.Log($"HUDCanvas Start: collectible={_collectibleCount}");
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            PopulateLoadoutDropdown();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyDebugVisibility();
        }

        // Update is called once per frame
        void Update()
        {
            if (_isFoldingTimerRunning && _timerText != null)
            {
                _foldingTimer += Time.deltaTime;
                UpdateTimerDisplay();
            }

            UpdateAbilityUI();
        }

        private void UpdateAbilityUI()
        {
            if (_abilityController == null)
                return;

            bool hasPrimary = _abilityController.PrimaryEquippedAbility != null;
            bool hasSecondary = _abilityController.SecondaryEquippedAbility != null;

            if (_primaryAbilityUI != null)
                _primaryAbilityUI.SetActive(hasPrimary);

            if (_secondaryAbilityUI != null)
                _secondaryAbilityUI.SetActive(hasSecondary);

            if (hasPrimary)
            {
                if (_primaryAbilityRechargeBar != null)
                    _primaryAbilityRechargeBar.fillAmount = _abilityController.RechargeNormalized;

                UpdateReadyBorder(_primaryAbilityReadyBorder, _abilityController.CanActivate);
            }
            else if (_primaryAbilityReadyBorder != null)
            {
                _primaryAbilityReadyBorder.SetActive(false);
            }

            if (hasSecondary)
            {
                if (_secondaryAbilityRechargeBar != null)
                    _secondaryAbilityRechargeBar.fillAmount = _abilityController.SecondaryRechargeNormalized;

                UpdateReadyBorder(_secondaryAbilityReadyBorder, _abilityController.SecondaryCanActivate);
            }
            else if (_secondaryAbilityReadyBorder != null)
            {
                _secondaryAbilityReadyBorder.SetActive(false);
            }
        }

        private static void UpdateReadyBorder(GameObject readyBorder, bool canActivate)
        {
            if (readyBorder == null)
                return;

            if (canActivate)
            {
                bool flip = Mathf.PingPong(Time.time * 2f, 1f) > 0.5f;
                readyBorder.SetActive(flip);
            }
            else
            {
                readyBorder.SetActive(false);
            }
        }

        public void ShowFoldingUI(bool show)
        {
            _foldingUI.SetActive(show);
            _flyingUI.SetActive(!show);
        }

        /// <summary>
        /// Called when entering normal folding mode. Shows folding HUD and, if configured,
        /// the plane selection screen before any loadout is applied.
        /// </summary>
        public void OnEnterNormalFoldingMode()
        {
            ShowFoldingUI(true);
            StartCoroutine(PresentPlaneSelectionIfNeeded());
        }

        public void SetFlyCurrentVisible(bool visible)
        {
            _flyCurrentRequestedVisible = visible;
            ApplyFlyCurrentVisibility();
        }

        public void ShowFlyingUI(bool show)
        {
            _flyingUI.SetActive(show);
            _foldingUI.SetActive(!show);

            if (show)
                HidePlaneSelectionScreen();
        }

        /// <summary>
        /// Shows the minimal level-end UI (return-to-menu button) and hides the
        /// folding, flying, and sticker HUD groups. Used by the goal/level-end flow.
        /// </summary>
        public void ShowLevelEndUI(bool show)
        {
            if (_levelEndUI != null)
                _levelEndUI.SetActive(show);

            if (show)
            {
                HidePlaneSelectionScreen();
                if (_foldingUI != null) _foldingUI.SetActive(false);
                if (_flyingUI != null) _flyingUI.SetActive(false);
                _stickerUiRequestedVisible = false;
                if (_stickerUI != null) _stickerUI.SetActive(false);
            }
        }

        public void ShowStickerUI(bool show)
        {
            _stickerUiRequestedVisible = show;
            ApplyStickerUiVisibility();
        }

        public void RefreshStickerUiVisibility() => ApplyStickerUiVisibility();

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
                UnityEngine.Debug.LogWarning("HUDCanvas.Damage: playerHealth is not assigned");
                return;
            }

            UnityEngine.Debug.Log($"HUDCanvas.Damage delegating to playerHealth: type={type}, absolute={absoluteDamage}");
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
                UnityEngine.Debug.LogWarning("HUDCanvas.Heal: playerHealth is not assigned");
                return;
            }

            UnityEngine.Debug.Log($"HUDCanvas.Heal delegating to playerHealth: absolute={absoluteAmount}, type={type}");
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

        public void PopulateLoadoutDropdown()
        {
            if (_planeTypeDropdown == null || _loadoutLibrary == null)
                return;

            _planeTypeDropdown.ClearOptions();
            var options = new List<string>();
            foreach (PlaneLoadout loadout in _loadoutLibrary.Loadouts)
            {
                if (loadout == null)
                    options.Add("Missing");
                else if (!string.IsNullOrEmpty(loadout.DisplayName))
                    options.Add(loadout.DisplayName);
                else
                    options.Add(loadout.name);
            }

            if (options.Count == 0)
                options.Add("No loadouts");

            _isUpdatingLoadoutDropdown = true;
            _planeTypeDropdown.AddOptions(options);
            SyncLoadoutDropdownToRunner();
            _isUpdatingLoadoutDropdown = false;
        }

        public void SetLoadout(int index)
        {
            if (_isUpdatingLoadoutDropdown)
                return;

            _selectedLoadoutIndex = index;
            _hasStartedFolding = true;
            ApplyFlyCurrentVisibility();
            ApplySelectedLoadout();
        }

        public void SelectLoadoutFromCard(PlaneLoadout loadout)
        {
            if (loadout == null)
                return;

            if (_loadoutApplier == null)
            {
                UnityEngine.Debug.LogWarning("HUDCanvas: LoadoutApplier is not assigned.");
                return;
            }

            _hasStartedFolding = true;
            ApplyFlyCurrentVisibility();
            _loadoutApplier.ApplyLoadout(loadout, _preserveDecalsForNextLoadout);
            _preserveDecalsForNextLoadout = false;
            SyncDropdownToLoadout(loadout);
            HidePlaneSelectionScreen();
        }

        public void ShowLoadoutDetails(PlaneLoadout loadout)
        {
            if (_planeSelectionScreen != null)
                _planeSelectionScreen.ShowDetails(loadout);
        }

        public void ShowPlaneSelectionScreen()
        {
            if (_planeSelectionScreen != null)
                _planeSelectionScreen.Show();
        }

        public void HidePlaneSelectionScreen()
        {
            if (_planeSelectionScreen != null)
                _planeSelectionScreen.Hide();
        }

        public void SetRefoldAvailable(bool available)
        {
            _refoldAvailable = available;
            UpdateRefoldUi();
        }

        public void Refold()
        {
            ShowStickerUI(false);
            _refoldAvailable = false;
            _hasStartedFolding = false;
            _flyCurrentRequestedVisible = false;
            UpdateRefoldUi();

            if (FoldingManager.Instance != null)
            {
                FoldingManager.Instance.UnfoldForRefold(BeginRefoldPlaneSelection);
                return;
            }

            if (_foldInstructionRunner == null)
            {
                UnityEngine.Debug.LogWarning("HUDCanvas.Refold: FoldInstructionRunner is not assigned.");
                BeginRefoldPlaneSelection();
                return;
            }

            _foldInstructionRunner.UnfoldForRefold(BeginRefoldPlaneSelection);
        }

        private void BeginRefoldPlaneSelection()
        {
            _preserveDecalsForNextLoadout = true;
            _hasStartedFolding = false;
            StopFoldingTimer();
            ResetAccuracyDisplay();
            ShowStickerUI(false);
            SetFlyCurrentVisible(false);
            ShowPlaneSelectionScreen();
        }

        private void UpdateRefoldUi()
        {
            if (_refoldButton != null)
                _refoldButton.SetActive(_refoldAvailable);

            ApplyFlyCurrentVisibility();
            ApplyStickerUiVisibility();
        }

        private void ApplyStickerUiVisibility()
        {
            if (_stickerUI == null)
                return;

            bool unfolding = _foldInstructionRunner != null && _foldInstructionRunner.IsUnfolding;
            bool visible = _stickerUiRequestedVisible && !_refoldAvailable && !unfolding;
            _stickerUI.SetActive(visible);
        }

        private void ApplyFlyCurrentVisibility()
        {
            if (_flyCurrentButton == null)
                return;

            bool visible = _flyCurrentRequestedVisible && !_refoldAvailable && _hasStartedFolding;
            _flyCurrentButton.SetActive(visible);
        }

        private IEnumerator PresentPlaneSelectionIfNeeded()
        {
            if (!RequiresPlaneSelection || _hasStartedFolding)
                yield break;

            LetterController letterController = LetterController.Instance;
            if (letterController != null)
                yield return letterController.WriteSectionAndWait("Intro");

            if (!RequiresPlaneSelection || _hasStartedFolding)
                yield break;

            ShowPlaneSelectionScreen();
        }

        private void SyncDropdownToLoadout(PlaneLoadout loadout)
        {
            if (_planeTypeDropdown == null || _loadoutLibrary == null)
                return;

            int index = _loadoutLibrary.Loadouts.IndexOf(loadout);
            if (index < 0)
                return;

            _selectedLoadoutIndex = index;
            _isUpdatingLoadoutDropdown = true;
            _planeTypeDropdown.SetValueWithoutNotify(index);
            _planeTypeDropdown.RefreshShownValue();
            _isUpdatingLoadoutDropdown = false;
        }

        private void ApplySelectedLoadout()
        {
            PlaneLoadout loadout = GetSelectedLoadout();
            if (loadout == null)
                return;

            if (_loadoutApplier == null)
            {
                UnityEngine.Debug.LogWarning("HUDCanvas: LoadoutApplier is not assigned.");
                return;
            }

            _loadoutApplier.ApplyLoadout(loadout, _preserveDecalsForNextLoadout);
            _preserveDecalsForNextLoadout = false;
        }

        private void SyncLoadoutDropdownToRunner()
        {
            if (_planeTypeDropdown == null || _loadoutLibrary == null || _loadoutLibrary.Loadouts.Count == 0)
                return;

            int index = 0;
            FoldInstruction currentInstruction = _foldInstructionRunner != null
                ? _foldInstructionRunner.Instruction
                : null;

            if (currentInstruction != null)
            {
                for (int i = 0; i < _loadoutLibrary.Loadouts.Count; i++)
                {
                    PlaneLoadout loadout = _loadoutLibrary.Loadouts[i];
                    if (loadout != null && loadout.FoldInstruction == currentInstruction)
                    {
                        index = i;
                        break;
                    }
                }
            }

            _selectedLoadoutIndex = index;
            _planeTypeDropdown.SetValueWithoutNotify(index);
            _planeTypeDropdown.RefreshShownValue();
        }

        private PlaneLoadout GetSelectedLoadout()
        {
            if (_loadoutLibrary == null || _loadoutLibrary.Loadouts.Count == 0)
                return null;

            int index = _planeTypeDropdown != null ? _planeTypeDropdown.value : _selectedLoadoutIndex;
            index = Mathf.Clamp(index, 0, _loadoutLibrary.Loadouts.Count - 1);
            return _loadoutLibrary.Loadouts[index];
        }

        /// <summary>
        /// Enables or disables debug visibility for all GameObjects tagged "Debug".
        /// Persists the setting in PlayerPrefs.
        /// </summary>
        public void SetDebug(bool enabled)
        {
            if (Debug == enabled)
                return;

            Debug = enabled;
            PlayerPrefs.SetInt(DebugPrefsKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            ApplyDebugVisibility();
            if (InputManager.Instance != null)
                InputManager.Instance.SyncDebugControls();
        }

        /// <summary>
        /// Toggles debug visibility for all GameObjects tagged "Debug".
        /// </summary>
        public void ToggleDebug()
        {
            SetDebug(!Debug);
        }

        private void ApplyDebugVisibility()
        {
            GameObject[] sceneObjects = FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (GameObject obj in sceneObjects)
            {
                if (!obj.CompareTag("Debug"))
                    continue;

                if (!obj.scene.IsValid() || !obj.scene.isLoaded)
                    continue;

                obj.SetActive(Debug);
            }
        }

        /// <summary>
        /// Enables or disables the debug paper texture on paper graph visualizers.
        /// </summary>
        public void SetDebugPaperTexture(bool enabled)
        {
            DebugPaperTexture = enabled;

            foreach (PaperGraphVisualizer visualizer in _resolvedPaperGraphVisualizers)
            {
                if (visualizer != null)
                    visualizer.SetDebugPaperTexture(enabled);
            }
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
    }
}
