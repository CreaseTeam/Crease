using Crease.Folding.PaperGraph;
using UnityEngine;

namespace Crease.Folding.Decals
{
    public enum PaperSide
    {
        Front = 0,
        Back = 1
    }

    /// <summary>
    /// SheetUv and Side are the stable sticker identity. ViewRayOrigin/Dir pick the correct layer
    /// among folded UV overlaps when topology changes.
    /// </summary>
    public class DecalPlacement
    {
        public Texture2D Texture;
        public int Anchor0Index;
        public int Anchor1Index;
        public int Anchor2Index;
        public Vector3 Barycentric;
        public Vector2 SheetUv;
        public float RotationUv;
        public float Scale;
        /// <summary>When &gt; 0, overrides texture aspect for quad height in local space.</summary>
        public float HeightScale;
        public PaperSide Side;
        public Vector3 LocalPoint;
        public Vector3 LocalNormal;
        public Vector3 ViewRayOriginLocal;
        public Vector3 ViewRayDirLocal;

        /// <summary>
        /// When true, quad Y aligns to <see cref="AlignAxisLocal"/> projected on the surface at display time.
        /// </summary>
        public bool UseAxisAlignment;
        public Vector3 AlignAxisLocal;

        /// <summary>
        /// When true, this decal was placed by damage and is preserved through refold.
        /// </summary>
        public bool IsDamageDecal;

        /// <summary>
        /// Tint applied when rendering this decal into the sheet texture.
        /// </summary>
        public Color StampColor = Color.white;

        /// <summary>
        /// <see cref="Crease.Flying.Player.Health.DamageType"/> ordinal when <see cref="IsDamageDecal"/> is true; otherwise -1.
        /// </summary>
        public int DamageSourceType = -1;
    }

    public static class DecalPlacementUtility
    {
        public static DecalPlacement FromSurfaceHit(
            Texture2D texture,
            DecalSurfaceQuery.SurfaceHit hit,
            float scale,
            float rotationUv = 0f,
            float heightScale = 0f,
            Vector3 alignAxisLocal = default,
            bool useAxisAlignment = false)
        {
            return new DecalPlacement
            {
                Texture = texture,
                Anchor0Index = hit.Anchor0Index,
                Anchor1Index = hit.Anchor1Index,
                Anchor2Index = hit.Anchor2Index,
                Barycentric = hit.Barycentric,
                SheetUv = hit.SheetUv,
                RotationUv = rotationUv,
                Scale = scale,
                HeightScale = heightScale,
                Side = hit.Side,
                LocalPoint = hit.LocalPoint,
                LocalNormal = hit.LocalNormal,
                ViewRayOriginLocal = hit.ViewRayOriginLocal,
                ViewRayDirLocal = hit.ViewRayDirLocal,
                UseAxisAlignment = useAxisAlignment,
                AlignAxisLocal = alignAxisLocal
            };
        }
    }
}
