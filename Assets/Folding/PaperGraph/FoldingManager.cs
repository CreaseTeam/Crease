using UnityEngine;

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
    public GameMode defaultMode = GameMode.Folding;

    [Header("Cameras")]
    [Tooltip("Camera used during folding mode.")]
    public Camera foldingCamera;

    [Tooltip("Camera used during flying mode.")]
    public Camera flyingCamera;

    [Header("Transition")]
    [Tooltip("Duration of the camera transition in seconds.")]
    public float transitionDuration = 1f;

    [Tooltip("Duration of the paper alignment lerp before switching to flying.")]
    public float paperAlignDuration = 0.6f;

    [Header("Mesh Settings")]
    [Tooltip("Preset mesh used when entering flying mode without saving a fold.")]
    public Mesh defaultPlayerMesh;

    [Tooltip("Rotation (euler angles) applied to mesh vertices on save to correct orientation.")]
    public Vector3 meshRotation = Vector3.zero;

    [Tooltip("Euler angle offset applied when aligning paper to player rotation.")]
    public Vector3 paperToPlayerRotationOffset = Vector3.zero;

    [Header("References")]
    [Tooltip("Current PaperGraph reference. Assign in the Inspector.")]
    public PaperGraph paperGraph;

    [Tooltip("The player GameObject. Disabled during folding, re-enabled when flying.")]
    public GameObject player;

    /// <summary>True when the game is in folding mode.</summary>
    public bool IsFolding { get; private set; } = true;

    /// <summary>True while a camera transition or paper alignment is in progress.</summary>
    public bool IsTransitioning { get; private set; } = false;

    private Mesh savedMesh;

    // Camera transition state
    private Camera transitionCamera;
    private Vector3 transitionStartPos;
    private Quaternion transitionStartRot;
    private float transitionStartFOV;
    private Camera transitionTarget;
    private float transitionElapsed;

    // Paper alignment state
    private bool isAligningPaper = false;
    private Quaternion paperAlignStartRot;
    private Quaternion paperAlignTargetRot;
    private float paperAlignElapsed;
    private bool pendingUseSavedMesh; // true = use saved fold mesh, false = use default
    private bool pendingFlyingSwitch; // true when a flying switch is deferred until both lerps finish

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start() {
        if (defaultMode == GameMode.Folding) {
            IsFolding = true;
            if (foldingCamera != null) foldingCamera.enabled = true;
            if (flyingCamera != null) flyingCamera.enabled = false;

            if (InputManager.Instance != null)
                InputManager.Instance.SwitchToFolding();

            if (HUDCanvas.Instance != null)
                HUDCanvas.Instance.ShowFoldingUI(true);

            // Disable player and flying camera, teleport paper + camera, enable paperGraph
            if (player != null) {
                TeleportPaperToPlayer();
                player.SetActive(false);
            }
            if (flyingCamera != null) flyingCamera.gameObject.SetActive(false);
            if (paperGraph != null) paperGraph.gameObject.SetActive(true);
        } else {
            IsFolding = false;
            if (foldingCamera != null) {
                foldingCamera.enabled = false;
                foldingCamera.gameObject.SetActive(false);
            }
            if (flyingCamera != null) flyingCamera.enabled = true;

            if (InputManager.Instance != null)
                InputManager.Instance.SwitchToPlayerAndDebug();

            if (HUDCanvas.Instance != null)
                HUDCanvas.Instance.ShowFlyingUI(true);

            if (player != null) player.SetActive(true);
            if (paperGraph != null) paperGraph.gameObject.SetActive(false);
            ApplyDefaultMeshToPlayer();
        }
    }

    private void Update() {
        // While flying, check for Return input to go back to folding
        if (!IsFolding && !IsTransitioning && !isAligningPaper
            && InputManager.Instance != null
            && InputManager.Instance.ReturnTriggered) {
            EnterFoldingMode();
        }

        // Paper alignment lerp (runs in parallel with camera transition)
        if (isAligningPaper) {
            paperAlignElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(paperAlignElapsed / paperAlignDuration);
            float s = t * t * (3f - 2f * t); // smoothstep

            paperGraph.transform.rotation = Quaternion.Slerp(
                paperAlignStartRot, paperAlignTargetRot, s);

            if (t >= 1f) {
                isAligningPaper = false;
                paperGraph.transform.rotation = paperAlignTargetRot;

                // If camera transition already done, finalize the switch
                if (!IsTransitioning)
                    ExecuteFlyingSwitch(pendingUseSavedMesh);
            }
        }

        // Camera transition lerp (runs in parallel with paper alignment)
        if (IsTransitioning) {
            transitionElapsed += Time.deltaTime;
            float ct = Mathf.Clamp01(transitionElapsed / transitionDuration);
            float cs = ct * ct * (3f - 2f * ct);

            transitionCamera.transform.position = Vector3.Lerp(
                transitionStartPos, transitionTarget.transform.position, cs);
            transitionCamera.transform.rotation = Quaternion.Slerp(
                transitionStartRot, transitionTarget.transform.rotation, cs);
            transitionCamera.fieldOfView = Mathf.Lerp(
                transitionStartFOV, transitionTarget.fieldOfView, cs);

            if (ct >= 1f) {
                FinishTransition();

                // If paper alignment already done, finalize the switch
                if (!isAligningPaper && pendingFlyingSwitch)
                    ExecuteFlyingSwitch(pendingUseSavedMesh);
            }
        }
    }

    private void OnDestroy() {
        if (Instance == this)
            Instance = null;

        if (transitionCamera != null)
            Destroy(transitionCamera.gameObject);
    }

    // ─── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Switches to folding mode with a smooth camera transition.
    /// Instantly sets paper rotation to match the player (with offset).
    /// </summary>
    public void EnterFoldingMode() {
        if (IsFolding || IsTransitioning || isAligningPaper) return;

        IsFolding = true;

        // Switch inputs immediately
        if (InputManager.Instance != null)
            InputManager.Instance.SwitchToFolding();

        if (HUDCanvas.Instance != null)
            HUDCanvas.Instance.ShowFoldingUI(true);

        // Re-enable paperGraph, teleport + rotate to match player, disable player
        if (paperGraph != null) paperGraph.gameObject.SetActive(true);

        if (player != null) {
            TeleportPaperToPlayer();
            player.SetActive(false);
        }
        if (flyingCamera != null) flyingCamera.gameObject.SetActive(false);
        if (foldingCamera != null) foldingCamera.gameObject.SetActive(true);

        BeginTransition(flyingCamera, foldingCamera);
    }

    /// <summary>
    /// Lerps paper to match player rotation, saves mesh, then transitions to flying.
    /// </summary>
    public void EnterFlyingMode() {
        if (!IsFolding || IsTransitioning || isAligningPaper) return;
        BeginPaperAlignment(useSavedMesh: true);
    }

    /// <summary>
    /// Lerps paper to match player rotation, then transitions to flying with the default mesh.
    /// </summary>
    public void EnterFlyingModeNoMesh() {
        if (!IsFolding || IsTransitioning || isAligningPaper) return;
        BeginPaperAlignment(useSavedMesh: false);
    }

    /// <summary>
    /// Generates a mesh from the current PaperGraph and saves it internally.
    /// </summary>
    public void SaveMesh() {
        if (paperGraph == null) {
            Debug.LogError("FoldingManager: No PaperGraph assigned. Cannot save mesh.");
            return;
        }

        savedMesh = paperGraph.GenerateMesh();
        savedMesh.name = "FoldingManager_SavedMesh";

        // Rotate mesh vertices to correct orientation
        if (meshRotation != Vector3.zero) {
            Quaternion rot = Quaternion.Euler(meshRotation);
            Vector3[] verts = savedMesh.vertices;
            for (int i = 0; i < verts.Length; i++) {
                verts[i] = rot * verts[i];
            }
            savedMesh.vertices = verts;
            savedMesh.RecalculateNormals();
            savedMesh.RecalculateBounds();
        }

        Debug.Log("FoldingManager: Mesh saved.");
    }

    /// <summary>
    /// Returns the previously saved mesh, or null if none has been saved.
    /// </summary>
    public Mesh GetSavedMesh() {
        return savedMesh;
    }

    // ─── Paper Alignment ─────────────────────────────────────────────

    private void BeginPaperAlignment(bool useSavedMesh) {
        if (paperGraph == null || player == null) {
            // Skip alignment if references are missing, go straight to flying
            ExecuteFlyingSwitch(useSavedMesh);
            return;
        }

        pendingFlyingSwitch = true;
        pendingUseSavedMesh = useSavedMesh;

        // Start paper rotation lerp
        isAligningPaper = true;
        paperAlignElapsed = 0f;
        paperAlignStartRot = paperGraph.transform.rotation;
        paperAlignTargetRot = GetPlayerMeshRotation() * Quaternion.Euler(paperToPlayerRotationOffset);

        // Start camera transition at the same time
        BeginTransition(foldingCamera, flyingCamera);
    }

    /// <summary>
    /// Common logic for switching to flying after both alignment and transition are done.
    /// </summary>
    private void ExecuteFlyingSwitch(bool useSavedMesh) {
        pendingFlyingSwitch = false;
        if (useSavedMesh)
            SaveMesh();

        // Re-enable player before applying mesh (FindWithTag needs active objects)
        if (player != null) player.SetActive(true);

        if (useSavedMesh)
            ApplyMeshToPlayer();
        else
            ApplyDefaultMeshToPlayer();

        // Disable the paper graph
        if (paperGraph != null) paperGraph.gameObject.SetActive(false);

        IsFolding = false;

        // Switch inputs
        if (InputManager.Instance != null)
            InputManager.Instance.SwitchToPlayerAndDebug();

        if (HUDCanvas.Instance != null)
            HUDCanvas.Instance.ShowFlyingUI(true);

        // Re-enable flying camera
        if (flyingCamera != null) {
            flyingCamera.gameObject.SetActive(true);
        }
    }

    // ─── Camera Transition ───────────────────────────────────────────

    private void BeginTransition(Camera from, Camera to) {
        IsTransitioning = true;
        transitionElapsed = 0f;
        transitionTarget = to;

        // Snapshot the source camera's state
        transitionStartPos = from.transform.position;
        transitionStartRot = from.transform.rotation;
        transitionStartFOV = from.fieldOfView;

        // Create a temporary camera to render during the transition
        var go = new GameObject("FoldingManager_TransitionCamera");
        transitionCamera = go.AddComponent<Camera>();
        transitionCamera.CopyFrom(from);
        transitionCamera.transform.position = transitionStartPos;
        transitionCamera.transform.rotation = transitionStartRot;
        transitionCamera.fieldOfView = transitionStartFOV;

        // Disable both real cameras while the transition camera is active
        from.enabled = false;
        to.enabled = false;
    }

    private void FinishTransition() {
        IsTransitioning = false;

        // Enable the destination camera
        transitionTarget.enabled = true;

        // Clean up the transition camera
        if (transitionCamera != null) {
            Destroy(transitionCamera.gameObject);
            transitionCamera = null;
        }

        transitionTarget = null;
    }

    // ─── Internal Helpers ────────────────────────────────────────────

    private void ApplyMeshToPlayer() {
        if (savedMesh == null) return;

        GameObject playerObj = GameObject.FindWithTag("PlayerMesh");
        if (playerObj == null) {
            Debug.LogWarning("FoldingManager: No GameObject tagged 'PlayerMesh' found.");
            return;
        }

        MeshFilter mf = playerObj.GetComponent<MeshFilter>();
        if (mf == null) {
            Debug.LogWarning($"FoldingManager: '{playerObj.name}' has no MeshFilter component.");
            return;
        }

        mf.mesh = savedMesh;
        Debug.Log($"FoldingManager: Applied saved mesh to '{playerObj.name}'.");
    }

    private void ApplyDefaultMeshToPlayer() {
        if (defaultPlayerMesh == null) return;

        GameObject playerObj = GameObject.FindWithTag("PlayerMesh");
        if (playerObj == null) {
            Debug.LogWarning("FoldingManager: No GameObject tagged 'PlayerMesh' found.");
            return;
        }

        MeshFilter mf = playerObj.GetComponent<MeshFilter>();
        if (mf == null) {
            Debug.LogWarning($"FoldingManager: '{playerObj.name}' has no MeshFilter component.");
            return;
        }

        mf.mesh = defaultPlayerMesh;
        // Debug.Log($"FoldingManager: Applied default mesh to '{playerObj.name}'.");
    }

    private void TeleportPaperToPlayer() {
        if (player == null || paperGraph == null) return;

        // Match position and rotation to the player's mesh child
        Transform meshTarget = GetPlayerMeshTransform();
        Vector3 delta = meshTarget.position - paperGraph.transform.position;
        paperGraph.transform.position += delta;

        if (foldingCamera != null)
            foldingCamera.transform.position += delta;

        paperGraph.transform.rotation = meshTarget.rotation * Quaternion.Euler(paperToPlayerRotationOffset);
    }

    /// <summary>
    /// Returns the FlightController's exposed MeshTransform, or falls back to the player transform.
    /// </summary>
    private Transform GetPlayerMeshTransform() {
        FlightController fc = player.GetComponent<FlightController>();
        if (fc != null && fc.MeshTransform != null)
            return fc.MeshTransform;
        return player.transform;
    }

    private Quaternion GetPlayerMeshRotation() {
        return GetPlayerMeshTransform().rotation;
    }
}
