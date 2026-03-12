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
    public Vector2 CameraPanInput => Actions.Player.CameraPan.ReadValue<Vector2>();
    public bool BoostPressed => Actions.Debug.Boost.IsPressed() || _micBoostActive;
    public bool BoostTriggered => Actions.Debug.Boost.WasPerformedThisFrame();
    public bool ResetTriggered => Actions.Debug.Reset.WasPerformedThisFrame();
    public bool DashTriggered => Actions.Player.Dash.WasPerformedThisFrame();
    public bool DropTriggered => Actions.Player.Drop.WasPerformedThisFrame();
    public bool ReturnTriggered => Actions.Player.Return.WasPerformedThisFrame();
    public bool MenuTriggered => Actions.Player.Menu.WasPerformedThisFrame();

    // ── Folding convenience accessors ───────────────────────────────
    public bool RecenterTriggered => Actions.Folding.Recenter.WasPerformedThisFrame();
    public bool ExecuteFoldTriggered => Actions.Folding.ExecuteFold.WasPerformedThisFrame();

    void Update() {
        DoStuff();
    }


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
            StopMic();
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

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
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

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // ── Menu callback ────────────────────────────────────────────────
    private void OnMenuPerformed(InputAction.CallbackContext ctx)
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SceneManager.LoadScene("StartScene");
    }


    // IMPORTANT: Dont worry about this section its a secret (if it ever causes an issue, just remove it)
    #region Nothing to see here
    private float micVolumeThreshold = 0.05f;
    private int micSampleSize = 128;

    private bool _micBoostActive;
    private AudioClip _micClip;
    private string _micDevice;
    private float[] _micSamples;

    // ── Update ───────────────────────────────────────────────────────
    void DoStuff()
    {

        if (Actions.Debug.Secret.WasPressedThisFrame() && _micDevice == null)
            StartMic();
        // Sample mic volume only while Secret is held
        if (Actions.Debug.Secret.IsPressed() && _micClip != null)
            _micBoostActive = GetMicVolume() > micVolumeThreshold;
        else
            _micBoostActive = false;
    }

    // ── Mic helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Call once to start the mic (called from Awake).
    /// The mic stays on so pressing Secret never causes a hitch.
    /// </summary>
    private void StartMic()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("InputManager: No microphone found – mic-boost disabled.");
            return;
        }

        _micDevice = Microphone.devices[0];
        _micClip = Microphone.Start(_micDevice, loop: true, lengthSec: 1, frequency: 44100);
        _micSamples = new float[micSampleSize];
    }

    private void StopMic()
    {
        if (_micDevice != null && Microphone.IsRecording(_micDevice))
            Microphone.End(_micDevice);

        _micClip = null;
        _micDevice = null;
        _micBoostActive = false;
    }

    private float GetMicVolume()
    {
        int micPos = Microphone.GetPosition(_micDevice);
        if (micPos < micSampleSize || _micClip == null)
            return 0f;

        _micClip.GetData(_micSamples, micPos - micSampleSize);

        float sum = 0f;
        for (int i = 0; i < micSampleSize; i++)
            sum += _micSamples[i] * _micSamples[i];

        return Mathf.Sqrt(sum / micSampleSize);
    }
    #endregion
}


