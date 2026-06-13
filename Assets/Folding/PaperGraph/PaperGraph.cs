using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Folding.PaperGraph
{

public class PaperGraph : MonoBehaviour
{
    [FormerlySerializedAs("vertices")]
    public List<Vertex> Vertices = new List<Vertex>();
    [FormerlySerializedAs("edges")]
    public List<Edge> Edges = new List<Edge>();
    [FormerlySerializedAs("faces")]
    public List<Face> Faces = new List<Face>();
    [FormerlySerializedAs("tags")]
    public Dictionary<string, List<Vertex>> Tags = new Dictionary<string, List<Vertex>>();

    [FormerlySerializedAs("width")]
    public float Width = 1f;
    [FormerlySerializedAs("height")]
    public float Height = 1f;

    private List<PaperGraphSnapshot> _undoStack = new List<PaperGraphSnapshot>();
    private List<PaperGraphSnapshot> _redoStack = new List<PaperGraphSnapshot>();
    private AccordionCollapseData _accordionData;

    private struct VertexRotationAnimation {
        public bool Active;
        public Vector3 Pivot;
        public Vector3 Axis;
        public float TargetDegrees;
        public List<Vector3> BaselinePositions;
    }

    private VertexRotationAnimation _vertexRotationAnimation;

    public bool HasAccordionData => _accordionData != null;

    /// <summary>
    /// Adds a vertex to the given tag list, creating the list if needed.
    /// </summary>
    public void AddVertexToTag(string tag, Vertex v) {
        if (!Tags.ContainsKey(tag))
            Tags[tag] = new List<Vertex>();
        if (!Tags[tag].Contains(v))
            Tags[tag].Add(v);
    }

    /// <summary>
    /// Returns the vertex list for a tag, or an empty list if the tag doesn't exist.
    /// </summary>
    public List<Vertex> GetVerticesForTag(string tag) {
        if (Tags.ContainsKey(tag))
            return Tags[tag];
        return new List<Vertex>();
    }

    private HashSet<Vertex> BuildFilterSet(IReadOnlyList<string> filterTags) {
        if (filterTags == null || filterTags.Count == 0) return null;

        HashSet<Vertex> filterSet = null;
        foreach (string tag in filterTags) {
            if (string.IsNullOrEmpty(tag)) continue;
            if (!Tags.ContainsKey(tag))
                return new HashSet<Vertex>();

            HashSet<Vertex> tagVertices = new HashSet<Vertex>(Tags[tag]);
            if (filterSet == null)
                filterSet = tagVertices;
            else
                filterSet.IntersectWith(tagVertices);
        }

        return filterSet ?? new HashSet<Vertex>();
    }

    private void Start() {
        Vertices.Clear();
        Edges.Clear();
        Faces.Clear();
        Tags.Clear();
        CreateSheet(Width, Height);
    }
    
    public bool ExecuteFold(Vector3 foldPoint1, Vector3 foldPoint2, Vector3 planeVector, float degrees, string tagName = null, IReadOnlyList<string> filterTags = null, float foldOffset = 0f) {
        // Save state before the fold for undo
        _undoStack.Add(CreateSnapshot());
        _redoStack.Clear();

        HashSet<Vertex> filterSet = BuildFilterSet(filterTags);

        Vector3 foldAxis = (foldPoint2 - foldPoint1).normalized;
        Vector3 planeNormal = Vector3.Cross(foldAxis, planeVector).normalized;

        // For flat folds the signed offset is stored on crease edges so the graph is self-describing.
        float signedOffset = foldOffset * -Mathf.Sign(degrees);

        // Split edges along the fold plane; crease edges produced by SplitFace will carry signedOffset.
        bool splitValid;
        List<Vertex> splitVertices = SplitEdgesCrossingPlane(foldPoint1, planeNormal, degrees, filterSet, signedOffset, out splitValid);

        // Also invalid if the fold axis doesn't cleanly cross the paper (< 2 intersection points):
        // this means the axis missed the mesh entirely, or only grazed a single corner, and the
        // subsequent rotation would yank all vertices on one side causing unnatural stretching.
        if (!splitValid || splitVertices.Count < 2) {
            PaperGraphSnapshot bad = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            RestoreSnapshot(bad);
            _redoStack.Clear();
            return false;
        }

        // Pre-compute the set of vertices on the positive (moved) side of the plane.
        HashSet<Vertex> movedSet = new HashSet<Vertex>();
        foreach (Vertex v in Vertices) {
            if (filterSet != null && !filterSet.Contains(v)) continue;
            float s = Vector3.Dot(v.Position - foldPoint1, planeNormal);
            if (s > 0.0001f) movedSet.Add(v);
        }

        // Duplicate split vertices: originals stay at crease, duplicates travel with the moved side.
        // Must run before rotation so duplicates are included in the rotation loop.
        Dictionary<Vertex, Vertex> foldDupMap = DuplicateSplitVertices(splitVertices, movedSet);
        HashSet<Vertex> splitSet = new HashSet<Vertex>(splitVertices);
        HashSet<Vertex> foldDupSet = new HashSet<Vertex>(foldDupMap.Values);

        // Tag all crease vertices (originals + fold duplicates) as _edge
        if (!string.IsNullOrEmpty(tagName)) {
            foreach (Vertex sv in splitVertices)
                AddVertexToTag(tagName + "_edge", sv);
            foreach (Vertex dv in foldDupMap.Values)
                AddVertexToTag(tagName + "_edge", dv);
        }

        // Rotate vertices on the moved side (or on the plane but off the fold axis).
        // Original split vertices are never rotated.
        // Fold duplicates are ALWAYS rotated+offset — they represent the moved side of the crease.
        Quaternion rotation = Quaternion.AngleAxis(degrees, foldAxis);
        foreach (Vertex v in Vertices) {
            // Fold duplicates always travel with the moved side, regardless of position
            bool isFoldDup = foldDupSet.Contains(v);

            if (filterSet != null && !filterSet.Contains(v) && !isFoldDup)
                continue;

            if (splitSet.Contains(v))
                continue;

            if (isFoldDup) {
                v.Position = foldPoint1 + rotation * (v.Position - foldPoint1);
                if (Mathf.Approximately(Mathf.Abs(degrees), 180f) && Mathf.Abs(signedOffset) > 0.00001f) {
                    v.Position += planeVector.normalized * signedOffset;
                }
                // Already tagged _edge above
                continue;
            }

            float side = Vector3.Dot(v.Position - foldPoint1, planeNormal);
            Vector3 toV = v.Position - foldPoint1;
            float distToAxis = Vector3.Cross(foldAxis, toV).magnitude;

            bool onPositiveSide = side > 0.0001f;
            bool onPlaneOffAxis = Mathf.Abs(side) <= 0.0001f && distToAxis > 0.0001f;

            if (onPlaneOffAxis) {
                // Only move an on-plane vertex if it is actually attached to the moved side.
                // Otherwise this rips apart perfectly static sheets that happen to touch the fold plane.
                bool attachedToMoved = false;
                foreach (Edge e in v.Edges) {
                    Vertex other = (e.V1 == v) ? e.V2 : e.V1;
                    if (Vector3.Dot(other.Position - foldPoint1, planeNormal) > 0.0001f) {
                        attachedToMoved = true;
                        break;
                    }
                }
                if (!attachedToMoved) {
                    onPlaneOffAxis = false;
                }
            }

            if (onPositiveSide || onPlaneOffAxis) {
                v.Position = foldPoint1 + rotation * (v.Position - foldPoint1);

                if (Mathf.Approximately(Mathf.Abs(degrees), 180f) && Mathf.Abs(signedOffset) > 0.00001f) {
                    v.Position += planeVector.normalized * signedOffset;
                }

                if (!string.IsNullOrEmpty(tagName))
                    AddVertexToTag(tagName + "_moved", v);
            } else {
                if (!string.IsNullOrEmpty(tagName))
                    AddVertexToTag(tagName + "_static", v);
            }
        }

        // Flat fold hinge: create hinge face quads between originals and fold duplicates.
        if (Mathf.Approximately(Mathf.Abs(degrees), 180f) && splitVertices.Count > 0) {
            CreateHinge(splitVertices, foldDupMap);
        }
        return true;
    }

    /// <summary>
    /// Splits edges and faces along the fold plane without rotating geometry.
    /// When tagged, applies the same _edge / _moved / _static labels as <see cref="ExecuteFold"/>.
    /// </summary>
    public bool ExecuteCrease(Vector3 foldPoint1, Vector3 foldPoint2, Vector3 planeVector, string tagName = null, IReadOnlyList<string> filterTags = null, float foldAngle = 180f) {
        _undoStack.Add(CreateSnapshot());
        _redoStack.Clear();

        HashSet<Vertex> filterSet = BuildFilterSet(filterTags);

        Vector3 foldAxis = (foldPoint2 - foldPoint1).normalized;
        Vector3 planeNormal = Vector3.Cross(foldAxis, planeVector).normalized;

        bool splitValid;
        List<Vertex> splitVertices = SplitEdgesCrossingPlane(foldPoint1, planeNormal, foldAngle, filterSet, 0f, out splitValid);

        if (!splitValid || splitVertices.Count < 2) {
            PaperGraphSnapshot bad = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            RestoreSnapshot(bad);
            _redoStack.Clear();
            return false;
        }

        if (!string.IsNullOrEmpty(tagName)) {
            foreach (Vertex sv in splitVertices)
                AddVertexToTag(tagName + "_edge", sv);

            TagVerticesByFoldSide(foldPoint1, foldAxis, planeNormal, splitVertices, tagName, filterSet);
        }

        return true;
    }

    /// <summary>
    /// Captures the current vertex positions and prepares an animated rotation.
    /// Does not push an undo snapshot — the preceding fold/crease owns the step undo.
    /// </summary>
    public bool BeginVertexRotationAnimation(Vector3 pivot, Vector3 axis, float degrees) {
        if (axis.sqrMagnitude < 0.00001f)
            return false;

        var baselinePositions = new List<Vector3>(Vertices.Count);
        foreach (Vertex v in Vertices)
            baselinePositions.Add(v.Position);

        _vertexRotationAnimation = new VertexRotationAnimation {
            Active = true,
            Pivot = pivot,
            Axis = axis.normalized,
            TargetDegrees = degrees,
            BaselinePositions = baselinePositions
        };
        return true;
    }

    public void SetVertexRotationProgress(float t) {
        if (!_vertexRotationAnimation.Active)
            return;

        float degrees = _vertexRotationAnimation.TargetDegrees * Mathf.Clamp01(t);
        Quaternion rotation = Quaternion.AngleAxis(degrees, _vertexRotationAnimation.Axis);
        Vector3 pivot = _vertexRotationAnimation.Pivot;

        for (int i = 0; i < Vertices.Count; i++) {
            Vector3 baseline = _vertexRotationAnimation.BaselinePositions[i];
            Vertices[i].Position = pivot + rotation * (baseline - pivot);
        }
    }

    public void CommitVertexRotationAnimation() {
        if (!_vertexRotationAnimation.Active)
            return;

        SetVertexRotationProgress(1f);
        ClearVertexRotationAnimation();
    }

    public void ClearVertexRotationAnimation() {
        _vertexRotationAnimation = default;
    }

    /// <summary>
    /// Tags non-crease vertices as tag_moved or tag_static based on which side of the fold plane
    /// they lie on, matching <see cref="ExecuteFold"/> side classification.
    /// </summary>
    private void TagVerticesByFoldSide(
        Vector3 foldPoint1,
        Vector3 foldAxis,
        Vector3 planeNormal,
        List<Vertex> splitVertices,
        string tagName,
        HashSet<Vertex> filterSet) {
        HashSet<Vertex> splitSet = new HashSet<Vertex>(splitVertices);

        foreach (Vertex v in Vertices) {
            if (filterSet != null && !filterSet.Contains(v)) continue;
            if (splitSet.Contains(v)) continue;

            float side = Vector3.Dot(v.Position - foldPoint1, planeNormal);
            Vector3 toV = v.Position - foldPoint1;
            float distToAxis = Vector3.Cross(foldAxis, toV).magnitude;

            bool onPositiveSide = side > 0.0001f;
            bool onPlaneOffAxis = Mathf.Abs(side) <= 0.0001f && distToAxis > 0.0001f;

            if (onPlaneOffAxis) {
                bool attachedToMoved = false;
                foreach (Edge e in v.Edges) {
                    Vertex other = (e.V1 == v) ? e.V2 : e.V1;
                    if (Vector3.Dot(other.Position - foldPoint1, planeNormal) > 0.0001f) {
                        attachedToMoved = true;
                        break;
                    }
                }
                if (!attachedToMoved)
                    onPlaneOffAxis = false;
            }

            if (onPositiveSide || onPlaneOffAxis)
                AddVertexToTag(tagName + "_moved", v);
            else
                AddVertexToTag(tagName + "_static", v);
        }
    }

    /// <summary>
    /// Splits topology for an accordion collapse (horizontal face splits) and records flat pose data.
    /// Does not move vertices. Adds an undo snapshot.
    /// </summary>
    public bool PrepareAccordionCollapse(
        string creaseTagA,
        string creaseTagB,
        string tagName,
        Vector3 creaseAxisA1,
        Vector3 creaseAxisA2,
        Vector3 creaseAxisB1,
        Vector3 creaseAxisB2,
        float foldDegrees,
        Vector3 planeVector,
        float foldOffset) {
        _undoStack.Add(CreateSnapshot());
        _redoStack.Clear();

        AccordionCollapseData data;
        if (!AccordionCollapse.TryPrepare(
            this, creaseTagA, creaseTagB, tagName,
            creaseAxisA1, creaseAxisA2, creaseAxisB1, creaseAxisB2,
            foldDegrees, planeVector, foldOffset,
            out data, out string error)) {
            PaperGraphSnapshot bad = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            RestoreSnapshot(bad);
            _redoStack.Clear();
            Debug.LogWarning($"PaperGraph: Accordion prepare failed — {error}");
            return false;
        }

        _accordionData = data;
        return true;
    }

    /// <summary>
    /// Applies accordion collapse pose for preview or commit. Requires <see cref="PrepareAccordionCollapse"/> first.
    /// </summary>
    public bool ApplyAccordionCollapsePose(float t, float foldOffset = 0f) {
        if (_accordionData == null) return false;
        AccordionCollapse.ApplyPose(this, _accordionData, t, foldOffset);
        return true;
    }

    public AccordionCollapseData GetAccordionData() => _accordionData;

    /// <summary>
    /// Restores vertices to the flat pose recorded during accordion prepare.
    /// </summary>
    public void RestoreAccordionFlatPose() {
        if (_accordionData == null) return;
        AccordionCollapse.RestoreFlatPose(this, _accordionData);
    }

    public bool CommitAccordionCollapse(float foldOffset = 0f, float t = 1f) {
        if (_accordionData == null) return false;
        AccordionCollapse.ApplyPose(this, _accordionData, t, foldOffset);
        _accordionData = null;
        return true;
    }

    public void ClearAccordionData() {
        _accordionData = null;
    }

    /// <summary>
    /// Inserts a vertex at the midpoint of an edge and updates adjacent faces.
    /// </summary>
    public Vertex SplitEdgeAtMidpoint(Edge edge) {
        if (edge == null) return null;

        Vector3 midPos = (edge.V1.Position + edge.V2.Position) * 0.5f;
        Vector2 midUv = (edge.V1.Uv + edge.V2.Uv) * 0.5f;

        Vertex mid = new Vertex(midPos);
        mid.Uv = midUv;
        Vertices.Add(mid);

        foreach (var kvp in Tags) {
            bool v1InTag = kvp.Value.Contains(edge.V1);
            bool v2InTag = kvp.Value.Contains(edge.V2);
            if ((v1InTag || v2InTag) && !kvp.Value.Contains(mid))
                kvp.Value.Add(mid);
        }

        Edge edgeA = new Edge(edge.V1, mid);
        Edge edgeB = new Edge(mid, edge.V2);
        edgeA.FoldAngle = edge.FoldAngle;
        edgeB.FoldAngle = edge.FoldAngle;
        edgeA.FoldOffset = edge.FoldOffset;
        edgeB.FoldOffset = edge.FoldOffset;
        edgeA.Face1 = edge.Face1;
        edgeA.Face2 = edge.Face2;
        edgeB.Face1 = edge.Face1;
        edgeB.Face2 = edge.Face2;

        edge.V1.Edges.Remove(edge);
        edge.V2.Edges.Remove(edge);
        Edges.Remove(edge);
        Edges.Add(edgeA);
        Edges.Add(edgeB);

        if (edge.Face1 != null)
            edge.Face1.ReplaceSplitEdge(edge, edgeA, edgeB, mid);
        if (edge.Face2 != null)
            edge.Face2.ReplaceSplitEdge(edge, edgeA, edgeB, mid);

        return mid;
    }

    /// <summary>
    /// Splits an edge at parameter <paramref name="t"/> along v1→v2 and updates adjacent faces.
    /// </summary>
    public Vertex SplitEdgeAtPoint(Edge edge, float t) {
        if (edge == null) return null;

        t = Mathf.Clamp01(t);
        if (t <= 0.00001f || t >= 0.99999f)
            return t < 0.5f ? edge.V1 : edge.V2;

        Vector3 pos = edge.V1.Position + t * (edge.V2.Position - edge.V1.Position);
        Vector2 uv = edge.V1.Uv + t * (edge.V2.Uv - edge.V1.Uv);

        Vertex split = new Vertex(pos);
        split.Uv = uv;
        Vertices.Add(split);

        foreach (var kvp in Tags) {
            bool v1InTag = kvp.Value.Contains(edge.V1);
            bool v2InTag = kvp.Value.Contains(edge.V2);
            if ((v1InTag || v2InTag) && !kvp.Value.Contains(split))
                kvp.Value.Add(split);
        }

        Edge edgeA = new Edge(edge.V1, split);
        Edge edgeB = new Edge(split, edge.V2);
        edgeA.FoldAngle = edge.FoldAngle;
        edgeB.FoldAngle = edge.FoldAngle;
        edgeA.FoldOffset = edge.FoldOffset;
        edgeB.FoldOffset = edge.FoldOffset;
        edgeA.Face1 = edge.Face1;
        edgeA.Face2 = edge.Face2;
        edgeB.Face1 = edge.Face1;
        edgeB.Face2 = edge.Face2;

        edge.V1.Edges.Remove(edge);
        edge.V2.Edges.Remove(edge);
        Edges.Remove(edge);
        Edges.Add(edgeA);
        Edges.Add(edgeB);

        if (edge.Face1 != null)
            edge.Face1.ReplaceSplitEdge(edge, edgeA, edgeB, split);
        if (edge.Face2 != null)
            edge.Face2.ReplaceSplitEdge(edge, edgeA, edgeB, split);

        return split;
    }

    public List<Vertex> SplitEdgesCrossingPlane(Vector3 planePoint, Vector3 planeNormal, float foldAngle, HashSet<Vertex> filterSet, float foldOffset, out bool isValid) {
        isValid = true;
        List<Edge> edgeSnapshot = new List<Edge>(Edges);
        Dictionary<Face, List<Vertex>> faceSplitVertices = new Dictionary<Face, List<Vertex>>();
        List<Vertex> newSplitVertices = new List<Vertex>();

        foreach (Edge oldEdge in edgeSnapshot) {
            // If filtering, only split edges where at least one endpoint is in the filter set
            if (filterSet != null && !filterSet.Contains(oldEdge.V1) && !filterSet.Contains(oldEdge.V2))
                continue;

            float d1 = Vector3.Dot(oldEdge.V1.Position - planePoint, planeNormal);
            float d2 = Vector3.Dot(oldEdge.V2.Position - planePoint, planeNormal);

            // Only split edges that cross the plane (endpoints on opposite sides)
            if (d1 * d2 >= 0f)
                continue;

            // Compute intersection point
            float t = d1 / (d1 - d2);
            Vector3 intersectionPoint = oldEdge.V1.Position + t * (oldEdge.V2.Position - oldEdge.V1.Position);
            Vector2 intersectionUV = oldEdge.V1.Uv + t * (oldEdge.V2.Uv - oldEdge.V1.Uv);

            Vertex vNew = new Vertex(intersectionPoint);
            vNew.Uv = intersectionUV;
            Vertices.Add(vNew);
            newSplitVertices.Add(vNew);

            // Propagate tags: add vNew to every tag that either endpoint belongs to
            foreach (var kvp in Tags) {
                bool v1InTag = kvp.Value.Contains(oldEdge.V1);
                bool v2InTag = kvp.Value.Contains(oldEdge.V2);
                if (v1InTag || v2InTag) {
                    if (!kvp.Value.Contains(vNew))
                        kvp.Value.Add(vNew);
                }
            }

            // Create two new edges to replace the old one
            Edge edgeA = new Edge(oldEdge.V1, vNew);
            Edge edgeB = new Edge(vNew, oldEdge.V2);
            edgeA.FoldAngle = oldEdge.FoldAngle;
            edgeB.FoldAngle = oldEdge.FoldAngle;
            edgeA.Face1 = oldEdge.Face1;
            edgeA.Face2 = oldEdge.Face2;
            edgeB.Face1 = oldEdge.Face1;
            edgeB.Face2 = oldEdge.Face2;

            // Remove the old edge entirely
            oldEdge.V1.Edges.Remove(oldEdge);
            oldEdge.V2.Edges.Remove(oldEdge);
            Edges.Remove(oldEdge);

            // Add the two new edges
            Edges.Add(edgeA);
            Edges.Add(edgeB);

            // Update faces to swap old edge for the two new ones
            if (oldEdge.Face1 != null) {
                oldEdge.Face1.ReplaceSplitEdge(oldEdge, edgeA, edgeB, vNew);
                if (!faceSplitVertices.ContainsKey(oldEdge.Face1))
                    faceSplitVertices[oldEdge.Face1] = new List<Vertex>();
                faceSplitVertices[oldEdge.Face1].Add(vNew);
            }
            if (oldEdge.Face2 != null) {
                oldEdge.Face2.ReplaceSplitEdge(oldEdge, edgeA, edgeB, vNew);
                if (!faceSplitVertices.ContainsKey(oldEdge.Face2))
                    faceSplitVertices[oldEdge.Face2] = new List<Vertex>();
                faceSplitVertices[oldEdge.Face2].Add(vNew);
            }
        }

        // Ensure any face that straddles the plane also registers its existing vertices that lie exactly on the fold plane
        foreach (Face face in Faces) {
            int posCount = 0;
            int negCount = 0;
            foreach (Vertex v in face.Vertices) {
                float d = Vector3.Dot(v.Position - planePoint, planeNormal);
                if (d > 0.0001f) posCount++;
                else if (d < -0.0001f) negCount++;
            }

            if (posCount > 0 && negCount > 0) {
                foreach (Vertex v in face.Vertices) {
                    float d = Vector3.Dot(v.Position - planePoint, planeNormal);
                    if (Mathf.Abs(d) <= 0.0001f) {
                        if (!faceSplitVertices.ContainsKey(face))
                            faceSplitVertices[face] = new List<Vertex>();
                        if (!faceSplitVertices[face].Contains(v))
                            faceSplitVertices[face].Add(v);
                        if (!newSplitVertices.Contains(v))
                            newSplitVertices.Add(v); // Also add to returned split vertices to ensure it becomes a proper crease
                    }
                }
            }
        }

        // Split any face that had exactly two edges cut by the plane
        foreach (var kvp in faceSplitVertices) {
            if (kvp.Value.Count == 2)
                SplitFace(kvp.Value[0], kvp.Value[1], kvp.Key, foldAngle, foldOffset);
            else if (kvp.Value.Count > 2)
                isValid = false;
        }

        return newSplitVertices;
    }

    public void SplitFace(Vertex vA, Vertex vB, Face face, float foldAngle = 180f, float foldOffset = 0f) {
        int idxA = face.Vertices.IndexOf(vA);
        int idxB = face.Vertices.IndexOf(vB);

        // Ensure idxA < idxB for consistent slicing
        if (idxA > idxB) {
            int tmp = idxA; idxA = idxB; idxB = tmp;
            Vertex vtmp = vA; vA = vB; vB = vtmp;
        }

        int n = face.Vertices.Count;

        // Create the splitting edge between the two vertices
        // Both foldAngle and foldOffset are stored on the crease edge so the
        // graph is self-describing and CreateHinge() can read them directly.
        Edge splitEdge = new Edge(vA, vB);
        splitEdge.FoldAngle = foldAngle;
        splitEdge.FoldOffset = foldOffset;
        Edges.Add(splitEdge);

        // --- Build face1: vertices[idxA..idxB], closed by splitEdge ---
        List<Vertex> verts1 = new List<Vertex>();
        List<Edge> edges1 = new List<Edge>();
        for (int i = idxA; i <= idxB; i++) {
            verts1.Add(face.Vertices[i]);
        }
        for (int i = idxA; i < idxB; i++) {
            edges1.Add(face.Edges[i]);
        }
        edges1.Add(splitEdge); // closing edge: vB -> vA

        // --- Build face2: vertices[idxB..idxA] wrapping around, closed by splitEdge ---
        List<Vertex> verts2 = new List<Vertex>();
        List<Edge> edges2 = new List<Edge>();
        for (int i = idxB; i != idxA; i = (i + 1) % n) {
            verts2.Add(face.Vertices[i]);
            edges2.Add(face.Edges[i]);
        }
        verts2.Add(face.Vertices[idxA]);
        edges2.Add(splitEdge); // closing edge: vA -> vB

        // Create the new second face
        Face face2 = new Face(verts2, edges2);
        Faces.Add(face2);

        // Update the existing face in-place to become face1
        face.Vertices = verts1;
        face.Edges = edges1;

        // Assign both faces to the split edge
        splitEdge.Face1 = face;
        splitEdge.Face2 = face2;

        // Update all edges that moved to face2
        foreach (Edge e in face2.Edges) {
            if (e == splitEdge) continue;
            if (e.Face1 == face) e.Face1 = face2;
            else if (e.Face2 == face) e.Face2 = face2;
        }
    }

    // ─── Hinge helpers ───────────────────────────────────────────────

    /// <summary>
    /// Rewires an edge so that oldV is replaced by newV.
    /// Updates the edge's v1/v2 and the vertex edge-lists.
    /// </summary>
    private void RewireEdgeVertex(Edge e, Vertex oldV, Vertex newV) {
        oldV.Edges.Remove(e);
        newV.Edges.Add(e);
        if (e.V1 == oldV) e.V1 = newV;
        else e.V2 = newV;
    }

    /// <summary>
    /// Replaces every occurrence of oldV with newV in a face's vertex list.
    /// </summary>
    private void ReplaceFaceVertex(Face face, Vertex oldV, Vertex newV) {
        for (int i = 0; i < face.Vertices.Count; i++) {
            if (face.Vertices[i] == oldV)
                face.Vertices[i] = newV;
        }
    }

    /// <summary>
    /// Replaces oldE with newE in a face's edge list and updates the edge's face references.
    /// </summary>
    private void ReplaceFaceEdge(Face face, Edge oldE, Edge newE) {
        int idx = face.Edges.IndexOf(oldE);
        if (idx >= 0) face.Edges[idx] = newE;

        // Update face references on the old and new edges
        if (oldE.Face1 == face) oldE.Face1 = null;
        else if (oldE.Face2 == face) oldE.Face2 = null;

        if (newE.Face1 == null) newE.Face1 = face;
        else if (newE.Face2 == null) newE.Face2 = face;
    }

    /// <summary>
    /// Returns true if any vertex of the face is in the movedSet.
    /// </summary>
    private bool FaceHasMovedVertex(Face face, HashSet<Vertex> movedSet) {
        foreach (Vertex v in face.Vertices) {
            if (movedSet.Contains(v)) return true;
        }
        return false;
    }

    /// <summary>
    /// Assigns a face to the first available slot (face1 or face2) on an edge.
    /// </summary>
    private void AssignFaceToEdge(Edge e, Face f) {
        if (e.Face1 == null) e.Face1 = f;
        else if (e.Face2 == null) e.Face2 = f;
    }

    /// <summary>
    /// Duplicates each split vertex so the original stays at the crease (static side)
    /// and the duplicate is used by moved-side faces/edges. The duplicate will later
    /// be rotated by the rotation loop.
    /// </summary>
    private Dictionary<Vertex, Vertex> DuplicateSplitVertices(List<Vertex> splitVertices, HashSet<Vertex> movedSet) {
        Dictionary<Vertex, Vertex> dupMap = new Dictionary<Vertex, Vertex>();
        HashSet<Vertex> splitSet = new HashSet<Vertex>(splitVertices);

        // 1. Create duplicates at the same position
        foreach (Vertex v in splitVertices) {
            Vertex dup = new Vertex(v.Position);
            dup.Uv = v.Uv;
            Vertices.Add(dup);
            dupMap[v] = dup;

            // Propagate all tags from original to duplicate
            foreach (var kvp in Tags) {
                if (kvp.Value.Contains(v) && !kvp.Value.Contains(dup))
                    kvp.Value.Add(dup);
            }
        }

        // 2. Rewire non-crease edges from split vertex to duplicate on the moved side
        foreach (Vertex splitV in splitVertices) {
            Vertex dupV = dupMap[splitV];
            List<Edge> snapshot = new List<Edge>(splitV.Edges);
            foreach (Edge e in snapshot) {
                Vertex other = (e.V1 == splitV) ? e.V2 : e.V1;
                if (splitSet.Contains(other)) continue; // crease edge — handled in step 3
                if (!movedSet.Contains(other)) continue; // static side — keep as-is

                if (e.Face1 != null && e.Face1.ContainsVertex(splitV))
                    ReplaceFaceVertex(e.Face1, splitV, dupV);
                if (e.Face2 != null && e.Face2.ContainsVertex(splitV))
                    ReplaceFaceVertex(e.Face2, splitV, dupV);

                RewireEdgeVertex(e, splitV, dupV);
            }
        }

        // 3. Handle crease edges (edges between two split vertices)
        HashSet<Edge> processedCrease = new HashSet<Edge>();
        foreach (Vertex splitV in splitVertices) {
            List<Edge> snapshot = new List<Edge>(splitV.Edges);
            foreach (Edge e in snapshot) {
                if (processedCrease.Contains(e)) continue;
                Vertex other = (e.V1 == splitV) ? e.V2 : e.V1;
                if (!dupMap.ContainsKey(other)) continue;
                processedCrease.Add(e);

                Vertex dupV = dupMap[splitV];
                Vertex dupOther = dupMap[other];

                // Create duplicate crease edge for the moved side
                Edge dupEdge = new Edge(dupV, dupOther);
                dupEdge.FoldAngle = e.FoldAngle;
                Edges.Add(dupEdge);

                // Find the moved-side faces and rewire them
                if (e.Face1 != null && FaceHasMovedVertex(e.Face1, movedSet)) {
                    ReplaceFaceVertex(e.Face1, splitV, dupV);
                    ReplaceFaceVertex(e.Face1, other, dupOther);
                    ReplaceFaceEdge(e.Face1, e, dupEdge);
                }
                
                if (e.Face2 != null && FaceHasMovedVertex(e.Face2, movedSet)) {
                    ReplaceFaceVertex(e.Face2, splitV, dupV);
                    ReplaceFaceVertex(e.Face2, other, dupOther);
                    ReplaceFaceEdge(e.Face2, e, dupEdge);
                }
            }
        }

        return dupMap;
    }

    /// <summary>
    /// Creates hinge face quads between original split vertices and their fold duplicates.
    /// The fold duplicates have already been rotated+offset by the rotation loop, so they
    /// are at their final position. Must be called AFTER DuplicateSplitVertices and rotation.
    /// Only creates geometry if crease edges carry a non-zero foldOffset.
    /// </summary>
    private void CreateHinge(List<Vertex> splitVertices, Dictionary<Vertex, Vertex> foldDupMap) {
        // Check if any crease edge has a non-zero foldOffset
        HashSet<Vertex> splitSet = new HashSet<Vertex>(splitVertices);
        bool hasOffset = false;
        foreach (Vertex v in splitVertices) {
            foreach (Edge e in v.Edges) {
                Vertex other = (e.V1 == v) ? e.V2 : e.V1;
                if (splitSet.Contains(other) && Mathf.Abs(e.FoldOffset) > 0.00001f) {
                    hasOffset = true;
                    goto checkedOffset;
                }
            }
        }
        checkedOffset:
        if (!hasOffset) return;

        // Create connecting edges and hinge faces between originals and fold duplicates
        HashSet<Edge> processedCrease = new HashSet<Edge>();
        Dictionary<Vertex, Edge> connectEdges = new Dictionary<Vertex, Edge>();

        foreach (Vertex splitV in splitVertices) {
            if (!foldDupMap.ContainsKey(splitV)) continue;
            Vertex dupV = foldDupMap[splitV];

            // Connecting edge (once per vertex)
            if (!connectEdges.ContainsKey(splitV)) {
                Edge ce = new Edge(splitV, dupV);
                Edges.Add(ce);
                connectEdges[splitV] = ce;
            }

            List<Edge> snapshot = new List<Edge>(splitV.Edges);
            foreach (Edge e in snapshot) {
                if (processedCrease.Contains(e)) continue;
                Vertex other = (e.V1 == splitV) ? e.V2 : e.V1;
                if (!splitSet.Contains(other)) continue;
                processedCrease.Add(e);

                if (!foldDupMap.ContainsKey(other)) continue;
                Vertex dupOther = foldDupMap[other];

                // Ensure connecting edge for the other vertex
                if (!connectEdges.ContainsKey(other)) {
                    Edge ce = new Edge(other, dupOther);
                    Edges.Add(ce);
                    connectEdges[other] = ce;
                }

                // Find the duplicate crease edge (between fold duplicates)
                Edge dupCreaseEdge = null;
                foreach (Edge de in dupV.Edges) {
                    if ((de.V1 == dupV && de.V2 == dupOther) || (de.V1 == dupOther && de.V2 == dupV)) {
                        dupCreaseEdge = de;
                        break;
                    }
                }
                if (dupCreaseEdge == null) continue;

                // Hinge face quad: [original, other, dupOther, dupV]
                Face hingeFace = new Face(
                    new List<Vertex> { splitV, other, dupOther, dupV },
                    new List<Edge> { e, connectEdges[other], dupCreaseEdge, connectEdges[splitV] }
                );
                Faces.Add(hingeFace);

                AssignFaceToEdge(e, hingeFace);
                AssignFaceToEdge(dupCreaseEdge, hingeFace);
                AssignFaceToEdge(connectEdges[splitV], hingeFace);
                AssignFaceToEdge(connectEdges[other], hingeFace);
            }
        }
    }

    public void CreateSheet(float width, float height) {
        
        // Create 4 corner vertices
        // Centered at origin, lying flat in XZ plane
        Vertex v0 = new Vertex(new Vector3(-width/2, 0, -height/2)); // bottom-left
        v0.Uv = new Vector2(0f, 0f);
        Vertex v1 = new Vertex(new Vector3(width/2, 0, -height/2));  // bottom-right
        v1.Uv = new Vector2(1f, 0f);
        Vertex v2 = new Vertex(new Vector3(width/2, 0, height/2));   // top-right
        v2.Uv = new Vector2(1f, 1f);
        Vertex v3 = new Vertex(new Vector3(-width/2, 0, height/2));  // top-left
        v3.Uv = new Vector2(0f, 1f);
        
        Vertices.AddRange(new[] { v0, v1, v2, v3 });
        
        // Create 4 boundary edges (connecting corners in order)
        Edge e0 = new Edge(v0, v1); // bottom
        Edge e1 = new Edge(v1, v2); // right
        Edge e2 = new Edge(v2, v3); // top
        Edge e3 = new Edge(v3, v0); // left
        
        Edges.AddRange(new[] { e0, e1, e2, e3 });

        // Create the single face for the sheet
        Face face = new Face(
            new List<Vertex> { v0, v1, v2, v3 },
            new List<Edge> { e0, e1, e2, e3 }
        );
        Faces.Add(face);

        // Assign face to all boundary edges (single face, so face1 only)
        e0.Face1 = face;
        e1.Face1 = face;
        e2.Face1 = face;
        e3.Face1 = face;
    }

    /// <summary>
    /// Undoes the last fold, restoring the previous state.
    /// </summary>
    public void Undo() {
        if (_undoStack.Count == 0) {
            Debug.Log("Nothing to undo.");
            return;
        }

        // Save current state to redo stack before restoring
        _redoStack.Add(CreateSnapshot());

        PaperGraphSnapshot snapshot = _undoStack[_undoStack.Count - 1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        RestoreSnapshot(snapshot);
    }

    /// <summary>
    /// Redoes the last undone fold. Only works if no new folds have been made since the last undo.
    /// </summary>
    public void Redo() {
        if (_redoStack.Count == 0) {
            Debug.Log("Nothing to redo.");
            return;
        }

        // Save current state to undo stack before restoring
        _undoStack.Add(CreateSnapshot());

        PaperGraphSnapshot snapshot = _redoStack[_redoStack.Count - 1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        RestoreSnapshot(snapshot);
    }

    /// <summary>
    /// Creates a deep-copy snapshot of the current graph state.
    /// </summary>
    public PaperGraphSnapshot CreateSnapshot() {
        // Clone vertices
        Dictionary<Vertex, Vertex> vertexMap = new Dictionary<Vertex, Vertex>();
        List<Vertex> clonedVertices = new List<Vertex>();
        foreach (Vertex v in Vertices) {
            Vertex clone = new Vertex(v.Position);
            clone.Uv = v.Uv;
            vertexMap[v] = clone;
            clonedVertices.Add(clone);
        }

        // Clone edges (using cloned vertices)
        Dictionary<Edge, Edge> edgeMap = new Dictionary<Edge, Edge>();
        List<Edge> clonedEdges = new List<Edge>();
        foreach (Edge e in Edges) {
            Edge clone = new Edge(vertexMap[e.V1], vertexMap[e.V2]);
            clone.FoldAngle = e.FoldAngle;
            clone.FoldOffset = e.FoldOffset;
            edgeMap[e] = clone;
            clonedEdges.Add(clone);
        }

        // Clone faces (using cloned vertices and edges)
        Dictionary<Face, Face> faceMap = new Dictionary<Face, Face>();
        List<Face> clonedFaces = new List<Face>();
        foreach (Face f in Faces) {
            List<Vertex> fVerts = new List<Vertex>();
            foreach (Vertex v in f.Vertices) fVerts.Add(vertexMap[v]);
            List<Edge> fEdges = new List<Edge>();
            foreach (Edge e in f.Edges) fEdges.Add(edgeMap[e]);
            Face clone = new Face(fVerts, fEdges);
            faceMap[f] = clone;
            clonedFaces.Add(clone);
        }

        // Wire up face references on cloned edges
        foreach (Edge e in Edges) {
            Edge clone = edgeMap[e];
            clone.Face1 = e.Face1 != null && faceMap.ContainsKey(e.Face1) ? faceMap[e.Face1] : null;
            clone.Face2 = e.Face2 != null && faceMap.ContainsKey(e.Face2) ? faceMap[e.Face2] : null;
        }

        // Clone tags (remap vertex references to cloned vertices)
        Dictionary<string, List<Vertex>> clonedTags = new Dictionary<string, List<Vertex>>();
        foreach (var kvp in Tags) {
            List<Vertex> clonedList = new List<Vertex>();
            foreach (Vertex v in kvp.Value) {
                if (vertexMap.ContainsKey(v))
                    clonedList.Add(vertexMap[v]);
            }
            clonedTags[kvp.Key] = clonedList;
        }

        return new PaperGraphSnapshot(clonedVertices, clonedEdges, clonedFaces, clonedTags);
    }

    /// <summary>
    /// Restores the graph state from a snapshot.
    /// </summary>
    public void RestoreSnapshot(PaperGraphSnapshot snapshot) {
        Vertices = snapshot.Vertices;
        Edges = snapshot.Edges;
        Faces = snapshot.Faces;
        Tags = snapshot.Tags;
        _accordionData = null;
        ClearVertexRotationAnimation();
    }

    /// <summary>
    /// Generates a double-sided Mesh from the current faces.
    /// Each face is fan-triangulated from its first vertex.
    /// Normals are computed per fan triangle so creases stay hard and non-planar poses
    /// (such as mid-accordion collapse) stay visible. Back faces are appended with reversed
    /// winding and negated normals.
    /// </summary>
    public Mesh GenerateMesh() {
        List<Vector3> positions = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<int> frontTriangles = new List<int>();

        foreach (Face face in Faces) {
            if (face.Vertices.Count < 3) continue;

            Vertex anchor = face.Vertices[0];
            for (int i = 1; i < face.Vertices.Count - 1; i++) {
                Vertex v1 = face.Vertices[i];
                Vertex v2 = face.Vertices[i + 1];

                // Front-submesh winding is (v0, v2, v1).
                Vector3 edgeA = v2.Position - anchor.Position;
                Vector3 edgeB = v1.Position - anchor.Position;
                Vector3 triNormal = Vector3.Cross(edgeA, edgeB);
                if (triNormal.sqrMagnitude < 0.000001f) continue;

                triNormal.Normalize();

                int index = positions.Count;
                positions.Add(anchor.Position);
                uvs.Add(anchor.Uv);
                normals.Add(triNormal);

                positions.Add(v2.Position);
                uvs.Add(v2.Uv);
                normals.Add(triNormal);

                positions.Add(v1.Position);
                uvs.Add(v1.Uv);
                normals.Add(triNormal);

                frontTriangles.Add(index);
                frontTriangles.Add(index + 1);
                frontTriangles.Add(index + 2);
            }
        }

        int frontVertCount = positions.Count;
        for (int i = 0; i < frontVertCount; i++) {
            positions.Add(positions[i]);
            uvs.Add(new Vector2(1f - uvs[i].x, uvs[i].y));
            normals.Add(-normals[i]);
        }

        List<int> backTriangles = new List<int>();
        for (int i = 0; i < frontTriangles.Count; i += 3) {
            backTriangles.Add(frontTriangles[i] + frontVertCount);
            backTriangles.Add(frontTriangles[i + 2] + frontVertCount);
            backTriangles.Add(frontTriangles[i + 1] + frontVertCount);
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(positions);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);

        mesh.subMeshCount = 2;
        mesh.SetTriangles(frontTriangles, 0);
        mesh.SetTriangles(backTriangles, 1);

        mesh.RecalculateBounds();
        return mesh;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Saves the current mesh as an asset file in the project natively using a timestamp.
    /// </summary>
    public void SaveCurrentMeshAsAsset()
    {
        Mesh mesh = GenerateMesh();
        
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"Assets/Folding/SavedAssets/PaperMesh_{timestamp}.asset";

        // Ensure the directory exists
        if (!System.IO.Directory.Exists("Assets/Folding/SavedAssets"))
        {
            System.IO.Directory.CreateDirectory("Assets/Folding/SavedAssets");
        }

        UnityEditor.AssetDatabase.CreateAsset(mesh, filename);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"PaperGraph: Saved current mesh to {filename}");
    }
#endif
}

public class Vertex {
    public Vector3 Position;
    public Vector2 Uv;
    public List<Edge> Edges = new List<Edge>();
    
    public Vertex(Vector3 pos) {
        Position = pos;
    }
}

public class Edge {
    public Vertex V1, V2;
    public float FoldAngle = 180f;
    /// <summary>Hinge thickness offset stored on crease edges during flat folds. Zero for non-hinge edges.</summary>
    public float FoldOffset = 0f;
    public Face Face1;  // One adjacent face
    public Face Face2;  // Other adjacent face (null for boundary)

    public Edge(Vertex vertex1, Vertex vertex2) {
        V1 = vertex1;
        V2 = vertex2;
        V1.Edges.Add(this);
        V2.Edges.Add(this);
    }
}

public class Face {
    public List<Vertex> Vertices;  // Ordered around the face
    public List<Edge> Edges;       // Edges[i] connects Vertices[i] to Vertices[(i+1) % n]

    public Face(List<Vertex> verts, List<Edge> edgeList) {
        Vertices = new List<Vertex>(verts);
        Edges = new List<Edge>(edgeList);
    }

    /// <summary>
    /// Replaces oldEdge with edgeA (v1->vNew) and edgeB (vNew->v2),
    /// inserting vNew into the vertex list at the correct position.
    /// </summary>
    public void ReplaceSplitEdge(Edge oldEdge, Edge edgeA, Edge edgeB, Vertex vNew) {
        int edgeIndex = Edges.IndexOf(oldEdge);
        if (edgeIndex < 0) return;

        // Determine which direction the face traverses this edge
        bool forwardOrder = (Vertices[edgeIndex] == edgeA.V1);

        // Insert vNew between the two endpoint vertices
        int insertAt = (edgeIndex + 1) % Vertices.Count;
        if (insertAt == 0) insertAt = Vertices.Count;
        Vertices.Insert(insertAt, vNew);

        // Replace the old edge with the two new ones in the correct order
        Edges.RemoveAt(edgeIndex);
        if (forwardOrder) {
            Edges.Insert(edgeIndex, edgeB);
            Edges.Insert(edgeIndex, edgeA);
        } else {
            Edges.Insert(edgeIndex, edgeA);
            Edges.Insert(edgeIndex, edgeB);
        }
    }

    public bool ContainsVertex(Vertex v) {
        return Vertices.Contains(v);
    }

    public bool ContainsEdge(Edge e) {
        return Edges.Contains(e);
    }
}

/// <summary>
/// Stores a deep-copy snapshot of a PaperGraph's state for undo/redo.
/// </summary>
public class PaperGraphSnapshot {
    public List<Vertex> Vertices;
    public List<Edge> Edges;
    public List<Face> Faces;
    public Dictionary<string, List<Vertex>> Tags;

    public PaperGraphSnapshot(List<Vertex> vertices, List<Edge> edges, List<Face> faces, Dictionary<string, List<Vertex>> tags) {
        Vertices = vertices;
        Edges = edges;
        Faces = faces;
        Tags = tags;
    }
}
}
