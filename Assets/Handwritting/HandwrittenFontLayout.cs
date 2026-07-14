using UnityEngine;

namespace Crease.Handwritting
{
    public static class HandwrittenFontLayout
    {
        /// <summary>
        /// Normalized Y of the editor baseline in each glyph cell (0 = bottom, 1 = top).
        /// </summary>
        public const float BaselineNormalized = 0.25f;

        public static float ComputeStartPixels(
            HandwrittenGlyph glyph,
            HandwrittenFontAsset font,
            int cellSize,
            int inkMinX,
            bool hasInk)
        {
            if (glyph != null && glyph.StartNormalized > 0f)
                return glyph.StartNormalized * cellSize;

            if (hasInk)
                return Mathf.Max(0f, inkMinX - font.GlobalStartPadding);

            return 0f;
        }

        /// <summary>
        /// Horizontal bearing for TMP: negated start position so the editor start line
        /// (cursor position in the cell) aligns with the pen when the full cell is the glyph quad.
        /// </summary>
        public static float ComputeBearingX(
            HandwrittenGlyph glyph,
            HandwrittenFontAsset font,
            int cellSize,
            int inkMinX,
            bool hasInk)
        {
            return -ComputeStartPixels(glyph, font, cellSize, inkMinX, hasInk);
        }

        public static float ComputeBaselinePixels(int cellSize)
        {
            return cellSize * BaselineNormalized;
        }

        /// <summary>
        /// Vertical bearing for TMP: positions the full cell so the editor baseline guide
        /// aligns with the font baseline, independent of ink bounds (descenders stay below).
        /// </summary>
        public static float ComputeBearingY(int cellSize)
        {
            return cellSize - ComputeBaselinePixels(cellSize);
        }

        public static float ComputeAdvanceLinePixels(
            HandwrittenGlyph glyph,
            HandwrittenFontAsset font,
            int cellSize,
            int inkMaxX,
            bool hasInk)
        {
            float advanceLine;
            if (glyph != null && glyph.AdvanceNormalized > 0f)
                advanceLine = glyph.AdvanceNormalized * cellSize;
            else if (hasInk)
                advanceLine = inkMaxX + font.GlobalAdvancePadding;
            else
                advanceLine = cellSize * 0.35f;

            return advanceLine + font.GlobalLetterSpacing;
        }

        /// <summary>
        /// TMP horizontal advance: distance from the start line (pen) to the advance line.
        /// This matches the visual width between the editor guides.
        /// </summary>
        public static float ComputeHorizontalAdvance(
            HandwrittenGlyph glyph,
            HandwrittenFontAsset font,
            int cellSize,
            int inkMinX,
            int inkMaxX,
            bool hasInk)
        {
            float startPx = ComputeStartPixels(glyph, font, cellSize, inkMinX, hasInk);
            float advanceLinePx = ComputeAdvanceLinePixels(glyph, font, cellSize, inkMaxX, hasInk);
            return Mathf.Max(1f, advanceLinePx - startPx);
        }

        public static float GetPreviewStartNormalized(
            HandwrittenGlyph glyph,
            HandwrittenFontAsset font,
            int cellSize)
        {
            if (glyph != null && glyph.StartNormalized > 0f)
                return Mathf.Clamp01(glyph.StartNormalized);

            float estimatedInkMinX = EstimateInkMinXFromStrokes(glyph, font.LineWidth, cellSize);
            bool hasInk = estimatedInkMinX >= 0f;
            float startPx = ComputeStartPixels(glyph, font, cellSize, Mathf.RoundToInt(estimatedInkMinX), hasInk);
            return Mathf.Clamp01(startPx / cellSize);
        }

        public static float GetPreviewAdvanceNormalized(
            HandwrittenGlyph glyph,
            HandwrittenFontAsset font,
            int cellSize)
        {
            float estimatedInkMaxX = EstimateInkMaxXFromStrokes(glyph, font.LineWidth, cellSize);
            bool hasInk = estimatedInkMaxX >= 0f;
            float advanceLinePx = ComputeAdvanceLinePixels(glyph, font, cellSize, Mathf.RoundToInt(estimatedInkMaxX), hasInk);
            return Mathf.Clamp01(advanceLinePx / cellSize);
        }

        public static float EstimateInkMinXFromStrokes(HandwrittenGlyph glyph, float lineWidth, int cellSize)
        {
            if (glyph == null || glyph.Strokes.Count == 0)
                return -1f;

            float minNormalizedX = 1f;
            foreach (HandwrittenStroke stroke in glyph.Strokes)
            {
                foreach (Vector2 point in stroke.Points)
                    minNormalizedX = Mathf.Min(minNormalizedX, point.x);
            }

            return minNormalizedX * cellSize - lineWidth;
        }

        public static float EstimateInkMaxXFromStrokes(HandwrittenGlyph glyph, float lineWidth, int cellSize)
        {
            if (glyph == null || glyph.Strokes.Count == 0)
                return -1f;

            float maxNormalizedX = 0f;
            foreach (HandwrittenStroke stroke in glyph.Strokes)
            {
                foreach (Vector2 point in stroke.Points)
                    maxNormalizedX = Mathf.Max(maxNormalizedX, point.x);
            }

            return maxNormalizedX * cellSize + lineWidth;
        }

        public static float GetWriteDuration(HandwrittenGlyph glyph, HandwrittenFontAsset font)
        {
            if (glyph != null && glyph.WriteDuration > 0f)
                return glyph.WriteDuration;

            return Mathf.Max(0.01f, font.DefaultWriteDuration);
        }
    }
}
