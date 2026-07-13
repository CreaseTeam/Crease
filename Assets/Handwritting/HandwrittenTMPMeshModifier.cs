using TMPro;
using UnityEngine;

namespace Crease.Handwritting
{
    public static class HandwrittenTMPMeshModifier
    {
        public static void InjectCharacterIndices(TMP_Text text)
        {
            if (text == null)
                return;

            text.ForceMeshUpdate();

            TMP_TextInfo textInfo = text.textInfo;
            if (textInfo == null)
                return;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                    continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;
                Vector2[] uvs2 = textInfo.meshInfo[materialIndex].uvs2;

                Vector2 uv = new Vector2(i, 0f);
                uvs2[vertexIndex + 0] = uv;
                uvs2[vertexIndex + 1] = uv;
                uvs2[vertexIndex + 2] = uv;
                uvs2[vertexIndex + 3] = uv;
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Uv2);
        }

        public static void SetCharacterVisible(TMP_Text text, int charIndex, bool visible)
        {
            if (text == null)
                return;

            TMP_TextInfo textInfo = text.textInfo;
            if (textInfo == null || charIndex < 0 || charIndex >= textInfo.characterCount)
                return;

            TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];
            if (!charInfo.isVisible)
                return;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;
            Color32[] colors = textInfo.meshInfo[materialIndex].colors32;
            byte alpha = visible ? (byte)Mathf.Clamp(Mathf.RoundToInt(charInfo.color.a * 255f), 0, 255) : (byte)0;

            for (int i = 0; i < 4; i++)
            {
                Color32 color = colors[vertexIndex + i];
                color.a = alpha;
                colors[vertexIndex + i] = color;
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
