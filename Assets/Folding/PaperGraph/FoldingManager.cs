using System.Collections.Generic;
using Crease.Flying.Player;
using Crease.Folding.Decals;
using Crease.Managers.Input;
using Crease.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Folding.PaperGraph
{
    public enum GameMode { Folding, Flying }

    /// <summary>
    /// Singleton that manages switching between Folding and Flying modes
    /// within the same scene. Performs smooth camera transitions between
    /// a dedicated folding camera and flying camera, and lerps the paper
    /// to match the player orientation before switching to flying.
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

        [Tooltip("Duration of the paper alignment lerp before switching to flying.")]
        [FormerlySerializedAs("paperAlignDuration")]
        public float PaperAlignDuration = 0.6f;

        [Header("Mesh Settings")]
        [Tooltip("Preset mesh used when entering flying mode without saving a fold.")]
        [FormerlySerializedAs("defaultPlayerMesh")]
        public Mesh DefaultPlayerMesh;

        [Tooltip("Rotation (euler angles) applied to mesh vertices on save to correct orientation.")]
        [FormerlySerializedAs("meshRotation")]
        public Vector3 MeshRotation = Vector3.zero;

        [Tooltip("Euler angle offset applied when aligning paper to player rotation.")]
        [FormerlySerializedAs("paperToPlayerRotationOffset")]
        public Vector3 PaperToPlayerRotationOffset = Vector3.zero;

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

        private MeshFilter _playerMeshFilter;
        private MeshRenderer _playerMeshRenderer;

        private Camera _transitionCamera;
        private Vector3 _transitionStartPos;
        private Quaternion _transitionStartRot;
        private float _transitionStartFOV;
        private Camera _transitionTarget;
        private float _transitionElapsed;

        private bool _isAligningPaper = false;
        private Quaternion _paperAlignStartRot;
        private Quaternion _paperAlignTargetRot;
        private float _paperAlignElapsed;
        private bool _pendingUseSavedMesh;
        private bool _pendingFlyingSwitch;

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
                    HUDCanvas.Instance.ShowFoldingUI(true);

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

            if (_isAligningPaper) {
                _paperAlignElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_paperAlignElapsed / PaperAlignDuration);
                float s = t * t * (3f - 2f * t);

                PaperGraph.transform.rotation = Quaternion.Slerp(
                    _paperAlignStartRot, _paperAlignTargetRot, s);

                if (t >= 1f) {
                    _isAligningPaper = false;
                    PaperGraph.transform.rotation = _paperAlignTargetRot;

                    if (!IsTransitioning)
                        ExecuteFlyingSwitch(_pendingUseSavedMesh);
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
            if (IsFolding || IsTransitioning || _isAligningPaper) return;

            IsFolding = true;

            if (InputManager.Instance != null)
                InputManager.Instance.SwitchToFolding();

            if (HUDCanvas.Instance != null)
                HUDCanvas.Instance.ShowFoldingUI(true);

            if (PaperGraph != null) PaperGraph.gameObject.SetActive(true);

            if (Player != null) {
                TeleportPaperToPlayer();
                Player.SetActive(false);
            }

            RestoreDecalsToFoldingPaper();
            RestoreFoldingPhaseUi();
            if (FlyingCamera != null) FlyingCamera.gameObject.SetActive(false);
            if (FoldingCamera != null) FoldingCamera.gameObject.SetActive(true);

            BeginTransition(FlyingCamera, FoldingCamera);
        }

        public void EnterFlyingMode() {
            if (!IsFolding || IsTransitioning || _isAligningPaper) return;
            BeginPaperAlignment(useSavedMesh: true);
        }

        public void EnterFlyingModeNoMesh() {
            if (!IsFolding || IsTransitioning || _isAligningPaper) return;
            BeginPaperAlignment(useSavedMesh: false);
        }

        public void SaveMesh() {
            if (PaperGraph == null) {
                Debug.LogError("FoldingManager: No PaperGraph assigned. Cannot save mesh.");
                return;
            }

            SavedMesh = PaperGraph.GenerateMesh();
            SavedMesh.name = "FoldingManager_SavedMesh";

            if (MeshRotation != Vector3.zero) {
                Quaternion rot = Quaternion.Euler(MeshRotation);
                Vector3[] verts = SavedMesh.vertices;
                for (int i = 0; i < verts.Length; i++) {
                    verts[i] = rot * verts[i];
                }
                SavedMesh.vertices = verts;
                SavedMesh.RecalculateNormals();
                SavedMesh.RecalculateBounds();
            }

            Debug.Log("FoldingManager: Mesh saved.");
        }

        private void BeginPaperAlignment(bool useSavedMesh) {
            if (PaperGraph == null || Player == null) {
                ExecuteFlyingSwitch(useSavedMesh);
                return;
            }

            _pendingFlyingSwitch = true;
            _pendingUseSavedMesh = useSavedMesh;

            if (HUDCanvas.Instance != null)
                HUDCanvas.Instance.StopFoldingTimer();

            _isAligningPaper = true;
            _paperAlignElapsed = 0f;
            _paperAlignStartRot = PaperGraph.transform.rotation;
            _paperAlignTargetRot = GetPlayerMeshRotation() * Quaternion.Euler(PaperToPlayerRotationOffset);

            if (FoldingCamera != null) FoldingCamera.gameObject.SetActive(false);
            if (FlyingCamera != null) FlyingCamera.gameObject.SetActive(true);

            BeginTransition(FoldingCamera, FlyingCamera);
        }

        private void ExecuteFlyingSwitch(bool useSavedMesh) {
            _pendingFlyingSwitch = false;
            if (Player != null) Player.SetActive(true);

            if (useSavedMesh)
                SaveMesh();

            if (useSavedMesh)
                ApplyMeshToPlayer();
            else
                ApplyDefaultMeshToPlayer();

            if (useSavedMesh)
                AttachDecalsToPlayerMesh();

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
            if (controller == null || controller.PreviewGraph == null) return;

            PaperGraphVisualizer previewVisualizer = controller.PreviewGraph.GetComponent<PaperGraphVisualizer>();
            if (previewVisualizer == null) return;

            if (previewVisualizer.MeshMaterials != null && previewVisualizer.MeshMaterials.Length >= mesh.subMeshCount) {
                _playerMeshRenderer.sharedMaterials = previewVisualizer.MeshMaterials;
            } else if (previewVisualizer.MeshMaterial != null) {
                Material[] materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = previewVisualizer.MeshMaterial;
                _playerMeshRenderer.sharedMaterials = materials;
            }

            PaperEdgeShading.Apply(_playerMeshRenderer, PaperGraph);
        }

        private void AttachDecalsToPlayerMesh() {
            PaperDecalManager decalManager = GetDecalManager();
            if (decalManager == null || decalManager.Placements.Count == 0) return;

            CachePlayerMeshReferences();
            Transform flightMeshRoot = GetPlayerMeshTransform();
            if (flightMeshRoot == null) return;

            if (PaperGraph != null)
                flightMeshRoot.SetPositionAndRotation(PaperGraph.transform.position, flightMeshRoot.rotation);

            decalManager.AttachToFlight(flightMeshRoot, Quaternion.Euler(MeshRotation));
        }

        private void RestoreDecalsToFoldingPaper() {
            PaperDecalManager decalManager = GetDecalManager();
            decalManager?.RestoreToFolding();
        }

        private void RestoreFoldingPhaseUi() {
            if (PaperGraph == null) return;
            FoldInstructionRunner runner = PaperGraph.GetComponent<FoldInstructionRunner>();
            runner?.OnEnterFoldingMode();
        }

        private PaperDecalManager GetDecalManager() {
            if (PaperGraph == null) return null;
            PaperGraphController controller = PaperGraph.GetComponent<PaperGraphController>();
            if (controller != null && controller.DecalManager != null)
                return controller.DecalManager;
            return PaperGraph.GetComponent<PaperDecalManager>();
        }

        private void ApplyDefaultMeshToPlayer() {
            if (DefaultPlayerMesh == null) return;

            if (_playerMeshFilter == null) {
                Debug.LogWarning("FoldingManager: No GameObject tagged 'PlayerMesh' found.");
                return;
            }

            _playerMeshFilter.mesh = DefaultPlayerMesh;
        }

        private void TeleportPaperToPlayer() {
            if (Player == null || PaperGraph == null) return;

            Transform meshTarget = GetPlayerMeshTransform();
            Vector3 delta = meshTarget.position - PaperGraph.transform.position;
            PaperGraph.transform.position += delta;

            if (FoldingCamera != null)
                FoldingCamera.transform.position += delta;

            PaperGraph.transform.rotation = meshTarget.rotation * Quaternion.Euler(PaperToPlayerRotationOffset);
        }

        private Transform GetPlayerMeshTransform() {
            FlightController fc = Player.GetComponent<FlightController>();
            if (fc != null && fc.MeshTransform != null)
                return fc.MeshTransform;
            return Player.transform;
        }

        private Quaternion GetPlayerMeshRotation() {
            return GetPlayerMeshTransform().rotation;
        }
    }
}
