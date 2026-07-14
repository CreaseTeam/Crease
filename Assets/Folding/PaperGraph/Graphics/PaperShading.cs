using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crease.Folding.Paper
{
    /// <summary>
    /// Crease and boundary-edge segment data baked for a saved flight mesh.
    /// </summary>
    public sealed class FlightShadingData
    {
        public float FoldEdgeDarkenWidth;
        public float FoldEdgeMinBrightness;
        public float CreaseEdgeDarkenWidth;
        public float CreaseEdgeMinBrightness;
        public float EdgeShadowDarkenWidth;
        public float EdgeShadowInnerOffset;
        public float EdgeShadowMinBrightness;
        public int FoldEdgeSegmentCount;
        public int CreaseEdgeSegmentCount;
        public int BoundarySegmentCount;
        public float[] FoldEdgeSegments;
        public float[] CreaseEdgeSegments;
        public float[] BoundarySegments;
    }

    /// <summary>
    /// Pushes crease-line shading and decal overlay textures to paper materials via MaterialPropertyBlock.
    /// </summary>
    public static class PaperShading
    {
        private const int PixelsPerSegment = 3;
        private const int PixelsPerBoundarySegment = 3;
        private const int FloatsPerPixel = 4;
        private const int FloatsPerSegment = PixelsPerSegment * FloatsPerPixel;
        private const int FloatsPerBoundarySegment = PixelsPerBoundarySegment * FloatsPerPixel;
        private const int MaxSegments = 4096;

        private static readonly int FoldEdgeSegmentCountId = Shader.PropertyToID("_FoldEdgeSegmentCount");
        private static readonly int FoldEdgeSegmentTexId = Shader.PropertyToID("_FoldEdgeSegmentTex");
        private static readonly int FoldEdgeDarkenWidthId = Shader.PropertyToID("_FoldEdgeDarkenWidth");
        private static readonly int FoldEdgeMinBrightnessId = Shader.PropertyToID("_FoldEdgeMinBrightness");
        private static readonly int CreaseEdgeSegmentCountId = Shader.PropertyToID("_CreaseEdgeSegmentCount");
        private static readonly int CreaseEdgeSegmentTexId = Shader.PropertyToID("_CreaseEdgeSegmentTex");
        private static readonly int CreaseEdgeDarkenWidthId = Shader.PropertyToID("_CreaseEdgeDarkenWidth");
        private static readonly int CreaseEdgeMinBrightnessId = Shader.PropertyToID("_CreaseEdgeMinBrightness");
        private static readonly int BoundaryEdgeSegmentCountId = Shader.PropertyToID("_BoundaryEdgeSegmentCount");
        private static readonly int BoundaryEdgeSegmentTexId = Shader.PropertyToID("_BoundaryEdgeSegmentTex");
        private static readonly int EdgeShadowDarkenWidthId = Shader.PropertyToID("_EdgeShadowDarkenWidth");
        private static readonly int EdgeShadowInnerOffsetId = Shader.PropertyToID("_EdgeShadowInnerOffset");
        private static readonly int EdgeShadowMinBrightnessId = Shader.PropertyToID("_EdgeShadowMinBrightness");
        private static readonly int DecalMapId = Shader.PropertyToID("_DecalMap");

        private static readonly float[] FoldEdgeSegmentData = new float[MaxSegments * FloatsPerSegment];
        private static readonly float[] CreaseEdgeSegmentData = new float[MaxSegments * FloatsPerSegment];
        private static readonly float[] BoundarySegmentData = new float[MaxSegments * FloatsPerBoundarySegment];
        private static MaterialPropertyBlock _propertyBlock;
        private static bool _foldEdgeSegmentLimitWarned;
        private static bool _creaseEdgeSegmentLimitWarned;
        private static bool _boundarySegmentLimitWarned;

        private sealed class RendererSegmentTextures
        {
            public Texture2D FoldEdge;
            public Texture2D CreaseEdge;
            public Texture2D Boundary;
            public int FoldEdgeCapacity;
            public int CreaseEdgeCapacity;
            public int BoundaryCapacity;
            public float[] FoldEdgeUploadBuffer;
            public float[] CreaseEdgeUploadBuffer;
            public float[] BoundaryUploadBuffer;
        }

        private static readonly Dictionary<int, RendererSegmentTextures> _rendererSegmentTextures = new();

        /// <summary>
        /// Maps graph-local crease/edge segment positions into the target mesh object space.
        /// </summary>
        public static bool TryComputeSegmentTransform(
            PaperGraph topologyGraph,
            Mesh targetMesh,
            out Matrix4x4 segmentTransform)
        {
            segmentTransform = Matrix4x4.identity;
            if (topologyGraph == null || targetMesh == null)
                return false;

            Mesh sourceMesh = topologyGraph.GenerateMesh();
            try
            {
                if (sourceMesh.vertexCount != targetMesh.vertexCount || sourceMesh.vertexCount < 3)
                    return false;

                Vector3[] source = sourceMesh.vertices;
                Vector3[] target = targetMesh.vertices;

                for (int anchor = 0; anchor < source.Length; anchor++)
                {
                    for (int i = 0; i < source.Length; i++)
                    {
                        if (i == anchor)
                            continue;

                        Vector3 srcA = source[i] - source[anchor];
                        if (srcA.sqrMagnitude < 1e-10f)
                            continue;

                        for (int j = i + 1; j < source.Length; j++)
                        {
                            Vector3 srcB = source[j] - source[anchor];
                            if (srcB.sqrMagnitude < 1e-10f)
                                continue;

                            if (Vector3.Cross(srcA, srcB).sqrMagnitude < 1e-10f)
                                continue;

                            Vector3 dstA = target[i] - target[anchor];
                            Vector3 dstB = target[j] - target[anchor];
                            if (Vector3.Cross(dstA, dstB).sqrMagnitude < 1e-10f)
                                continue;

                            Quaternion rotation = Quaternion.FromToRotation(srcA, dstA);
                            if ((rotation * srcB - dstB).sqrMagnitude > 1e-4f)
                                continue;

                            Vector3 sourceAnchor = source[anchor];
                            Vector3 targetAnchor = target[anchor];
                            Vector3 translation = targetAnchor - rotation * sourceAnchor;
                            segmentTransform = Matrix4x4.TRS(translation, rotation, Vector3.one);
                            return true;
                        }
                    }
                }

                return false;
            }
            finally
            {
                UnityEngine.Object.Destroy(sourceMesh);
            }
        }

        /// <summary>
        /// Maps each graph vertex to its position in an already-generated target mesh.
        /// Used for flight mode where segment endpoints must match baked mesh vertices exactly.
        /// </summary>
        public static bool TryBuildVertexMeshPositions(
            PaperGraph topologyGraph,
            Mesh targetMesh,
            out Dictionary<Vertex, Vector3> vertexMeshPositions)
        {
            vertexMeshPositions = null;
            if (topologyGraph == null || targetMesh == null)
                return false;

            Mesh sourceMesh = topologyGraph.GenerateMesh();
            try
            {
                if (sourceMesh.vertexCount != targetMesh.vertexCount)
                    return false;

                Vector3[] source = sourceMesh.vertices;
                Vector3[] target = targetMesh.vertices;
                vertexMeshPositions = new Dictionary<Vertex, Vector3>();

                foreach (Vertex vertex in topologyGraph.Vertices)
                {
                    Vector3 graphPos = vertex.Position;
                    bool found = false;
                    for (int i = 0; i < source.Length; i++)
                    {
                        if ((source[i] - graphPos).sqrMagnitude > 1e-8f)
                            continue;

                        if (vertexMeshPositions.TryGetValue(vertex, out Vector3 existing)
                            && (existing - target[i]).sqrMagnitude > 1e-6f)
                        {
                            return false;
                        }

                        vertexMeshPositions[vertex] = target[i];
                        found = true;
                        break;
                    }

                    if (!found)
                        return false;
                }

                return vertexMeshPositions.Count > 0;
            }
            finally
            {
                UnityEngine.Object.Destroy(sourceMesh);
            }
        }

        public static void ApplyEdgeSegments(Renderer renderer, PaperGraph graph)
        {
            ApplyEdgeSegments(renderer, graph, graph, Matrix4x4.identity);
        }

        /// <param name="segmentTransform">
        /// Maps graph-local crease endpoints into the target mesh object space.
        /// </param>
        public static void ApplyEdgeSegments(
            Renderer renderer,
            PaperGraph topologyGraph,
            Matrix4x4 segmentTransform)
        {
            ApplyEdgeSegments(renderer, topologyGraph, topologyGraph, segmentTransform);
        }

        public static void ApplyEdgeSegments(
            Renderer renderer,
            PaperGraph topologyGraph,
            PaperGraph settingsGraph)
        {
            ApplyEdgeSegments(renderer, topologyGraph, settingsGraph, Matrix4x4.identity);
        }

        /// <param name="topologyGraph">Edge topology used to build crease segments.</param>
        /// <param name="settingsGraph">Crease width/brightness settings. Defaults to topologyGraph.</param>
        /// <param name="segmentTransform">
        /// Maps graph-local crease endpoints into the target mesh object space.
        /// </param>
        public static void ApplyEdgeSegments(
            Renderer renderer,
            PaperGraph topologyGraph,
            PaperGraph settingsGraph,
            Matrix4x4 segmentTransform)
        {
            ApplyRendererShading(renderer, topologyGraph, settingsGraph, null, null, segmentTransform);
        }

        public static void ApplyDecalMaps(Renderer renderer, Texture frontDecalMap, Texture backDecalMap)
        {
            ApplyDecalMapsOnly(renderer, frontDecalMap, backDecalMap);
        }

        /// <summary>
        /// Updates only decal overlay textures. Does not touch crease or edge-shadow segment data.
        /// </summary>
        public static void ApplyDecalMapsOnly(Renderer renderer, Texture frontDecalMap, Texture backDecalMap)
        {
            if (renderer == null)
                return;

            _propertyBlock ??= new MaterialPropertyBlock();

            int materialCount = renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0
                ? renderer.sharedMaterials.Length
                : 1;

            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                renderer.GetPropertyBlock(_propertyBlock, materialIndex);

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
            ApplyRendererShadingInternal(
                renderer,
                topologyGraph,
                settingsGraph,
                frontDecalMap,
                backDecalMap,
                segmentTransform,
                null);
        }

        /// <param name="targetMesh">
        /// When provided, segment endpoints are read directly from this mesh so shading
        /// matches whatever vertex bake was applied at save time.
        /// </param>
        /// <param name="fallbackSegmentTransform">
        /// Used only when <paramref name="targetMesh"/> cannot be matched to the topology graph.
        /// </param>
        public static void ApplyRendererShading(
            Renderer renderer,
            PaperGraph topologyGraph,
            PaperGraph settingsGraph,
            Texture frontDecalMap,
            Texture backDecalMap,
            Mesh targetMesh,
            Matrix4x4 fallbackSegmentTransform,
            Dictionary<Vertex, Vector3> vertexMeshPositions = null)
        {
            settingsGraph ??= topologyGraph;

            if (vertexMeshPositions == null && targetMesh != null)
                TryBuildVertexMeshPositions(topologyGraph, targetMesh, out vertexMeshPositions);

            ApplyRendererShadingInternal(
                renderer,
                topologyGraph,
                settingsGraph,
                frontDecalMap,
                backDecalMap,
                fallbackSegmentTransform,
                vertexMeshPositions);
        }

        public static void ApplyRendererShading(
            Renderer renderer,
            PaperGraph topologyGraph,
            PaperGraph settingsGraph,
            Texture frontDecalMap,
            Texture backDecalMap,
            Mesh targetMesh,
            Matrix4x4 fallbackSegmentTransform)
        {
            ApplyRendererShading(
                renderer,
                topologyGraph,
                settingsGraph,
                frontDecalMap,
                backDecalMap,
                targetMesh,
                fallbackSegmentTransform,
                null);
        }

        public static FlightShadingData BuildFlightShadingData(
            PaperGraph topologyGraph,
            PaperGraph settingsGraph,
            Dictionary<Vertex, Vector3> vertexMeshPositions,
            Matrix4x4 fallbackSegmentTransform)
        {
            if (topologyGraph == null)
                return null;

            if (!TryPopulateSegmentBuffers(
                    topologyGraph,
                    settingsGraph,
                    fallbackSegmentTransform,
                    vertexMeshPositions,
                    out float foldEdgeWidth,
                    out float foldEdgeMinBrightness,
                    out float creaseEdgeWidth,
                    out float creaseEdgeMinBrightness,
                    out float edgeShadowWidth,
                    out float edgeShadowInnerOffset,
                    out float edgeShadowMinBrightness,
                    out int foldEdgeSegmentCount,
                    out int creaseEdgeSegmentCount,
                    out int boundarySegmentCount))
            {
                return null;
            }

            FlightShadingData data = new FlightShadingData
            {
                FoldEdgeDarkenWidth = foldEdgeWidth,
                FoldEdgeMinBrightness = foldEdgeMinBrightness,
                CreaseEdgeDarkenWidth = creaseEdgeWidth,
                CreaseEdgeMinBrightness = creaseEdgeMinBrightness,
                EdgeShadowDarkenWidth = edgeShadowWidth,
                EdgeShadowInnerOffset = edgeShadowInnerOffset,
                EdgeShadowMinBrightness = edgeShadowMinBrightness,
                FoldEdgeSegmentCount = foldEdgeSegmentCount,
                CreaseEdgeSegmentCount = creaseEdgeSegmentCount,
                BoundarySegmentCount = boundarySegmentCount
            };

            if (foldEdgeSegmentCount > 0)
            {
                data.FoldEdgeSegments = new float[foldEdgeSegmentCount * FloatsPerSegment];
                Array.Copy(FoldEdgeSegmentData, 0, data.FoldEdgeSegments, 0, data.FoldEdgeSegments.Length);
            }

            if (creaseEdgeSegmentCount > 0)
            {
                data.CreaseEdgeSegments = new float[creaseEdgeSegmentCount * FloatsPerSegment];
                Array.Copy(CreaseEdgeSegmentData, 0, data.CreaseEdgeSegments, 0, data.CreaseEdgeSegments.Length);
            }

            if (boundarySegmentCount > 0)
            {
                data.BoundarySegments = new float[boundarySegmentCount * FloatsPerBoundarySegment];
                Array.Copy(BoundarySegmentData, 0, data.BoundarySegments, 0, data.BoundarySegments.Length);
            }

            return data;
        }

        public static void ApplyFlightShadingData(
            Renderer renderer,
            FlightShadingData data)
        {
            if (renderer == null || data == null)
                return;

            if (data.FoldEdgeSegmentCount > 0 && data.FoldEdgeSegments != null)
            {
                Array.Copy(data.FoldEdgeSegments, 0, FoldEdgeSegmentData, 0, data.FoldEdgeSegments.Length);
                UploadFoldEdgeSegmentTexture(renderer, data.FoldEdgeSegmentCount);
            }

            if (data.CreaseEdgeSegmentCount > 0 && data.CreaseEdgeSegments != null)
            {
                Array.Copy(data.CreaseEdgeSegments, 0, CreaseEdgeSegmentData, 0, data.CreaseEdgeSegments.Length);
                UploadCreaseEdgeSegmentTexture(renderer, data.CreaseEdgeSegmentCount);
            }

            if (data.BoundarySegmentCount > 0 && data.BoundarySegments != null)
            {
                Array.Copy(data.BoundarySegments, 0, BoundarySegmentData, 0, data.BoundarySegments.Length);
                UploadBoundarySegmentTexture(renderer, data.BoundarySegmentCount);
            }

            ApplySegmentMaterialProperties(
                renderer,
                data.FoldEdgeDarkenWidth,
                data.FoldEdgeMinBrightness,
                data.FoldEdgeSegmentCount,
                data.CreaseEdgeDarkenWidth,
                data.CreaseEdgeMinBrightness,
                data.CreaseEdgeSegmentCount,
                data.EdgeShadowDarkenWidth,
                data.EdgeShadowInnerOffset,
                data.EdgeShadowMinBrightness,
                data.BoundarySegmentCount,
                null,
                null);
        }

        private static void ApplyRendererShadingInternal(
            Renderer renderer,
            PaperGraph topologyGraph,
            PaperGraph settingsGraph,
            Texture frontDecalMap,
            Texture backDecalMap,
            Matrix4x4 segmentTransform,
            Dictionary<Vertex, Vector3> vertexMeshPositions)
        {
            if (renderer == null)
                return;

            if (!TryPopulateSegmentBuffers(
                    topologyGraph,
                    settingsGraph,
                    segmentTransform,
                    vertexMeshPositions,
                    out float foldEdgeWidth,
                    out float foldEdgeMinBrightness,
                    out float creaseEdgeWidth,
                    out float creaseEdgeMinBrightness,
                    out float edgeShadowWidth,
                    out float edgeShadowInnerOffset,
                    out float edgeShadowMinBrightness,
                    out int foldEdgeSegmentCount,
                    out int creaseEdgeSegmentCount,
                    out int boundarySegmentCount))
            {
                ApplySegmentMaterialProperties(
                    renderer,
                    0f,
                    1f,
                    0,
                    0f,
                    1f,
                    0,
                    0f,
                    0f,
                    1f,
                    0,
                    frontDecalMap,
                    backDecalMap);
                return;
            }

            if (foldEdgeSegmentCount > 0)
                UploadFoldEdgeSegmentTexture(renderer, foldEdgeSegmentCount);

            if (creaseEdgeSegmentCount > 0)
                UploadCreaseEdgeSegmentTexture(renderer, creaseEdgeSegmentCount);

            if (boundarySegmentCount > 0)
                UploadBoundarySegmentTexture(renderer, boundarySegmentCount);

            ApplySegmentMaterialProperties(
                renderer,
                foldEdgeWidth,
                foldEdgeMinBrightness,
                foldEdgeSegmentCount,
                creaseEdgeWidth,
                creaseEdgeMinBrightness,
                creaseEdgeSegmentCount,
                edgeShadowWidth,
                edgeShadowInnerOffset,
                edgeShadowMinBrightness,
                boundarySegmentCount,
                frontDecalMap,
                backDecalMap);
        }

        private static bool TryPopulateSegmentBuffers(
            PaperGraph topologyGraph,
            PaperGraph settingsGraph,
            Matrix4x4 segmentTransform,
            Dictionary<Vertex, Vector3> vertexMeshPositions,
            out float foldEdgeWidth,
            out float foldEdgeMinBrightness,
            out float creaseEdgeWidth,
            out float creaseEdgeMinBrightness,
            out float edgeShadowWidth,
            out float edgeShadowInnerOffset,
            out float edgeShadowMinBrightness,
            out int foldEdgeSegmentCount,
            out int creaseEdgeSegmentCount,
            out int boundarySegmentCount)
        {
            foldEdgeWidth = 0f;
            foldEdgeMinBrightness = 1f;
            creaseEdgeWidth = 0f;
            creaseEdgeMinBrightness = 1f;
            edgeShadowWidth = 0f;
            edgeShadowInnerOffset = 0f;
            edgeShadowMinBrightness = 1f;
            foldEdgeSegmentCount = 0;
            creaseEdgeSegmentCount = 0;
            boundarySegmentCount = 0;

            if (topologyGraph == null)
                return false;

            settingsGraph ??= topologyGraph;

            foldEdgeWidth = settingsGraph.FoldEdgeDarkenWidth;
            foldEdgeMinBrightness = settingsGraph.FoldEdgeMinBrightness;
            creaseEdgeWidth = settingsGraph.CreaseEdgeDarkenWidth;
            creaseEdgeMinBrightness = settingsGraph.CreaseEdgeMinBrightness;
            edgeShadowWidth = settingsGraph.EdgeShadowDarkenWidth;
            edgeShadowInnerOffset = settingsGraph.EdgeShadowInnerOffset;
            edgeShadowMinBrightness = settingsGraph.EdgeShadowMinBrightness;

            Dictionary<Face, int> faceToIndex = new Dictionary<Face, int>();
            for (int i = 0; i < topologyGraph.Faces.Count; i++)
                faceToIndex[topologyGraph.Faces[i]] = i;

            Vector3 TransformVertex(Vertex vertex)
            {
                if (vertexMeshPositions != null
                    && vertexMeshPositions.TryGetValue(vertex, out Vector3 meshPosition))
                {
                    return meshPosition;
                }

                return segmentTransform.MultiplyPoint3x4(vertex.Position);
            }

            if (foldEdgeWidth > 0f)
            {
                foreach (Edge edge in topologyGraph.Edges)
                {
                    if (!IsFoldEdge(edge))
                        continue;

                    if (foldEdgeSegmentCount >= MaxSegments)
                    {
                        if (!_foldEdgeSegmentLimitWarned)
                        {
                            _foldEdgeSegmentLimitWarned = true;
                            Debug.LogWarning(
                                $"PaperShading: More than {MaxSegments} fold edge segments; extras are skipped.");
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
                        FoldEdgeSegmentData,
                        foldEdgeSegmentCount,
                        TransformVertex(edge.V1),
                        TransformVertex(edge.V2),
                        faceA,
                        faceB);
                    foldEdgeSegmentCount++;
                }
            }

            if (creaseEdgeWidth > 0f)
            {
                foreach (Edge edge in topologyGraph.Edges)
                {
                    if (!IsCreaseEdge(edge))
                        continue;

                    if (creaseEdgeSegmentCount >= MaxSegments)
                    {
                        if (!_creaseEdgeSegmentLimitWarned)
                        {
                            _creaseEdgeSegmentLimitWarned = true;
                            Debug.LogWarning(
                                $"PaperShading: More than {MaxSegments} crease edge segments; extras are skipped.");
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
                        CreaseEdgeSegmentData,
                        creaseEdgeSegmentCount,
                        TransformVertex(edge.V1),
                        TransformVertex(edge.V2),
                        faceA,
                        faceB);
                    creaseEdgeSegmentCount++;
                }
            }

            if (edgeShadowWidth > 0f)
            {
                foreach (Edge edge in topologyGraph.Edges)
                {
                    if (!IsBoundaryEdge(edge))
                        continue;

                    if (!TryGetBoundaryEdgeShadowData(
                            edge,
                            faceToIndex,
                            TransformVertex,
                            out int ownerFaceIndex,
                            out Vector3 shadowDir,
                            out Vector3 pointA,
                            out Vector3 pointB))
                        continue;

                    if (boundarySegmentCount >= MaxSegments)
                    {
                        if (!_boundarySegmentLimitWarned)
                        {
                            _boundarySegmentLimitWarned = true;
                            Debug.LogWarning(
                                $"PaperShading: More than {MaxSegments} boundary edge segments; extras are skipped.");
                        }
                        break;
                    }

                    WriteBoundarySegment(
                        boundarySegmentCount,
                        pointA,
                        pointB,
                        ownerFaceIndex,
                        shadowDir);
                    boundarySegmentCount++;
                }
            }

            return true;
        }

        private static void ApplySegmentMaterialProperties(
            Renderer renderer,
            float foldEdgeWidth,
            float foldEdgeMinBrightness,
            int foldEdgeSegmentCount,
            float creaseEdgeWidth,
            float creaseEdgeMinBrightness,
            int creaseEdgeSegmentCount,
            float edgeShadowWidth,
            float edgeShadowInnerOffset,
            float edgeShadowMinBrightness,
            int boundarySegmentCount,
            Texture frontDecalMap,
            Texture backDecalMap)
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            RendererSegmentTextures segmentTextures = GetRendererSegmentTextures(renderer);

            int materialCount = renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0
                ? renderer.sharedMaterials.Length
                : 1;

            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                renderer.GetPropertyBlock(_propertyBlock, materialIndex);

                _propertyBlock.SetFloat(FoldEdgeDarkenWidthId, foldEdgeWidth);
                _propertyBlock.SetFloat(FoldEdgeMinBrightnessId, foldEdgeMinBrightness);
                _propertyBlock.SetFloat(FoldEdgeSegmentCountId, foldEdgeSegmentCount);
                if (foldEdgeSegmentCount > 0 && segmentTextures.FoldEdge != null)
                    _propertyBlock.SetTexture(FoldEdgeSegmentTexId, segmentTextures.FoldEdge);

                _propertyBlock.SetFloat(CreaseEdgeDarkenWidthId, creaseEdgeWidth);
                _propertyBlock.SetFloat(CreaseEdgeMinBrightnessId, creaseEdgeMinBrightness);
                _propertyBlock.SetFloat(CreaseEdgeSegmentCountId, creaseEdgeSegmentCount);
                if (creaseEdgeSegmentCount > 0 && segmentTextures.CreaseEdge != null)
                    _propertyBlock.SetTexture(CreaseEdgeSegmentTexId, segmentTextures.CreaseEdge);

                _propertyBlock.SetFloat(EdgeShadowDarkenWidthId, edgeShadowWidth);
                _propertyBlock.SetFloat(EdgeShadowInnerOffsetId, edgeShadowInnerOffset);
                _propertyBlock.SetFloat(EdgeShadowMinBrightnessId, edgeShadowMinBrightness);
                _propertyBlock.SetFloat(BoundaryEdgeSegmentCountId, boundarySegmentCount);
                if (boundarySegmentCount > 0 && segmentTextures.Boundary != null)
                    _propertyBlock.SetTexture(BoundaryEdgeSegmentTexId, segmentTextures.Boundary);

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
            float[] segmentBuffer,
            int segmentIndex,
            Vector3 a,
            Vector3 b,
            int faceA,
            int faceB)
        {
            int offset = segmentIndex * FloatsPerSegment;
            segmentBuffer[offset + 0] = a.x;
            segmentBuffer[offset + 1] = a.y;
            segmentBuffer[offset + 2] = a.z;
            segmentBuffer[offset + 3] = 0f;
            segmentBuffer[offset + 4] = b.x;
            segmentBuffer[offset + 5] = b.y;
            segmentBuffer[offset + 6] = b.z;
            segmentBuffer[offset + 7] = 0f;
            segmentBuffer[offset + 8] = faceA;
            segmentBuffer[offset + 9] = faceB;
            segmentBuffer[offset + 10] = 0f;
            segmentBuffer[offset + 11] = 0f;
        }

        private static RendererSegmentTextures GetRendererSegmentTextures(Renderer renderer)
        {
            int rendererId = renderer.GetInstanceID();
            if (!_rendererSegmentTextures.TryGetValue(rendererId, out RendererSegmentTextures textures))
            {
                textures = new RendererSegmentTextures();
                _rendererSegmentTextures[rendererId] = textures;
            }

            return textures;
        }

        private static void UploadFoldEdgeSegmentTexture(Renderer renderer, int segmentCount)
        {
            RendererSegmentTextures textures = GetRendererSegmentTextures(renderer);
            EnsureFoldEdgeSegmentTextureCapacity(textures, segmentCount);

            int uploadFloatCount = textures.FoldEdge.width * FloatsPerPixel;
            if (textures.FoldEdgeUploadBuffer == null || textures.FoldEdgeUploadBuffer.Length != uploadFloatCount)
                textures.FoldEdgeUploadBuffer = new float[uploadFloatCount];

            Array.Clear(textures.FoldEdgeUploadBuffer, 0, uploadFloatCount);
            Array.Copy(FoldEdgeSegmentData, 0, textures.FoldEdgeUploadBuffer, 0, segmentCount * FloatsPerSegment);
            textures.FoldEdge.SetPixelData(textures.FoldEdgeUploadBuffer, 0, 0);
            textures.FoldEdge.Apply(false, false);
        }

        private static void EnsureFoldEdgeSegmentTextureCapacity(RendererSegmentTextures textures, int segmentCount)
        {
            if (textures.FoldEdge != null && segmentCount <= textures.FoldEdgeCapacity)
                return;

            int capacity = Math.Max(segmentCount, 64);
            capacity = Mathf.NextPowerOfTwo(capacity);
            capacity = Math.Min(capacity, MaxSegments);

            if (textures.FoldEdge != null)
                UnityEngine.Object.Destroy(textures.FoldEdge);

            textures.FoldEdge = new Texture2D(
                capacity * PixelsPerSegment,
                1,
                TextureFormat.RGBAFloat,
                mipChain: false,
                linear: true)
            {
                name = "PaperFoldEdgeSegments",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            textures.FoldEdgeCapacity = capacity;
        }

        private static void WriteBoundarySegment(
            int segmentIndex,
            Vector3 a,
            Vector3 b,
            int ownerFaceIndex,
            Vector3 shadowDir)
        {
            int offset = segmentIndex * FloatsPerBoundarySegment;
            BoundarySegmentData[offset + 0] = a.x;
            BoundarySegmentData[offset + 1] = a.y;
            BoundarySegmentData[offset + 2] = a.z;
            BoundarySegmentData[offset + 3] = 0f;
            BoundarySegmentData[offset + 4] = b.x;
            BoundarySegmentData[offset + 5] = b.y;
            BoundarySegmentData[offset + 6] = b.z;
            BoundarySegmentData[offset + 7] = 0f;
            BoundarySegmentData[offset + 8] = ownerFaceIndex;
            BoundarySegmentData[offset + 9] = shadowDir.x;
            BoundarySegmentData[offset + 10] = shadowDir.y;
            BoundarySegmentData[offset + 11] = shadowDir.z;
        }

        private static bool TryGetBoundaryEdgeShadowData(
            Edge edge,
            Dictionary<Face, int> faceToIndex,
            Func<Vertex, Vector3> transformVertex,
            out int ownerFaceIndex,
            out Vector3 shadowDir,
            out Vector3 pointA,
            out Vector3 pointB)
        {
            ownerFaceIndex = -1;
            shadowDir = Vector3.zero;
            pointA = Vector3.zero;
            pointB = Vector3.zero;

            Face ownerFace = edge.Face1 ?? edge.Face2;
            if (ownerFace == null || !faceToIndex.TryGetValue(ownerFace, out ownerFaceIndex))
                return false;

            pointA = transformVertex(edge.V1);
            pointB = transformVertex(edge.V2);
            Vector3 centroid = ComputeFaceCentroid(ownerFace, transformVertex);
            Vector3 midpoint = (pointA + pointB) * 0.5f;
            Vector3 edgeDir = pointB - pointA;
            if (edgeDir.sqrMagnitude < 1e-10f)
                return false;

            edgeDir.Normalize();
            Vector3 toCenter = centroid - midpoint;
            Vector3 inward = toCenter - Vector3.Dot(toCenter, edgeDir) * edgeDir;
            if (inward.sqrMagnitude < 1e-10f)
                return false;

            shadowDir = -inward.normalized;
            return true;
        }

        private static Vector3 ComputeFaceCentroid(Face face, Func<Vertex, Vector3> transformVertex)
        {
            if (face.Vertices == null || face.Vertices.Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < face.Vertices.Count; i++)
                sum += transformVertex(face.Vertices[i]);

            return sum / face.Vertices.Count;
        }

        private static void UploadBoundarySegmentTexture(Renderer renderer, int segmentCount)
        {
            RendererSegmentTextures textures = GetRendererSegmentTextures(renderer);
            EnsureBoundarySegmentTextureCapacity(textures, segmentCount);

            int uploadFloatCount = textures.Boundary.width * FloatsPerPixel;
            if (textures.BoundaryUploadBuffer == null || textures.BoundaryUploadBuffer.Length != uploadFloatCount)
                textures.BoundaryUploadBuffer = new float[uploadFloatCount];

            Array.Clear(textures.BoundaryUploadBuffer, 0, uploadFloatCount);
            Array.Copy(BoundarySegmentData, 0, textures.BoundaryUploadBuffer, 0, segmentCount * FloatsPerBoundarySegment);
            textures.Boundary.SetPixelData(textures.BoundaryUploadBuffer, 0, 0);
            textures.Boundary.Apply(false, false);
        }

        private static void EnsureBoundarySegmentTextureCapacity(RendererSegmentTextures textures, int segmentCount)
        {
            if (textures.Boundary != null && segmentCount <= textures.BoundaryCapacity)
                return;

            int capacity = Math.Max(segmentCount, 64);
            capacity = Mathf.NextPowerOfTwo(capacity);
            capacity = Math.Min(capacity, MaxSegments);

            if (textures.Boundary != null)
                UnityEngine.Object.Destroy(textures.Boundary);

            textures.Boundary = new Texture2D(
                capacity * PixelsPerBoundarySegment,
                1,
                TextureFormat.RGBAFloat,
                mipChain: false,
                linear: true)
            {
                name = "PaperBoundaryEdgeSegments",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            textures.BoundaryCapacity = capacity;
        }

        private static void UploadCreaseEdgeSegmentTexture(Renderer renderer, int segmentCount)
        {
            RendererSegmentTextures textures = GetRendererSegmentTextures(renderer);
            EnsureCreaseEdgeSegmentTextureCapacity(textures, segmentCount);

            int uploadFloatCount = textures.CreaseEdge.width * FloatsPerPixel;
            if (textures.CreaseEdgeUploadBuffer == null || textures.CreaseEdgeUploadBuffer.Length != uploadFloatCount)
                textures.CreaseEdgeUploadBuffer = new float[uploadFloatCount];

            Array.Clear(textures.CreaseEdgeUploadBuffer, 0, uploadFloatCount);
            Array.Copy(CreaseEdgeSegmentData, 0, textures.CreaseEdgeUploadBuffer, 0, segmentCount * FloatsPerSegment);
            textures.CreaseEdge.SetPixelData(textures.CreaseEdgeUploadBuffer, 0, 0);
            textures.CreaseEdge.Apply(false, false);
        }

        private static void EnsureCreaseEdgeSegmentTextureCapacity(RendererSegmentTextures textures, int segmentCount)
        {
            if (textures.CreaseEdge != null && segmentCount <= textures.CreaseEdgeCapacity)
                return;

            int capacity = Math.Max(segmentCount, 64);
            capacity = Mathf.NextPowerOfTwo(capacity);
            capacity = Math.Min(capacity, MaxSegments);

            if (textures.CreaseEdge != null)
                UnityEngine.Object.Destroy(textures.CreaseEdge);

            textures.CreaseEdge = new Texture2D(
                capacity * PixelsPerSegment,
                1,
                TextureFormat.RGBAFloat,
                mipChain: false,
                linear: true)
            {
                name = "PaperCreaseEdgeSegments",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            textures.CreaseEdgeCapacity = capacity;
        }

        private static bool IsBoundaryEdge(Edge edge)
        {
            return edge.Face1 == null || edge.Face2 == null;
        }

        private static bool IsFoldEdge(Edge edge)
        {
            if (edge.IsCreaseEdge)
                return false;

            if (edge.Face1 == null || edge.Face2 == null)
                return false;

            // Default Edge.FoldAngle is +180 (unset hinge connectors along fold thickness).
            if (Mathf.Abs(edge.FoldAngle - 180f) <= 0.01f)
                return false;

            return true;
        }

        private static bool IsCreaseEdge(Edge edge)
        {
            if (!edge.IsCreaseEdge)
                return false;

            return edge.Face1 != null && edge.Face2 != null;
        }
    }
}
