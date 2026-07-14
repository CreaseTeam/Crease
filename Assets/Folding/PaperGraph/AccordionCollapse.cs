using System.Collections.Generic;
using UnityEngine;

namespace Crease.Folding.PaperGraph
{
    /// <summary>
    /// Runtime state for an in-progress or previewed accordion (water-bomb style) collapse.
    /// Uses vertex indices so poses can be applied to preview graph copies.
    /// </summary>
    public class AccordionCollapseData
    {
        public int CenterIndex;
        public Dictionary<int, Vector3> FlatPositions = new Dictionary<int, Vector3>();
        public Dictionary<int, Vector3> CollapsedPositions = new Dictionary<int, Vector3>();
        public HashSet<int> MovedVertexIndices = new HashSet<int>();
        public HashSet<int> AccordionBoundaryIndices = new HashSet<int>();

        public Vector3 CenterPosition;
        public Vector3 CreaseAxisA;
        public Vector3 CreaseAxisB;
        public Vector3 AccordionAxisDir;
        public Vector3 AccordionPlaneNormal;
        public Vector3 PlaneNormal;
        public Vector3 DragAxis;
        public float FoldDegrees;
        public float FoldOffset;
    }

    public struct AccordionDragPath
    {
        public Vector3 Intersection;
        public Vector3 AccordionAxisDir;
        public Vector3 DragStart;
        public Vector3 DragEnd;
    }

    /// <summary>
    /// Water-bomb style accordion collapse after two crossing creases.
    /// Cuts along the in-plane accordion axis (angle bisector of the crease axes, chosen
    /// relative to world X and allowed to skew when creases are asymmetric),
    /// then folds the top half and boundary intersection vertices to their flat-fold targets.
    /// </summary>
    public static class AccordionCollapse
    {
        private const float PositionTolerance = 0.0001f;

        public static bool CreaseAxesCross(Vector3 aP1, Vector3 aP2, Vector3 bP1, Vector3 bP2, Vector3 planeNormal) {
            Vector3 n = planeNormal.sqrMagnitude > 0.0001f ? planeNormal.normalized : Vector3.up;
            Vector3 aDir = Vector3.ProjectOnPlane(aP2 - aP1, n);
            if (aDir.sqrMagnitude < 0.00001f) return false;
            Vector3 u = aDir.normalized;
            Vector3 v = Vector3.Cross(n, u).normalized;

            Vector2 A = ToPlane(aP1, u, v);
            Vector2 B = ToPlane(aP2, u, v);
            Vector2 C = ToPlane(bP1, u, v);
            Vector2 D = ToPlane(bP2, u, v);

            float sideC = Cross2(B - A, C - A);
            float sideD = Cross2(B - A, D - A);
            return (sideC > 0.0001f && sideD < -0.0001f) || (sideC < -0.0001f && sideD > 0.0001f);
        }

        /// <summary>
        /// Computes the constrained drag path from prepared accordion data.
        /// Start: where the axis perpendicular to the accordion axis (through the crease intersection)
        /// meets the paper edge toward sheet-up. End: top-half collapse of that point.
        /// </summary>
        public static bool TryComputeDragPath(
            PaperGraph graph,
            AccordionCollapseData data,
            out AccordionDragPath path,
            out string error) {
            path = default;
            error = null;

            if (graph == null) {
                error = "PaperGraph is null.";
                return false;
            }

            if (data == null) {
                error = "Accordion collapse data is null.";
                return false;
            }

            Vector3 dragAxis = data.DragAxis.sqrMagnitude > 0.00001f ? data.DragAxis.normalized : Vector3.zero;
            if (dragAxis.sqrMagnitude < 0.00001f) {
                error = "Accordion drag axis is invalid.";
                return false;
            }

            if (!ClipAxisToPaperEdges(
                    graph,
                    data.CenterPosition,
                    dragAxis,
                    data.PlaneNormal,
                    out Vector3 clippedMin,
                    out Vector3 clippedMax)) {
                error = "Failed to clip accordion drag axis to paper edges.";
                return false;
            }

            Vector3 dragStart = Vector3.Dot(clippedMin - data.CenterPosition, dragAxis)
                >= Vector3.Dot(clippedMax - data.CenterPosition, dragAxis)
                ? clippedMin
                : clippedMax;
            ComputeCollapsedPositionAsTopHalf(dragStart, data, out Vector3 dragEnd);

            path = new AccordionDragPath {
                Intersection = data.CenterPosition,
                AccordionAxisDir = data.AccordionAxisDir,
                DragStart = dragStart,
                DragEnd = dragEnd
            };
            return true;
        }

        /// <summary>
        /// Clips an in-plane axis to the paper polygon using the same edge-intersection
        /// method as <see cref="FoldInstructionRunner"/> fold-axis clipping.
        /// </summary>
        private static bool ClipAxisToPaperEdges(
            PaperGraph graph,
            Vector3 axisMidpoint,
            Vector3 axisDir,
            Vector3 planeNormal,
            out Vector3 clippedMin,
            out Vector3 clippedMax) {
            clippedMin = axisMidpoint;
            clippedMax = axisMidpoint;

            Vector3 basisU = axisDir.sqrMagnitude > 0.00001f ? axisDir.normalized : Vector3.zero;
            if (basisU.sqrMagnitude < 0.00001f)
                return false;

            Vector3 basisV = Vector3.Cross(planeNormal, basisU).normalized;

            float minT = float.PositiveInfinity;
            float maxT = float.NegativeInfinity;
            bool foundAny = false;

            foreach (Edge edge in graph.Edges) {
                float aU = Vector3.Dot(edge.V1.Position - axisMidpoint, basisU);
                float aV = Vector3.Dot(edge.V1.Position - axisMidpoint, basisV);
                float bU = Vector3.Dot(edge.V2.Position - axisMidpoint, basisU);
                float bV = Vector3.Dot(edge.V2.Position - axisMidpoint, basisV);

                float dV = bV - aV;
                if (Mathf.Abs(dV) < 0.000001f)
                    continue;

                float s = -aV / dV;
                if (s < -0.0001f || s > 1.0001f)
                    continue;

                float t = aU + s * (bU - aU);

                if (t < minT)
                    minT = t;
                if (t > maxT)
                    maxT = t;
                foundAny = true;
            }

            if (!foundAny || (maxT - minT) < 0.0001f)
                return false;

            clippedMin = axisMidpoint + basisU * minT;
            clippedMax = axisMidpoint + basisU * maxT;
            return true;
        }

        private static void ComputeCollapsedPositionAsTopHalf(
            Vector3 flat,
            AccordionCollapseData data,
            out Vector3 collapsed) {
            collapsed = MirrorFoldInPaperPlane(
                flat,
                data.CenterPosition,
                data.AccordionAxisDir,
                data.PlaneNormal,
                data.FoldDegrees,
                data.FoldOffset * 2f);
        }

        /// <summary>
        /// Shared collapse target for a vertex — used by <see cref="BuildCollapseData"/> and drag-path resolution.
        /// </summary>
        private static void ComputeCollapsedPosition(
            int vertexIndex,
            Vector3 flat,
            AccordionCollapseData data,
            out Vector3 collapsed,
            out bool moves) {
            collapsed = flat;
            moves = false;

            if (data == null)
                return;

            Vector3 centerPos = data.CenterPosition;
            if (vertexIndex == data.CenterIndex) {
                collapsed = centerPos;
                return;
            }

            if (data.AccordionBoundaryIndices.Contains(vertexIndex)) {
                if (TryGetBoundaryCreaseAxis(
                        flat, centerPos, data.CreaseAxisA, data.CreaseAxisB, data.PlaneNormal, out Vector3 creaseAxis)) {
                    collapsed = MirrorFoldInPaperPlane(
                        flat, centerPos, creaseAxis, data.PlaneNormal, data.FoldDegrees, data.FoldOffset);
                    moves = true;
                }
                return;
            }

            float accordionSide = Vector3.Dot(flat - centerPos, data.AccordionPlaneNormal);
            if (accordionSide > PositionTolerance) {
                ComputeCollapsedPositionAsTopHalf(flat, data, out collapsed);
                moves = true;
            }
        }

        public static bool TryPrepare(
            PaperGraph graph,
            string creaseTagA,
            string creaseTagB,
            string tagName,
            Vector3 creaseAxisA1,
            Vector3 creaseAxisA2,
            Vector3 creaseAxisB1,
            Vector3 creaseAxisB2,
            float foldDegrees,
            Vector3 planeVector,
            float foldOffset,
            out AccordionCollapseData data,
            out string error)
        {
            data = null;
            error = null;

            if (graph == null) {
                error = "PaperGraph is null.";
                return false;
            }

            Vector3 planeNormal = planeVector.sqrMagnitude > 0.0001f ? planeVector.normalized : Vector3.up;

            if (!TryComputeCreaseIntersection(
                    creaseAxisA1, creaseAxisA2, creaseAxisB1, creaseAxisB2, planeNormal, out Vector3 intersection)) {
                error = "Crease axes do not intersect.";
                return false;
            }

            Vertex center = FindOrCreateCenterVertex(graph, intersection, planeNormal);
            if (center == null) {
                error = "Failed to create crease intersection vertex.";
                return false;
            }

            if (!IsVertexInAnyFace(graph, center))
                SplitCreasePlanesThroughPoint(
                    graph, center.Position, creaseAxisA1, creaseAxisA2, creaseAxisB1, creaseAxisB2, planeNormal);

            center = FindVertexNear(graph, intersection) ?? center;

            TagVertex(graph, creaseTagA, center);
            TagVertex(graph, creaseTagB, center);

            Vector3 fallbackAxisA = Vector3.ProjectOnPlane(creaseAxisA2 - creaseAxisA1, planeNormal).normalized;
            Vector3 fallbackAxisB = Vector3.ProjectOnPlane(creaseAxisB2 - creaseAxisB1, planeNormal).normalized;

            EnsureCreaseRaysFromCenter(graph, center, creaseAxisA1, creaseAxisA2, creaseTagA, planeNormal);
            EnsureCreaseRaysFromCenter(graph, center, creaseAxisB1, creaseAxisB2, creaseTagB, planeNormal);

            Vector3 sheetUp = GetSheetUpInPlane(graph, center.Position, planeNormal);
            Vector3 creaseAxisA = ResolveCreaseAxisFromGraph(graph, center, planeNormal, fallbackAxisA);
            Vector3 creaseAxisB = ResolveCreaseAxisFromGraph(graph, center, planeNormal, fallbackAxisB);
            OrientCreaseAxisTowardSheetUp(ref creaseAxisA, planeNormal, sheetUp);
            OrientCreaseAxisTowardSheetUp(ref creaseAxisB, planeNormal, sheetUp);

            Vector3 referenceX = GetReferenceXInPlane(planeNormal);
            Vector3 accordionAxisDir = ComputeAccordionAxisDir(creaseAxisA, creaseAxisB, referenceX);
            Vector3 accordionPlaneNormal = Vector3.Cross(accordionAxisDir, planeNormal).normalized;
            EnsureAccordionPlaneNormalTowardSheetUp(
                ref accordionPlaneNormal, graph, center.Position, planeNormal, sheetUp);

            Vector3 dragAxis = Vector3.Cross(accordionAxisDir, planeNormal).normalized;
            OrientNormalTowardSheetUp(ref dragAxis, sheetUp);

            graph.SplitEdgesCrossingPlane(center.Position, accordionPlaneNormal, 180f, null, 0f, out bool _);

            EnsureCreaseRaysFromCenter(
                graph, center,
                center.Position + accordionAxisDir,
                center.Position - accordionAxisDir,
                null, planeNormal);

            center = FindVertexNear(graph, intersection) ?? center;

            data = BuildCollapseData(
                graph, center,
                creaseAxisA, creaseAxisB,
                accordionAxisDir, accordionPlaneNormal,
                planeNormal, dragAxis, foldDegrees, foldOffset);

            if (!string.IsNullOrEmpty(tagName)) {
                graph.AddVertexToTag(tagName + "_edge", center);
                foreach (var kvp in data.FlatPositions) {
                    int idx = kvp.Key;
                    if (idx < 0 || idx >= graph.Vertices.Count) continue;
                    if (idx == data.CenterIndex) continue;

                    if (data.MovedVertexIndices.Contains(idx))
                        graph.AddVertexToTag(tagName + "_moved", graph.Vertices[idx]);
                    else
                        graph.AddVertexToTag(tagName + "_static", graph.Vertices[idx]);
                }
            }

            return true;
        }

        public static void ApplyPose(PaperGraph graph, AccordionCollapseData data, float t, float foldOffset = 0f) {
            if (graph == null || data == null) return;
            if (data.CenterIndex < 0 || data.CenterIndex >= graph.Vertices.Count) return;

            t = Mathf.Clamp01(t);
            Vector3 centerFlat = data.FlatPositions[data.CenterIndex];

            foreach (var kvp in data.FlatPositions) {
                int idx = kvp.Key;
                if (idx < 0 || idx >= graph.Vertices.Count) continue;

                Vector3 flat = kvp.Value;
                Vector3 collapsed = data.CollapsedPositions.TryGetValue(idx, out Vector3 target)
                    ? target
                    : flat;
                graph.Vertices[idx].Position = Vector3.Lerp(flat, collapsed, t);
            }

            graph.Vertices[data.CenterIndex].Position = centerFlat;
        }

        public static void RestoreFlatPose(PaperGraph graph, AccordionCollapseData data) {
            if (graph == null || data == null) return;

            foreach (var kvp in data.FlatPositions) {
                if (kvp.Key < 0 || kvp.Key >= graph.Vertices.Count) continue;
                graph.Vertices[kvp.Key].Position = kvp.Value;
            }
        }

        /// <summary>
        /// Horizontal (in-plane) bisector of the two crease axes, chosen to be the one more
        /// aligned with reference X. May be skewed when creases are asymmetric.
        /// </summary>
        private static Vector3 ComputeAccordionAxisDir(Vector3 creaseAxisA, Vector3 creaseAxisB, Vector3 referenceX) {
            Vector3 flatA = creaseAxisA.normalized;
            Vector3 flatB = creaseAxisB.normalized;

            Vector3 sum = flatA + flatB;
            Vector3 diff = flatA - flatB;
            Vector3 bisectorA = sum.sqrMagnitude > 0.00001f ? sum.normalized : Vector3.zero;
            Vector3 bisectorB = diff.sqrMagnitude > 0.00001f ? diff.normalized : Vector3.zero;

            float alignA = bisectorA.sqrMagnitude > 0.00001f ? Mathf.Abs(Vector3.Dot(bisectorA, referenceX)) : -1f;
            float alignB = bisectorB.sqrMagnitude > 0.00001f ? Mathf.Abs(Vector3.Dot(bisectorB, referenceX)) : -1f;

            Vector3 accordionDir = alignA >= alignB ? bisectorA : bisectorB;
            if (accordionDir.sqrMagnitude < 0.00001f)
                accordionDir = referenceX;
            else if (Vector3.Dot(accordionDir, referenceX) < 0f)
                accordionDir = -accordionDir;

            return accordionDir;
        }

        /// <summary>
        /// World X projected into the paper plane — the reference direction for picking the horizontal bisector.
        /// </summary>
        private static Vector3 GetReferenceXInPlane(Vector3 planeNormal) {
            Vector3 referenceX = Vector3.ProjectOnPlane(Vector3.right, planeNormal);
            if (referenceX.sqrMagnitude < 0.00001f)
                referenceX = Vector3.ProjectOnPlane(Vector3.forward, planeNormal);
            return referenceX.sqrMagnitude > 0.00001f ? referenceX.normalized : Vector3.right;
        }

        /// <summary>
        /// In-plane direction toward the top of the sheet, aligned with the topmost mesh point.
        /// </summary>
        private static Vector3 GetSheetUpInPlane(PaperGraph graph, Vector3 centerPos, Vector3 planeNormal) {
            Vector3 referenceX = GetReferenceXInPlane(planeNormal);
            Vector3 candidate = Vector3.Cross(planeNormal, referenceX).normalized;
            if (candidate.sqrMagnitude < 0.00001f)
                candidate = Vector3.ProjectOnPlane(Vector3.forward, planeNormal).normalized;

            Vector3 toTopmost = GetToTopmostRelInPlane(graph, centerPos, planeNormal);
            if (toTopmost.sqrMagnitude > 0.00001f && Vector3.Dot(toTopmost, candidate) < 0f)
                candidate = -candidate;

            return candidate;
        }

        /// <summary>
        /// Vector from center to the topmost point in the paper plane.
        /// Uses world forward projected into the plane so the search axis is independent of crease geometry.
        /// </summary>
        private static Vector3 GetToTopmostRelInPlane(PaperGraph graph, Vector3 centerPos, Vector3 planeNormal) {
            Vector3 searchUp = Vector3.ProjectOnPlane(Vector3.forward, planeNormal);
            if (searchUp.sqrMagnitude < 0.00001f)
                searchUp = Vector3.Cross(planeNormal, GetReferenceXInPlane(planeNormal));
            searchUp.Normalize();

            Vector3 topmostRel = Vector3.zero;
            float bestScore = float.NegativeInfinity;

            foreach (Vertex vertex in graph.Vertices) {
                Vector3 rel = Vector3.ProjectOnPlane(vertex.Position - centerPos, planeNormal);
                if (rel.sqrMagnitude < 0.00001f) continue;

                float score = Vector3.Dot(rel, searchUp);
                if (score > bestScore) {
                    bestScore = score;
                    topmostRel = rel;
                }
            }

            return topmostRel;
        }

        /// <summary>
        /// Ensures the positive side of the accordion cut contains the top of the sheet.
        /// </summary>
        private static void OrientNormalTowardSheetUp(ref Vector3 normal, Vector3 sheetUp) {
            if (Vector3.Dot(normal, sheetUp) < 0f)
                normal = -normal;
        }

        private static void EnsureAccordionPlaneNormalTowardSheetUp(
            ref Vector3 accordionPlaneNormal,
            PaperGraph graph,
            Vector3 centerPos,
            Vector3 planeNormal,
            Vector3 sheetUp) {
            Vector3 toTopmost = GetToTopmostRelInPlane(graph, centerPos, planeNormal);
            if (toTopmost.sqrMagnitude < 0.00001f) {
                if (Vector3.Dot(accordionPlaneNormal, sheetUp) < 0f)
                    accordionPlaneNormal = -accordionPlaneNormal;
                return;
            }

            if (Vector3.Dot(toTopmost, accordionPlaneNormal) < 0f)
                accordionPlaneNormal = -accordionPlaneNormal;
        }

        private static Vector3 FlattenToPaperPlane(Vector3 v, Vector3 planeNormal) {
            Vector3 flat = Vector3.ProjectOnPlane(v, planeNormal);
            return flat.sqrMagnitude > 0.00001f ? flat.normalized : Vector3.zero;
        }

        private static void OrientCreaseAxisTowardSheetUp(ref Vector3 creaseAxis, Vector3 planeNormal, Vector3 sheetUp) {
            Vector3 foldPlaneNormal = Vector3.Cross(creaseAxis, planeNormal).normalized;
            if (Vector3.Dot(foldPlaneNormal, sheetUp) < 0f)
                creaseAxis = -creaseAxis;
        }

        /// <summary>
        /// Resolves a crease direction from graph edges at the center, falling back to the saved axis.
        /// </summary>
        private static Vector3 ResolveCreaseAxisFromGraph(
            PaperGraph graph,
            Vertex center,
            Vector3 planeNormal,
            Vector3 fallbackAxis) {
            Vector3 centerPos = center.Position;
            Vector3 fallback = FlattenToPaperPlane(fallbackAxis, planeNormal);
            if (fallback.sqrMagnitude < 0.00001f)
                fallback = GetReferenceXInPlane(planeNormal);

            Vector3 bestPosDir = Vector3.zero;
            float bestPosDist = 0f;
            Vector3 bestNegDir = Vector3.zero;
            float bestNegDist = 0f;

            foreach (Edge edge in center.Edges) {
                Vertex other = edge.V1 == center ? edge.V2 : edge.V1;
                Vector3 delta = Vector3.ProjectOnPlane(other.Position - centerPos, planeNormal);
                if (delta.sqrMagnitude < 0.00001f) continue;

                Vector3 dir = delta.normalized;
                float dist = delta.sqrMagnitude;
                float align = Vector3.Dot(dir, fallback);

                if (align > 0.25f && dist > bestPosDist) {
                    bestPosDist = dist;
                    bestPosDir = dir;
                } else if (align < -0.25f && dist > bestNegDist) {
                    bestNegDist = dist;
                    bestNegDir = dir;
                }
            }

            if (bestPosDist > 0.00001f && bestNegDist > 0.00001f) {
                Vector3 combined = bestPosDir - bestNegDir;
                if (combined.sqrMagnitude > 0.00001f)
                    return combined.normalized;
            }

            if (bestPosDist > 0.00001f)
                return bestPosDir;

            if (bestNegDist > 0.00001f)
                return -bestNegDir;

            return fallback;
        }

        private static AccordionCollapseData BuildCollapseData(
            PaperGraph graph,
            Vertex center,
            Vector3 creaseAxisA,
            Vector3 creaseAxisB,
            Vector3 accordionAxisDir,
            Vector3 accordionPlaneNormal,
            Vector3 planeNormal,
            Vector3 dragAxis,
            float foldDegrees,
            float foldOffset) {
            AccordionCollapseData data = new AccordionCollapseData();
            data.CenterIndex = graph.Vertices.IndexOf(center);
            Vector3 centerPos = center.Position;

            data.CenterPosition = centerPos;
            data.CreaseAxisA = creaseAxisA;
            data.CreaseAxisB = creaseAxisB;
            data.AccordionAxisDir = accordionAxisDir;
            data.AccordionPlaneNormal = accordionPlaneNormal;
            data.PlaneNormal = planeNormal;
            data.DragAxis = dragAxis;
            data.FoldDegrees = foldDegrees;
            data.FoldOffset = foldOffset;

            foreach (Vertex v in graph.Vertices) {
                int idx = graph.Vertices.IndexOf(v);
                data.FlatPositions[idx] = v.Position;
            }

            data.AccordionBoundaryIndices = CollectAccordionBoundaryVertexIndices(
                graph, centerPos, accordionAxisDir, data.CenterIndex);

            foreach (var kvp in data.FlatPositions) {
                int idx = kvp.Key;
                Vector3 flat = kvp.Value;
                ComputeCollapsedPosition(idx, flat, data, out Vector3 collapsed, out bool moves);
                data.CollapsedPositions[idx] = collapsed;
                if (moves)
                    data.MovedVertexIndices.Add(idx);
            }

            return data;
        }

        private static HashSet<int> CollectAccordionBoundaryVertexIndices(
            PaperGraph graph,
            Vector3 centerPos,
            Vector3 accordionAxisDir,
            int centerIndex) {
            HashSet<int> indices = new HashSet<int>();
            float axisTol = PositionTolerance * 50f;

            foreach (Vertex v in graph.Vertices) {
                int idx = graph.Vertices.IndexOf(v);
                if (idx < 0 || idx == centerIndex) continue;
                if (!IsVertexOnBoundary(v)) continue;
                if (!IsOnAxisLine(v.Position, centerPos, accordionAxisDir, axisTol)) continue;
                indices.Add(idx);
            }

            return indices;
        }

        private static bool IsVertexOnBoundary(Vertex vertex) {
            foreach (Edge edge in vertex.Edges) {
                if (IsBoundaryEdge(edge))
                    return true;
            }
            return false;
        }

        private static bool IsOnAxisLine(Vector3 pos, Vector3 axisPoint, Vector3 axisDir, float tolerance) {
            Vector3 toPoint = pos - axisPoint;
            float distToLine = Vector3.Cross(axisDir, toPoint).magnitude;
            return distToLine <= tolerance;
        }

        /// <summary>
        /// For a boundary hit on the accordion axis, returns the crease to fold over.
        /// The vertex is above one crease and below the other; we fold over the crease it sits
        /// above (positive side), matching <see cref="PaperGraph.ExecuteFold"/> moved-side convention.
        /// </summary>
        private static bool TryGetBoundaryCreaseAxis(
            Vector3 pos,
            Vector3 centerPos,
            Vector3 creaseAxisA,
            Vector3 creaseAxisB,
            Vector3 planeNormal,
            out Vector3 creaseAxisDir) {
            Vector3 planeNormalA = Vector3.Cross(creaseAxisA, planeNormal).normalized;
            Vector3 planeNormalB = Vector3.Cross(creaseAxisB, planeNormal).normalized;

            float sideA = Vector3.Dot(pos - centerPos, planeNormalA);
            float sideB = Vector3.Dot(pos - centerPos, planeNormalB);

            bool aboveA = sideA > PositionTolerance;
            bool aboveB = sideB > PositionTolerance;
            bool belowA = sideA < -PositionTolerance;
            bool belowB = sideB < -PositionTolerance;

            if (aboveA && belowB) {
                creaseAxisDir = creaseAxisA;
                return true;
            }

            if (aboveB && belowA) {
                creaseAxisDir = creaseAxisB;
                return true;
            }

            creaseAxisDir = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Reflects a point across a fold axis in the paper plane, then applies hinge offset.
        /// For 180° folds this matches <see cref="PaperGraph.ExecuteFold"/> for vertices on the moved side.
        /// </summary>
        private static Vector3 MirrorPointInPaperPlane(
            Vector3 point,
            Vector3 hingeOnAxis,
            Vector3 foldAxis,
            Vector3 planeNormal) {
            Vector3 axis = FlattenToPaperPlane(foldAxis, planeNormal);
            if (axis.sqrMagnitude < 0.00001f)
                axis = GetReferenceXInPlane(planeNormal);

            Vector3 n = planeNormal.sqrMagnitude > 0.0001f ? planeNormal.normalized : Vector3.up;
            Vector3 rel = point - hingeOnAxis;
            Vector3 relInPlane = Vector3.ProjectOnPlane(rel, n);

            float along = Vector3.Dot(relInPlane, axis);
            Vector3 parallel = axis * along;
            Vector3 perpendicular = relInPlane - parallel;
            Vector3 mirroredInPlane = parallel - perpendicular;

            return hingeOnAxis + mirroredInPlane + Vector3.Project(rel, n);
        }

        private static Vector3 MirrorFoldInPaperPlane(
            Vector3 flat,
            Vector3 hingeOnAxis,
            Vector3 foldAxis,
            Vector3 planeVector,
            float degrees,
            float foldOffset) {
            Vector3 folded = MirrorPointInPaperPlane(flat, hingeOnAxis, foldAxis, planeVector);

            float signedOffset = foldOffset * -Mathf.Sign(degrees);
            Vector3 n = planeVector.sqrMagnitude > 0.0001f ? planeVector.normalized : Vector3.up;
            if (Mathf.Approximately(Mathf.Abs(degrees), 180f) && Mathf.Abs(signedOffset) > 0.00001f)
                folded += n * signedOffset;

            return folded;
        }

        private static bool TryComputeCreaseIntersection(
            Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2, Vector3 planeNormal, out Vector3 intersection) {
            Vector3 n = planeNormal.sqrMagnitude > 0.0001f ? planeNormal.normalized : Vector3.up;
            Vector3 u = Vector3.ProjectOnPlane(a2 - a1, n);
            if (u.sqrMagnitude < 0.00001f) {
                intersection = Vector3.zero;
                return false;
            }
            u.Normalize();
            Vector3 v = Vector3.Cross(n, u).normalized;

            Vector2 A = ToPlane(a1, u, v);
            Vector2 B = ToPlane(a2, u, v);
            Vector2 C = ToPlane(b1, u, v);
            Vector2 D = ToPlane(b2, u, v);

            float denom = (B.x - A.x) * (D.y - C.y) - (B.y - A.y) * (D.x - C.x);
            if (Mathf.Abs(denom) < 0.00001f) {
                intersection = Vector3.zero;
                return false;
            }

            float t = ((C.x - A.x) * (D.y - C.y) - (C.y - A.y) * (D.x - C.x)) / denom;
            intersection = a1 + (a2 - a1) * t;
            return true;
        }

        private static Vertex FindOrCreateCenterVertex(PaperGraph graph, Vector3 position, Vector3 planeNormal) {
            Vertex existing = FindVertexNear(graph, position);
            if (existing != null)
                return existing;

            SplitEdgesCrossingPoint(graph, position, planeNormal);

            existing = FindVertexNear(graph, position);
            if (existing != null)
                return existing;

            Vertex center = new Vertex(position);
            graph.Vertices.Add(center);
            return center;
        }

        private static bool IsVertexInAnyFace(PaperGraph graph, Vertex v) {
            foreach (Face face in graph.Faces) {
                if (face.ContainsVertex(v))
                    return true;
            }
            return false;
        }

        private static void SplitCreasePlanesThroughPoint(
            PaperGraph graph,
            Vector3 point,
            Vector3 axisA1,
            Vector3 axisA2,
            Vector3 axisB1,
            Vector3 axisB2,
            Vector3 planeNormal) {
            Vector3 axisA = Vector3.ProjectOnPlane(axisA2 - axisA1, planeNormal);
            if (axisA.sqrMagnitude > 0.00001f) {
                Vector3 planeA = Vector3.Cross(axisA.normalized, planeNormal).normalized;
                graph.SplitEdgesCrossingPlane(point, planeA, 180f, null, 0f, out bool _);
            }

            Vector3 axisB = Vector3.ProjectOnPlane(axisB2 - axisB1, planeNormal);
            if (axisB.sqrMagnitude > 0.00001f) {
                Vector3 planeB = Vector3.Cross(axisB.normalized, planeNormal).normalized;
                graph.SplitEdgesCrossingPlane(point, planeB, 180f, null, 0f, out bool _);
            }
        }

        private static void SplitEdgesCrossingPoint(PaperGraph graph, Vector3 point, Vector3 planeNormal) {
            List<Edge> snapshot = new List<Edge>(graph.Edges);
            foreach (Edge edge in snapshot) {
                if (!IsPointOnEdgeInterior(point, edge.V1.Position, edge.V2.Position))
                    continue;

                float t = EdgeParameter(point, edge.V1.Position, edge.V2.Position);
                graph.SplitEdgeAtPoint(edge, t);
            }
        }

        private static void EnsureCreaseRaysFromCenter(
            PaperGraph graph,
            Vertex center,
            Vector3 axisP1,
            Vector3 axisP2,
            string creaseTag,
            Vector3 planeNormal) {
            Vector3 axisDir = Vector3.ProjectOnPlane(axisP2 - axisP1, planeNormal);
            if (axisDir.sqrMagnitude < 0.00001f) return;
            axisDir.Normalize();

            List<Vertex> hits = FindBoundaryVerticesAlongAxis(graph, center.Position, axisDir, planeNormal);
            foreach (Vertex hit in hits) {
                TagVertex(graph, creaseTag, hit);
                EnsureFaceSplitBetween(graph, center, hit);
            }
        }

        private static List<Vertex> FindBoundaryVerticesAlongAxis(
            PaperGraph graph, Vector3 origin, Vector3 axisDir, Vector3 planeNormal) {
            List<Vertex> hits = new List<Vertex>();
            Vector3 dir = axisDir.normalized;

            foreach (int sign in new[] { 1, -1 }) {
                Vertex best = null;
                float bestDist = float.PositiveInfinity;

                foreach (Edge edge in graph.Edges) {
                    if (!IsBoundaryEdge(edge)) continue;

                    if (TryRaySegmentIntersection(origin, dir * sign, edge.V1.Position, edge.V2.Position, planeNormal, out Vector3 hit, out float dist)) {
                        Vertex vertex = PickEndpointNear(edge, hit);
                        if (vertex != null && dist > PositionTolerance && dist < bestDist) {
                            bestDist = dist;
                            best = vertex;
                        }
                    }
                }

                if (best != null)
                    hits.Add(best);
            }

            return hits;
        }

        private static Vertex PickEndpointNear(Edge edge, Vector3 hit) {
            float d1 = Vector3.Distance(edge.V1.Position, hit);
            float d2 = Vector3.Distance(edge.V2.Position, hit);
            return d1 <= d2 ? edge.V1 : edge.V2;
        }

        private static bool TryRaySegmentIntersection(
            Vector3 rayOrigin, Vector3 rayDir, Vector3 segA, Vector3 segB, Vector3 planeNormal,
            out Vector3 hit, out float distance) {
            Vector3 n = planeNormal.normalized;
            Vector3 u = rayDir.sqrMagnitude > 0.00001f ? rayDir.normalized : Vector3.zero;
            if (u.sqrMagnitude < 0.00001f) {
                hit = Vector3.zero;
                distance = 0f;
                return false;
            }
            Vector3 v = Vector3.Cross(n, u).normalized;

            Vector2 O = ToPlane(rayOrigin, u, v);
            Vector2 D = ToPlane(rayOrigin + u, u, v);
            Vector2 A = ToPlane(segA, u, v);
            Vector2 B = ToPlane(segB, u, v);

            Vector2 r = D - O;
            Vector2 s = B - A;
            float denom = Cross2(r, s);
            if (Mathf.Abs(denom) < 0.00001f) {
                hit = Vector3.zero;
                distance = 0f;
                return false;
            }

            float t = Cross2(A - O, s) / denom;
            float uParam = Cross2(A - O, r) / denom;
            if (t < 0.0001f || uParam < -0.0001f || uParam > 1.0001f) {
                hit = Vector3.zero;
                distance = 0f;
                return false;
            }

            hit = rayOrigin + u * t;
            distance = t;
            return true;
        }

        private static void EnsureFaceSplitBetween(PaperGraph graph, Vertex a, Vertex b, Face preferredFace = null) {
            if (a == null || b == null || a == b) return;
            if (HasEdgeBetweenVertices(a, b)) return;

            Face face = preferredFace;
            if (face == null || !face.ContainsVertex(a) || !face.ContainsVertex(b))
                face = FindFaceContaining(graph, a, b);

            if (face == null) return;
            if (!face.ContainsVertex(a) || !face.ContainsVertex(b)) return;
            if (HasEdgeBetween(face, a, b)) return;

            graph.SplitFace(a, b, face);
        }

        private static Face FindFaceContaining(PaperGraph graph, Vertex a, Vertex b) {
            foreach (Face face in graph.Faces) {
                if (face.ContainsVertex(a) && face.ContainsVertex(b))
                    return face;
            }
            return null;
        }

        private static Vertex FindVertexNear(PaperGraph graph, Vector3 position) {
            Vertex best = null;
            float bestDist = float.PositiveInfinity;
            foreach (Vertex v in graph.Vertices) {
                float d = Vector3.Distance(v.Position, position);
                if (d < bestDist) {
                    bestDist = d;
                    best = v;
                }
            }
            return bestDist <= PositionTolerance * 50f ? best : null;
        }

        private static void TagVertex(PaperGraph graph, string tag, Vertex v) {
            if (string.IsNullOrEmpty(tag) || v == null) return;
            graph.AddVertexToTag(tag + "_edge", v);
        }

        private static bool HasEdgeBetweenVertices(Vertex a, Vertex b) {
            foreach (Edge e in a.Edges) {
                if (e.V1 == b || e.V2 == b) return true;
            }
            return false;
        }

        private static bool HasEdgeBetween(Face face, Vertex a, Vertex b) {
            foreach (Edge e in face.Edges) {
                if ((e.V1 == a && e.V2 == b) || (e.V1 == b && e.V2 == a))
                    return true;
            }
            return false;
        }

        private static bool IsPointOnEdgeInterior(Vector3 p, Vector3 a, Vector3 b) {
            if (!IsPointOnEdgeSegment(p, a, b)) return false;
            float t = EdgeParameter(p, a, b);
            return t > 0.0001f && t < 0.9999f;
        }

        private static float EdgeParameter(Vector3 p, Vector3 a, Vector3 b) {
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f) return 0f;
            return Vector3.Dot(p - a, ab) / lenSq;
        }

        private static bool IsBoundaryEdge(Edge e) => e.Face1 == null || e.Face2 == null;

        private static bool IsPointOnEdgeSegment(Vector3 p, Vector3 a, Vector3 b) {
            Vector3 ab = b - a;
            float len = ab.magnitude;
            if (len < 0.0001f) return Vector3.Distance(p, a) < PositionTolerance;

            float t = Vector3.Dot(p - a, ab) / (len * len);
            if (t < -PositionTolerance || t > 1f + PositionTolerance) return false;

            Vector3 closest = a + t * ab;
            return Vector3.Distance(p, closest) < PositionTolerance * 10f;
        }

        private static Vector2 ToPlane(Vector3 p, Vector3 u, Vector3 v) {
            return new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v));
        }

        private static float Cross2(Vector2 a, Vector2 b) {
            return a.x * b.y - a.y * b.x;
        }
    }
}
