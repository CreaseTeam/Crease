using System.Collections.Generic;
using System.IO;
using System.Text;
using Crease.Handwritting;
using UnityEditor;
using UnityEngine;

namespace Crease.Handwritting.Editor
{
    public static class HandwrittenBMFontExporter
    {
        public static void Export(
            HandwrittenFontAsset font,
            Texture2D inkAtlas,
            IReadOnlyList<HandwrittenBakedGlyph> bakedGlyphs,
            int cellSize,
            int atlasWidth,
            int atlasHeight,
            string fontDirectory)
        {
            string baseName = font.name;
            string pngFileName = baseName + "_BMFont.png";
            string fntFileName = baseName + "_BMFont.fnt";
            string pngPath = Path.Combine(fontDirectory, pngFileName).Replace('\\', '/');
            string fntPath = Path.Combine(fontDirectory, fntFileName).Replace('\\', '/');

            byte[] pngBytes = inkAtlas.EncodeToPNG();
            File.WriteAllBytes(pngPath, pngBytes);

            int lineHeight = cellSize;
            int baseLine = Mathf.RoundToInt(cellSize * (1f - HandwrittenFontLayout.BaselineNormalized));

            var fnt = new StringBuilder();
            fnt.AppendLine(
                $"info face=\"{EscapeQuotes(baseName)}\" size={cellSize} bold=0 italic=0 charset=\"\" unicode=1 stretchH=100 smooth=1 aa=1 padding=0,0,0,0 spacing=0,0 outline=0");
            fnt.AppendLine(
                $"common lineHeight={lineHeight} base={baseLine} scaleW={atlasWidth} scaleH={atlasHeight} pages=1 packed=0 alphaChnl=1 redChnl=0 greenChnl=0 blueChnl=0");
            fnt.AppendLine($"page id=0 file=\"{pngFileName}\"");
            fnt.AppendLine($"chars count={bakedGlyphs.Count}");

            foreach (HandwrittenBakedGlyph baked in bakedGlyphs)
            {
                int atlasY = atlasHeight - baked.AtlasY - cellSize;
                int xOffset = Mathf.RoundToInt(baked.BearingX);
                int xAdvance = Mathf.RoundToInt(baked.Advance);

                fnt.AppendLine(
                    $"char id={(int)baked.Character} x={baked.AtlasX} y={atlasY} width={cellSize} height={cellSize} xoffset={xOffset} yoffset=0 xadvance={xAdvance} page=0 chnl=15");
            }

            File.WriteAllText(fntPath, fnt.ToString(), Encoding.UTF8);

            ConfigureImportedTexture(pngPath);

            Debug.Log($"Exported BMFont to {fntPath}");
        }

        static string EscapeQuotes(string value)
        {
            return value.Replace("\"", "\\\"");
        }

        static void ConfigureImportedTexture(string pngPath)
        {
            AssetDatabase.Refresh();

            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
