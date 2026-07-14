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

        public struct ResolvedSurfaceFrame
        {
            public bool Found;
            public int Anchor0Index;
            public int Anchor1Index;
            public int Anchor2Index;
            public Vector3 Barycentric;
            public Vector3 Position;
            public Vector3 Normal;
        }

        private readonly GraphMesh _authoringGraph;
        private readonly GraphMesh _surfaceGraph;
        private readonly Transform _meshSurfaceRoot;
        private readonly MeshCollider _meshCollider;
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
            MeshCollider meshCollider)
        {
            _authoringGraph = authoringGraph;
            _surfaceGraph = surfaceGraph;
            _meshSurfaceRoot = meshSurfaceRoot;
            _meshCollider = meshCollider;
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
            Vector3 rayOriginLocal = _meshSurfaceRoot.InverseTransformPoint(worldRay.origin);
            Vector3 rayDirLocal = _meshSurfaceRoot.InverseTransformDirection(worldRay.direction).normalized;
            return RaycastAlongRay(worldRay, rayOriginLocal, rayDirLocal, null);
        }

        /// <summary>
        /// Casts from above the folding plane along -planeNormal and returns the nearest surface hit
        /// on an allowed face. Uses analytic triangle tests in paper-local space so the query
        /// does not depend on a physics collider (authoring mesh is often not collidable).
        /// </summary>
        private static void BuildTriangleListWithBackFaces(GraphMesh graph, List<MeshTriangle> output)
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
            SurfaceHit miss = new SurfaceHit { Hit = false };
            if (authoringGraph == null || planeNormalLocal.sqrMagnitude < 0.0001f)
                return miss;

            planeNormalLocal = planeNormalLocal.normalized;
            Vector3 rayOriginLocal = samplePointLocal + planeNormalLocal * PlanarRayStartOffset;
            Vector3 rayDirLocal = -planeNormalLocal;

            var triangles = new List<MeshTriangle>();
            BuildTriangleListWithBackFaces(authoringGraph, triangles);
            return RaycastTrianglesAnalyticOnGraph(authoringGraph, triangles, rayOriginLocal, rayDirLocal, allowedFaces);
        }

        public SurfaceHit RaycastPlanarTop(
            Vector3 samplePointLocal,
            Vector3 planeNormalLocal,
            HashSet<Face> allowedFaces)
        {
            if (_authoringGraph == null)
                return new SurfaceHit { Hit = false };

            if (_authoringGraph == _surfaceGraph)
                return RaycastPlanarTopOnGraph(_authoringGraph, samplePointLocal, planeNormalLocal, allowedFaces);

            SurfaceHit miss = new SurfaceHit { Hit = false };
            if (planeNormalLocal.sqrMagnitude < 0.0001f)
                return miss;

            planeNormalLocal = planeNormalLocal.normalized;
            Vector3 rayOriginLocal = samplePointLocal + planeNormalLocal * PlanarRayStartOffset;
            Vector3 rayDirLocal = -planeNormalLocal;
            return RaycastTrianglesAnalytic(rayOriginLocal, rayDirLocal, allowedFaces);
        }

        private static SurfaceHit RaycastTrianglesAnalyticOnGraph(
            GraphMesh graph,
            List<MeshTriangle> triangles,
            Vector3 rayOriginLocal,
            Vector3 rayDirLocal,
            HashSet<Face> allowedFaces)
        {
            SurfaceHit miss = new SurfaceHit { Hit = false };
            if (triangles.Count == 0)
                return miss;

            rayDirLocal = rayDirLocal.normalized;
            bool found = false;
            float bestT = float.MaxValue;
            MeshTriangle bestTri = default;
            Vector3 bestBary = default;

            for (int t = 0; t < triangles.Count; t++)
            {
                MeshTriangle tri = triangles[t];
                if (allowedFaces != null && (tri.Face == null || !allowedFaces.Contains(tri.Face)))
                    continue;

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

                if (tHit >= bestT - DepthEpsilon)
                    continue;

                bestT = tHit;
                bestTri = tri;
                bestBary = bary;
                found = true;
            }

            if (!found)
                return miss;

            Vertex hitV0 = graph.Vertices[bestTri.V0Index];
            Vertex hitV1 = graph.Vertices[bestTri.V1Index];
            Vertex hitV2 = graph.Vertices[bestTri.V2Index];
            Vector3 localPoint = bestBary.x * hitV0.Position + bestBary.y * hitV1.Position + bestBary.z * hitV2.Position;
            Vector3 authBary = ComputeBarycentric(hitV0.Position, hitV1.Position, hitV2.Position, localPoint);
            PaperSide side = ResolveCameraFacingSide(hitV0.Position, hitV1.Position, hitV2.Position, rayDirLocal);
            Vector3 localNormal = OutwardNormalForSide(hitV0.Position, hitV1.Position, hitV2.Position, side);

            return new SurfaceHit
            {
                Hit = true,
                Anchor0Index = bestTri.V0Index,
                Anchor1Index = bestTri.V1Index,
                Anchor2Index = bestTri.V2Index,
                Barycentric = authBary,
                LocalPoint = localPoint,
                LocalNormal = localNormal,
                SheetUv = BarycentricInterpolateSheetUv(hitV0, hitV1, hitV2, side, authBary),
                Side = side,
                HitFace = bestTri.Face,
                ViewRayOriginLocal = rayOriginLocal,
                ViewRayDirLocal = rayDirLocal
            };
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

            for (int t = 0; t < _triangles.Count; t++)
            {
                MeshTriangle tri = _triangles[t];
                if (allowedFaces != null && (tri.Face == null || !allowedFaces.Contains(tri.Face)))
                    continue;

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

                if (tHit >= bestT - DepthEpsilon)
                    continue;

                bestT = tHit;
                bestTri = tri;
                bestBary = bary;
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
            PaperSide side = ResolveCameraFacingSide(v0.Position, v1.Position, v2.Position, rayDirLocal);
            Vector3 localNormal = OutwardNormalForSide(v0.Position, v1.Position, v2.Position, side);

            return new SurfaceHit
            {
                Hit = true,
                Anchor0Index = a0,
                Anchor1Index = a1,
                Anchor2Index = a2,
                Barycentric = authBary,
                LocalPoint = localPoint,
                LocalNormal = localNormal,
                SheetUv = BarycentricInterpolateSheetUv(v0, v1, v2, side, authBary),
                Side = side,
                HitFace = bestTri.Face,
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

            return _triangles[logicalIndex].Face;
        }

        private SurfaceHit BuildSurfaceHit(RaycastHit physicsHit, Vector3 viewOriginLocal, Vector3 viewDirLocal)
        {
            SurfaceHit miss = new SurfaceHit { Hit = false };
            Vector3 localPoint = _meshSurfaceRoot.InverseTransformPoint(physicsHit.point);
            Vector3 localNormal = _meshSurfaceRoot.InverseTransformDirection(physicsHit.normal).normalized;

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

                Vertex anchor0 = _authoringGraph.Vertices[i0];
                Vertex anchor1 = _authoringGraph.Vertices[i1];
                Vertex anchor2 = _authoringGraph.Vertices[i2];

                side = ResolveCameraFacingSide(
                    anchor0.Position, anchor1.Position, anchor2.Position, localNormal, viewDirLocal);
                bary = ComputeBarycentric(anchor0.Position, anchor1.Position, anchor2.Position, localPoint);
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

            for (int t = 0; t < _triangles.Count; t++)
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
                barycentric = ComputeBarycentric(
                    _authoringGraph.Vertices[a0].Position,
                    _authoringGraph.Vertices[a1].Position,
                    _authoringGraph.Vertices[a2].Position,
                    localPoint);
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

            if (!TryGetDisplayFrame(graph, placement, out Vector3 center, out Vector3 normal, out Vector3 tangent, null))
                return false;

            Quaternion decalRotation = Quaternion.LookRotation(normal, tangent)
                * Quaternion.Euler(0f, 0f, placement.RotationUv);
            Vector3 axisU = decalRotation * Vector3.right;
            Vector3 axisV = decalRotation * Vector3.up;

            float halfWidth = placement.Scale * 0.5f;
            float halfHeight = halfWidth;
            if (placement.Texture != null && placement.Texture.width > 0)
                halfHeight = halfWidth * ((float)placement.Texture.height / placement.Texture.width);

            halfWidth *= pickSlop;
            halfHeight *= pickSlop;

            Vector3 offset = hit.LocalPoint - center;
            float u = Vector3.Dot(offset, axisU);
            float v = Vector3.Dot(offset, axisV);
            return Mathf.Abs(u) <= halfWidth && Mathf.Abs(v) <= halfHeight;
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

        public static bool TryResolveSurfaceFrame(GraphMesh graph, DecalPlacement placement, out ResolvedSurfaceFrame frame)
        {
            frame = default;
            if (graph == null) return false;
            if (placement.ViewRayDirLocal.sqrMagnitude < 0.0001f) return false;

            var triangles = new List<MeshTriangle>();
            BuildTriangleList(graph, triangles);

            Vector2 targetUv = placement.SheetUv;
            PaperSide lockedSide = placement.Side;
            Vector3 rayOrigin = placement.ViewRayOriginLocal;
            Vector3 rayDir = placement.ViewRayDirLocal.normalized;

            bool found = false;
            float bestLineDistSq = float.MaxValue;
            float bestDepth = float.MaxValue;
            ResolvedSurfaceFrame best = default;

            for (int t = 0; t < triangles.Count; t++)
            {
                MeshTriangle tri = triangles[t];
                if (!IsValidIndex(graph, tri.V0Index) || !IsValidIndex(graph, tri.V1Index) || !IsValidIndex(graph, tri.V2Index))
                    continue;

                Vertex v0 = graph.Vertices[tri.V0Index];
                Vertex v1 = graph.Vertices[tri.V1Index];
                Vertex v2 = graph.Vertices[tri.V2Index];

                if (!TryMatchTriangleSurface(
                        v0, v1, v2, tri.V0Index, tri.V1Index, tri.V2Index,
                        targetUv, lockedSide,
                        out ResolvedSurfaceFrame candidate))
                    continue;

                float depth = Vector3.Dot(candidate.Position - rayOrigin, rayDir);
                if (depth < DepthEpsilon)
                    continue;

                float lineDistSq = Vector3.Cross(candidate.Position - rayOrigin, rayDir).sqrMagnitude;
                if (lineDistSq > bestLineDistSq + 1e-8f)
                    continue;
                if (Mathf.Abs(lineDistSq - bestLineDistSq) <= 1e-6f && depth >= bestDepth - DepthEpsilon)
                    continue;

                bestLineDistSq = lineDistSq;
                bestDepth = depth;
                best = candidate;
                found = true;
            }

            if (!found) return false;

            frame = best;
            frame.Found = true;
            return true;
        }

        public static bool RefreshPlacementAnchors(GraphMesh graph, DecalPlacement placement)
        {
            if (!TryResolveSurfaceFrame(graph, placement, out ResolvedSurfaceFrame frame))
                return false;

            placement.Anchor0Index = frame.Anchor0Index;
            placement.Anchor1Index = frame.Anchor1Index;
            placement.Anchor2Index = frame.Anchor2Index;
            placement.Barycentric = frame.Barycentric;
            placement.LocalPoint = frame.Position;
            placement.LocalNormal = frame.Normal;
            return true;
        }

        public static bool TryGetDisplayFrame(
            GraphMesh graph,
            DecalPlacement placement,
            out Vector3 position,
            out Vector3 normal,
            out Vector3 tangent,
            PreviewAnchorCache previewCache = null)
        {
            position = placement.LocalPoint;
            normal = placement.LocalNormal.sqrMagnitude > 0.01f ? placement.LocalNormal : Vector3.up;
            tangent = Vector3.right;

            if (graph == null) return false;

            if (previewCache != null)
            {
                if (previewCache.IsValid
                    && TryInterpolateFromCache(graph, placement, previewCache, out position, out normal, out tangent))
                    return true;

                if (TryResolveSurfaceFrame(graph, placement, out ResolvedSurfaceFrame frame))
                {
                    previewCache.SeedFrom(frame);
                    return TryInterpolateFromCache(graph, placement, previewCache, out position, out normal, out tangent);
                }

                if (IsValidIndex(graph, placement.Anchor0Index)
                    && IsValidIndex(graph, placement.Anchor1Index)
                    && IsValidIndex(graph, placement.Anchor2Index))
                {
                    previewCache.Anchor0Index = placement.Anchor0Index;
                    previewCache.Anchor1Index = placement.Anchor1Index;
                    previewCache.Anchor2Index = placement.Anchor2Index;
                    previewCache.Barycentric = placement.Barycentric;
                    previewCache.IsValid = true;
                    return TryInterpolateFromCache(graph, placement, previewCache, out position, out normal, out tangent);
                }

                return false;
            }

            if (TryGetAnchorVertices(graph, placement, out Vertex v0, out Vertex v1, out Vertex v2))
            {
                position = placement.Barycentric.x * v0.Position
                         + placement.Barycentric.y * v1.Position
                         + placement.Barycentric.z * v2.Position;
                normal = OutwardNormalForSide(v0.Position, v1.Position, v2.Position, placement.Side);
                tangent = InterpolateTangent(graph, placement, normal);
                return true;
            }

            if (!TryResolveSurfaceFrame(graph, placement, out ResolvedSurfaceFrame resolved))
                return false;

            position = resolved.Position;
            Vertex rv0 = graph.Vertices[resolved.Anchor0Index];
            Vertex rv1 = graph.Vertices[resolved.Anchor1Index];
            Vertex rv2 = graph.Vertices[resolved.Anchor2Index];
            normal = OutwardNormalForSide(rv0.Position, rv1.Position, rv2.Position, placement.Side);
            var tempPlacement = new DecalPlacement
            {
                Anchor0Index = resolved.Anchor0Index,
                Anchor1Index = resolved.Anchor1Index,
                Anchor2Index = resolved.Anchor2Index,
                Barycentric = resolved.Barycentric,
                Side = placement.Side
            };
            tangent = InterpolateTangent(graph, tempPlacement, normal);
            return true;
        }

        public static bool TryInterpolateFromCache(
            GraphMesh graph,
            DecalPlacement placement,
            PreviewAnchorCache cache,
            out Vector3 position,
            out Vector3 normal,
            out Vector3 tangent)
        {
            position = placement.LocalPoint;
            normal = placement.LocalNormal.sqrMagnitude > 0.01f ? placement.LocalNormal : Vector3.up;
            tangent = Vector3.right;

            if (graph == null || cache == null || !cache.IsValid)
                return false;

            if (!IsValidIndex(graph, cache.Anchor0Index)
                || !IsValidIndex(graph, cache.Anchor1Index)
                || !IsValidIndex(graph, cache.Anchor2Index))
                return false;

            Vertex v0 = graph.Vertices[cache.Anchor0Index];
            Vertex v1 = graph.Vertices[cache.Anchor1Index];
            Vertex v2 = graph.Vertices[cache.Anchor2Index];

            position = cache.Barycentric.x * v0.Position
                     + cache.Barycentric.y * v1.Position
                     + cache.Barycentric.z * v2.Position;
            normal = OutwardNormalForSide(v0.Position, v1.Position, v2.Position, placement.Side);

            var cachedPlacement = new DecalPlacement
            {
                Anchor0Index = cache.Anchor0Index,
                Anchor1Index = cache.Anchor1Index,
                Anchor2Index = cache.Anchor2Index,
                Barycentric = cache.Barycentric,
                Side = placement.Side
            };
            tangent = InterpolateTangent(graph, cachedPlacement, normal);
            return true;
        }

        private static bool TryMatchTriangleSurface(
            Vertex v0, Vertex v1, Vertex v2,
            int i0, int i1, int i2,
            Vector2 targetUv,
            PaperSide side,
            out ResolvedSurfaceFrame frame)
        {
            frame = default;

            Vector2 uv0 = VertexSheetUv(v0, side);
            Vector2 uv1 = VertexSheetUv(v1, side);
            Vector2 uv2 = VertexSheetUv(v2, side);
            if (!TryComputeBarycentric2D(uv0, uv1, uv2, targetUv, out Vector3 uvBary))
                return false;

            Vector3 pos = uvBary.x * v0.Position + uvBary.y * v1.Position + uvBary.z * v2.Position;
            frame = new ResolvedSurfaceFrame
            {
                Anchor0Index = i0,
                Anchor1Index = i1,
                Anchor2Index = i2,
                Barycentric = ComputeBarycentric(v0.Position, v1.Position, v2.Position, pos),
                Position = pos,
                Normal = OutwardNormalForSide(v0.Position, v1.Position, v2.Position, side)
            };
            return true;
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

        private static bool TryComputeBarycentric2D(Vector2 a, Vector2 b, Vector2 c, Vector2 p, out Vector3 bary)
        {
            Vector2 v0 = b - a;
            Vector2 v1 = c - a;
            Vector2 v2 = p - a;
            float d00 = Vector2.Dot(v0, v0);
            float d01 = Vector2.Dot(v0, v1);
            float d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0);
            float d21 = Vector2.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < 0.000001f)
            {
                bary = default;
                return false;
            }

            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            bary = new Vector3(1f - v - w, v, w);
            if (bary.x < -0.02f || bary.y < -0.02f || bary.z < -0.02f)
                return false;

            return true;
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

        public static Vector3 InterpolatePosition(GraphMesh authoringGraph, DecalPlacement placement)
        {
            if (!TryGetAnchorVertices(authoringGraph, placement, out Vertex v0, out Vertex v1, out Vertex v2))
                return placement.LocalPoint;

            return placement.Barycentric.x * v0.Position
                 + placement.Barycentric.y * v1.Position
                 + placement.Barycentric.z * v2.Position;
        }

        public static Vector3 InterpolateNormal(GraphMesh authoringGraph, DecalPlacement placement)
        {
            if (TryGetAnchorVertices(authoringGraph, placement, out Vertex v0, out Vertex v1, out Vertex v2))
                return OutwardNormalForSide(v0.Position, v1.Position, v2.Position, placement.Side);

            if (placement.LocalNormal.sqrMagnitude > 0.01f)
                return placement.LocalNormal;

            return Vector3.up;
        }

        public static Vector3 InterpolateTangent(GraphMesh authoringGraph, DecalPlacement placement, Vector3 localNormal)
        {
            if (localNormal.sqrMagnitude < 0.0001f)
                localNormal = Vector3.up;

            if (!TryGetAnchorVertices(authoringGraph, placement, out Vertex v0, out Vertex v1, out Vertex v2))
                return SafeTangentFallback(localNormal);

            Vector2 uv0 = VertexSheetUv(v0, placement.Side);
            Vector2 uv1 = VertexSheetUv(v1, placement.Side);
            Vector2 uv2 = VertexSheetUv(v2, placement.Side);
            Vector3 p0 = v0.Position;
            Vector3 p1 = v1.Position;
            Vector3 p2 = v2.Position;

            Vector2 duv1 = uv1 - uv0;
            Vector2 duv2 = uv2 - uv0;
            Vector3 dp1 = p1 - p0;
            Vector3 dp2 = p2 - p0;
            float denom = duv1.x * duv2.y - duv1.y * duv2.x;
            if (Mathf.Abs(denom) < 0.000001f)
                return SafeTangentFallback(localNormal);
            float r = 1f / denom;
            Vector3 tangent = (dp1 * duv2.y - dp2 * duv1.y) * r;
            if (tangent.sqrMagnitude < 0.000001f)
                return SafeTangentFallback(localNormal);
            return tangent.normalized;
        }

        private static Vector3 SafeTangentFallback(Vector3 localNormal)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(Vector3.right, localNormal);
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = Vector3.Cross(localNormal, Vector3.forward);
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = Vector3.right;
            return tangent.normalized;
        }

        private static bool IsValidIndex(GraphMesh graph, int index)
        {
            return index >= 0 && index < graph.Vertices.Count;
        }
    }
}
