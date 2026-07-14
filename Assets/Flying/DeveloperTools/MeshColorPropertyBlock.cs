using UnityEngine;

namespace Crease.Flying.DeveloperTools
{
    public static class MeshColorPropertyBlock
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        public static bool TryGetColorPropertyId(MeshRenderer meshRenderer, out int colorPropertyId)
        {
            colorPropertyId = -1;

            if (meshRenderer == null)
                return false;

            Material material = meshRenderer.sharedMaterial;
            if (material == null)
                return false;

            if (material.HasProperty(BaseColorId))
            {
                colorPropertyId = BaseColorId;
                return true;
            }

            if (material.HasProperty(LegacyColorId))
            {
                colorPropertyId = LegacyColorId;
                return true;
            }

            return false;
        }

        public static bool TryGetColor(MeshRenderer meshRenderer, out Color color)
        {
            if (!TryGetColorPropertyId(meshRenderer, out int colorPropertyId))
            {
                color = default;
                return false;
            }

            color = meshRenderer.sharedMaterial.GetColor(colorPropertyId);
            return true;
        }

        public static void Apply(
            MeshRenderer meshRenderer,
            Color color,
            int colorPropertyId,
            MaterialPropertyBlock propertyBlock)
        {
            if (meshRenderer == null || colorPropertyId < 0 || propertyBlock == null)
                return;

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(colorPropertyId, color);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
