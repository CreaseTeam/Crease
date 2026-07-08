using System.Collections;
using System.Collections.Generic;
using Crease.Flying.Player;
using Crease.Flying.Player.Camera;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("_disappear")]
        [Tooltip("How the text disappears after the linger time.")]
        HandwrittenTextDisappearMode _disappearMode;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds to keep fully visible text on screen before disappearing.")]
        float _lingerTime = 2f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Seconds for the disappear effect. Used for fade out and letter collection.")]
        float _fadeOutDuration = 0.5f;

        [SerializeField]
        [Tooltip("Destination for collected letters. Uses the first KinematicBody in the scene when unset.")]
        Transform _letterCollectionTarget;

        TextMeshPro _text;
        Material _materialInstance;
        Coroutine _playRoutine;
        Transform _streamingLettersRoot;

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

        public bool IsPlaying => _playRoutine != null;

        void Awake()
        {
            _text = GetComponent<TextMeshPro>();
            ResolveCameraController();
            ResolveLetterCollectionTarget();
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

            ClearStreamingLetters();

            if (_materialInstance != null)
                Destroy(_materialInstance);
        }

        void ResolveLetterCollectionTarget()
        {
            if (_letterCollectionTarget != null)
                return;

            KinematicBody playerBody = FindFirstObjectByType<KinematicBody>();
            if (playerBody != null)
                _letterCollectionTarget = playerBody.transform;
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
            {
                StopCoroutine(_playRoutine);
                _playRoutine = null;
            }

            ClearStreamingLetters();

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

            ClearStreamingLetters();

            SetText(ResolveText(text));
            ResetTextVisibility();
            SetFullyVisible();
            TryStartCameraCapture();

            if (_disappearMode != HandwrittenTextDisappearMode.None)
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

            if (_disappearMode != HandwrittenTextDisappearMode.None)
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

            switch (_disappearMode)
            {
                case HandwrittenTextDisappearMode.FadeOut:
                    yield return FadeOutRoutine();
                    break;
                case HandwrittenTextDisappearMode.LetterCollection:
                    yield return LetterCollectionRoutine();
                    break;
            }

            _playRoutine = null;
        }

        IEnumerator FadeOutRoutine()
        {
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
        }

        IEnumerator LetterCollectionRoutine()
        {
            ResolveLetterCollectionTarget();
            if (_letterCollectionTarget == null)
            {
                Debug.LogWarning($"{nameof(HandwrittenTextPlayer)} on {name} could not find a letter collection target.");
                yield return FadeOutRoutine();
                yield break;
            }

            HandwrittenTMPMeshModifier.InjectCharacterIndices(_text);
            TMP_TextInfo textInfo = _text.textInfo;
            List<int> visibleCharIndices = new List<int>(textInfo.characterCount);
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (textInfo.characterInfo[i].isVisible)
                    visibleCharIndices.Add(i);
            }

            int visibleCount = visibleCharIndices.Count;
            if (visibleCount == 0)
            {
                _text.alpha = 0f;
                yield break;
            }

            float interval = _fadeOutDuration / visibleCount;
            float flyDuration = Mathf.Max(interval, _fadeOutDuration * 0.35f);

            for (int streamIndex = 0; streamIndex < visibleCount; streamIndex++)
            {
                int charIndex = visibleCharIndices[streamIndex];
                TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];
                SpawnStreamingLetter(charInfo, flyDuration);
                HandwrittenTMPMeshModifier.SetCharacterVisible(_text, charIndex, false);

                if (streamIndex < visibleCount - 1)
                    yield return new WaitForSeconds(interval);
            }

            yield return new WaitForSeconds(flyDuration);
            _text.alpha = 0f;
        }

        void SpawnStreamingLetter(TMP_CharacterInfo charInfo, float flyDuration)
        {
            EnsureStreamingLettersRoot();

            Vector3 sourceLocalCenter = (charInfo.bottomLeft + charInfo.topRight) * 0.5f;

            GameObject letterObject = new GameObject("StreamingLetter");
            letterObject.transform.SetParent(_streamingLettersRoot, false);

            TextMeshPro letterText = letterObject.AddComponent<TextMeshPro>();
            CopyTextMeshSettings(_text, letterText);
            letterText.text = charInfo.character.ToString();
            letterText.fontSharedMaterial = new Material(_materialInstance);
            letterText.ForceMeshUpdate();
            HandwrittenTMPMeshModifier.InjectCharacterIndices(letterText);

            if (letterText.textInfo.characterCount > 0 && letterText.textInfo.characterInfo[0].isVisible)
            {
                TMP_CharacterInfo spawnedCharInfo = letterText.textInfo.characterInfo[0];
                Vector3 spawnedLocalCenter = (spawnedCharInfo.bottomLeft + spawnedCharInfo.topRight) * 0.5f;
                letterObject.transform.localPosition = sourceLocalCenter - spawnedLocalCenter;
            }
            else
            {
                letterObject.transform.localPosition = sourceLocalCenter;
            }

            HandwrittenStreamingLetter streamingLetter = letterObject.AddComponent<HandwrittenStreamingLetter>();
            streamingLetter.Begin(
                _letterCollectionTarget,
                flyDuration,
                letterText.fontSharedMaterial,
                _text.transform.lossyScale);
        }

        static void CopyTextMeshSettings(TextMeshPro source, TextMeshPro destination)
        {
            destination.font = source.font;
            destination.fontSize = source.fontSize;
            destination.fontStyle = source.fontStyle;
            destination.color = source.color;
            destination.characterSpacing = source.characterSpacing;
            destination.wordSpacing = source.wordSpacing;
            destination.lineSpacing = source.lineSpacing;
            destination.paragraphSpacing = source.paragraphSpacing;
            destination.alignment = source.alignment;
            destination.enableWordWrapping = false;
            destination.overflowMode = TextOverflowModes.Overflow;
        }

        void EnsureStreamingLettersRoot()
        {
            if (_streamingLettersRoot != null)
                return;

            GameObject rootObject = new GameObject("StreamingLetters");
            rootObject.transform.SetParent(_text.transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            _streamingLettersRoot = rootObject.transform;
        }

        void ClearStreamingLetters()
        {
            if (_streamingLettersRoot == null)
                return;

            Destroy(_streamingLettersRoot.gameObject);
            _streamingLettersRoot = null;
        }

        float GetWriteDurationForCharacter(char character)
        {
            if (_font.TryGetGlyph(character, out HandwrittenGlyph glyph))
                return HandwrittenFontLayout.GetWriteDuration(glyph, _font);

            return HandwrittenFontLayout.GetWriteDuration(null, _font);
        }
    }
}
