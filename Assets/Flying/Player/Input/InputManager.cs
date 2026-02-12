using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public GameInput Actions { get; private set; }
    public static InputManager Instance { get; private set; }

    // Convenience accessors for input values
    public Vector2 MoveInput => Actions.Player.Move.ReadValue<Vector2>();
    public Vector2 CameraZoomInput => Actions.Player.CameraZoom.ReadValue<Vector2>();
    public bool BoostPressed => Actions.Debug.Boost.IsPressed();
    public bool BoostTriggered => Actions.Debug.Boost.WasPerformedThisFrame();
    public bool ResetTriggered => Actions.Debug.Reset.WasPerformedThisFrame();
    public bool DashTriggered => Actions.Player.Dash.WasPerformedThisFrame();
    public bool DropTriggered => Actions.Player.Drop.WasPerformedThisFrame();

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
        Actions.Player.Enable();
        Actions.Debug.Enable();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Actions?.Dispose();
            Instance = null;
        }
    }
}
