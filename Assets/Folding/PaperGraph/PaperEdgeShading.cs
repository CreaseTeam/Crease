using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crease.Folding.PaperGraph
{
    /// <summary>
    /// Pushes boundary and crease edge segments to paper materials via MaterialPropertyBlock.
    /// Boundary edges are face-local with surface-plane distance; crease edges use 3D distance
    /// on both faces of the fold so they remain visible from either side of the sheet.
    /// </summary>
    public static class PaperEdgeShading
    {
        private const int PixelsPerSegment = 3;
        private const int FloatsPerPixel = 4;
        private const int FloatsPerSegment = PixelsPerSegment * FloatsPerPixel;
        private const int MaxSegments = 4096;

        private const float SegmentTypeBoundary = 0f;
        private const float SegmentTypeCrease = 1f;

        private static readonly int EdgeSegmentCountId = Shader.PropertyToID("_EdgeSegmentCount");
        private static readonly int EdgeSegmentTexId = Shader.PropertyToID("_EdgeSegmentTex");
        private static readonly int BoundaryDarkenWidthId = Shader.PropertyToID("_BoundaryDarkenWidth");
        private static readonly int BoundaryMinBrightnessId = Shader.PropertyToID("_BoundaryMinBrightness");
        private static readonly int CreaseDarkenWidthId = Shader.PropertyToID("_CreaseDarkenWidth");
        private static readonly int CreaseMinBrightnessId = Shader.PropertyToID("_CreaseMinBrightness");

        private static readonly float[] SegmentData = new float[MaxSegments * FloatsPerSegment];
        private static float[] _uploadBuffer;
        private static MaterialPropertyBlock _propertyBlock;
        private static Texture2D _segmentTexture;
        private static int _segmentTextureSegmentCapacity;
        private static bool _segmentLimitWarned;

        public static void Apply(Renderer renderer, PaperGraph graph) {
            Apply(renderer, graph, Matrix4x4.identity);
        }

        /// <param name="segmentTransform">
        /// Maps graph-local edge endpoints into the target mesh object space.
        /// Use the same rotation applied when saving the flight mesh.
        /// </param>
        public static void Apply(Renderer renderer, PaperGraph graph, Matrix4x4 segmentTransform) {
            if (renderer == null || graph == null)
                return;

            _propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);

            bool shadingEnabled = graph.BoundaryEdgeDarkenWidth > 0f || graph.CreaseDarkenWidth > 0f;
            if (!shadingEnabled) {
                _propertyBlock.SetInt(EdgeSegmentCountId, 0);
                renderer.SetPropertyBlock(_propertyBlock);
                return;
            }

            Dictionary<Face, int> faceToIndex = new Dictionary<Face, int>();
            for (int i = 0; i < graph.Faces.Count; i++)
                faceToIndex[graph.Faces[i]] = i;

            int segmentCount = 0;
            foreach (Edge edge in graph.Edges) {
                if (segmentCount >= MaxSegments) {
                    if (!_segmentLimitWarned) {
                        _segmentLimitWarned = true;
                        Debug.LogWarning(
                            $"PaperEdgeShading: More than {MaxSegments} edge segments; extras are skipped.");
                    }
                    break;
                }

                int faceA = edge.Face1 != null && faceToIndex.TryGetValue(edge.Face1, out int indexA)
                    ? indexA
                    : -1;
                int faceB = edge.Face2 != null && faceToIndex.TryGetValue(edge.Face2, out int indexB)
                    ? indexB
                    : -1;

                float segmentType;
                if (IsBoundaryEdge(edge))
                    segmentType = SegmentTypeBoundary;
                else if (IsFoldCrease(edge))
                    segmentType = SegmentTypeCrease;
                else
                    continue;

                WriteSegment(
                    segmentCount,
                    segmentTransform.MultiplyPoint3x4(edge.V1.Position),
                    segmentTransform.MultiplyPoint3x4(edge.V2.Position),
                    faceA,
                    faceB,
                    segmentType);
                segmentCount++;
            }

            UploadSegmentTexture(segmentCount);

            _propertyBlock.SetInt(EdgeSegmentCountId, segmentCount);
            _propertyBlock.SetTexture(EdgeSegmentTexId, _segmentTexture);
            _propertyBlock.SetFloat(BoundaryDarkenWidthId, graph.BoundaryEdgeDarkenWidth);
            _propertyBlock.SetFloat(BoundaryMinBrightnessId, graph.BoundaryEdgeMinBrightness);
            _propertyBlock.SetFloat(CreaseDarkenWidthId, graph.CreaseDarkenWidth);
            _propertyBlock.SetFloat(CreaseMinBrightnessId, graph.CreaseMinBrightness);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private static void WriteSegment(
            int segmentIndex,
            Vector3 a,
            Vector3 b,
            int faceA,
            int faceB,
            float segmentType) {
            int offset = segmentIndex * FloatsPerSegment;
            SegmentData[offset + 0] = a.x;
            SegmentData[offset + 1] = a.y;
            SegmentData[offset + 2] = a.z;
            SegmentData[offset + 3] = 0f;
            SegmentData[offset + 4] = b.x;
            SegmentData[offset + 5] = b.y;
            SegmentData[offset + 6] = b.z;
            SegmentData[offset + 7] = 0f;
            SegmentData[offset + 8] = faceA;
            SegmentData[offset + 9] = faceB;
            SegmentData[offset + 10] = segmentType;
            SegmentData[offset + 11] = 0f;
        }

        private static void UploadSegmentTexture(int segmentCount) {
            EnsureSegmentTextureCapacity(segmentCount);

            int uploadFloatCount = _segmentTexture.width * FloatsPerPixel;
            if (_uploadBuffer == null || _uploadBuffer.Length != uploadFloatCount)
                _uploadBuffer = new float[uploadFloatCount];

            Array.Clear(_uploadBuffer, 0, uploadFloatCount);
            Array.Copy(SegmentData, 0, _uploadBuffer, 0, segmentCount * FloatsPerSegment);
            _segmentTexture.SetPixelData(_uploadBuffer, 0, 0);
            _segmentTexture.Apply(false, false);
        }

        private static void EnsureSegmentTextureCapacity(int segmentCount) {
            if (_segmentTexture != null && segmentCount <= _segmentTextureSegmentCapacity)
                return;

            int capacity = Math.Max(segmentCount, 64);
            capacity = Mathf.NextPowerOfTwo(capacity);
            capacity = Math.Min(capacity, MaxSegments);

            if (_segmentTexture != null)
                UnityEngine.Object.Destroy(_segmentTexture);

            _segmentTexture = new Texture2D(
                capacity * PixelsPerSegment,
                1,
                TextureFormat.RGBAFloat,
                mipChain: false,
                linear: true)
            {
                name = "PaperEdgeSegments",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            _segmentTextureSegmentCapacity = capacity;
        }

        private static bool IsBoundaryEdge(Edge edge) {
            return edge.Face1 == null || edge.Face2 == null;
        }

        private static bool IsFoldCrease(Edge edge) {
            if (edge.Face1 == null || edge.Face2 == null)
                return false;

            return Mathf.Abs(edge.FoldAngle - 180f) > 0.01f;
        }
    }
}
