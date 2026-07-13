using System;
using System.Collections;
using System.Collections.Generic;
using Crease.Flying.Player;
using Crease.Folding.PaperSurface.Writing;
using Crease.Managers.Input;
using Crease.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Folding.Paper
{
    public enum GameMode { Folding, Flying }

    /// <summary>
    /// Singleton that manages switching between Folding and Flying modes
    /// within the same scene. Performs smooth camera transitions between
    /// a dedicated folding camera and flying camera.
    /// </summary>
    public class FoldingManager : MonoBehaviour
    {
        public static FoldingManager Instance { get; private set; }

        [Header("Default State")]
        [Tooltip("Which mode the game starts in.")]
        [FormerlySerializedAs("defaultMode")]
        public GameMode DefaultMode = GameMode.Folding;

        [Header("Cameras")]
        [Tooltip("Camera used during folding mode.")]
        [FormerlySerializedAs("foldingCamera")]
        public Camera FoldingCamera;

        [Tooltip("Camera used during flying mode.")]
        [FormerlySerializedAs("flyingCamera")]
        public Camera FlyingCamera;

        [Header("Transition")]
        [Tooltip("Duration of the camera transition in seconds.")]
        [FormerlySerializedAs("transitionDuration")]
        public float TransitionDuration = 1f;

        [Tooltip("Duration of the paper alignment lerp during mode transitions.")]
        [FormerlySerializedAs("paperAlignDuration")]
        public float PaperAlignDuration = 0.6f;

        [Header("Mesh Settings")]
        [Tooltip("Preset mesh used when entering flying mode without saving a fold. Must already be in flight frame (+Z = model front), e.g. from Save Current Mesh As Asset.")]
        [FormerlySerializedAs("defaultPlayerMesh")]
        public Mesh DefaultPlayerMesh;

        [Header("References")]
        [Tooltip("Current PaperGraph reference. Assign in the Inspector.")]
        [FormerlySerializedAs("paperGraph")]
        public PaperGraph PaperGraph;

        [Tooltip("The player GameObject. Disabled during folding, re-enabled when flying.")]
        [FormerlySerializedAs("player")]
        public GameObject Player;

        public bool IsFolding { get; private set; } = true;
        public bool IsTransitioning { get; private set; } = false;

        public Mesh SavedMesh { get; private set; }

        private FlightShadingData _flightShadingData;

        private MeshFilter _playerMeshFilter;
        private MeshRenderer _playerMeshRenderer;

        private Camera _transitionCamera;
        private Vector3 _transitionStartPos;
        private Quaternion _transitionStartRot;
        private float _transitionStartFOV;
        private Camera _transitionTarget;
        private float _transitionElapsed;

        private bool _pendingUseSavedMesh;
        private bool _pendingFlyingSwitch;
        private bool _pendingFoldingTransition;
        private bool _restoreFoldingPhaseUiAfterTransition;
        private bool _pendingLevelEndUnfold;
        private bool _isAligningPaper;
        private Quaternion _paperAlignStartRot;
        private Quaternion _paperAlignTargetRot;
        private Quaternion _capturedFlightAlignRotation;
        private Vector3 _capturedFlightMeshPosition;
        private float _paperAlignElapsed;
        private readonly Queue<string> _queuedLetterSections = new Queue<string>();

        private void Awake() {
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CachePlayerMeshReferences();
        }

        private void CachePlayerMeshReferences() {
            if (_playerMeshRenderer != null && _playerMeshFilter != null) return;

            if (Player != null) {
                Transform[] children = Player.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++) {
                    if (!children[i].CompareTag("PlayerMesh")) continue;
                    _playerMeshFilter = children[i].GetComponent<MeshFilter>();
                    _playerMeshRenderer = children[i].GetComponent<MeshRenderer>();
                    return;
                }
            }

            GameObject playerMeshObj = GameObject.FindWithTag("PlayerMesh");
            if (playerMeshObj != null) {
                _playerMeshFilter = playerMeshObj.GetComponent<MeshFilter>();
                _playerMeshRenderer = playerMeshObj.GetComponent<MeshRenderer>();
            }
        }

        private void Start() {
            if (DefaultMode == GameMode.Folding) {
                IsFolding = true;
                if (FoldingCamera != null) FoldingCamera.enabled = true;
                if (FlyingCamera != null) FlyingCamera.enabled = false;

                if (InputManager.Instance != null)
                    InputManager.Instance.SwitchToFolding();

                if (HUDCanvas.Instance != null)
                    HUDCanvas.Instance.OnEnterNormalFoldingMode();

                if (Player != null) {
                    TeleportPaperToPlayer();
                    Player.SetActive(false);
                }
                if (FlyingCamera != null) FlyingCamera.gameObject.SetActive(false);
                if (PaperGraph != null) PaperGraph.gameObject.SetActive(true);
            } else {
                IsFolding = false;
                if (FoldingCamera != null) {
                    FoldingCamera.enabled = false;
                    FoldingCamera.gameObject.SetActive(false);
                }
                if (FlyingCamera != null) FlyingCamera.enabled = true;

                if (InputManager.Instance != null)
                    InputManager.Instance.SwitchToPlayerAndDebug();

                if (HUDCanvas.Instance != null)
                    HUDCanvas.Instance.ShowFlyingUI(true);

                if (Player != null) Player.SetActive(true);
                if (PaperGraph != null) PaperGraph.gameObject.SetActive(false);
                ApplyDefaultMeshToPlayer();
            }
        }

        private void Update() {
            if (!IsFolding && !IsTransitioning && !_isAligningPaper
                && InputManager.Instance != null
                && InputManager.Instance.ReturnTriggered) {
                EnterFoldingMode();
            }

            if (_isAligningPaper && PaperGraph != null) {
                _paperAlignElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_paperAlignElapsed / PaperAlignDuration);
                float s = t * t * (3f - 2f * t);

                PaperGraph.transform.rotation = Quaternion.Slerp(
                    _paperAlignStartRot, _paperAlignTargetRot, s);

                if (_pendingFoldingTransition)
                    SyncPaperPositionToFlightMesh();

                if (t >= 1f) {
                    _isAligningPaper = false;
                    PaperGraph.transform.rotation = _paperAlignTargetRot;
                    SyncFoldInstructionRunnerToPaperRotation();

                    if (!IsTransitioning && _pendingFlyingSwitch)
                        ExecuteFlyingSwitch(_pendingUseSavedMesh);

                    if (!IsTransitioning && _pendingFoldingTransition)
                        CompleteFoldingTransition();
                }
            }

            if (IsTransitioning) {
                _transitionElapsed += Time.deltaTime;
                float ct = Mathf.Clamp01(_transitionElapsed / TransitionDuration);
                float cs = ct * ct * (3f - 2f * ct);

                _transitionCamera.transform.position = Vector3.Lerp(
                    _transitionStartPos, _transitionTarget.transform.position, cs);
                _transitionCamera.transform.rotation = Quaternion.Slerp(
                    _transitionStartRot, _transitionTarget.transform.rotation, cs);
                _transitionCamera.fieldOfView = Mathf.Lerp(
                    _transitionStartFOV, _transitionTarget.fieldOfView, cs);

                if (ct >= 1f) {
                    FinishTransition();

                    if (!_isAligningPaper && _pendingFlyingSwitch)
                        ExecuteFlyingSwitch(_pendingUseSavedMesh);

                    if (!_isAligningPaper && _pendingFoldingTransition)
                        CompleteFoldingTransition();

                    if (_pendingLevelEndUnfold) {
                        _pendingLevelEndUnfold = false;
                        StartCoroutine(UnfoldForLevelEndThenPlayQueuedSections());
                    }
                }
            }
        }

        private void OnDestroy() {
            if (Instance == this)
                Instance = null;

            if (_transitionCamera != null)
                Destroy(_transitionCamera.gameObject);
        }

        public void EnterFoldingMode() {
            EnterFoldingMode(levelEnd: false);
        }

        /// <summary>
        /// Switches from flying back to folding mode with a camera transition.
        /// When <paramref name="levelEnd"/> is true this is the goal/level-end path:
        /// the minimal LevelEndUI is shown instead of the folding UI, and the normal
        /// folding-phase restoration (decals + sticker/instruction UI) is skipped so
        /// only the LevelEndUI remains visible.
        /// </summary>
        public void EnterFoldingMode(bool levelEnd) {
            if (IsFolding || IsTransitioning || _isAligningPaper) return;

            IsFolding = true;

            if (InputManager.Instance != null)
                InputManager.Instance.SwitchToFolding();

            if (HUDCanvas.Instance != null) {
                if (levelEnd)
                    HUDCanvas.Instance.ShowLevelEndUI(true);
                else
                    HUDCanvas.Instance.OnEnterNormalFoldingMode();
            }

            if (PaperGraph != null) PaperGraph.gameObject.SetActive(true);

            _pendingFoldingTransition = true;
            _restoreFoldingPhaseUiAfterTransition = !levelEnd;
            _capturedFlightAlignRotation = GetFlightAlignedPaperRotation();
            _capturedFlightMeshPosition = GetPlayerMeshTransform().position;
            SetFlightTransitionFrozen(true);
            GetFoldInstructionRunner()?.SetPaperRotationLerpPaused(true);
            SetFoldingPreviewMeshVisible(false);

            if (Player != null)
                SyncPaperPositionToFlightMesh();

            BeginPaperAlignmentForFolding();

            if (FlyingCamera != null) FlyingCamera.gameObject.SetActive(false);
            if (FoldingCamera != null) FoldingCamera.gameObject.SetActive(true);

            BeginTransition(FlyingCamera, FoldingCamera);
        }

        private void CompleteFoldingTransition() {
            _pendingFoldingTransition = false;

            SyncPaperPositionToFlightMesh();
            SetFoldingPreviewMeshVisible(true);
            ForcePreviewMeshUpdate();
            CorrectPaperPositionForPreviewMesh();

            SetFlightTransitionFrozen(false);

            if (Player != null)
                Player.SetActive(false);

            GetFoldInstructionRunner()?.SetPaperRotationLerpPaused(false);
            if (_restoreFoldingPhaseUiAfterTransition)
                RestoreFoldingPhaseUi();
            SyncFoldInstructionRunnerToPaperRotation();
        }

        /// <summary>
        /// Level-end sequence fired by a Goal when the player reaches it: returns to
        /// folding mode (camera pan), reveals the clear letter material on the front of
        /// the folding preview paper, and resets the paper. Only the LevelEndUI is shown.
        /// </summary>
        public void TriggerLevelEnd(Material letterFront) {
            if (IsFolding || IsTransitioning || _isAligningPaper) return;

            QueueLetterSection("End");

            EnterFoldingMode(levelEnd: true);

            if (letterFront != null)
                SetPreviewFrontMaterial(letterFront);

            GetFoldInstructionRunner()?.PrepareLevelEndFold();

            _pendingLevelEndUnfold = true;
        }

        public void QueueLetterSection(string sectionName) {
            if (string.IsNullOrEmpty(sectionName))
                return;

            _queuedLetterSections.Enqueue(sectionName);
        }

        public void UnfoldForRefold(Action onComplete = null) {
            FoldInstructionRunner runner = GetFoldInstructionRunner();
            if (runner == null) {
                OnPaperFullyUnfolded(onComplete);
                return;
            }

            runner.UnfoldForRefold(() => OnPaperFullyUnfolded(onComplete));
        }

        void OnPaperFullyUnfolded(Action onComplete = null) {
            StartCoroutine(PlayQueuedLetterSectionsRoutine(onComplete));
        }

        IEnumerator UnfoldForLevelEndThenPlayQueuedSections() {
            FoldInstructionRunner runner = GetFoldInstructionRunner();
            if (runner != null) {
                runner.UnfoldForLevelEnd();
                yield return new WaitWhile(() => runner.IsUnfolding);
            }

            yield return PlayQueuedLetterSectionsRoutine(null);
        }

        IEnumerator PlayQueuedLetterSectionsRoutine(Action onComplete) {
            LetterController letterController = LetterController.Instance;

            while (_queuedLetterSections.Count > 0) {
                string sectionName = _queuedLetterSections.Dequeue();
                if (letterController != null)
                    yield return letterController.WriteSectionAndWait(sectionName);
            }

            onComplete?.Invoke();
        }

        private void SetPreviewFrontMaterial(Material material) {
            if (PaperGraph == null) return;
            PaperGraphController controller = PaperGraph.GetComponent<PaperGraphController>();
            if (controller != null)
                controller.SetPreviewFrontMaterial(material);
        }

        private FoldInstructionRunner GetFoldInstructionRunner() {
            return PaperGraph != null ? PaperGraph.GetComponent<FoldInstructionRunner>() : null;
        }

        public void EnterFlyingMode() {
            if (!IsFolding || IsTransitioning || _isAligningPaper) return;
            BeginFlyingTransition(useSavedMesh: true);
        }

        public void EnterFlyingModeNoMesh() {
            if (!IsFolding || IsTransitioning || _isAligningPaper) return;
            BeginFlyingTransition(useSavedMesh: false);
        }

        public void SaveMesh() {
            PaperGraphController controller = PaperGraph != null
                ? PaperGraph.GetComponent<PaperGraphController>()
                : null;
            if (controller != null)
                controller.ClearPreview();

            PaperGraph topologyGraph = GetFlightTopologyGraph();
            if (topologyGraph == null) {
                Debug.LogError("FoldingManager: No PaperGraph assigned. Cannot save mesh.");
                return;
            }

            PaperGraph settingsGraph = GetFlightSettingsGraph();
            Quaternion flightOrientation = Quaternion.Euler(GetActiveFlightMeshRotation());

            Mesh rawMesh = topologyGraph.GenerateMesh();
            SavedMesh = FlightMeshBaker.BakeFlightMesh(rawMesh, flightOrientation);
            SavedMesh.name = "FoldingManager_SavedMesh";
            Destroy(rawMesh);

            _flightShadingData = BuildFlightShadingData(topologyGraph, settingsGraph, flightOrientation);

            Debug.Log("FoldingManager: Mesh saved.");
        }

        public void RefreshPlayerMeshShadingIfFlying() {
            if (IsFolding)
                return;

            PaperGraph topologyGraph = GetFlightTopologyGraph();
            PaperGraph settingsGraph = GetFlightSettingsGraph();
            if (topologyGraph != null)
            {
                Quaternion flightOrientation = Quaternion.Euler(GetActiveFlightMeshRotation());
                _flightShadingData = BuildFlightShadingData(topologyGraph, settingsGraph, flightOrientation);
            }

            ApplyFlightShadingToPlayerMesh();
        }

        private void BeginFlyingTransition(bool useSavedMesh) {
            _pendingFlyingSwitch = true;
            _pendingUseSavedMesh = useSavedMesh;

            if (HUDCanvas.Instance != null)
                HUDCanvas.Instance.StopFoldingTimer();

            BeginPaperAlignmentForFlight();

            if (FoldingCamera != null) FoldingCamera.gameObject.SetActive(false);
            if (FlyingCamera != null) FlyingCamera.gameObject.SetActive(true);

            if (FoldingCamera != null && FlyingCamera != null)
                BeginTransition(FoldingCamera, FlyingCamera);
            else if (!_isAligningPaper)
                ExecuteFlyingSwitch(useSavedMesh);
        }

        private void ExecuteFlyingSwitch(bool useSavedMesh) {
            _pendingFlyingSwitch = false;
            if (Player != null) Player.SetActive(true);

            if (useSavedMesh) {
                SaveMesh();
                ApplyMeshToPlayer();
            } else {
                _flightShadingData = null;
                ApplyDefaultMeshToPlayer();
            }

            ApplyFlightShadingToPlayerMesh();

            if (PaperGraph != null) PaperGraph.gameObject.SetActive(false);

            IsFolding = false;

            if (InputManager.Instance != null)
                InputManager.Instance.SwitchToPlayerAndDebug();

            if (HUDCanvas.Instance != null) {
                HUDCanvas.Instance.ShowStickerUI(false);
                HUDCanvas.Instance.ShowFlyingUI(true);
            }

            if (FoldingCamera != null)
                FoldingCamera.gameObject.SetActive(false);
            if (FlyingCamera != null)
                FlyingCamera.gameObject.SetActive(true);
        }

        private void BeginTransition(Camera from, Camera to) {
            IsTransitioning = true;
            _transitionElapsed = 0f;
            _transitionTarget = to;

            _transitionStartPos = from.transform.position;
            _transitionStartRot = from.transform.rotation;
            _transitionStartFOV = from.fieldOfView;

            var go = new GameObject("FoldingManager_TransitionCamera");
            _transitionCamera = go.AddComponent<Camera>();
            _transitionCamera.CopyFrom(from);
            _transitionCamera.transform.position = _transitionStartPos;
            _transitionCamera.transform.rotation = _transitionStartRot;
            _transitionCamera.fieldOfView = _transitionStartFOV;

            from.enabled = false;
            to.enabled = false;
        }

        private void FinishTransition() {
            IsTransitioning = false;

            _transitionTarget.enabled = true;

            if (_transitionCamera != null) {
                Destroy(_transitionCamera.gameObject);
                _transitionCamera = null;
            }

            _transitionTarget = null;
        }

        private void ApplyMeshToPlayer() {
            if (SavedMesh == null) return;

            CachePlayerMeshReferences();
            if (_playerMeshFilter == null) {
                Debug.LogWarning("FoldingManager: No GameObject tagged 'PlayerMesh' found.");
                return;
            }

            _playerMeshFilter.sharedMesh = SavedMesh;
            ApplyPreviewMaterialsToPlayer(SavedMesh);
            Debug.Log($"FoldingManager: Applied saved mesh to '{_playerMeshFilter.gameObject.name}'.");
        }

        private void ApplyPreviewMaterialsToPlayer(Mesh mesh) {
            if (_playerMeshRenderer == null || mesh == null || PaperGraph == null) return;

            PaperGraphController controller = PaperGraph.GetComponent<PaperGraphController>();
            if (controller == null) return;

            PaperGraphVisualizer previewVisualizer = controller.PreviewVisualizer;
            if (previewVisualizer == null) return;

            if (previewVisualizer.MeshMaterials != null && previewVisualizer.MeshMaterials.Length >= mesh.subMeshCount) {
                _playerMeshRenderer.sharedMaterials = previewVisualizer.MeshMaterials;
            } else if (previewVisualizer.MeshMaterial != null) {
                Material[] materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = previewVisualizer.MeshMaterial;
                _playerMeshRenderer.sharedMaterials = materials;
            }
        }

        private PaperGraph GetFlightTopologyGraph() {
            if (PaperGraph == null)
                return null;

            PaperGraphController controller = PaperGraph.GetComponent<PaperGraphController>();
            if (controller?.AuthoringGraph != null && controller.AuthoringGraph.Faces.Count > 0)
                return controller.AuthoringGraph;

            return PaperGraph;
        }

        private PaperGraph GetFlightSettingsGraph() {
            if (PaperGraph == null)
                return null;

            PaperGraphController controller = PaperGraph.GetComponent<PaperGraphController>();
            return controller?.AuthoringGraph ?? PaperGraph;
        }

        private Vector3 GetActiveFlightMeshRotation() {
            FoldInstructionRunner runner = GetFoldInstructionRunner();
            if (runner?.Instruction != null)
                return runner.Instruction.FlightMeshRotation;

            return Vector3.zero;
        }

        private static FlightShadingData BuildFlightShadingData(
            PaperGraph topologyGraph,
            PaperGraph settingsGraph,
            Quaternion flightOrientation)
        {
            Matrix4x4 segmentTransform = Matrix4x4.Rotate(flightOrientation);
            return PaperShading.BuildFlightShadingData(
                topologyGraph,
                settingsGraph,
                null,
                segmentTransform);
        }

        private void ApplyFlightShadingToPlayerMesh() {
            CachePlayerMeshReferences();
            if (_playerMeshRenderer == null)
                return;

            if (_flightShadingData != null) {
                PaperShading.ApplyFlightShadingData(_playerMeshRenderer, _flightShadingData);
                return;
            }

            PaperGraph topologyGraph = GetFlightTopologyGraph();
            PaperGraph settingsGraph = GetFlightSettingsGraph();
            if (topologyGraph == null)
                return;

            Quaternion flightOrientation = Quaternion.Euler(GetActiveFlightMeshRotation());
            PaperShading.ApplyCreaseSegments(
                _playerMeshRenderer,
                topologyGraph,
                settingsGraph,
                Matrix4x4.Rotate(flightOrientation));
        }

        private void RestoreFoldingPhaseUi() {
            if (PaperGraph == null) return;
            FoldInstructionRunner runner = PaperGraph.GetComponent<FoldInstructionRunner>();
            runner?.OnEnterFoldingMode();
        }

        private void ApplyDefaultMeshToPlayer() {
            if (DefaultPlayerMesh == null) return;

            CachePlayerMeshReferences();
            if (_playerMeshFilter == null) {
                Debug.LogWarning("FoldingManager: No GameObject tagged 'PlayerMesh' found.");
                return;
            }

            _playerMeshFilter.sharedMesh = DefaultPlayerMesh;
        }

        private void TeleportPaperToPlayer() {
            TeleportPaperPositionToPlayer();
            SnapPaperToFoldingViewRotation();
        }

        private void TeleportPaperPositionToPlayer() {
            if (Player == null || PaperGraph == null) return;

            Transform meshTarget = GetPlayerMeshTransform();
            Vector3 delta = meshTarget.position - PaperGraph.transform.position;
            PaperGraph.transform.position += delta;

            if (FoldingCamera != null)
                FoldingCamera.transform.position += delta;
        }

        private void BeginPaperAlignmentForFlight() {
            if (PaperGraph == null) return;

            _paperAlignStartRot = PaperGraph.transform.rotation;
            _paperAlignTargetRot = GetFlightAlignedPaperRotation();
            _paperAlignElapsed = 0f;
            _isAligningPaper = true;
        }

        private void BeginPaperAlignmentForFolding() {
            if (PaperGraph == null) return;

            _paperAlignStartRot = PaperGraph.transform.rotation;
            _paperAlignTargetRot = _capturedFlightAlignRotation;
            _paperAlignElapsed = 0f;
            _isAligningPaper = true;
        }

        /// <summary>
        /// World rotation that matches the in-flight mesh: mesh world orientation
        /// composed with the flight bake rotation from the fold instruction.
        /// </summary>
        private Quaternion GetFlightAlignedPaperRotation() {
            if (PaperGraph == null)
                return Quaternion.identity;

            Transform meshTarget = GetPlayerMeshTransform();
            if (meshTarget == null)
                return PaperGraph.transform.rotation;

            return meshTarget.rotation * Quaternion.Euler(GetActiveFlightMeshRotation());
        }

        private Quaternion GetFoldingViewPaperRotation() {
            FoldInstructionRunner runner = GetFoldInstructionRunner();
            if (runner != null)
                return runner.GetFoldingViewRotation();

            return PaperGraph != null ? PaperGraph.transform.rotation : Quaternion.identity;
        }

        private void SnapPaperToFoldingViewRotation() {
            if (PaperGraph == null) return;

            PaperGraph.transform.rotation = GetFoldingViewPaperRotation();
            SyncFoldInstructionRunnerToPaperRotation();
        }

        private void SyncFoldInstructionRunnerToPaperRotation() {
            if (PaperGraph == null) return;

            FoldInstructionRunner runner = GetFoldInstructionRunner();
            runner?.SyncPaperRotationToTransform();
        }

        private void SetFlightTransitionFrozen(bool frozen) {
            if (Player == null) return;

            FlightController flightController = Player.GetComponent<FlightController>();
            flightController?.SetTransitionFrozen(frozen);
        }

        private void SetFoldingPreviewMeshVisible(bool visible) {
            if (PaperGraph == null) return;

            PaperGraphController controller = PaperGraph.GetComponent<PaperGraphController>();
            if (controller?.PreviewVisualizer != null)
                controller.PreviewVisualizer.ShowMesh = visible;
        }

        private void SyncPaperPositionToFlightMesh() {
            if (PaperGraph == null) return;

            Vector3 targetPosition = Player != null && Player.activeSelf
                ? GetPlayerMeshTransform().position
                : _capturedFlightMeshPosition;

            Vector3 delta = targetPosition - PaperGraph.transform.position;
            if (delta.sqrMagnitude < 1e-10f)
                return;

            PaperGraph.transform.position += delta;
            if (FoldingCamera != null)
                FoldingCamera.transform.position += delta;
        }

        private void ForcePreviewMeshUpdate() {
            if (PaperGraph == null) return;

            PaperGraphController controller = PaperGraph.GetComponent<PaperGraphController>();
            controller?.PreviewVisualizer?.UpdateMesh();
        }

        private void CorrectPaperPositionForPreviewMesh() {
            if (PaperGraph == null || Player == null || !Player.activeSelf)
                return;

            CachePlayerMeshReferences();
            if (_playerMeshFilter == null || _playerMeshFilter.sharedMesh == null)
                return;

            PaperGraphController controller = PaperGraph.GetComponent<PaperGraphController>();
            Transform previewRoot = controller?.PreviewMeshRoot;
            MeshFilter previewFilter = previewRoot != null ? previewRoot.GetComponent<MeshFilter>() : null;
            if (previewFilter == null || previewFilter.sharedMesh == null)
                return;

            Vector3 flightCentroid = GetMeshWorldCentroid(_playerMeshFilter.sharedMesh, _playerMeshFilter.transform);
            Vector3 previewCentroid = GetMeshWorldCentroid(previewFilter.sharedMesh, previewFilter.transform);
            Vector3 delta = flightCentroid - previewCentroid;
            if (delta.sqrMagnitude < 1e-10f)
                return;

            PaperGraph.transform.position += delta;
            if (FoldingCamera != null)
                FoldingCamera.transform.position += delta;
        }

        private static Vector3 GetMeshWorldCentroid(Mesh mesh, Transform transform) {
            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
                return transform.position;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < vertices.Length; i++)
                sum += transform.TransformPoint(vertices[i]);

            return sum / vertices.Length;
        }

        private Transform GetPlayerMeshTransform() {
            FlightController fc = Player.GetComponent<FlightController>();
            if (fc != null && fc.MeshTransform != null)
                return fc.MeshTransform;
            return Player.transform;
        }
    }
}
