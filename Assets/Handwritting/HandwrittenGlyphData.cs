using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crease.Handwritting
{
    [Serializable]
    public class HandwrittenStroke
    {
        public List<Vector2> Points = new List<Vector2>();
    }

    [Serializable]
    public class HandwrittenGlyph
    {
        public char Character;
        public List<HandwrittenStroke> Strokes = new List<HandwrittenStroke>();

        /// <summary>Normalized cell X (0–1) where this glyph's ink begins. 0 = auto from ink bounds on bake.</summary>
        public float StartNormalized;

        /// <summary>Normalized cell width (0–1) where the cursor advances after this glyph. 0 = auto from ink bounds on bake.</summary>
        public float AdvanceNormalized;

        /// <summary>Seconds to write this glyph. 0 = use the font's DefaultWriteDuration at runtime.</summary>
        public float WriteDuration;
    }
}
