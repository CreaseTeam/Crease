using System.Collections.Generic;
using Crease.Folding.PaperGraph;
using UnityEngine;
using GraphMesh = Crease.Folding.PaperGraph.PaperGraph;

namespace Crease.Folding.Decals
{
    public class DecalSurfaceQuery
    {
        private const float FacingEpsilon = 0.01f;
        private const float DepthEpsilon = 0.0001f;
        private const float PlanarRayStartOffset = 0.5f;

        /// <summary>
        /// Canonical world-space view direction for the fixed folding camera layout.
        /// Guide placement uses step <see cref="ResolveGuideApproachNormal"/> instead of live camera pose.
        /// </summary>
        private static readonly Vector3 FoldingViewDirectionWorld = Vector3.down;

        /// <summary>
        /// View direction in graph-local space for the step's authored paper rotation.
        /// </summary>
        public static Vector3 ResolveGuideApproachNormal(Vector3 planeNormal, Vector3 stepPaperRotationEuler)
        {
            Vector3 viewDirGraph = Quaternion.Inverse(Quaternion.Euler(stepPaperRotationEuler)) * FoldingViewDirectionWorld;
            if (viewDirGraph.sqrMagnitude > 0.0001f)
                return viewDirGraph.normalized;

            planeNormal = planeNormal.sqrMagnitude > 0.0001f ? planeNormal.normalized : Vector3.up;
            return -planeNormal;
        }

        public static float ComputeGuideRayOffset(GraphMesh graph, Vector3 samplePointLocal, Vector3 approachNormalLocal)
        {
            if (graph == null || graph.Vertices.Count == 0 || approachNormalLocal.sqrMagnitude < 0.0001f)
                return PlanarRayStartOffset;

            approachNormalLocal = approachNormalLocal.normalized;
            float maxDepth = PlanarRayStartOffset;
            for (int i = 0; i < graph.Vertices.Count; i++)
            {
                float depth = Vector3.Dot(graph.Vertices[i].Position - samplePointLocal, approachNormalLocal);
                if (depth > maxDepth)
                    maxDepth = depth;
            }

            return maxDepth + PlanarRayStartOffset;
        }

        private static void BuildGuideRay(
            GraphMesh graph,
            Vector3 samplePointLocal,
            Vector3 approachNormalLocal,
            out Vector3 rayOriginLocal,
            out Vector3 rayDirLocal)
        {
            approachNormalLocal = approachNormalLocal.normalized;
            float offset = ComputeGuideRayOffset(graph, samplePointLocal, approachNormalLocal);
            rayOriginLocal = samplePointLocal + approachNormalLocal * offset;
            rayDirLocal = -approachNormalLocal;
        }

        public struct SurfaceHit
        {
            public bool Hit;
            public int Anchor0Index;
            public int Anchor1Index;
            public int Anchor2Index;
            public Vector3 Barycentric;
            public Vector3 LocalPoint;
            public Vector3 LocalNormal;
            public Vector2 SheetUv;
            public PaperSide Side;
            public Face HitFace;
            public Vector3 ViewRayOriginLocal;
            public Vector3 ViewRayDirLocal;
        }

        private readonly GraphMesh _authoringGraph;
        private readonly GraphMesh _surfaceGraph;
        private readonly Transform _meshSurfaceRoot;
        private readonly MeshCollider _meshCollider;
        private readonly Quaternion _meshVertexRotation;
        private readonly List<MeshTriangle> _triangles = new List<MeshTriangle>();
        private int _frontTriangleCount;

        private struct MeshTriangle
        {
            public int V0Index;
            public int V1Index;
            public int V2Index;
            public Face Face;
        }

        public DecalSurfaceQuery(
            GraphMesh authoringGraph,
            GraphMesh surfaceGraph,
            Transform meshSurfaceRoot,
            MeshCollider meshCollider,
            Quaternion meshVertexRotation = default)
        {
            _authoringGraph = authoringGraph;
            _surfaceGraph = surfaceGraph;
            _meshSurfaceRoot = meshSurfaceRoot;
            _meshCollider = meshCollider;
            _meshVertexRotation = meshVertexRotation == default ? Quaternion.identity : meshVertexRotation;
        }

        public void RebuildTriangleMap()
        {
            _triangles.Clear();
            if (_surfaceGraph == null) return;
            BuildTriangleList(_surfaceGraph, _triangles);
            _frontTriangleCount = _triangles.Count;

            int frontCount = _triangles.Count;
            for (int i = 0; i < frontCount; i++)
            {
                MeshTriangle tri = _triangles[i];
                _triangles.Add(new MeshTriangle
                {
                    V0Index = tri.V0Index,
                    V1Index = tri.V2Index,
                    V2Index = tri.V1Index,
                    Face = tri.Face
                });
            }
        }

        /// <summary>
        /// One entry per mesh fan triangle (matches submesh 0 topology). Back submesh reuses the same indices.
        /// Must skip degenerate fans the same way <see cref="GraphMesh.GenerateMesh"/> does so
        /// physics triangleIndex aligns with this list.
        /// </summary>
        private static void BuildTriangleList(GraphMesh graph, List<MeshTriangle> output)
        {
            output.Clear();
            if (graph == null) return;

            foreach (Face face in graph.Faces)
            {
                if (face.Vertices.Count < 3) continue;

                Vertex anchor = face.Vertices[0];
                int v0Index = graph.Vertices.IndexOf(anchor);
                for (int i = 1; i < face.Vertices.Count - 1; i++)
                {
                    Vertex v1 = face.Vertices[i];
                    Vertex v2 = face.Vertices[i + 1];

                    Vector3 edgeA = v2.Position - anchor.Position;
                    Vector3 edgeB = v1.Position - anchor.Position;
                    if (Vector3.Cross(edgeA, edgeB).sqrMagnitude < 0.000001f)
                        continue;

                    output.Add(new MeshTriangle
                    {
                        V0Index = v0Index,
                        V1Index = graph.Vertices.IndexOf(v2),
                        V2Index = graph.Vertices.IndexOf(v1),
                        Face = face
                    });
                }
            }
        }

        public SurfaceHit Raycast(Camera camera, Vector2 screenPosition)
        {
            SurfaceHit miss = new SurfaceHit { Hit = false };
            if (_meshCollider == null || _authoringGraph == null || _surfaceGraph == null || camera == null)
                return miss;

            Ray worldRay = camera.ScreenPointToRay(screenPosition);
            Vector3 rayOriginLocal = MeshLocalToGraph(_meshSurfaceRoot.InverseTransformPoint(worldRay.origin));
            Vector3 rayDirLocal = MeshLocalDirectionToGraph(_meshSurfaceRoot.InverseTransformDirection(worldRay.direction).normalized);
            return RaycastAlongRay(worldRay, rayOriginLocal, rayDirLocal, null);
        }

        /// <summary>
        /// Casts from above the folding plane along -planeNormal and returns the nearest surface hit
        /// on an allowed face. Uses analytic triangle tests in paper-local space so the query
        /// does not depend on a physics collider (authoring mesh is often not collidable).
        /// </summary>
        private static int BuildTriangleListWithBackFaces(GraphMesh graph, List<MeshTriangle> output)
        {
            BuildTriangleList(graph, output);
            int frontCount = output.Count;
            for (int i = 0; i < frontCount; i++)
            {
                MeshTriangle tri = output[i];
                output.Add(new MeshTriangle
                {
                    V0Index = tri.V0Index,
                    V1Index = tri.V2Index,
                    V2Index = tri.V1Index,
                    Face = tri.Face
                });
            }

            return frontCount;
        }

        /// <summary>
        /// Planar top-side raycast against the authoring graph in paper-local space.
        /// Used to author fold-guide placements before the current fold is applied.
        /// </summary>
        public static SurfaceHit RaycastPlanarTopOnGraph(
            GraphMesh authoringGraph,
            Vector3 samplePointLocal,
            Vector3 planeNormalLocal,
            HashSet<Face> allowedFaces)
        {
            return RaycastPlanarTopForGuide(
                authoringGraph,
                authoringGraph,
                samplePointLocal,
                planeNormalLocal,
                allowedFaces);
        }

        /// <summary>
        /// Planar raycast for fold guides. Geometry can come from the preview graph while
        /// allowed-face filtering uses authoring-graph face references.
        /// </summary>
        public static SurfaceHit RaycastPlanarTopForGuide(
            GraphMesh geometryGraph,
            GraphMesh faceFilterGraph,
            Vector3 samplePointLocal,
            Vector3 approachNormalLocal,
            HashSet<Face> allowedFaces)
        {
            SurfaceHit miss = new SurfaceHit { Hit = false };
            if (geometryGraph == null || approachNormalLocal.sqrMagnitude < 0.0001f)
                return miss;

            GraphMesh filterGraph = faceFilterGraph ?? geometryGraph;
            BuildGuideRay(geometryGraph, samplePointLocal, approachNormalLocal, out Vector3 rayOriginLocal, out Vector3 rayDirLocal);

            var triangles = new List<MeshTriangle>();
            int frontTriangleCount = BuildTriangleListWithBackFaces(geometryGraph, triangles);
            return RaycastTrianglesAnalyticOnGraph(
                geometryGraph,
                filterGraph,
                triangles,
                frontTriangleCount,
                rayOriginLocal,
                rayDirLocal,
                allowedFaces);
        }

        /// <summary>
        /// Finds the topmost guide hit on an allowed face that contains <paramref name="samplePointLocal"/>
        /// and faces the step approach direction. Uses point-in-triangle tests so crease edges
        /// and vertex crossings resolve reliably (rays can miss shared edges).
        /// Picks the outermost hit along the approach ray (furthest from the ray origin).
        /// </summary>
        public static void RaycastPlanarTopForGuideVisibleSides(
            GraphMesh geometryGraph,
            GraphMesh faceFilterGraph,
            Vector3 samplePointLocal,
            Vector3 approachNormalLocal,
            HashSet<Face> allowedFaces,
            List<SurfaceHit> results)
        {
            results.Clear();
            if (geometryGraph == null || approachNormalLocal.sqrMagnitude < 0.0001f)
                return;

            GraphMesh filterGraph = faceFilterGraph ?? geometryGraph;
            BuildGuideRay(geometryGraph, samplePointLocal, approachNormalLocal, out Vector3 rayOriginLocal, out Vector3 rayDirLocal);

            var triangles = new List<MeshTriangle>();
            int frontTriangleCount = BuildTriangleListWithBackFaces(geometryGraph, triangles);
            ResolveGuideHitsAtSurfacePoint(
                geometryGraph,
                filterGraph,
                triangles,
                frontTriangleCount,
                samplePointLocal,
                rayOriginLocal,
                rayDirLocal,
                allowedFaces,
                results);
        }

        public static Face FindFaceForTriangleIndices(GraphMesh graph, int i0, int i1, int i2)
        {
            if (graph == null)
                return null;

            foreach (Face face in graph.Faces)
            {
                if (face.Vertices.Count < 3)
                    continue;

                Vertex anchor = face.Vertices[0];
                int v0Index = graph.Vertices.IndexOf(anchor);
                for (int j = 1; j < face.Vertices.Count - 1; j++)
                {
                    int v1Index = graph.Vertices.IndexOf(face.Vertices[j]);
                    int v2Index = graph.Vertices.IndexOf(face.Vertices[j + 1]);
                    if (MatchesTriangleWinding(i0, i1, i2, v0Index, v1Index, v2Index))
                        return face;
                }
            }

            return null;
        }

        private static bool MatchesTriangleWinding(int i0, int i1, int i2, int a0, int a1, int a2)
        {
            return (i0 == a0 && i1 == a1 && i2 == a2)
                || (i0 == a0 && i1 == a2 && i2 == a1);
        }

        /// <summary>
        /// Analytic raycast against front and back triangles on the authoring graph.
        /// Returns the nearest hit along the ray (outermost visible surface from that direction).
        /// </summary>
        public static SurfaceHit RaycastOuterSurfaceOnGraph(
            GraphMesh graph,
            Vector3 rayOriginLocal,
            Vector3 rayDirLocal,
            HashSet<Face> allowedFaces = null)
        {
            SurfaceHit miss = new SurfaceHit { Hit = false };
            if (graph == null || rayDirLocal.sqrMagnitude < 0.0001f)
                return miss;

            var triangles = new List<MeshTriangle>();
            int frontTriangleCount = BuildTriangleListWithBackFaces(graph, triangles);
            return RaycastTrianglesAnalyticOnGraph(
                graph, graph, triangles, frontTriangleCount, rayOriginLocal, rayDirLocal.normalized, allowedFaces);
        }

        /// <summary>
        /// Analytic raycast that only accepts triangles facing the ray origin, matching the
        /// visible outer surface a camera/sticker placement ray would hit.
        /// </summary>
        public static SurfaceHit RaycastVisibleSurfaceOnGraph(
            GraphMesh graph,
            Vector3 rayOriginLocal,
            Vector3 rayDirLocal,
            HashSet<Face> allowedFaces = null)
        {
            SurfaceHit miss = new SurfaceHit { Hit = false };
            if (graph == null || rayDirLocal.sqrMagnitude < 0.0001f)
                return miss;

            var triangles = new List<MeshTriangle>();
            int frontTriangleCount = BuildTriangleListWithBackFaces(graph, triangles);
            return RaycastTrianglesAnalyticOnGraph(
                graph, graph, triangles, frontTriangleCount, rayOriginLocal, rayDirLocal.normalized, allowedFaces);
        }

        /// <summary>
        /// Picks random on-screen points and raycasts from the camera, using the same
        /// view-facing surface rules as sticker placement.
        /// </summary>
        public static bool TryRaycastVisibleFromCamera(
            GraphMesh graph,
            Camera camera,
            Transform surfaceRoot,
            Quaternion graphSpaceRotation,
            Vector2 screenPosition,
            out SurfaceHit hit)
        {
            hit = new SurfaceHit { Hit = false };
            if (graph == null || camera == null || surfaceRoot == null)
                return false;

            Ray worldRay = camera.ScreenPointToRay(screenPosition);
            Vector3 rayOriginLocal = surfaceRoot.InverseTransformPoint(worldRay.origin);
            Vector3 rayDirLocal = surfaceRoot.InverseTransformDirection(worldRay.direction).normalized;
            if (graphSpaceRotation != Quaternion.identity)
            {
                Quaternion inverseRotation = Quaternion.Inverse(graphSpaceRotation);
                rayOriginLocal = inverseRotation * rayOriginLocal;
                rayDirLocal = inverseRotation * rayDirLocal;
            }

            hit = RaycastVisibleSurfaceOnGraph(graph, rayOriginLocal, rayDirLocal);
            return hit.Hit;
        }

        public static bool TryGetRandomVisibleSurfaceFromCamera(
            GraphMesh graph,
            Camera camera,
            Transform surfaceRoot,
            Quaternion graphSpaceRotation,
            out SurfaceHit hit,
            int maxAttempts = 16,
            float screenMargin = 0.15f)
        {
            hit = new SurfaceHit { Hit = false };
            if (graph == null || camera == null || surfaceRoot == null)
                return false;

            float minX = screenMargin * camera.pixelWidth;
            float maxX = (1f - screenMargin) * camera.pixelWidth;
            float minY = screenMargin * camera.pixelHeight;
            float maxY = (1f - screenMargin) * camera.pixelHeight;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var screenPosition = new Vector2(
                    Random.Range(minX, maxX),
                    Random.Range(minY, maxY));

                if (!TryRaycastVisibleFromCamera(
                        graph,
                        camera,
                        surfaceRoot,
                        graphSpaceRotation,
                        screenPosition,
                        out SurfaceHit candidate))
                    continue;

                hit = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Picks a random visible outer-surface point by casting from random exterior directions
        /// into the graph. Only camera-facing hits are accepted, so folded interior faces are skipped.
        /// </summary>
        public static bool TryGetRandomVisibleSurfaceOnGraph(
            GraphMesh graph,
            out SurfaceHit hit,
            int maxAttempts = 32)
        {
            hit = new SurfaceHit { Hit = false };
            if (graph == null || graph.Vertices.Count == 0)
                return false;

            ComputeGraphBounds(graph, out Vector3 center, out Vector3 min, out Vector3 max, out float enclosingRadius);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector3 target = new Vector3(
                    Random.Range(min.x, max.x),
                    Random.Range(min.y, max.y),
                    Random.Range(min.z, max.z));
                Vector3 outward = Random.onUnitSphere;
                Vector3 rayOriginLocal = target + outward * enclosingRadius;
                Vector3 rayDirLocal = -outward;
                hit = RaycastVisibleSurfaceOnGraph(graph, rayOriginLocal, rayDirLocal);
                if (hit.Hit)
                    return true;
            }

            Vector3 fallbackOrigin = center + Vector3.up * enclosingRadius;
            hit = RaycastVisibleSurfaceOnGraph(graph, fallbackOrigin, -Vector3.up);
            return hit.Hit;
        }

        /// <summary>
        /// Axis-aligned bounds plus a sphere that encloses every graph vertex from the centroid.
        /// </summary>
        private static void ComputeGraphBounds(
            GraphMesh graph,
            out Vector3 center,
            out Vector3 min,
            out Vector3 max,
            out float enclosingRadius)
        {
            min = graph.Vertices[0].Position;
            max = min;
            Vector3 sum = min;
            for (int i = 1; i < graph.Vertices.Count; i++)
            {
                Vector3 position = graph.Vertices[i].Position;
                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
                sum += position;
            }

            center = sum / graph.Vertices.Count;
            enclosingRadius = 0f;
            for (int i = 0; i < graph.Vertices.Count; i++)
            {
                float distance = (graph.Vertices[i].Position - center).magnitude;
                if (distance > enclosingRadius)
                    enclosingRadius = distance;
            }

            enclosingRadius += 0.5f;
        }

        private static SurfaceHit BuildGraphSurfaceHit(
            GraphMesh graph,
            MeshTriangle tri,
            Vector3 bary,
            PaperSide side,
            Vector3 viewOriginLocal,
            Vector3 viewDirLocal,
            Face hitFace = null)
        {
            Vertex hitV0 = graph.Vertices[tri.V0Index];
            Vertex hitV1 = graph.Vertices[tri.V1Index];
            Vertex hitV2 = graph.Vertices[tri.V2Index];
            Vector3 localPoint = bary.x * hitV0.Position + bary.y * hitV1.Position + bary.z * hitV2.Position;
            Vector3 authBary = ComputeBarycentric(hitV0.Position, hitV1.Position, hitV2.Position, localPoint);
            Vector3 localNormal = OutwardNormalForSide(hitV0.Position, hitV1.Position, hitV2.Position, side);

            return new SurfaceHit
            {
                Hit = true,
                Anchor0Index = tri.V0Index,
                Anchor1Index = tri.V1Index,
                Anchor2Index = tri.V2Index,
                Barycentric = authBary,
                LocalPoint = localPoint,
                LocalNormal = localNormal,
                SheetUv = BarycentricInterpolateSheetUv(hitV0, hitV1, hitV2, side, authBary),
                Side = side,
                HitFace = hitFace ?? tri.Face,
                ViewRayOriginLocal = viewOriginLocal,
                ViewRayDirLocal = viewDirLocal
            };
        }

        public SurfaceHit RaycastPlanarTop(
            Vector3 samplePointLocal,
            Vector3 approachNormalLocal,
            HashSet<Face> allowedFaces)
        {
            if (_authoringGraph == null)
                return new SurfaceHit { Hit = false };

            if (approachNormalLocal.sqrMagnitude < 0.0001f)
                return new SurfaceHit { Hit = false };

            GraphMesh offsetGraph = _surfaceGraph != null ? _surfaceGraph : _authoringGraph;
            BuildGuideRay(offsetGraph, samplePointLocal, approachNormalLocal, out Vector3 rayOriginLocal, out Vector3 rayDirLocal);

            if (_meshCollider != null && _meshSurfaceRoot != null)
            {
                Vector3 meshOrigin = GraphLocalToMesh(rayOriginLocal);
                Vector3 meshDir = GraphLocalDirectionToMesh(rayDirLocal);
                Ray worldRay = new Ray(_meshSurfaceRoot.TransformPoint(meshOrigin), _meshSurfaceRoot.TransformDirection(meshDir));
                if (TryGetNearestHitAlongRay(worldRay, allowedFaces, out RaycastHit physicsHit))
                    return BuildSurfaceHit(physicsHit, rayOriginLocal, rayDirLocal);
            }

            if (_authoringGraph == _surfaceGraph)
                return RaycastPlanarTopOnGraph(_authoringGraph, samplePointLocal, approachNormalLocal, allowedFaces);

            return RaycastTrianglesAnalytic(rayOriginLocal, rayDirLocal, allowedFaces);
        }

        private Vector3 GraphLocalToMesh(Vector3 graphLocalPoint)
        {
            if (_meshVertexRotation == Quaternion.identity)
                return graphLocalPoint;
            return _meshVertexRotation * graphLocalPoint;
        }

        private Vector3 GraphLocalDirectionToMesh(Vector3 graphLocalDirection)
        {
            if (_meshVertexRotation == Quaternion.identity)
                return graphLocalDirection;
            return _meshVertexRotation * graphLocalDirection;
        }

        private static SurfaceHit RaycastTrianglesAnalyticOnGraph(
            GraphMesh graph,
            GraphMesh faceFilterGraph,
            List<MeshTriangle> triangles,
            int frontTriangleCount,
            Vector3 rayOriginLocal,
            Vector3 rayDirLocal,
            HashSet<Face> allowedFaces)
        {
            SurfaceHit miss = new SurfaceHit { Hit = false };
            if (triangles.Count == 0)
                return miss;

            GraphMesh filterGraph = faceFilterGraph ?? graph;
            rayDirLocal = rayDirLocal.normalized;
            bool found = false;
            float bestT = float.MaxValue;
            MeshTriangle bestTri = default;
            Vector3 bestBary = default;
            PaperSide bestSide = PaperSide.Front;

            for (int t = 0; t < triangles.Count; t++)
            {
                MeshTriangle tri = triangles[t];
                if (allowedFaces != null)
                {
                    Face filterFace = filterGraph == graph
                        ? tri.Face
                        : FindFaceForTriangleIndices(filterGraph, tri.V0Index, tri.V1Index, tri.V2Index);
                    if (filterFace == null || !allowedFaces.Contains(filterFace))
                        continue;
                }

                if (!IsValidIndex(graph, tri.V0Index)
                    || !IsValidIndex(graph, tri.V1Index)
                    || !IsValidIndex(graph, tri.V2Index))
                    continue;

                Vertex v0 = graph.Vertices[tri.V0Index];
                Vertex v1 = graph.Vertices[tri.V1Index];
                Vertex v2 = graph.Vertices[tri.V2Index];
                Vector3 p0 = v0.Position;
                Vector3 p1 = v1.Position;
                Vector3 p2 = v2.Position;

                if (!TryRayIntersectTriangle(rayOriginLocal, rayDirLocal, p0, p1, p2, out float tHit, out Vector3 bary))
                    continue;

                Vector3 triNormal = ComputeFrontNormal(p0, p1, p2);
                if (Vector3.Dot(triNormal, rayDirLocal) >= -FacingEpsilon)
                    continue;

                if (tHit >= bestT - DepthEpsilon)
                    continue;

                bestT = tHit;
                bestTri = tri;
                bestBary = bary;
                bestSide = frontTriangleCount > 0 && t >= frontTriangleCount ? PaperSide.Back : PaperSide.Front;
                found = true;
            }

            if (!found)
                return miss;

            Face hitFace = filterGraph == graph
                ? bestTri.Face
                : FindFaceForTriangleIndices(filterGraph, bestTri.V0Index, bestTri.V1Index, bestTri.V2Index);
            return BuildGraphSurfaceHit(
                graph,
                bestTri,
                bestBary,
                bestSide,
                rayOriginLocal,
                rayDirLocal,
                hitFace);
        }

        private const float SurfacePointBarycentricEpsilon = 0.002f;

        private static bool ContainsBarycentric(Vector3 bary, float epsilon = SurfacePointBarycentricEpsilon)
        {
            return bary.x >= -epsilon && bary.y >= -epsilon && bary.z >= -epsilon;
        }

        private static void ResolveGuideHitsAtSurfacePoint(
            GraphMesh graph,
            GraphMesh faceFilterGraph,
            List<MeshTriangle> triangles,
            int frontTriangleCount,
            Vector3 surfacePoint,
            Vector3 rayOriginLocal,
            Vector3 rayDirLocal,
            HashSet<Face> allowedFaces,
            List<SurfaceHit> results)
        {
            if (triangles.Count == 0)
                return;

            GraphMesh filterGraph = faceFilterGraph ?? graph;
            rayDirLocal = rayDirLocal.normalized;
            bool found = false;
            float bestDepth = float.MinValue;
            MeshTriangle bestTri = default;
            Vector3 bestBary = default;
            PaperSide bestSide = PaperSide.Front;
            Face bestFace = null;

            for (int t = 0; t < triangles.Count; t++)
            {
                MeshTriangle tri = triangles[t];
                Face filterFace = null;
                if (allowedFaces != null)
                {
                    filterFace = filterGraph == graph
                        ? tri.Face
                        : FindFaceForTriangleIndices(filterGraph, tri.V0Index, tri.V1Index, tri.V2Index);
                    if (filterFace == null || !allowedFaces.Contains(filterFace))
                        continue;
                }

                if (!IsValidIndex(graph, tri.V0Index)
                    || !IsValidIndex(graph, tri.V1Index)
                    || !IsValidIndex(graph, tri.V2Index))
                    continue;

                Vertex v0 = graph.Vertices[tri.V0Index];
                Vertex v1 = graph.Vertices[tri.V1Index];
                Vertex v2 = graph.Vertices[tri.V2Index];
                Vector3 p0 = v0.Position;
                Vector3 p1 = v1.Position;
                Vector3 p2 = v2.Position;

                Vector3 bary = ComputeBarycentric(p0, p1, p2, surfacePoint);
                if (!ContainsBarycentric(bary))
                    continue;

                Vector3 triNormal = ComputeFrontNormal(p0, p1, p2);
                if (Vector3.Dot(triNormal, rayDirLocal) >= -FacingEpsilon)
                    continue;

                if (filterFace == null)
                    filterFace = tri.Face;

                if (filterFace == null)
                    continue;

                Vector3 hitPoint = bary.x * p0 + bary.y * p1 + bary.z * p2;
                float depth = Vector3.Dot(hitPoint - rayOriginLocal, rayDirLocal);
                if (depth < DepthEpsilon || depth <= bestDepth + DepthEpsilon)
                    continue;

                bestDepth = depth;
                bestTri = tri;
                bestBary = bary;
                bestSide = frontTriangleCount > 0 && t >= frontTriangleCount ? PaperSide.Back : PaperSide.Front;
                bestFace = filterFace;
                found = true;
            }

            if (!found)
                return;

            results.Add(BuildGraphSurfaceHit(
                graph,
                bestTri,
                bestBary,
                bestSide,
                rayOriginLocal,
                rayDirLocal,
                bestFace));
        }

        private SurfaceHit RaycastTrianglesAnalytic(
            Vector3 rayOriginLocal,
            Vector3 rayDirLocal,
            HashSet<Face> allowedFaces)
        {
            SurfaceHit miss = new SurfaceHit { Hit = false };
            if (_triangles.Count == 0)
                return miss;

            rayDirLocal = rayDirLocal.normalized;
            bool found = false;
            float bestT = float.MaxValue;
            MeshTriangle bestTri = default;
            Vector3 bestBary = default;
            PaperSide bestSide = PaperSide.Front;

            for (int t = 0; t < _triangles.Count; t++)
            {
                MeshTriangle tri = _triangles[t];
                if (allowedFaces != null)
                {
                    Face filterFace = FindFaceForTriangleIndices(_authoringGraph, tri.V0Index, tri.V1Index, tri.V2Index);
                    if (filterFace == null || !allowedFaces.Contains(filterFace))
                        continue;
                }

                if (!TryResolveAuthoringTriangle(tri, out int i0, out int i1, out int i2))
                    continue;

                Vertex sv0 = _surfaceGraph.Vertices[tri.V0Index];
                Vertex sv1 = _surfaceGraph.Vertices[tri.V1Index];
                Vertex sv2 = _surfaceGraph.Vertices[tri.V2Index];
                Vector3 p0 = sv0.Position;
                Vector3 p1 = sv1.Position;
                Vector3 p2 = sv2.Position;

                if (!TryRayIntersectTriangle(rayOriginLocal, rayDirLocal, p0, p1, p2, out float tHit, out Vector3 bary))
                    continue;

                Vector3 triNormal = ComputeFrontNormal(p0, p1, p2);
                if (Vector3.Dot(triNormal, rayDirLocal) >= -FacingEpsilon)
                    continue;

                if (tHit >= bestT - DepthEpsilon)
                    continue;

                bestT = tHit;
                bestTri = tri;
                bestBary = bary;
                bestSide = _frontTriangleCount > 0 && t >= _frontTriangleCount ? PaperSide.Back : PaperSide.Front;
                found = true;
            }

            if (!found)
                return miss;

            if (!TryResolveAuthoringTriangle(bestTri, out int a0, out int a1, out int a2))
                return miss;

            Vertex v0 = _authoringGraph.Vertices[a0];
            Vertex v1 = _authoringGraph.Vertices[a1];
            Vertex v2 = _authoringGraph.Vertices[a2];
            Vector3 localPoint = bestBary.x * v0.Position + bestBary.y * v1.Position + bestBary.z * v2.Position;
            Vector3 authBary = ComputeBarycentric(v0.Position, v1.Position, v2.Position, localPoint);
            Vector3 localNormal = OutwardNormalForSide(v0.Position, v1.Position, v2.Position, bestSide);

            Face hitFace = FindFaceForTriangleIndices(_authoringGraph, a0, a1, a2) ?? bestTri.Face;
            return new SurfaceHit
            {
                Hit = true,
                Anchor0Index = a0,
                Anchor1Index = a1,
                Anchor2Index = a2,
                Barycentric = authBary,
                LocalPoint = localPoint,
                LocalNormal = localNormal,
                SheetUv = BarycentricInterpolateSheetUv(v0, v1, v2, bestSide, authBary),
                Side = bestSide,
                HitFace = hitFace,
                ViewRayOriginLocal = rayOriginLocal,
                ViewRayDirLocal = rayDirLocal
            };
        }

        private static bool TryRayIntersectTriangle(
            Vector3 rayOrigin,
            Vector3 rayDir,
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            out float t,
            out Vector3 barycentric)
        {
            t = 0f;
            barycentric = default;

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 pvec = Vector3.Cross(rayDir, edge2);
            float det = Vector3.Dot(edge1, pvec);
            if (Mathf.Abs(det) < 1e-8f)
                return false;

            float invDet = 1f / det;
            Vector3 tvec = rayOrigin - v0;
            float u = Vector3.Dot(tvec, pvec) * invDet;
            if (u < -0.001f || u > 1.001f)
                return false;

            Vector3 qvec = Vector3.Cross(tvec, edge1);
            float v = Vector3.Dot(rayDir, qvec) * invDet;
            if (v < -0.001f || u + v > 1.001f)
                return false;

            t = Vector3.Dot(edge2, qvec) * invDet;
            if (t < DepthEpsilon)
                return false;

            barycentric = new Vector3(1f - u - v, u, v);
            return true;
        }

        private SurfaceHit RaycastAlongRay(
            Ray worldRay,
            Vector3 rayOriginLocal,
            Vector3 rayDirLocal,
            HashSet<Face> allowedFaces)
        {
            SurfaceHit miss = new SurfaceHit { Hit = false };
            if (!TryGetNearestHitAlongRay(worldRay, allowedFaces, out RaycastHit physicsHit))
                return miss;

            return BuildSurfaceHit(physicsHit, rayOriginLocal, rayDirLocal);
        }

        private bool TryGetNearestHitAlongRay(Ray worldRay, HashSet<Face> allowedFaces, out RaycastHit bestHit)
        {
            bestHit = default;
            if (_meshCollider == null)
                return false;
            RaycastHit[] hits = Physics.RaycastAll(worldRay, 100f);
            bool found = false;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider != _meshCollider)
                    continue;

                if (allowedFaces != null)
                {
                    Face face = GetFaceForPhysicsHit(hit);
                    if (face == null || !allowedFaces.Contains(face))
                        continue;
                }

                if (hit.distance < bestDistance - DepthEpsilon)
                {
                    bestDistance = hit.distance;
                    bestHit = hit;
                    found = true;
                }
            }

            return found;
        }

        private Face GetFaceForPhysicsHit(RaycastHit hit)
        {
            int triangleIndex = hit.triangleIndex;
            if (triangleIndex < 0)
                return null;

            int logicalIndex = triangleIndex;
            if (_frontTriangleCount > 0 && logicalIndex >= _frontTriangleCount)
                logicalIndex -= _frontTriangleCount;

            if (logicalIndex < 0 || logicalIndex >= _triangles.Count)
                return null;

            MeshTriangle tri = _triangles[logicalIndex];
            return FindFaceForTriangleIndices(_authoringGraph, tri.V0Index, tri.V1Index, tri.V2Index);
        }

        private Vector3 MeshLocalToGraph(Vector3 meshLocalPoint)
        {
            if (_meshVertexRotation == Quaternion.identity)
                return meshLocalPoint;
            return Quaternion.Inverse(_meshVertexRotation) * meshLocalPoint;
        }

        private Vector3 MeshLocalDirectionToGraph(Vector3 meshLocalDirection)
        {
            if (_meshVertexRotation == Quaternion.identity)
                return meshLocalDirection;
            return Quaternion.Inverse(_meshVertexRotation) * meshLocalDirection;
        }

        private SurfaceHit BuildSurfaceHit(RaycastHit physicsHit, Vector3 viewOriginLocal, Vector3 viewDirLocal)
        {
            SurfaceHit miss = new SurfaceHit { Hit = false };
            Vector3 localPoint = MeshLocalToGraph(_meshSurfaceRoot.InverseTransformPoint(physicsHit.point));
            Vector3 localNormal = MeshLocalDirectionToGraph(
                _meshSurfaceRoot.InverseTransformDirection(physicsHit.normal).normalized);

            int i0;
            int i1;
            int i2;
            Vector3 bary;
            PaperSide side;

            int triangleIndex = physicsHit.triangleIndex;
            if (triangleIndex < 0)
            {
                if (!TryFindTriangleFromLocalPoint(localPoint, viewOriginLocal, viewDirLocal, out i0, out i1, out i2, out bary, out side, out localNormal))
                    return miss;
            }
            else
            {
                int logicalIndex = triangleIndex;
                if (_frontTriangleCount > 0 && logicalIndex >= _frontTriangleCount)
                    logicalIndex -= _frontTriangleCount;
                if (logicalIndex < 0 || logicalIndex >= _triangles.Count)
                    return miss;

                MeshTriangle tri = _triangles[logicalIndex];
                if (!TryResolveAuthoringTriangle(tri, out i0, out i1, out i2))
                    return miss;

                Vertex surface0 = _surfaceGraph.Vertices[i0];
                Vertex surface1 = _surfaceGraph.Vertices[i1];
                Vertex surface2 = _surfaceGraph.Vertices[i2];

                // Submesh 0 = front, submesh 1 = back (see PaperGraph.GenerateMesh). This matches the
                // mesh the physics ray actually hit and stays correct when the preview mesh is folded.
                bool hitBackSubmesh = _frontTriangleCount > 0 && triangleIndex >= _frontTriangleCount;
                side = hitBackSubmesh ? PaperSide.Back : PaperSide.Front;
                bary = ComputeBarycentric(surface0.Position, surface1.Position, surface2.Position, localPoint);
            }

            Vertex v0 = _authoringGraph.Vertices[i0];
            Vertex v1 = _authoringGraph.Vertices[i1];
            Vertex v2 = _authoringGraph.Vertices[i2];

            return new SurfaceHit
            {
                Hit = true,
                Anchor0Index = i0,
                Anchor1Index = i1,
                Anchor2Index = i2,
                Barycentric = bary,
                LocalPoint = localPoint,
                LocalNormal = localNormal,
                SheetUv = BarycentricInterpolateSheetUv(v0, v1, v2, side, bary),
                Side = side,
                HitFace = GetFaceForPhysicsHit(physicsHit),
                ViewRayOriginLocal = viewOriginLocal,
                ViewRayDirLocal = viewDirLocal
            };
        }

        private bool TryFindTriangleFromLocalPoint(
            Vector3 localPoint,
            Vector3 viewOriginLocal,
            Vector3 viewDirLocal,
            out int i0,
            out int i1,
            out int i2,
            out Vector3 barycentric,
            out PaperSide side,
            out Vector3 localNormal)
        {
            i0 = -1;
            i1 = -1;
            i2 = -1;
            barycentric = default;
            side = PaperSide.Front;
            localNormal = Vector3.up;

            bool found = false;
            float bestDepth = float.MaxValue;

            int triangleCount = _frontTriangleCount > 0 ? _frontTriangleCount : _triangles.Count;

            for (int t = 0; t < triangleCount; t++)
            {
                MeshTriangle tri = _triangles[t];
                if (!TryResolveAuthoringTriangle(tri, out int a0, out int a1, out int a2))
                    continue;

                Vertex sv0 = _surfaceGraph.Vertices[tri.V0Index];
                Vertex sv1 = _surfaceGraph.Vertices[tri.V1Index];
                Vertex sv2 = _surfaceGraph.Vertices[tri.V2Index];
                Vector3 p0 = sv0.Position;
                Vector3 p1 = sv1.Position;
                Vector3 p2 = sv2.Position;

                Vector3 candidateBary = ComputeBarycentric(p0, p1, p2, localPoint);
                if (candidateBary.x < -0.02f || candidateBary.y < -0.02f || candidateBary.z < -0.02f)
                    continue;

                float depth = Vector3.Dot(localPoint - viewOriginLocal, viewDirLocal);
                if (depth < DepthEpsilon || depth >= bestDepth - DepthEpsilon)
                    continue;

                PaperSide candidateSide = ResolveCameraFacingSide(p0, p1, p2, viewDirLocal);
                bestDepth = depth;
                i0 = a0;
                i1 = a1;
                i2 = a2;
                barycentric = ComputeBarycentric(p0, p1, p2, localPoint);
                side = candidateSide;
                localNormal = OutwardNormalForSide(p0, p1, p2, candidateSide);
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Returns true when a paper surface hit lies within a placed sticker's oriented bounds.
        /// </summary>
        public static bool TrySurfaceHitOverlapsPlacement(
            GraphMesh graph,
            DecalPlacement placement,
            SurfaceHit hit,
            float pickSlop = 1.05f)
        {
            if (graph == null || !hit.Hit || hit.Side != placement.Side)
                return false;

            Vector2 size = DecalStampClipUtility.GetPlacementSize(placement);
            size *= pickSlop;

            float halfWidthUv = size.x * 0.5f / graph.Width;
            float halfHeightUv = size.y * 0.5f / graph.Height;

            Vector2 offset = hit.SheetUv - placement.SheetUv;
            float rotationRad = -placement.RotationUv * Mathf.Deg2Rad;
            if (placement.UseAxisAlignment && placement.AlignAxisLocal.sqrMagnitude > 0.0001f)
            {
                Vector2 axis = new Vector2(placement.AlignAxisLocal.x, placement.AlignAxisLocal.z).normalized;
                rotationRad = -Mathf.Atan2(axis.x, axis.y);
            }

            float cos = Mathf.Cos(rotationRad);
            float sin = Mathf.Sin(rotationRad);
            Vector2 rotated = new Vector2(
                offset.x * cos - offset.y * sin,
                offset.x * sin + offset.y * cos);

            return Mathf.Abs(rotated.x) <= halfWidthUv && Mathf.Abs(rotated.y) <= halfHeightUv;
        }

        private bool TryResolveAuthoringTriangle(MeshTriangle tri, out int i0, out int i1, out int i2)
        {
            i0 = tri.V0Index;
            i1 = tri.V1Index;
            i2 = tri.V2Index;

            if (_authoringGraph.Vertices.Count != _surfaceGraph.Vertices.Count)
                return false;

            return IsValidIndex(_authoringGraph, i0)
                && IsValidIndex(_authoringGraph, i1)
                && IsValidIndex(_authoringGraph, i2);
        }

        /// <summary>
        /// Picks the paper side whose outward normal matches the camera-facing hit normal.
        /// </summary>
        private static PaperSide ResolveCameraFacingSide(
            Vector3 p0, Vector3 p1, Vector3 p2,
            Vector3 localHitNormal,
            Vector3 viewDirLocal)
        {
            Vector3 frontNormal = ComputeFrontNormal(p0, p1, p2);
            Vector3 backNormal = -frontNormal;

            bool frontFacesCamera = Vector3.Dot(frontNormal, viewDirLocal) < -FacingEpsilon;
            bool backFacesCamera = Vector3.Dot(backNormal, viewDirLocal) < -FacingEpsilon;

            if (frontFacesCamera && !backFacesCamera) return PaperSide.Front;
            if (backFacesCamera && !frontFacesCamera) return PaperSide.Back;

            float frontAlign = Vector3.Dot(localHitNormal, frontNormal);
            float backAlign = Vector3.Dot(localHitNormal, backNormal);
            return frontAlign >= backAlign ? PaperSide.Front : PaperSide.Back;
        }

        private static PaperSide ResolveCameraFacingSide(
            Vector3 p0, Vector3 p1, Vector3 p2,
            Vector3 viewDirLocal)
        {
            Vector3 frontNormal = ComputeFrontNormal(p0, p1, p2);
            return Vector3.Dot(frontNormal, viewDirLocal) < -FacingEpsilon
                ? PaperSide.Front
                : PaperSide.Back;
        }

        /// <summary>
        /// Matches GenerateMesh front-submesh winding (i0, i2, i1) with fan indices (v0, v{i+1}, v{i}).
        /// </summary>
        private static Vector3 ComputeFrontNormal(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 e1 = p1 - p0;
            Vector3 e2 = p2 - p0;
            Vector3 normal = Vector3.Cross(e2, e1);
            if (normal.sqrMagnitude < 0.000001f)
                return Vector3.up;
            return normal.normalized;
        }

        private static Vector3 OutwardNormalForSide(Vector3 p0, Vector3 p1, Vector3 p2, PaperSide side)
        {
            Vector3 frontNormal = ComputeFrontNormal(p0, p1, p2);
            return side == PaperSide.Front ? frontNormal : -frontNormal;
        }

        public static Vector3 ComputeBarycentric(Vector3 a, Vector3 b, Vector3 c, Vector3 p)
        {
            Vector3 v0 = b - a;
            Vector3 v1 = c - a;
            Vector3 v2 = p - a;
            float d00 = Vector3.Dot(v0, v0);
            float d01 = Vector3.Dot(v0, v1);
            float d11 = Vector3.Dot(v1, v1);
            float d20 = Vector3.Dot(v2, v0);
            float d21 = Vector3.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < 0.000001f) return new Vector3(1f, 0f, 0f);
            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            return new Vector3(1f - v - w, v, w);
        }

        private static Vector2 VertexSheetUv(Vertex vertex, PaperSide side)
        {
            if (side == PaperSide.Back)
                return new Vector2(1f - vertex.Uv.x, vertex.Uv.y);
            return vertex.Uv;
        }

        private static Vector2 BarycentricInterpolateSheetUv(
            Vertex v0,
            Vertex v1,
            Vertex v2,
            PaperSide side,
            Vector3 bary)
        {
            Vector2 uv0 = VertexSheetUv(v0, side);
            Vector2 uv1 = VertexSheetUv(v1, side);
            Vector2 uv2 = VertexSheetUv(v2, side);
            return bary.x * uv0 + bary.y * uv1 + bary.z * uv2;
        }

        public static bool TryGetAnchorVertices(GraphMesh authoringGraph, DecalPlacement placement, out Vertex v0, out Vertex v1, out Vertex v2)
        {
            v0 = null;
            v1 = null;
            v2 = null;
            if (authoringGraph == null) return false;
            if (!IsValidIndex(authoringGraph, placement.Anchor0Index)
                || !IsValidIndex(authoringGraph, placement.Anchor1Index)
                || !IsValidIndex(authoringGraph, placement.Anchor2Index))
                return false;

            v0 = authoringGraph.Vertices[placement.Anchor0Index];
            v1 = authoringGraph.Vertices[placement.Anchor1Index];
            v2 = authoringGraph.Vertices[placement.Anchor2Index];
            return true;
        }

        public static Vector2 ResolveSheetUv(GraphMesh authoringGraph, DecalPlacement placement)
        {
            if (!TryGetAnchorVertices(authoringGraph, placement, out Vertex v0, out Vertex v1, out Vertex v2))
                return placement.SheetUv;

            return BarycentricInterpolateSheetUv(v0, v1, v2, placement.Side, placement.Barycentric);
        }

        /// <summary>
        /// Extrapolates sheet UV along a guide axis on the hit triangle. UV is in the
        /// side-specific space used by decal RT stamping (back U is already mirrored).
        /// </summary>
        public static bool TryInterpolateSheetUvAlongAxis(
            GraphMesh graph,
            SurfaceHit hit,
            Vector3 axisLocal,
            float distance,
            out Vector2 sheetUv)
        {
            sheetUv = default;
            if (graph == null || !hit.Hit || axisLocal.sqrMagnitude < 0.0001f || distance <= 0f)
                return false;

            if (!IsValidIndex(graph, hit.Anchor0Index)
                || !IsValidIndex(graph, hit.Anchor1Index)
                || !IsValidIndex(graph, hit.Anchor2Index))
                return false;

            Vertex v0 = graph.Vertices[hit.Anchor0Index];
            Vertex v1 = graph.Vertices[hit.Anchor1Index];
            Vertex v2 = graph.Vertices[hit.Anchor2Index];
            Vector3 p0 = v0.Position;
            Vector3 p1 = v1.Position;
            Vector3 p2 = v2.Position;
            Vector3 samplePoint = hit.LocalPoint + axisLocal.normalized * distance;
            Vector3 bary = ComputeBarycentric(p0, p1, p2, samplePoint);
            sheetUv = BarycentricInterpolateSheetUv(v0, v1, v2, hit.Side, bary);
            return true;
        }

        private static bool IsValidIndex(GraphMesh graph, int index)
        {
            return index >= 0 && index < graph.Vertices.Count;
        }
    }
}
