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
    }
}
