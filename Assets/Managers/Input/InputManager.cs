using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;


public class InputManager : MonoBehaviour
{
    public GameInput Actions { get; private set; }
    public static InputManager Instance { get; private set; }

    // ── Player & Debug convenience accessors ────────────────────────
    public Vector2 MoveInput => Actions.Player.Move.ReadValue<Vector2>();
    public Vector2 CameraZoomInput => Actions.Player.CameraZoom.ReadValue<Vector2>();
    public bool BoostPressed => Actions.Debug.Boost.IsPressed();
    public bool BoostTriggered => Actions.Debug.Boost.WasPerformedThisFrame();
    public bool ResetTriggered => Actions.Debug.Reset.WasPerformedThisFrame();
    public bool DashTriggered => Actions.Player.Dash.WasPerformedThisFrame();
    public bool DropTriggered => Actions.Player.Drop.WasPerformedThisFrame();
    public bool ReturnTriggered => Actions.Player.Return.WasPerformedThisFrame();
    public bool MenuTriggered => Actions.Player.Menu.WasPerformedThisFrame();


    // ── Folding convenience accessors ───────────────────────────────
    public bool RecenterTriggered => Actions.Folding.Recenter.WasPerformedThisFrame();
    public bool ExecuteFoldTriggered => Actions.Folding.ExecuteFold.WasPerformedThisFrame();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // DontDestroyOnLoad(gameObject);

        Actions = new GameInput();
        Actions.Player.Menu.performed += OnMenuPerformed;
        Actions.Player.Enable();

        Actions.Debug.Enable();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            if (Actions != null)
                Actions.Player.Menu.performed -= OnMenuPerformed;
            Actions?.Player.Disable();
            Actions?.Debug.Disable();

            Actions?.Folding.Disable();
            Actions?.Dispose();
            Instance = null;
        }
    }

    // ── Action-map switching ────────────────────────────────────────

    /// <summary>
    /// Enables the Folding action map and disables Player + Debug.
    /// Call when entering the folding scene.
    /// </summary>
    public void SwitchToFolding()
    {
        Actions.Player.Disable();
        Actions.Debug.Disable();
        Actions.Folding.Enable();
    }

    /// <summary>
    /// Enables Player + Debug action maps and disables Folding.
    /// Call when entering the flight / gameplay scene.
    /// </summary>
    public void SwitchToPlayerAndDebug()
    {
        Actions.Folding.Disable();
        Actions.Player.Enable();
        Actions.Debug.Enable();
    }

    // ── Menu callback ────────────────────────────────────────────────
    private void OnMenuPerformed(InputAction.CallbackContext ctx)
    {
        SceneManager.LoadScene("StartScene");
    }
}

