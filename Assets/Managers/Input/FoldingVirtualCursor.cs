using Crease.Folding.PaperGraph;
using Crease.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Crease.Managers.Input
{
    /// <summary>
    /// Drop-in component that gives folding mode a gamepad-driven mouse cursor.
    ///
    /// It builds a software cursor under the HUD canvas and drives it with
    /// Unity's built-in <see cref="VirtualMouseInput"/> (left stick moves the
    /// cursor, right trigger acts as the left mouse button). The virtual mouse
    /// becomes <c>Mouse.current</c> while the stick is used, so the existing
    /// mouse consumers (FoldDragHandle, StickerUIController) and the UI pointer
    /// all work with a controller unchanged.
    ///
    /// Only active while <see cref="FoldingManager.IsFolding"/> is true (covers
    /// both the fold and sticker phases). While active it also neutralizes the
    /// UI module's gamepad navigation (Move/Submit/Cancel) so that the left
    /// stick / A / B do not both move the cursor AND drive UI navigation.
    ///
    /// Fully self-wiring: just drop it on any GameObject in the scene. Max can
    /// later bake the cursor into the HUDCanvas prefab and swap the placeholder
    /// sprite.
    /// </summary>
    public class FoldingVirtualCursor : MonoBehaviour
    {
        [Tooltip("Cursor speed in pixels/second at full stick deflection (scaled by the canvas resolution).")]
        [SerializeField] private float _cursorSpeed = 1200f;

        [Tooltip("On-screen size (pixels) of the placeholder cursor graphic.")]
        [SerializeField] private float _cursorSize = 28f;

        [Tooltip("Optional cursor sprite. A simple placeholder dot is generated when left empty.")]
        [SerializeField] private Sprite _cursorSprite;

        [Tooltip("Trigger value (0-1) at which the right trigger counts as a click.")]
        [SerializeField] private float _clickThreshold = 0.5f;

        [Tooltip("Log cursor/click diagnostics to the console. Turn off once verified.")]
        [SerializeField] private bool _debugLogging = true;

        private VirtualMouseInput _virtualMouse;
        private GameObject _cursorObject;
        private Image _cursorImage;
        private RectTransform _cursorRect;
        private Canvas _canvas;

        private InputAction _stickAction;

        private InputSystemUIInputModule _uiModule;
        private InputActionReference _savedMove;
        private InputActionReference _savedSubmit;
        private InputActionReference _savedCancel;
        private UIPointerBehavior _savedPointerBehavior;
        private bool _navNeutralized;

        private bool _built;
        private bool _active;
        private bool _gamepadMode;
        private bool _clickDown;
        private Mouse _systemMouse;

        private void Update()
        {
            bool shouldBeActive = FoldingManager.Instance != null && FoldingManager.Instance.IsFolding;

            if (shouldBeActive && !_built)
                TryBuild();

            if (!_built)
                return;

            if (shouldBeActive != _active)
                SetActive(shouldBeActive);

            if (_active)
            {
                UpdateDeviceVisibility();
                if (_debugLogging)
                    LogFoldingInputs();
            }
        }

        /// <summary>TEMP: reports whether the Folding-map gamepad actions are firing.</summary>
        private void LogFoldingInputs()
        {
            if (InputManager.Instance == null || InputManager.Instance.Actions == null)
                return;

            var folding = InputManager.Instance.Actions.Folding;
            if (folding.ExecuteFold.WasPerformedThisFrame())
                Debug.Log("[VirtualCursor] Folding.ExecuteFold performed (East)");
            if (folding.Recenter.WasPerformedThisFrame())
                Debug.Log("[VirtualCursor] Folding.Recenter performed (South)");

            float scale = folding.ScaleSticker.ReadValue<float>();
            float rotate = folding.RotateSticker.ReadValue<float>();
            if (Mathf.Abs(scale) > 0.1f || Mathf.Abs(rotate) > 0.1f)
                Debug.Log($"[VirtualCursor] Folding D-pad scale={scale:F2} rotate={rotate:F2} (map enabled={folding.enabled})");
        }

        /// <summary>
        /// Drives the virtual mouse's left button directly from the right trigger.
        /// We do this ourselves instead of via VirtualMouseInput.leftButtonAction
        /// because that path only reads the action's `started`/`canceled` events and
        /// assumes a digital button — an analog trigger fires `started` below the
        /// press point (where IsPressed() is false), so the button never registered.
        ///
        /// Runs from InputSystem.onAfterUpdate (NOT MonoBehaviour.Update): the input
        /// update happens before script Updates, so the button change is visible to
        /// every consumer's `wasPressedThisFrame` the same frame. Driving it from
        /// Update() raced with FoldDragHandle/StickerUIController's Update order and
        /// the one-frame `wasPressedThisFrame` flag was missed entirely.
        /// </summary>
        private void DriveClick()
        {
            if (!_active)
                return;
            Mouse vm = _virtualMouse != null ? _virtualMouse.virtualMouse : null;
            if (vm == null)
                return;

            SyncCursorVisual(vm);

            // A real mouse's delta is reset to zero by the backend every frame,
            // but VirtualMouseInput just stops writing when the stick goes idle,
            // leaving the last nonzero delta on the device forever. Any later
            // state change (like our click events below, which copy full device
            // state) re-broadcasts that stale delta, and the Folding RotateDelta
            // action (bound to <Mouse>/delta) latches onto it, so the paper spins
            // by itself while RotateToggle is held. Zeroing right after
            // VirtualMouseInput's motion update keeps cursor movement out of
            // mouse-delta bindings entirely (paper rotation belongs to the right
            // stick, not the cursor stick).
            if (vm.delta.ReadValue() != Vector2.zero)
                InputState.Change(vm.delta, Vector2.zero);

            // RT acts as the virtual left mouse button: hold to drag the fold
            // handle, tap to click UI buttons / place stickers. (South is taken
            // by Recenter in the folding scheme, so it cannot double as click.)
            Gamepad pad = Gamepad.current;
            bool pressed = pad != null && pad.rightTrigger.ReadValue() >= _clickThreshold;
            if (pressed == _clickDown)
                return;

            _clickDown = pressed;
            vm.CopyState(out MouseState state);
            state.WithButton(MouseButton.Left, pressed);
            InputState.Change(vm, state);

            if (_debugLogging)
            {
                Debug.Log($"[VirtualCursor] RT click={pressed} | Mouse.current={(Mouse.current == vm ? "virtual" : Mouse.current != null ? Mouse.current.name : "null")} | cursorPos={vm.position.ReadValue()}");
                if (pressed)
                    LogUiRaycast(vm.position.ReadValue());
            }
        }

        /// <summary>
        /// Places the cursor visual exactly at the virtual mouse's screen position.
        /// On a Screen Space Overlay canvas, RectTransform.position IS screen pixels,
        /// so this stays correct for any CanvasScaler configuration. Runs right after
        /// VirtualMouseInput's own motion update (both on InputSystem.onAfterUpdate;
        /// it subscribed first).
        /// </summary>
        private void SyncCursorVisual(Mouse vm)
        {
            if (_cursorRect == null)
                return;
            Vector2 screenPosition = vm.position.ReadValue();
            _cursorRect.position = new Vector3(screenPosition.x, screenPosition.y, 0f);
        }

        /// <summary>TEMP: reports what UI element sits under the cursor position.</summary>
        private static void LogUiRaycast(Vector2 screenPosition)
        {
            if (EventSystem.current == null)
            {
                Debug.Log("[VirtualCursor] UI raycast: no EventSystem");
                return;
            }

            var eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            Debug.Log(results.Count > 0
                ? $"[VirtualCursor] UI raycast hit: {results[0].gameObject.name} ({results.Count} total)"
                : "[VirtualCursor] UI raycast hit nothing");
        }

        private void OnDisable()
        {
            if (_active)
                SetActive(false);
        }

        private void OnDestroy()
        {
            if (_cursorObject != null)
                Destroy(_cursorObject);
            _stickAction?.Dispose();
        }

        // ── Build ───────────────────────────────────────────────────────────

        private void TryBuild()
        {
            _canvas = ResolveCanvas();
            if (_canvas == null)
                return; // HUD not ready yet; retry next frame.

            EnsureEventSystem();

            // Cursor UI object, last child of the canvas so it draws on top.
            _cursorObject = new GameObject("VirtualCursor", typeof(RectTransform));
            _cursorRect = _cursorObject.GetComponent<RectTransform>();
            _cursorRect.SetParent(_canvas.transform, false);
            // Bottom-left anchor so anchoredPosition maps directly to screen space.
            _cursorRect.anchorMin = Vector2.zero;
            _cursorRect.anchorMax = Vector2.zero;
            _cursorRect.pivot = new Vector2(0.5f, 0.5f);
            _cursorRect.sizeDelta = new Vector2(_cursorSize, _cursorSize);
            _cursorRect.SetAsLastSibling();

            _cursorImage = _cursorObject.AddComponent<Image>();
            _cursorImage.raycastTarget = false; // never let the cursor block its own raycasts
            _cursorImage.sprite = _cursorSprite != null ? _cursorSprite : GenerateCursorSprite();

            // Left stick moves the cursor. Kept on this component so VirtualMouseInput
            // enables/disables it with the cursor object. The click (right trigger) is
            // NOT routed through VirtualMouseInput — see DriveClick().
            _stickAction = new InputAction("VirtualCursorMove", InputActionType.Value, expectedControlType: "Vector2");
            _stickAction.AddBinding("<Gamepad>/leftStick").WithProcessor("stickDeadzone");

            // Deactivate before adding VirtualMouseInput so its OnEnable (which adds
            // the virtual mouse device) is deferred until folding actually starts.
            _cursorObject.SetActive(false);

            _virtualMouse = _cursorObject.AddComponent<VirtualMouseInput>();
            _virtualMouse.cursorMode = VirtualMouseInput.CursorMode.SoftwareCursor;
            _virtualMouse.cursorGraphic = _cursorImage;
            // cursorTransform is deliberately NOT assigned. VirtualMouseInput places
            // it using per-axis pixelRect/referenceResolution factors, which disagree
            // with CanvasScaler's uniform match-width factor (HUD: 800x600, match=0)
            // on non-4:3 screens — the visual drifts vertically away from the real
            // pointer position. We place the rect ourselves in SyncCursorVisual().
            _virtualMouse.cursorSpeed = _cursorSpeed;
            _virtualMouse.stickAction = new InputActionProperty(_stickAction);

            _built = true;
            if (_debugLogging)
                Debug.Log($"[VirtualCursor] Built. canvas={_canvas.name}, EventSystem.current={(EventSystem.current != null ? EventSystem.current.name : "null")}");
        }

        private Canvas ResolveCanvas()
        {
            if (HUDCanvas.Instance != null)
            {
                Canvas hudCanvas = HUDCanvas.Instance.GetComponentInParent<Canvas>();
                if (hudCanvas != null)
                    return hudCanvas.rootCanvas != null ? hudCanvas.rootCanvas : hudCanvas;
            }

            return FindAnyObjectByType<Canvas>();
        }

        // ── Activation ──────────────────────────────────────────────────────

        private void SetActive(bool active)
        {
            _active = active;

            if (active)
            {
                _cursorObject.SetActive(true); // VirtualMouseInput.OnEnable adds the virtual mouse device

                // Center the pointer each time folding begins (the visual follows
                // via SyncCursorVisual). cursorTransform is not assigned, so
                // VirtualMouseInput has nothing to seed the position from itself.
                if (_virtualMouse.virtualMouse != null)
                {
                    var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                    InputState.Change(_virtualMouse.virtualMouse.position, center);
                    SyncCursorVisual(_virtualMouse.virtualMouse);
                }

                // Drive the click during the input update (before script Updates)
                // so wasPressedThisFrame is visible to consumers this same frame.
                InputSystem.onAfterUpdate += DriveClick;

                NeutralizeUiNavigation();

                // Default to mouse mode; switches to gamepad on the first stick input
                // so mouse-only users keep their normal cursor.
                _gamepadMode = false;
                ApplyCursorVisibility();

                if (_debugLogging)
                    Debug.Log($"[VirtualCursor] Activated. virtualMouse added={_virtualMouse.virtualMouse != null}, foldingMapEnabled={(InputManager.Instance != null && InputManager.Instance.Actions != null ? InputManager.Instance.Actions.Folding.enabled.ToString() : "n/a")}");
            }
            else
            {
                InputSystem.onAfterUpdate -= DriveClick;
                _clickDown = false;

                RestoreUiNavigation();

                if (_cursorObject != null)
                    _cursorObject.SetActive(false); // removes the virtual mouse device

                // Hand cursor state back to whatever set it (InputManager mode switch).
                if (_cursorImage != null)
                    _cursorImage.enabled = true;
            }
        }

        // ── Device-based cursor visibility ──────────────────────────────────

        private void UpdateDeviceVisibility()
        {
            bool gamepadActive = false;
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                gamepadActive = pad.leftStick.ReadValue().sqrMagnitude > 0.04f
                    || pad.rightTrigger.ReadValue() > 0.5f;
            }

            Mouse mouse = GetSystemMouse();
            bool mouseActive = mouse != null
                && (mouse.delta.ReadValue().sqrMagnitude > 1f
                    || mouse.leftButton.isPressed
                    || mouse.rightButton.isPressed);

            if (gamepadActive && !_gamepadMode)
            {
                _gamepadMode = true;
                ApplyCursorVisibility();
            }
            else if (mouseActive && _gamepadMode)
            {
                _gamepadMode = false;
                ApplyCursorVisibility();
            }
        }

        private void ApplyCursorVisibility()
        {
            if (_cursorImage != null)
                _cursorImage.enabled = _gamepadMode;
            Cursor.visible = !_gamepadMode;
        }

        /// <summary>Returns the physical mouse (the one that is not the virtual mouse).</summary>
        private Mouse GetSystemMouse()
        {
            Mouse virtualMouse = _virtualMouse != null ? _virtualMouse.virtualMouse : null;
            if (_systemMouse != null && _systemMouse.added && _systemMouse != virtualMouse)
                return _systemMouse;

            _systemMouse = null;
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                if (devices[i] is Mouse m && m != virtualMouse)
                {
                    _systemMouse = m;
                    break;
                }
            }
            return _systemMouse;
        }

        // ── UI navigation neutralization ────────────────────────────────────

        private void EnsureEventSystem()
        {
            // Avoid creating a duplicate — LevelStarter already provides one. Only
            // create if none exists at all (e.g. running the scene in isolation).
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            InputSystemUIInputModule module = go.GetComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            if (_debugLogging)
                Debug.Log("[VirtualCursor] No EventSystem found — created one.");
        }

        private void NeutralizeUiNavigation()
        {
            if (_navNeutralized)
                return;

            _uiModule = ResolveUiModule();
            if (_uiModule == null)
                return;

            _savedMove = _uiModule.move;
            _savedSubmit = _uiModule.submit;
            _savedCancel = _uiModule.cancel;
            _savedPointerBehavior = _uiModule.pointerBehavior;

            _uiModule.move = null;
            _uiModule.submit = null;
            _uiModule.cancel = null;

            // Give every pointer device its own UI pointer. The default
            // SingleMouseOrPen behavior merges all mice into one pointer, which
            // does not reliably track a second (virtual) mouse — the cursor then
            // never hovers or clicks UI.
            _uiModule.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
            _navNeutralized = true;

            if (_debugLogging)
                LogUiModuleState();
        }

        private void RestoreUiNavigation()
        {
            if (!_navNeutralized || _uiModule == null)
            {
                _navNeutralized = false;
                return;
            }

            _uiModule.move = _savedMove;
            _uiModule.submit = _savedSubmit;
            _uiModule.cancel = _savedCancel;
            _uiModule.pointerBehavior = _savedPointerBehavior;
            _navNeutralized = false;
        }

        /// <summary>TEMP: reports whether the UI module resolved the virtual mouse.</summary>
        private void LogUiModuleState()
        {
            Mouse vm = _virtualMouse != null ? _virtualMouse.virtualMouse : null;
            InputAction point = _uiModule.point != null ? _uiModule.point.action : null;

            bool pointSeesVirtualMouse = false;
            if (point != null && vm != null)
            {
                foreach (InputControl control in point.controls)
                {
                    if (control.device == vm)
                    {
                        pointSeesVirtualMouse = true;
                        break;
                    }
                }
            }

            Debug.Log($"[VirtualCursor] UI module '{_uiModule.name}': pointEnabled={point?.enabled}, "
                + $"pointSeesVirtualMouse={pointSeesVirtualMouse}, pointerBehavior={_uiModule.pointerBehavior}, "
                + $"actionsAsset={(_uiModule.actionsAsset != null ? _uiModule.actionsAsset.name : "null")}");
        }

        private InputSystemUIInputModule ResolveUiModule()
        {
            if (EventSystem.current != null)
            {
                InputSystemUIInputModule module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
                if (module != null)
                    return module;
            }
            return FindAnyObjectByType<InputSystemUIInputModule>();
        }

        // ── Placeholder cursor sprite ───────────────────────────────────────

        private static Sprite GenerateCursorSprite()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VirtualCursorPlaceholder",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float outer = size * 0.42f;
            float inner = size * 0.24f;
            var clear = new Color(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    Color c;
                    if (d <= inner)
                        c = Color.white;
                    else if (d <= outer)
                        c = Color.black; // dark ring outline for contrast on any background
                    else
                        c = clear;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
