using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Crease.Handwritting.Editor
{
    public static class HandwrittenFontBaker
    {
        public static void Bake(HandwrittenFontAsset font)
        {
            if (font == null)
            {
                Debug.LogError("HandwrittenFontBaker.Bake called with null font.");
                return;
            }

            if (font.Glyphs.Count == 0)
            {
                Debug.LogWarning($"Font '{font.name}' has no glyphs to bake.");
                return;
            }

            int cellSize = Mathf.Max(32, font.AtlasCellSize);
            float brushRadius = Mathf.Max(1f, font.LineWidth);

            var glyphsToBake = new List<HandwrittenGlyph>(font.Glyphs);
            glyphsToBake.Sort((a, b) => a.Character.CompareTo(b.Character));

            int count = glyphsToBake.Count;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            int atlasWidth = columns * cellSize;
            int atlasHeight = rows * cellSize;

            var inkPixels = new Color32[atlasWidth * atlasHeight];
            var revealPixels = new Color32[atlasWidth * atlasHeight];
            var bakedGlyphs = new List<HandwrittenBakedGlyph>(count);

            for (int i = 0; i < count; i++)
            {
                HandwrittenGlyph glyph = glyphsToBake[i];
                int col = i % columns;
                int row = i / columns;
                int originX = col * cellSize;
                int originY = row * cellSize;

                BakeGlyphCell(
                    font,
                    glyph,
                    cellSize,
                    brushRadius,
                    originX,
                    originY,
                    atlasWidth,
                    inkPixels,
                    revealPixels,
                    out HandwrittenBakedGlyph baked);

                bakedGlyphs.Add(baked);
            }

            Texture2D inkAtlas = GetOrCreateTextureAsset(font, "_InkAtlas", atlasWidth, atlasHeight);
            Texture2D revealAtlas = GetOrCreateTextureAsset(font, "_RevealAtlas", atlasWidth, atlasHeight);
            inkAtlas.SetPixels32(inkPixels);
            revealAtlas.SetPixels32(revealPixels);
            inkAtlas.Apply();
            revealAtlas.Apply();

            string fontDirectory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(font));
            string baseName = font.name;
            string tmpAssetPath = Path.Combine(fontDirectory, baseName + " TMP.asset").Replace('\\', '/');
            string materialPath = Path.Combine(fontDirectory, baseName + ".mat").Replace('\\', '/');

            TMP_FontAsset tmpFont = HandwrittenTMPFontBuilder.BuildOrUpdate(
                font,
                inkAtlas,
                revealAtlas,
                bakedGlyphs,
                tmpAssetPath,
                materialPath);

            font.FontAsset = tmpFont;
            font.RevealAtlas = revealAtlas;
            font.FontMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void BakeGlyphCell(
            HandwrittenFontAsset font,
            HandwrittenGlyph glyph,
            int cellSize,
            float brushRadius,
            int originX,
            int originY,
            int atlasWidth,
            Color32[] inkPixels,
            Color32[] revealPixels,
            out HandwrittenBakedGlyph baked)
        {
            bool[,] inkMask = new bool[cellSize, cellSize];
            float[,] revealTimes = new float[cellSize, cellSize];
            for (int y = 0; y < cellSize; y++)
            for (int x = 0; x < cellSize; x++)
                revealTimes[x, y] = float.MaxValue;

            float totalLength = 0f;
            var segmentLengths = new List<float>();
            foreach (HandwrittenStroke stroke in glyph.Strokes)
            {
                if (stroke.Points.Count < 2)
                    continue;

                for (int p = 1; p < stroke.Points.Count; p++)
                {
                    Vector2 a = stroke.Points[p - 1];
                    Vector2 b = stroke.Points[p];
                    float length = Vector2.Distance(a, b) * cellSize;
                    segmentLengths.Add(length);
                    totalLength += length;
                }
            }

            float distanceSoFar = 0f;
            int segmentIndex = 0;
            foreach (HandwrittenStroke stroke in glyph.Strokes)
            {
                if (stroke.Points.Count == 0)
                    continue;

                StampBrush(ToPixel(stroke.Points[0], cellSize), brushRadius, cellSize, inkMask, revealTimes, totalLength <= 0f ? 0f : distanceSoFar / totalLength);

                for (int p = 1; p < stroke.Points.Count; p++)
                {
                    Vector2 from = ToPixel(stroke.Points[p - 1], cellSize);
                    Vector2 to = ToPixel(stroke.Points[p], cellSize);
                    float segmentLength = segmentIndex < segmentLengths.Count ? segmentLengths[segmentIndex] : Vector2.Distance(from, to);
                    segmentIndex++;

                    int steps = Mathf.Max(1, Mathf.CeilToInt(segmentLength));
                    for (int step = 0; step <= steps; step++)
                    {
                        float t = step / (float)steps;
                        Vector2 point = Vector2.Lerp(from, to, t);
                        float arc = distanceSoFar + segmentLength * t;
                        float revealTime = totalLength <= 0f ? 0f : arc / totalLength;
                        StampBrush(point, brushRadius, cellSize, inkMask, revealTimes, revealTime);
                    }

                    distanceSoFar += segmentLength;
                }
            }

            int inkMinX = cellSize;
            int inkMinY = cellSize;
            int inkMaxX = -1;
            int inkMaxY = -1;

            for (int y = 0; y < cellSize; y++)
            {
                for (int x = 0; x < cellSize; x++)
                {
                    if (!inkMask[x, y])
                        continue;

                    inkMinX = Mathf.Min(inkMinX, x);
                    inkMinY = Mathf.Min(inkMinY, y);
                    inkMaxX = Mathf.Max(inkMaxX, x);
                    inkMaxY = Mathf.Max(inkMaxY, y);

                    int atlasX = originX + x;
                    int atlasY = originY + y;
                    int index = atlasY * atlasWidth + atlasX;
                    inkPixels[index] = new Color32(255, 255, 255, 255);
                    float reveal = revealTimes[x, y] == float.MaxValue ? 1f : revealTimes[x, y];
                    byte revealByte = (byte)Mathf.Clamp(Mathf.RoundToInt(reveal * 255f), 0, 255);
                    revealPixels[index] = new Color32(revealByte, 0, 0, 255);
                }
            }

            if (inkMaxX < 0)
            {
                inkMinX = 0;
                inkMinY = 0;
                inkMaxX = 0;
                inkMaxY = 0;
            }

            float advance = HandwrittenFontLayout.ComputeHorizontalAdvance(glyph, font, cellSize, inkMinX, inkMaxX, inkMaxX >= 0);
            float bearingX = HandwrittenFontLayout.ComputeBearingX(glyph, font, cellSize, inkMinX, inkMaxX >= 0);

            baked = new HandwrittenBakedGlyph
            {
                Character = glyph.Character,
                AtlasX = originX,
                AtlasY = originY,
                InkMinX = inkMinX,
                InkMinY = inkMinY,
                InkMaxX = inkMaxX,
                InkMaxY = inkMaxY,
                Advance = advance,
                BearingX = bearingX,
            };
        }

        static Vector2 ToPixel(Vector2 normalized, int cellSize)
        {
            return new Vector2(normalized.x * cellSize, normalized.y * cellSize);
        }

        static void StampBrush(
            Vector2 center,
            float radius,
            int cellSize,
            bool[,] inkMask,
            float[,] revealTimes,
            float revealTime)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius));
            int maxX = Mathf.Min(cellSize - 1, Mathf.CeilToInt(center.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius));
            int maxY = Mathf.Min(cellSize - 1, Mathf.CeilToInt(center.y + radius));
            float radiusSq = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - center.x;
                    float dy = y + 0.5f - center.y;
                    if (dx * dx + dy * dy > radiusSq)
                        continue;

                    inkMask[x, y] = true;
                    revealTimes[x, y] = Mathf.Min(revealTimes[x, y], revealTime);
                }
            }
        }

        static Texture2D GetOrCreateTextureAsset(HandwrittenFontAsset font, string suffix, int width, int height)
        {
            string fontPath = AssetDatabase.GetAssetPath(font);
            string directory = Path.GetDirectoryName(fontPath);
            string texturePath = Path.Combine(directory, font.name + suffix + ".asset").Replace('\\', '/');

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = font.name + suffix,
                };
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            else if (texture.width != width || texture.height != height)
            {
                texture.Reinitialize(width, height);
            }

            return texture;
        }
    }
}
