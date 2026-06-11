using UnityEngine;

/// <summary>
/// Inspector-configurable appearance for folding guide and crease line renderers.
/// </summary>
[System.Serializable]
public class FoldingLineStyle
{
    [Tooltip("Optional material. Use a tiled/dashed material for guide lines; leave empty for a solid vertex-color line.")]
    public Material Material;

    [Tooltip("Line width in local space.")]
    [Min(0f)]
    public float Width = 0.005f;

    [Tooltip("Line color.")]
    public Color Color = Color.white;

    [Tooltip("Extra lift above the paper surface so the line stays visible.")]
    [Min(0f)]
    public float HeightOffset = 0.002f;

    public void ApplyTo(LineRenderer line) {
        if (line == null)
            return;

        line.useWorldSpace = false;
        if (line.positionCount < 2)
            line.positionCount = 2;

        line.startWidth = Width;
        line.endWidth = Width;
        line.startColor = Color;
        line.endColor = Color;
        line.textureMode = Material != null ? LineTextureMode.Tile : LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.allowOcclusionWhenDynamic = false;

        if (Material != null) {
            Material lineMaterial = new Material(Material);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            line.material = lineMaterial;
        }
    }
}
