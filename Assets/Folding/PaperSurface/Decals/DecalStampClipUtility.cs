using Crease.Folding.Paper;
using UnityEngine;

namespace Crease.Folding.PaperSurface.Decals
{
    /// <summary>
    /// Stamp sizing and rotation helpers for the decal capture rig.
    /// </summary>
    public static class DecalStampClipUtility
    {
        private const float Epsilon = 0.00001f;

        public static Vector2 GetPlacementSize(DecalPlacement placement)
        {
            if (placement.HeightScale > 0f)
                return new Vector2(placement.Scale, placement.HeightScale);

            if (placement.Texture == null || placement.Texture.width <= 0)
                return new Vector2(placement.Scale, placement.Scale);

            float aspect = (float)placement.Texture.height / placement.Texture.width;
            return new Vector2(placement.Scale, placement.Scale * aspect);
        }

        /// <summary>
        /// Rotation for stamp quads in capture space. Scale (local X) maps to
        /// (cos θ, -sin θ) in capture XZ after Euler(90°, -θ, 0).
        /// </summary>
        public static float ComputeStampRotationRad(DecalPlacement placement, float graphWidth, float graphHeight)
        {
            if (!placement.UseAxisAlignment || placement.AlignAxisLocal.sqrMagnitude < Epsilon)
                return placement.RotationUv * Mathf.Deg2Rad;

            float signU = placement.Side == PaperSide.Back ? -1f : 1f;
            float captureDx = signU * placement.AlignAxisLocal.x * DecalTextureRenderer.CapturePlaneWidth / graphWidth;
            float captureDz = placement.AlignAxisLocal.z * DecalTextureRenderer.CapturePlaneHeight / graphHeight;
            return Mathf.Atan2(captureDz, captureDx);
        }

        /// <summary>
        /// Guide rotation from sheet-UV tangent. Unlike graph-space axis projection this stays
        /// correct on folded faces and both front/back RTs (UV is already side-specific).
        /// </summary>
        public static float ComputeGuideRotationRad(
            PaperGraph graph,
            DecalSurfaceQuery.SurfaceHit hit,
            Vector3 axisLocal,
            float sampleDistance = 0.002f)
        {
            if (graph == null || !hit.Hit || axisLocal.sqrMagnitude < Epsilon)
                return 0f;

            if (!DecalSurfaceQuery.TryInterpolateSheetUvAlongAxis(graph, hit, axisLocal, sampleDistance, out Vector2 uvForward))
                return ComputeStampRotationRad(BuildAxisAlignmentPlacement(hit, axisLocal), graph.Width, graph.Height);

            Vector2 deltaUv = uvForward - hit.SheetUv;
            if (deltaUv.sqrMagnitude < Epsilon * Epsilon
                && DecalSurfaceQuery.TryInterpolateSheetUvAlongAxis(graph, hit, -axisLocal, sampleDistance, out Vector2 uvBackward))
            {
                deltaUv = hit.SheetUv - uvBackward;
            }

            float captureDx = deltaUv.x * DecalTextureRenderer.CapturePlaneWidth;
            float captureDz = deltaUv.y * DecalTextureRenderer.CapturePlaneHeight;
            if (captureDx * captureDx + captureDz * captureDz < Epsilon * Epsilon)
                return ComputeStampRotationRad(BuildAxisAlignmentPlacement(hit, axisLocal), graph.Width, graph.Height);

            return Mathf.Atan2(captureDz, captureDx);
        }

        private static DecalPlacement BuildAxisAlignmentPlacement(DecalSurfaceQuery.SurfaceHit hit, Vector3 axisLocal)
        {
            return new DecalPlacement
            {
                Side = hit.Side,
                AlignAxisLocal = axisLocal,
                UseAxisAlignment = true
            };
        }
    }
}
