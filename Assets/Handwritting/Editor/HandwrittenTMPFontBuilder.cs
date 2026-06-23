using System.Collections.Generic;
using Crease.Handwritting;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace Crease.Handwritting.Editor
{
    public static class HandwrittenTMPFontBuilder
    {
        const string ShaderName = "Crease/Handwritting/TMP Bitmap Reveal";

        public static TMP_FontAsset BuildOrUpdate(
            HandwrittenFontAsset source,
            Texture2D atlas,
            Texture2D revealAtlas,
            IReadOnlyList<HandwrittenBakedGlyph> bakedGlyphs,
            string fontAssetPath,
            string materialPath)
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            if (fontAsset == null)
            {
                fontAsset = ScriptableObject.CreateInstance<TMP_FontAsset>();
                AssetDatabase.CreateAsset(fontAsset, fontAssetPath);
            }

            int cellSize = source.AtlasCellSize;
            float baselinePx = HandwrittenFontLayout.ComputeBaselinePixels(cellSize);
            float bearingY = HandwrittenFontLayout.ComputeBearingY(cellSize);

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.atlasTextures = new[] { atlas };
            ApplyAtlasSettings(fontAsset, atlas, padding: 1);

            fontAsset.faceInfo = new FaceInfo
            {
                familyName = source.name,
                styleName = "Handwritten",
                pointSize = cellSize,
                scale = 1f,
                lineHeight = cellSize,
                ascentLine = cellSize,
                capLine = cellSize * 0.7f,
                meanLine = cellSize * 0.5f,
                baseline = baselinePx,
                descentLine = 0f,
                underlineOffset = -cellSize * 0.1f,
                underlineThickness = cellSize * 0.05f,
                strikethroughOffset = cellSize * 0.35f,
                strikethroughThickness = cellSize * 0.05f,
                tabWidth = cellSize * 0.5f,
            };

            fontAsset.glyphTable.Clear();
            fontAsset.characterTable.Clear();

            uint glyphIndex = 1;
            foreach (HandwrittenBakedGlyph baked in bakedGlyphs)
            {
                int cellWidth = cellSize;
                int cellHeight = cellSize;

                float bearingX = baked.BearingX;
                float advance = baked.Advance;

                var metrics = new GlyphMetrics(
                    cellWidth,
                    cellHeight,
                    bearingX,
                    bearingY,
                    advance);

                var rect = new GlyphRect(baked.AtlasX, baked.AtlasY, cellSize, cellSize);
                var glyph = new Glyph(glyphIndex, metrics, rect, 1f, 0);
                fontAsset.glyphTable.Add(glyph);

                var character = new TMP_Character((uint)baked.Character, fontAsset, glyph);
                fontAsset.characterTable.Add(character);
                glyphIndex++;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogError($"Shader '{ShaderName}' was not found.");
                    return fontAsset;
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.SetTexture(ShaderUtilities.ID_MainTex, atlas);
            material.SetTexture("_RevealAtlas", revealAtlas);
            material.SetFloat(ShaderUtilities.ID_TextureWidth, atlas.width);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, atlas.height);
            material.SetColor(ShaderUtilities.ID_FaceColor, Color.white);

            fontAsset.material = material;
            fontAsset.ReadFontAssetDefinition();

            EditorUtility.SetDirty(fontAsset);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            return fontAsset;
        }

        static void ApplyAtlasSettings(TMP_FontAsset fontAsset, Texture2D atlas, int padding)
        {
            SerializedObject serializedFont = new SerializedObject(fontAsset);
            serializedFont.FindProperty("m_AtlasWidth").intValue = atlas.width;
            serializedFont.FindProperty("m_AtlasHeight").intValue = atlas.height;
            serializedFont.FindProperty("m_AtlasPadding").intValue = padding;
            serializedFont.FindProperty("m_AtlasRenderMode").intValue = (int)GlyphRenderMode.RASTER;
            serializedFont.FindProperty("m_Version").stringValue = "1.1.0";
            serializedFont.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
