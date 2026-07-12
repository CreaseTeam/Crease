using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crease.Folding.Paper
{
    /// <summary>
    /// Pushes crease-line shading and decal overlay textures to paper materials via MaterialPropertyBlock.
    /// </summary>
    public static class PaperShading
    {
        private const int PixelsPerSegment = 3;
        private const int FloatsPerPixel = 4;
        private const int FloatsPerSegment = PixelsPerSegment * FloatsPerPixel;
        private const int MaxSegments = 4096;

        private static readonly int CreaseSegmentCountId = Shader.PropertyToID("_CreaseSegmentCount");
        private static readonly int CreaseSegmentTexId = Shader.PropertyToID("_CreaseSegmentTex");
        private static readonly int CreaseDarkenWidthId = Shader.PropertyToID("_CreaseDarkenWidth");
        private static readonly int CreaseMinBrightnessId = Shader.PropertyToID("_CreaseMinBrightness");
        private static readonly int DecalMapId = Shader.PropertyToID("_DecalMap");

        private static readonly float[] SegmentData = new float[MaxSegments * FloatsPerSegment];
        private static float[] _uploadBuffer;
        private static MaterialPropertyBlock _propertyBlock;
        private static Texture2D _segmentTexture;
        private static int _segmentTextureSegmentCapacity;
        private static bool _segmentLimitWarned;

        public static void ApplyCreaseSegments(Renderer renderer, PaperGraph graph)
        {
            ApplyCreaseSegments(renderer, graph, graph, Matrix4x4.identity);
        }

        /// <param name="segmentTransform">
        /// Maps graph-local crease endpoints into the target mesh object space.
        /// </param>
        public static void ApplyCreaseSegments(
            Renderer renderer,
            PaperGraph topologyGraph,
            Matrix4x4 segmentTransform)
        {
            ApplyCreaseSegments(renderer, topologyGraph, topologyGraph, segmentTransform);
        }

        public static void ApplyCreaseSegments(
            Renderer renderer,
            PaperGraph topologyGraph,
            PaperGraph settingsGraph)
        {
            ApplyCreaseSegments(renderer, topologyGraph, settingsGraph, Matrix4x4.identity);
        }

        /// <param name="topologyGraph">Edge topology used to build crease segments.</param>
        /// <param name="settingsGraph">Crease width/brightness settings. Defaults to topologyGraph.</param>
        /// <param name="segmentTransform">
        /// Maps graph-local crease endpoints into the target mesh object space.
        /// </param>
        public static void ApplyCreaseSegments(
            Renderer renderer,
            PaperGraph topologyGraph,
            PaperGraph settingsGraph,
            Matrix4x4 segmentTransform)
        {
            ApplyRendererShading(renderer, topologyGraph, settingsGraph, null, null, segmentTransform);
        }

        public static void ApplyDecalMaps(Renderer renderer, Texture frontDecalMap, Texture backDecalMap)
        {
            ApplyRendererShading(renderer, null, frontDecalMap, backDecalMap, Matrix4x4.identity);
        }

        /// <summary>
        /// Applies crease shading and optional decal maps to every material slot on the renderer.
        /// Both passes share one property block per slot so neither overwrites the other.
        /// </summary>
        public static void ApplyRendererShading(
            Renderer renderer,
            PaperGraph graph,
            Texture frontDecalMap,
            Texture backDecalMap)
        {
            ApplyRendererShading(renderer, graph, graph, frontDecalMap, backDecalMap, Matrix4x4.identity);
        }

        /// <param name="segmentTransform">
        /// Maps graph-local crease endpoints into the target mesh object space.
        /// </param>
        public static void ApplyRendererShading(
            Renderer renderer,
            PaperGraph topologyGraph,
            Texture frontDecalMap,
            Texture backDecalMap,
            Matrix4x4 segmentTransform)
        {
            ApplyRendererShading(renderer, topologyGraph, topologyGraph, frontDecalMap, backDecalMap, segmentTransform);
        }

        public static void ApplyRendererShading(
            Renderer renderer,
            PaperGraph topologyGraph,
            PaperGraph settingsGraph,
            Texture frontDecalMap,
            Texture backDecalMap)
        {
            ApplyRendererShading(renderer, topologyGraph, settingsGraph, frontDecalMap, backDecalMap, Matrix4x4.identity);
        }

        /// <param name="topologyGraph">Edge topology used to build crease segments.</param>
        /// <param name="settingsGraph">Crease width/brightness settings. Defaults to topologyGraph.</param>
        /// <param name="segmentTransform">
        /// Maps graph-local crease endpoints into the target mesh object space.
        /// </param>
        public static void ApplyRendererShading(
            Renderer renderer,
            PaperGraph topologyGraph,
            PaperGraph settingsGraph,
            Texture frontDecalMap,
            Texture backDecalMap,
            Matrix4x4 segmentTransform)
        {
            settingsGraph ??= topologyGraph;
            ApplyRendererShadingInternal(renderer, topologyGraph, settingsGraph, frontDecalMap, backDecalMap, segmentTransform);
        }

        private static void ApplyRendererShadingInternal(
            Renderer renderer,
            PaperGraph topologyGraph,
            PaperGraph settingsGraph,
            Texture frontDecalMap,
            Texture backDecalMap,
            Matrix4x4 segmentTransform)
        {
            if (renderer == null)
                return;

            _propertyBlock ??= new MaterialPropertyBlock();

            int segmentCount = 0;
            float creaseWidth = 0f;
            float creaseMinBrightness = 1f;

            if (topologyGraph != null)
            {
                creaseWidth = settingsGraph.CreaseDarkenWidth;
                creaseMinBrightness = settingsGraph.CreaseMinBrightness;

                if (creaseWidth > 0f)
                {
                    Dictionary<Face, int> faceToIndex = new Dictionary<Face, int>();
                    for (int i = 0; i < topologyGraph.Faces.Count; i++)
                        faceToIndex[topologyGraph.Faces[i]] = i;

                    foreach (Edge edge in topologyGraph.Edges)
                    {
                        if (!IsFoldCrease(edge))
                            continue;

                        if (segmentCount >= MaxSegments)
                        {
                            if (!_segmentLimitWarned)
                            {
                                _segmentLimitWarned = true;
                                Debug.LogWarning(
                                    $"PaperShading: More than {MaxSegments} crease segments; extras are skipped.");
                            }
                            break;
                        }

                        int faceA = edge.Face1 != null && faceToIndex.TryGetValue(edge.Face1, out int indexA)
                            ? indexA
                            : -1;
                        int faceB = edge.Face2 != null && faceToIndex.TryGetValue(edge.Face2, out int indexB)
                            ? indexB
                            : -1;

                        WriteSegment(
                            segmentCount,
                            segmentTransform.MultiplyPoint3x4(edge.V1.Position),
                            segmentTransform.MultiplyPoint3x4(edge.V2.Position),
                            faceA,
                            faceB);
                        segmentCount++;
                    }

                    if (segmentCount > 0)
                        UploadSegmentTexture(segmentCount);
                }
            }

            int materialCount = renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0
                ? renderer.sharedMaterials.Length
                : 1;

            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                renderer.GetPropertyBlock(_propertyBlock, materialIndex);

                _propertyBlock.SetFloat(CreaseDarkenWidthId, creaseWidth);
                _propertyBlock.SetFloat(CreaseMinBrightnessId, creaseMinBrightness);
                _propertyBlock.SetFloat(CreaseSegmentCountId, segmentCount);
                if (segmentCount > 0 && _segmentTexture != null)
                    _propertyBlock.SetTexture(CreaseSegmentTexId, _segmentTexture);

                Texture decalMap = materialIndex == 0
                    ? frontDecalMap
                    : materialIndex == 1
                        ? backDecalMap
                        : null;
                if (decalMap != null)
                    _propertyBlock.SetTexture(DecalMapId, decalMap);

                renderer.SetPropertyBlock(_propertyBlock, materialIndex);
            }
        }

        private static void WriteSegment(
            int segmentIndex,
            Vector3 a,
            Vector3 b,
            int faceA,
            int faceB)
        {
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

        private static void UploadSegmentTexture(int segmentCount)
        {
            EnsureSegmentTextureCapacity(segmentCount);

            int uploadFloatCount = _segmentTexture.width * FloatsPerPixel;
            if (_uploadBuffer == null || _uploadBuffer.Length != uploadFloatCount)
                _uploadBuffer = new float[uploadFloatCount];

            Array.Clear(_uploadBuffer, 0, uploadFloatCount);
            Array.Copy(SegmentData, 0, _uploadBuffer, 0, segmentCount * FloatsPerSegment);
            _segmentTexture.SetPixelData(_uploadBuffer, 0, 0);
            _segmentTexture.Apply(false, false);
        }

        private static void EnsureSegmentTextureCapacity(int segmentCount)
        {
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
                name = "PaperCreaseSegments",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            _segmentTextureSegmentCapacity = capacity;
        }

        private static bool IsFoldCrease(Edge edge)
        {
            if (edge.Face1 == null || edge.Face2 == null)
                return false;

            // Default Edge.FoldAngle is +180 (unset hinge connectors along fold thickness).
            if (Mathf.Abs(edge.FoldAngle - 180f) <= 0.01f)
                return false;

            return true;
        }
    }
}
