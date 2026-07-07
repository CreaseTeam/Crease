using System.Collections.Generic;
using Crease.Folding.PaperGraph;
using UnityEngine;
using GraphMesh = Crease.Folding.PaperGraph.PaperGraph;

namespace Crease.Folding.Decals
{
    /// <summary>
    /// Clips decal quads to the visible paper surface so overhanging regions are culled.
    /// </summary>
    public static class DecalPaperClipUtility
    {
        private const float Epsilon = 0.00001f;

        private struct Triangle2D
        {
            public Vector2 A;
            public Vector2 B;
            public Vector2 C;
        }

        public static bool TryBuildClippedMesh(
            GraphMesh graph,
            DecalPlacement placement,
            Vector3 center,
            Vector3 surfaceNormal,
            Vector3 axisU,
            Vector3 axisV,
            Vector2 quadScale,
            out Mesh mesh,
            Mesh reuseMesh = null,
            Quaternion vertexRotation = default,
            bool useGraphToDisplayLocalTransform = false,
            Matrix4x4 graphToDisplayLocal = default)
        {
            mesh = null;
            if (vertexRotation == default)
                vertexRotation = Quaternion.identity;

            if (graph == null || quadScale.x <= Epsilon || quadScale.y <= Epsilon)
                return false;

            if (axisU.sqrMagnitude < Epsilon || axisV.sqrMagnitude < Epsilon)
                return false;

            axisU = axisU.normalized;
            axisV = axisV.normalized;

            float halfWidth = quadScale.x * 0.5f;
            float halfHeight = quadScale.y * 0.5f;
            var quad = new List<Vector2>(4)
            {
                new Vector2(-halfWidth, -halfHeight),
                new Vector2(halfWidth, -halfHeight),
                new Vector2(halfWidth, halfHeight),
                new Vector2(-halfWidth, halfHeight)
            };

            var clipTriangles = new List<Triangle2D>();
            CollectVisibleTriangles(
                graph,
                placement,
                center,
                surfaceNormal,
                axisU,
                axisV,
                vertexRotation,
                useGraphToDisplayLocalTransform,
                graphToDisplayLocal,
                clipTriangles);
            if (clipTriangles.Count == 0)
                return false;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var scratch = new List<Vector2>();

            for (int i = 0; i < clipTriangles.Count; i++)
            {
                Triangle2D tri = clipTriangles[i];
                var clipPoly = new List<Vector2>(3) { tri.A, tri.B, tri.C };
                scratch.Clear();
                scratch.AddRange(quad);
                List<Vector2> clipped = ClipConvexPolygon(scratch, clipPoly);
                if (clipped.Count < 3)
                    continue;

                AppendFan(clipped, quadScale, vertices, uvs, triangles);
            }

            if (vertices.Count == 0)
                return false;

            mesh = reuseMesh ?? new Mesh { name = "ClippedDecalQuad" };
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return true;
        }

        private static void CollectVisibleTriangles(
            GraphMesh graph,
            DecalPlacement placement,
            Vector3 center,
            Vector3 surfaceNormal,
            Vector3 axisU,
            Vector3 axisV,
            Quaternion vertexRotation,
            bool useGraphToDisplayLocalTransform,
            Matrix4x4 graphToDisplayLocal,
            List<Triangle2D> output)
        {
            output.Clear();
            foreach (Face face in graph.Faces)
            {
                if (face.Vertices.Count < 3)
                    continue;

                Vertex anchor = face.Vertices[0];
                for (int i = 1; i < face.Vertices.Count - 1; i++)
                {
                    Vertex v1 = face.Vertices[i];
                    Vertex v2 = face.Vertices[i + 1];

                    Vector3 p0 = TransformGraphPoint(anchor.Position, vertexRotation, useGraphToDisplayLocalTransform, graphToDisplayLocal);
                    Vector3 p1 = TransformGraphPoint(v2.Position, vertexRotation, useGraphToDisplayLocalTransform, graphToDisplayLocal);
                    Vector3 p2 = TransformGraphPoint(v1.Position, vertexRotation, useGraphToDisplayLocalTransform, graphToDisplayLocal);

                    Vector3 edgeA = p2 - p0;
                    Vector3 edgeB = p1 - p0;
                    if (Vector3.Cross(edgeA, edgeB).sqrMagnitude < 0.000001f)
                        continue;

                    if (!TriangleMatchesSide(p0, p1, p2, placement.Side, surfaceNormal))
                        continue;

                    if (!TriangleMatchesCullRegion(anchor, v1, v2, placement))
                        continue;

                    Vector2 a = ProjectToDecalPlane(p0, center, axisU, axisV);
                    Vector2 b = ProjectToDecalPlane(p1, center, axisU, axisV);
                    Vector2 c = ProjectToDecalPlane(p2, center, axisU, axisV);
                    if (Cross(b - a, c - a) < 0f)
                    {
                        Vector2 temp = b;
                        b = c;
                        c = temp;
                    }

                    output.Add(new Triangle2D { A = a, B = b, C = c });
                }
            }
        }

        private static Vector3 TransformGraphPoint(
            Vector3 graphPoint,
            Quaternion vertexRotation,
            bool useGraphToDisplayLocalTransform,
            Matrix4x4 graphToDisplayLocal)
        {
            if (useGraphToDisplayLocalTransform)
                return graphToDisplayLocal.MultiplyPoint3x4(graphPoint);

            return vertexRotation * graphPoint;
        }

        private static bool TriangleMatchesCullRegion(
            Vertex anchor,
            Vertex v1,
            Vertex v2,
            DecalPlacement placement)
        {
            if (placement.CullRegionSheetUvPolygons == null || placement.CullRegionSheetUvPolygons.Length == 0)
                return true;

            Vector2 uv0 = VertexSheetUv(anchor, placement.Side);
            Vector2 uv1 = VertexSheetUv(v1, placement.Side);
            Vector2 uv2 = VertexSheetUv(v2, placement.Side);
            Vector2 centroid = (uv0 + uv1 + uv2) / 3f;

            for (int i = 0; i < placement.CullRegionSheetUvPolygons.Length; i++)
            {
                Vector2[] polygon = placement.CullRegionSheetUvPolygons[i];
                if (polygon == null || polygon.Length < 3)
                    continue;

                if (IsPointInPolygon(centroid, polygon)
                    || IsPointInPolygon(uv0, polygon)
                    || IsPointInPolygon(uv1, polygon)
                    || IsPointInPolygon(uv2, polygon))
                    return true;
            }

            return false;
        }

        private static Vector2 VertexSheetUv(Vertex vertex, PaperSide side)
        {
            if (side == PaperSide.Back)
                return new Vector2(1f - vertex.Uv.x, vertex.Uv.y);
            return vertex.Uv;
        }

        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                bool intersects = (a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (intersects)
                    inside = !inside;
            }

            return inside;
        }

        private static bool TriangleMatchesSide(
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            PaperSide side,
            Vector3 surfaceNormal)
        {
            Vector3 frontNormal = ComputeFrontNormal(p0, p1, p2);
            float frontAlign = Vector3.Dot(frontNormal, surfaceNormal);
            float backAlign = Vector3.Dot(-frontNormal, surfaceNormal);
            return side == PaperSide.Front ? frontAlign >= backAlign : backAlign > frontAlign;
        }

        private static Vector3 ComputeFrontNormal(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vector3 e1 = p1 - p0;
            Vector3 e2 = p2 - p0;
            Vector3 normal = Vector3.Cross(e2, e1);
            if (normal.sqrMagnitude < 0.000001f)
                return Vector3.up;
            return normal.normalized;
        }

        private static Vector2 ProjectToDecalPlane(Vector3 point, Vector3 center, Vector3 axisU, Vector3 axisV)
        {
            Vector3 offset = point - center;
            return new Vector2(Vector3.Dot(offset, axisU), Vector3.Dot(offset, axisV));
        }

        private static List<Vector2> ClipConvexPolygon(List<Vector2> subject, List<Vector2> convexClip)
        {
            List<Vector2> output = subject;
            for (int i = 0; i < convexClip.Count; i++)
            {
                Vector2 edgeStart = convexClip[i];
                Vector2 edgeEnd = convexClip[(i + 1) % convexClip.Count];
                output = ClipByHalfPlane(output, edgeStart, edgeEnd);
                if (output.Count == 0)
                    break;
            }

            return output;
        }

        private static List<Vector2> ClipByHalfPlane(List<Vector2> input, Vector2 edgeStart, Vector2 edgeEnd)
        {
            if (input.Count == 0)
                return input;

            var output = new List<Vector2>(input.Count + 1);
            Vector2 edge = edgeEnd - edgeStart;

            for (int i = 0; i < input.Count; i++)
            {
                Vector2 current = input[i];
                Vector2 previous = input[(i + input.Count - 1) % input.Count];

                float currentSide = Cross(edge, current - edgeStart);
                float previousSide = Cross(edge, previous - edgeStart);
                bool currentInside = currentSide >= -Epsilon;
                bool previousInside = previousSide >= -Epsilon;

                if (currentInside)
                {
                    if (!previousInside)
                        output.Add(IntersectSegments(previous, current, edgeStart, edgeEnd));
                    output.Add(current);
                }
                else if (previousInside)
                {
                    output.Add(IntersectSegments(previous, current, edgeStart, edgeEnd));
                }
            }

            return output;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2 IntersectSegments(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Vector2 r = b - a;
            Vector2 s = d - c;
            float denom = Cross(r, s);
            if (Mathf.Abs(denom) < Epsilon)
                return a;

            float t = Cross(c - a, s) / denom;
            return a + r * t;
        }

        private static void AppendFan(
            List<Vector2> polygon,
            Vector2 quadScale,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles)
        {
            int baseIndex = vertices.Count;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 uvOffset = polygon[i];
                float localX = uvOffset.x / quadScale.x;
                float localY = uvOffset.y / quadScale.y;
                vertices.Add(new Vector3(localX, localY, 0f));
                uvs.Add(new Vector2(0.5f - localX, localY + 0.5f));
            }

            for (int i = 1; i < polygon.Count - 1; i++)
            {
                // Match DecalQuad shared mesh winding (0, 2, 1) for negative Z scale + Lit shading.
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + i + 1);
                triangles.Add(baseIndex + i);
            }
        }
    }
}
