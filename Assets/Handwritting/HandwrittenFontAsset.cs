using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Handwritting
{
    [CreateAssetMenu(menuName = "Crease/Handwritting/Handwritten Font", fileName = "HandwrittenFont")]
    public class HandwrittenFontAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        public TMP_FontAsset FontAsset;
        public Texture2D RevealAtlas;
        public Material FontMaterial;

        public float LineWidth = 12f;
        public int AtlasCellSize = 256;

        [Tooltip("Padding before the leftmost ink pixel when start is auto-computed.")]
        public float GlobalStartPadding = 0f;

        [Tooltip("Padding after the rightmost ink pixel when advance is auto-computed.")]
        public float GlobalAdvancePadding = 4f;

        [Tooltip("Added to every glyph's advance. Use negative values to tighten spacing.")]
        public float GlobalLetterSpacing = 0f;

        [Tooltip("Write duration used when a glyph has no per-glyph WriteDuration set.")]
        [FormerlySerializedAs("SecondsPerGlyph")]
        public float DefaultWriteDuration = 0.35f;

        [SerializeField]
        List<HandwrittenGlyph> _glyphs = new List<HandwrittenGlyph>();

        Dictionary<char, HandwrittenGlyph> _glyphLookup = new Dictionary<char, HandwrittenGlyph>();

        public IReadOnlyList<HandwrittenGlyph> Glyphs => _glyphs;

        public bool TryGetGlyph(char character, out HandwrittenGlyph glyph)
        {
            return _glyphLookup.TryGetValue(character, out glyph);
        }

        public HandwrittenGlyph GetOrCreateGlyph(char character)
        {
            if (_glyphLookup.TryGetValue(character, out HandwrittenGlyph existing))
                return existing;

            var glyph = new HandwrittenGlyph { Character = character };
            _glyphs.Add(glyph);
            _glyphLookup[character] = glyph;
            return glyph;
        }

        public void RemoveGlyph(char character)
        {
            if (!_glyphLookup.TryGetValue(character, out HandwrittenGlyph glyph))
                return;

            _glyphLookup.Remove(character);
            _glyphs.Remove(glyph);
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            RebuildLookup();
        }

        void OnEnable()
        {
            RebuildLookup();
        }

        void RebuildLookup()
        {
            _glyphLookup = new Dictionary<char, HandwrittenGlyph>(_glyphs.Count);
            foreach (HandwrittenGlyph glyph in _glyphs)
            {
                if (glyph == null)
                    continue;

                if (_glyphLookup.ContainsKey(glyph.Character))
                {
                    Debug.LogWarning($"Duplicate handwritten glyph for '{glyph.Character}' in {name}.");
                    continue;
                }

                _glyphLookup[glyph.Character] = glyph;
            }
        }
    }
}
