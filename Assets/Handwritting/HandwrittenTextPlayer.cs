using System.Collections;
using Crease.Flying.Player.Camera;
using TMPro;
using UnityEngine;

namespace Crease.Handwritting
{
    [RequireComponent(typeof(TextMeshPro))]
    public class HandwrittenTextPlayer : MonoBehaviour
    {
        static readonly int ActiveCharIndexId = Shader.PropertyToID("_ActiveCharIndex");
        static readonly int ActiveCharProgressId = Shader.PropertyToID("_ActiveCharProgress");
        static readonly int InstantRevealId = Shader.PropertyToID("_InstantReveal");

        [SerializeField]
        HandwrittenFontAsset _font;

        [SerializeField]
        [Min(0.01f)]
        float _writeSpeed = 1f;

        [SerializeField]
        [Tooltip("Play the write-in animation using the TextMeshPro text when the scene starts.")]
        bool _playOnStart;

        [SerializeField]
        [Tooltip("Show all text immediately when the scene starts.")]
        bool _instantOnStart;

        [SerializeField]
        [Tooltip("Rotate the text each frame to face the billboard target.")]
        bool _billboardTowardsPlayer;

        [SerializeField]
        [Tooltip("Transform to face when billboarding. Uses the main camera when unset.")]
        Transform _billboardTarget;

        [SerializeField]
        [Tooltip("Smoothly rotate the player camera to look at this text when playback starts.")]
        bool _captureCameraOnPlay;

        [SerializeField]
        CameraController _cameraController;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds to blend the camera toward and away from the text.")]
        float _cameraCaptureTransitionDuration = 0.75f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds to keep the camera locked on the text before returning.")]
        float _cameraCaptureHoldDuration = 3f;

        [SerializeField]
        [Tooltip("Fade out after the text has fully appeared.")]
        bool _disappear;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds to keep fully visible text on screen before fading out.")]
        float _lingerTime = 2f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Seconds to fade the text out after the linger time.")]
        float _fadeOutDuration = 0.5f;

        TextMeshPro _text;
        Material _materialInstance;
        Coroutine _playRoutine;

        public HandwrittenFontAsset Font
        {
            get => _font;
            set
            {
                _font = value;
                ApplyFont();
            }
        }

        public float WriteSpeed
        {
            get => _writeSpeed;
            set => _writeSpeed = Mathf.Max(0.01f, value);
        }

        void Awake()
        {
            _text = GetComponent<TextMeshPro>();
            ResolveCameraController();
            ApplyFont();
        }

        void OnValidate()
        {
            if (_text == null)
                _text = GetComponent<TextMeshPro>();

            if (Application.isPlaying)
                ApplyFont();
        }

        void Start()
        {
            if (!EnsureReady())
                return;

            if (_instantOnStart)
                ShowInstant();
            else if (_playOnStart)
                PlayWriteIn();
        }

        void LateUpdate()
        {
            if (!_billboardTowardsPlayer)
                return;

            Transform target = _billboardTarget;
            if (target == null && UnityEngine.Camera.main != null)
                target = UnityEngine.Camera.main.transform;

            if (target == null)
                return;

            Vector3 forward = transform.position - target.position;
            if (forward.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        void OnDestroy()
        {
            if (_cameraController != null)
                _cameraController.CancelLookCapture();

            if (_materialInstance != null)
                Destroy(_materialInstance);
        }

        void ResolveCameraController()
        {
            if (_cameraController != null)
                return;

            if (UnityEngine.Camera.main == null)
                return;

            _cameraController = UnityEngine.Camera.main.GetComponentInParent<CameraController>();
        }

        void TryStartCameraCapture()
        {
            if (!_captureCameraOnPlay)
                return;

            ResolveCameraController();
            if (_cameraController == null)
            {
                Debug.LogWarning($"{nameof(HandwrittenTextPlayer)} on {name} could not find a {nameof(CameraController)} for camera capture.");
                return;
            }

            _cameraController.StartLookCapture(
                transform,
                _cameraCaptureTransitionDuration,
                _cameraCaptureHoldDuration,
                _cameraCaptureTransitionDuration);
        }

        public void PlayWriteIn()
        {
            PlayWriteIn(null);
        }

        public void PlayWriteIn(string text)
        {
            if (!EnsureReady())
                return;

            if (_playRoutine != null)
                StopCoroutine(_playRoutine);

            TryStartCameraCapture();
            _playRoutine = StartCoroutine(PlayWriteInRoutine(ResolveText(text)));
        }

        public void ShowInstant()
        {
            ShowInstant(null);
        }

        public void ShowInstant(string text)
        {
            if (!EnsureReady())
                return;

            if (_playRoutine != null)
            {
                StopCoroutine(_playRoutine);
                _playRoutine = null;
            }

            SetText(ResolveText(text));
            ResetTextVisibility();
            SetFullyVisible();
            TryStartCameraCapture();

            if (_disappear)
                _playRoutine = StartCoroutine(DisappearAfterLingerRoutine());
        }

        string ResolveText(string text)
        {
            if (!string.IsNullOrEmpty(text))
                return text;

            return _text.text;
        }

        void ResetTextVisibility()
        {
            _text.alpha = 1f;
        }

        void SetFullyVisible()
        {
            _materialInstance.SetFloat(InstantRevealId, 1f);
            _materialInstance.SetFloat(ActiveCharProgressId, 1f);

            int lastCharIndex = Mathf.Max(0, _text.textInfo.characterCount - 1);
            _materialInstance.SetFloat(ActiveCharIndexId, lastCharIndex);
        }

        void ApplyFont()
        {
            if (_text == null || _font == null)
                return;

            if (_font.FontAsset != null)
                _text.font = _font.FontAsset;

            if (_font.FontMaterial == null)
                return;

            if (_materialInstance != null)
                Destroy(_materialInstance);

            _materialInstance = Instantiate(_font.FontMaterial);
            _materialInstance.SetFloat(InstantRevealId, 0f);
            _materialInstance.SetFloat(ActiveCharProgressId, 0f);
            _materialInstance.SetFloat(ActiveCharIndexId, 0f);
            _text.fontSharedMaterial = _materialInstance;
        }

        bool EnsureReady()
        {
            if (_text == null)
                _text = GetComponent<TextMeshPro>();

            if (_font == null)
            {
                Debug.LogWarning($"{nameof(HandwrittenTextPlayer)} on {name} has no font assigned.");
                return false;
            }

            if (_font.FontAsset == null || _font.FontMaterial == null)
            {
                Debug.LogWarning($"{nameof(HandwrittenTextPlayer)} on {name} font has not been baked.");
                return false;
            }

            if (_materialInstance == null)
                ApplyFont();

            return _materialInstance != null;
        }

        void SetText(string text)
        {
            _text.text = text;
            HandwrittenTMPMeshModifier.InjectCharacterIndices(_text);
        }

        void ResetRevealState()
        {
            _materialInstance.SetFloat(InstantRevealId, 0f);
            _materialInstance.SetFloat(ActiveCharProgressId, 0f);
            _materialInstance.SetFloat(ActiveCharIndexId, 0f);
        }

        IEnumerator PlayWriteInRoutine(string text)
        {
            ResetRevealState();
            ResetTextVisibility();
            SetText(text);

            int charCount = _text.textInfo.characterCount;
            float speed = Mathf.Max(0.01f, _writeSpeed);

            for (int i = 0; i < charCount; i++)
            {
                _materialInstance.SetFloat(ActiveCharIndexId, i);

                if (!_text.textInfo.characterInfo[i].isVisible)
                {
                    _materialInstance.SetFloat(ActiveCharProgressId, 1f);
                    continue;
                }

                char character = _text.textInfo.characterInfo[i].character;
                float writeDuration = GetWriteDurationForCharacter(character) / speed;

                float elapsed = 0f;
                while (elapsed < writeDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / writeDuration);
                    _materialInstance.SetFloat(ActiveCharProgressId, progress);
                    yield return null;
                }

                _materialInstance.SetFloat(ActiveCharProgressId, 1f);
            }

            SetFullyVisible();

            if (_disappear)
                yield return DisappearAfterLingerRoutine();
            else
                _playRoutine = null;
        }

        IEnumerator DisappearAfterLingerRoutine()
        {
            float lingerElapsed = 0f;
            while (lingerElapsed < _lingerTime)
            {
                lingerElapsed += Time.deltaTime;
                yield return null;
            }

            float startAlpha = _text.alpha;
            float fadeElapsed = 0f;
            while (fadeElapsed < _fadeOutDuration)
            {
                fadeElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(fadeElapsed / _fadeOutDuration);
                _text.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            _text.alpha = 0f;
            _playRoutine = null;
        }

        float GetWriteDurationForCharacter(char character)
        {
            if (_font.TryGetGlyph(character, out HandwrittenGlyph glyph))
                return HandwrittenFontLayout.GetWriteDuration(glyph, _font);

            return HandwrittenFontLayout.GetWriteDuration(null, _font);
        }
    }
}
