using UnityEngine;

namespace Crease.Folding.Decals
{
    /// <summary>
    /// Preview-graph anchor indices for one sticker during fold/unfold animation.
    /// Topology is stable while only fold degrees change, so barycentric tracking avoids per-frame UV searches.
    /// </summary>
    public class PreviewAnchorCache
    {
        public bool IsValid;
        public int Anchor0Index;
        public int Anchor1Index;
        public int Anchor2Index;
        public Vector3 Barycentric;

        public void Invalidate()
        {
            IsValid = false;
        }

        public void SeedFrom(DecalSurfaceQuery.ResolvedSurfaceFrame frame)
        {
            IsValid = frame.Found;
            Anchor0Index = frame.Anchor0Index;
            Anchor1Index = frame.Anchor1Index;
            Anchor2Index = frame.Anchor2Index;
            Barycentric = frame.Barycentric;
        }
    }
}
