using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crease.Folding.PaperGraph
{
    /// <summary>
    /// Pushes deduplicated feature edge segments to paper materials via MaterialPropertyBlock.
    /// Segment data is packed into a 1D float texture (3 texels per edge) so there is no
    /// practical edge count cap for folded paper.
    /// </summary>
    public static class PaperEdgeShading
    {
        private const int PixelsPerSegment = 3;
        private const int FloatsPerPixel = 4;
        private const int FloatsPerSegment = PixelsPerSegment * FloatsPerPixel;
        private const int MaxSegments = 4096;

        private static readonly int EdgeSegmentCountId = Shader.PropertyToID("_EdgeSegmentCount");
        private static readonly int EdgeSegmentTexId = Shader.PropertyToID("_EdgeSegmentTex");
        private static readonly int EdgeDarkenWidthId = Shader.PropertyToID("_EdgeDarkenWidth");
        private static readonly int EdgeMinBrightnessId = Shader.PropertyToID("_EdgeMinBrightness");

        private static readonly float[] SegmentData = new float[MaxSegments * FloatsPerSegment];
        private static float[] _uploadBuffer;
        private static MaterialPropertyBlock _propertyBlock;
        private static Texture2D _segmentTexture;
        private static int _segmentTextureSegmentCapacity;
        private static bool _segmentLimitWarned;

        public static void Apply(Renderer renderer, PaperGraph graph) {
            if (renderer == null || graph == null)
                return;

            _propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);

            if (graph.EdgeDarkenWidth <= 0f) {
                _propertyBlock.SetInt(EdgeSegmentCountId, 0);
                renderer.SetPropertyBlock(_propertyBlock);
                return;
            }

            Dictionary<Face, int> faceToIndex = new Dictionary<Face, int>();
            for (int i = 0; i < graph.Faces.Count; i++)
                faceToIndex[graph.Faces[i]] = i;

            int segmentCount = 0;
            foreach (Edge edge in graph.Edges) {
                if (!IsFeatureEdge(edge))
                    continue;

                if (segmentCount >= MaxSegments) {
                    if (!_segmentLimitWarned) {
                        _segmentLimitWarned = true;
                        Debug.LogWarning(
                            $"PaperEdgeShading: More than {MaxSegments} unique feature edges; extras are skipped.");
                    }
                    break;
                }

                int faceA = edge.Face1 != null && faceToIndex.TryGetValue(edge.Face1, out int indexA)
                    ? indexA
                    : -1;
                int faceB = edge.Face2 != null && faceToIndex.TryGetValue(edge.Face2, out int indexB)
                    ? indexB
                    : -1;

                WriteSegment(segmentCount, edge.V1.Position, edge.V2.Position, faceA, faceB);
                segmentCount++;
            }

            UploadSegmentTexture(segmentCount);

            _propertyBlock.SetInt(EdgeSegmentCountId, segmentCount);
            _propertyBlock.SetTexture(EdgeSegmentTexId, _segmentTexture);
            _propertyBlock.SetFloat(EdgeDarkenWidthId, graph.EdgeDarkenWidth);
            _propertyBlock.SetFloat(EdgeMinBrightnessId, graph.EdgeMinBrightness);
            renderer.SetPropertyBlock(_propertyBlock);
        }

        private static void WriteSegment(int segmentIndex, Vector3 a, Vector3 b, int faceA, int faceB) {
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
            SegmentData[offset + 10] = 0f;
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

        private static bool IsFeatureEdge(Edge edge) {
            if (edge.Face1 == null || edge.Face2 == null)
                return true;

            return Mathf.Abs(edge.FoldAngle - 180f) > 0.01f;
        }
    }
}
