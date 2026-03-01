using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton bridge that persists across scenes.
/// Captures a mesh from a PaperGraph in the folding scene and makes it
/// available in the flight scene. Automatically relinks to the PaperGraph
/// when the folding scene is loaded, and optionally swaps the player mesh
/// when the flight scene is loaded.
/// </summary>
public class FoldingBridge : MonoBehaviour
{
    public static FoldingBridge Instance { get; private set; }

    [Header("Scene Names")]
    [Tooltip("Name of the folding scene.")]
    public string foldingSceneName = "FoldingScene";

    [Tooltip("Name of the flight scene.")]
    public string flightSceneName = "FlightTest";

    [Header("Mesh Settings")]
    [Tooltip("Rotation (euler angles) applied to mesh vertices on save to correct orientation.")]
    public Vector3 meshRotation = Vector3.zero;

    [Header("References")]
    [Tooltip("Current PaperGraph reference. Auto-linked when the folding scene loads.")]
    public PaperGraph paperGraph;

    private Mesh savedMesh;
    private bool applyMeshOnLoad = false;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable() {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update() {
        if (SceneManager.GetActiveScene().name == flightSceneName
            && InputManager.Instance != null
            && InputManager.Instance.ReturnTriggered) {
            ReturnToFoldingScene();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (scene.name == foldingSceneName) {
            RelinkPaperGraph();

            // Activate Folding inputs, deactivate Player + Debug
            if (InputManager.Instance != null)
                InputManager.Instance.SwitchToFolding();
        } else if (scene.name == flightSceneName) {
            // Activate Player + Debug inputs, deactivate Folding
            if (InputManager.Instance != null)
                InputManager.Instance.SwitchToPlayerAndDebug();

            if (applyMeshOnLoad && savedMesh != null) {
                ApplyMeshToPlayer();
                applyMeshOnLoad = false;
            }
        }
    }

    /// <summary>
    /// Finds the PaperGraph tagged "MainPaper" and relinks to it.
    /// </summary>
    private void RelinkPaperGraph() {
        GameObject mainPaper = GameObject.FindWithTag("MainPaper");
        if (mainPaper != null) {
            paperGraph = mainPaper.GetComponent<PaperGraph>();
            if (paperGraph != null)
                Debug.Log($"FoldingBridge: Relinked to PaperGraph on '{mainPaper.name}'.");
            else
                Debug.LogWarning($"FoldingBridge: GameObject '{mainPaper.name}' tagged MainPaper has no PaperGraph component.");
        } else {
            paperGraph = null;
            Debug.LogWarning("FoldingBridge: No GameObject tagged 'MainPaper' found in scene.");
        }
    }

    /// <summary>
    /// Finds the PlayerMesh tagged object and replaces its MeshFilter mesh.
    /// </summary>
    private void ApplyMeshToPlayer() {
        GameObject playerObj = GameObject.FindWithTag("PlayerMesh");
        if (playerObj == null) {
            Debug.LogWarning("FoldingBridge: No GameObject tagged 'PlayerMesh' found in flight scene.");
            return;
        }

        MeshFilter mf = playerObj.GetComponent<MeshFilter>();
        if (mf == null) {
            Debug.LogWarning($"FoldingBridge: '{playerObj.name}' has no MeshFilter component.");
            return;
        }

        mf.mesh = savedMesh;
        Debug.Log($"FoldingBridge: Applied saved mesh to '{playerObj.name}'.");
    }

    // ─── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// Generates a mesh from the current PaperGraph and saves it.
    /// </summary>
    public void SaveMesh() {
        if (paperGraph == null) {
            Debug.LogError("FoldingBridge: No PaperGraph assigned. Cannot save mesh.");
            return;
        }

        savedMesh = paperGraph.GenerateMesh();
        savedMesh.name = "FoldingBridge_SavedMesh";

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

        Debug.Log("FoldingBridge: Mesh saved.");
    }

    /// <summary>
    /// Returns the previously saved mesh, or null if none has been saved.
    /// </summary>
    public Mesh GetSavedMesh() {
        return savedMesh;
    }

    /// <summary>
    /// Saves the mesh and transitions to the flight scene, applying the mesh to PlayerMesh on load.
    /// </summary>
    public void TransitionToFlightScene() {
        SaveMesh();
        applyMeshOnLoad = true;
        SceneManager.LoadScene(flightSceneName);
    }

    /// <summary>
    /// Transitions to the flight scene without changing the player mesh.
    /// </summary>
    public void TransitionToFlightSceneNoMesh() {
        applyMeshOnLoad = false;
        SceneManager.LoadScene(flightSceneName);
    }

    /// <summary>
    /// Returns to the folding scene (relinks automatically via OnSceneLoaded).
    /// </summary>
    public void ReturnToFoldingScene() {
        SceneManager.LoadScene(foldingSceneName);
    }
}
