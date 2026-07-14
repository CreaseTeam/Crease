using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crease.Folding.PaperSurface.Stickers
{
    [Serializable]
    public class StickerEntry
    {
        public string DisplayName = "Sticker";
        public Texture2D Texture;
        [Tooltip("Default size relative to paper width (1 = full width).")]
        public float DefaultScale = 0.15f;
    }

    [CreateAssetMenu(fileName = "StickerLibrary", menuName = "Crease/Sticker Library")]
    public class StickerLibrary : ScriptableObject
    {
        public List<StickerEntry> Stickers = new List<StickerEntry>();
    }
}
