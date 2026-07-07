using Crease.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Crease.Managers.Input
{
    public class InputManager : MonoBehaviour
    {
        public GameInput Actions { get; private set; }
        public static InputManager Instance { get; private set; }

        public bool PilotControlsEnabled { get; set; } = true;

        // ── Player & Debug convenience accessors ────────────────────────
        public Vector2 MoveInput
        {
            get
            {
                Vector2 input = Actions.Player.Move.ReadValue<Vector2>();
                if (!PilotControlsEnabled)
                {
                    input.y = -input.y;
                }
                return input;
            }
        }
        public Vector2 CameraZoomInput => Actions.Player.CameraZoom.ReadValue<Vector2>();
        public Vector2 CameraPanInput => Actions.Player.CameraPan.ReadValue<Vector2>();
        public bool BoostPressed => Actions.Debug.Boost.IsPressed() || _micBoostActive;
        public bool BoostTriggered => Actions.Debug.Boost.WasPerformedThisFrame();
        public bool ResetTriggered => Actions.Debug.Reset.WasPerformedThisFrame();
        public bool ActivateAbilityPressed => Actions.Player.ActivateAbility.WasPerformedThisFrame();
        public bool DropTriggered => Actions.Player.Drop.WasPerformedThisFrame();
        public bool ReturnTriggered => Actions.Player.Return.WasPerformedThisFrame();
        public bool PauseTriggered =>
            Actions.Player.Pause.WasPerformedThisFrame()
            || Actions.Folding.Pause.WasPerformedThisFrame();

        // ── Folding convenience accessors ───────────────────────────────
        public bool RecenterTriggered => Actions.Folding.Recenter.WasPerformedThisFrame();
        public bool ExecuteFoldTriggered => Actions.Folding.ExecuteFold.WasPerformedThisFrame();
        public bool RotatePaperPressed => Actions.Folding.RotateToggle.IsPressed();
        public Vector2 RotatePaperDelta => Actions.Folding.RotateDelta.ReadValue<Vector2>();
        public float ScaleStickerInput => Actions.Folding.ScaleSticker.ReadValue<float>();
        public float RotateStickerInput => Actions.Folding.RotateSticker.ReadValue<float>();
        // Center camera (teleport mouse to center + reset camera pan)
        public bool CenterCameraTriggered => Actions.Player.CenterCamera.WasPerformedThisFrame();

        void Update()
        {
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
            transform.SetParent(null); // detach from any parent to avoid unintended destruction
            DontDestroyOnLoad(gameObject);

            Actions = new GameInput();
            Actions.Player.Pause.performed += OnPausePerformed;
            Actions.Folding.Pause.performed += OnPausePerformed;
            Actions.Player.Enable();

            Actions.Debug.Enable();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                StopMic();
                if (Actions != null)
                {
                    Actions.Player.Pause.performed -= OnPausePerformed;
                    Actions.Folding.Pause.performed -= OnPausePerformed;
                }
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
            Cursor.lockState = CursorLockMode.Confined;

            if (Mouse.current != null)
            {
                // Instantly snap physical mouse coordinate to center screen (no hardware lag trailing)
                Mouse.current.WarpCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f));
            }
        }

        // ── Pause callback ────────────────────────────────────────────────
        private void OnPausePerformed(InputAction.CallbackContext ctx)
        {
            if (HUDCanvas.Instance != null)
            {
                HUDCanvas.Instance.TogglePause();
            }
        }


        // IMPORTANT: Dont worry about this section its a secret (if it ever causes an issue, just remove it)
        #region Nothing to see here
        // We guard microphone usage so Web builds (WebGL) which lack the Microphone API
        // won't attempt to reference the Microphone class. On WebGL we provide safe stubs
        // so the rest of the InputManager compiles and BoostPressed continues to work.
#if !UNITY_WEBGL
        private float _micVolumeThreshold = 0.05f;
        private int _micSampleSize = 128;

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
                _micBoostActive = GetMicVolume() > _micVolumeThreshold;
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
            _micSamples = new float[_micSampleSize];
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
            if (micPos < _micSampleSize || _micClip == null)
                return 0f;

            _micClip.GetData(_micSamples, micPos - _micSampleSize);

            float sum = 0f;
            for (int i = 0; i < _micSampleSize; i++)
                sum += _micSamples[i] * _micSamples[i];

            return Mathf.Sqrt(sum / _micSampleSize);
        }
#else
        // WebGL (and other builds that don't provide Microphone) — stubbed mic support
        private float _micVolumeThreshold = 0.05f;
        private int _micSampleSize = 128;

        // Keep the boost flag present so other code can query it safely.
        private bool _micBoostActive = false;
        private AudioClip _micClip = null;
        private string _micDevice = null;
        private float[] _micSamples = null;

        void DoStuff()
        {
            // No microphone on WebGL — never enable mic boost.
            _micBoostActive = false;
        }

        private void StartMic()
        {
            Debug.Log("InputManager: Microphone disabled on WebGL — StartMic() skipped.");
        }

        private void StopMic()
        {
            _micClip = null;
            _micDevice = null;
            _micBoostActive = false;
        }

        private float GetMicVolume()
        {
            return 0f;
        }
#endif
        #endregion
    }
}
