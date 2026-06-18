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
        public PaperSide Side;
        public Vector3 LocalPoint;
        public Vector3 LocalNormal;
        public Vector3 ViewRayOriginLocal;
        public Vector3 ViewRayDirLocal;
    }
}
