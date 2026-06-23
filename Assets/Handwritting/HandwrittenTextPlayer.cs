using System.Collections;
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

        void OnDestroy()
        {
            if (_materialInstance != null)
                Destroy(_materialInstance);
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
            SetFullyVisible();
        }

        string ResolveText(string text)
        {
            if (!string.IsNullOrEmpty(text))
                return text;

            return _text.text;
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
