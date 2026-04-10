using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PaperGraph : MonoBehaviour
{
    public List<Vertex> vertices = new List<Vertex>();
    public List<Edge> edges = new List<Edge>();
    public List<Face> faces = new List<Face>();
    public Dictionary<string, List<Vertex>> tags = new Dictionary<string, List<Vertex>>();

    public float width = 1f;
    public float height = 1f;

    private List<PaperGraphSnapshot> undoStack = new List<PaperGraphSnapshot>();
    private List<PaperGraphSnapshot> redoStack = new List<PaperGraphSnapshot>();

    /// <summary>
    /// Adds a vertex to the given tag list, creating the list if needed.
    /// </summary>
    public void AddVertexToTag(string tag, Vertex v) {
        if (!tags.ContainsKey(tag))
            tags[tag] = new List<Vertex>();
        if (!tags[tag].Contains(v))
            tags[tag].Add(v);
    }

    /// <summary>
    /// Returns the vertex list for a tag, or an empty list if the tag doesn't exist.
    /// </summary>
    public List<Vertex> GetVerticesForTag(string tag) {
        if (tags.ContainsKey(tag))
            return tags[tag];
        return new List<Vertex>();
    }

    private void Start() {
        vertices.Clear();
        edges.Clear();
        faces.Clear();
        tags.Clear();
        CreateSheet(width, height);
    }
    
    public void ExecuteFold(Vector3 foldPoint1, Vector3 foldPoint2, Vector3 planeVector, float degrees, string tagName = null, string filterTag = null, float foldOffset = 0f) {
        // Save state before the fold for undo
        undoStack.Add(CreateSnapshot());
        redoStack.Clear();

        // Resolve filter set from tag
        HashSet<Vertex> filterSet = null;
        if (!string.IsNullOrEmpty(filterTag) && tags.ContainsKey(filterTag)) {
            filterSet = new HashSet<Vertex>(tags[filterTag]);
        }

        Vector3 foldAxis = (foldPoint2 - foldPoint1).normalized;
        Vector3 planeNormal = Vector3.Cross(foldAxis, planeVector).normalized;

        // For flat folds the signed offset is stored on crease edges so the graph is self-describing.
        float signedOffset = foldOffset * -Mathf.Sign(degrees);

        // Split edges along the fold plane; crease edges produced by SplitFace will carry signedOffset.
        List<Vertex> splitVertices = SplitEdgesCrossingPlane(foldPoint1, planeNormal, degrees, filterSet, signedOffset);

        // Pre-compute the set of vertices on the positive (moved) side of the plane.
        HashSet<Vertex> movedSet = new HashSet<Vertex>();
        foreach (Vertex v in vertices) {
            if (filterSet != null && !filterSet.Contains(v)) continue;
            float s = Vector3.Dot(v.position - foldPoint1, planeNormal);
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
        foreach (Vertex v in vertices) {
            // Fold duplicates always travel with the moved side, regardless of position
            bool isFoldDup = foldDupSet.Contains(v);

            if (filterSet != null && !filterSet.Contains(v) && !isFoldDup)
                continue;

            if (splitSet.Contains(v))
                continue;

            if (isFoldDup) {
                v.position = foldPoint1 + rotation * (v.position - foldPoint1);
                if (Mathf.Approximately(Mathf.Abs(degrees), 180f) && Mathf.Abs(signedOffset) > 0.00001f) {
                    v.position += planeVector.normalized * signedOffset;
                }
                // Already tagged _edge above
                continue;
            }

            float side = Vector3.Dot(v.position - foldPoint1, planeNormal);
            Vector3 toV = v.position - foldPoint1;
            float distToAxis = Vector3.Cross(foldAxis, toV).magnitude;

            bool onPositiveSide = side > 0.0001f;
            bool onPlaneOffAxis = Mathf.Abs(side) <= 0.0001f && distToAxis > 0.0001f;

            if (onPlaneOffAxis) {
                // Only move an on-plane vertex if it is actually attached to the moved side.
                // Otherwise this rips apart perfectly static sheets that happen to touch the fold plane.
                bool attachedToMoved = false;
                foreach (Edge e in v.edges) {
                    Vertex other = (e.v1 == v) ? e.v2 : e.v1;
                    if (Vector3.Dot(other.position - foldPoint1, planeNormal) > 0.0001f) {
                        attachedToMoved = true;
                        break;
                    }
                }
                if (!attachedToMoved) {
                    onPlaneOffAxis = false;
                }
            }

            if (onPositiveSide || onPlaneOffAxis) {
                v.position = foldPoint1 + rotation * (v.position - foldPoint1);

                if (Mathf.Approximately(Mathf.Abs(degrees), 180f) && Mathf.Abs(signedOffset) > 0.00001f) {
                    v.position += planeVector.normalized * signedOffset;
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
    }

    public List<Vertex> SplitEdgesCrossingPlane(Vector3 planePoint, Vector3 planeNormal, float foldAngle = 180f, HashSet<Vertex> filterSet = null, float foldOffset = 0f) {
        List<Edge> edgeSnapshot = new List<Edge>(edges);
        Dictionary<Face, List<Vertex>> faceSplitVertices = new Dictionary<Face, List<Vertex>>();
        List<Vertex> newSplitVertices = new List<Vertex>();

        foreach (Edge oldEdge in edgeSnapshot) {
            // If filtering, only split edges where at least one endpoint is in the filter set
            if (filterSet != null && !filterSet.Contains(oldEdge.v1) && !filterSet.Contains(oldEdge.v2))
                continue;

            float d1 = Vector3.Dot(oldEdge.v1.position - planePoint, planeNormal);
            float d2 = Vector3.Dot(oldEdge.v2.position - planePoint, planeNormal);

            // Only split edges that cross the plane (endpoints on opposite sides)
            if (d1 * d2 >= 0f)
                continue;

            // Compute intersection point
            float t = d1 / (d1 - d2);
            Vector3 intersectionPoint = oldEdge.v1.position + t * (oldEdge.v2.position - oldEdge.v1.position);
            Vector2 intersectionUV = oldEdge.v1.uv + t * (oldEdge.v2.uv - oldEdge.v1.uv);

            Vertex vNew = new Vertex(intersectionPoint);
            vNew.uv = intersectionUV;
            vertices.Add(vNew);
            newSplitVertices.Add(vNew);

            // Propagate tags: add vNew to every tag that either endpoint belongs to
            foreach (var kvp in tags) {
                bool v1InTag = kvp.Value.Contains(oldEdge.v1);
                bool v2InTag = kvp.Value.Contains(oldEdge.v2);
                if (v1InTag || v2InTag) {
                    if (!kvp.Value.Contains(vNew))
                        kvp.Value.Add(vNew);
                }
            }

            // Create two new edges to replace the old one
            Edge edgeA = new Edge(oldEdge.v1, vNew);
            Edge edgeB = new Edge(vNew, oldEdge.v2);
            edgeA.foldAngle = oldEdge.foldAngle;
            edgeB.foldAngle = oldEdge.foldAngle;
            edgeA.face1 = oldEdge.face1;
            edgeA.face2 = oldEdge.face2;
            edgeB.face1 = oldEdge.face1;
            edgeB.face2 = oldEdge.face2;

            // Remove the old edge entirely
            oldEdge.v1.edges.Remove(oldEdge);
            oldEdge.v2.edges.Remove(oldEdge);
            edges.Remove(oldEdge);

            // Add the two new edges
            edges.Add(edgeA);
            edges.Add(edgeB);

            // Update faces to swap old edge for the two new ones
            if (oldEdge.face1 != null) {
                oldEdge.face1.ReplaceSplitEdge(oldEdge, edgeA, edgeB, vNew);
                if (!faceSplitVertices.ContainsKey(oldEdge.face1))
                    faceSplitVertices[oldEdge.face1] = new List<Vertex>();
                faceSplitVertices[oldEdge.face1].Add(vNew);
            }
            if (oldEdge.face2 != null) {
                oldEdge.face2.ReplaceSplitEdge(oldEdge, edgeA, edgeB, vNew);
                if (!faceSplitVertices.ContainsKey(oldEdge.face2))
                    faceSplitVertices[oldEdge.face2] = new List<Vertex>();
                faceSplitVertices[oldEdge.face2].Add(vNew);
            }
        }

        // Ensure any face that straddles the plane also registers its existing vertices that lie exactly on the fold plane
        foreach (Face face in faces) {
            int posCount = 0;
            int negCount = 0;
            foreach (Vertex v in face.vertices) {
                float d = Vector3.Dot(v.position - planePoint, planeNormal);
                if (d > 0.0001f) posCount++;
                else if (d < -0.0001f) negCount++;
            }

            if (posCount > 0 && negCount > 0) {
                foreach (Vertex v in face.vertices) {
                    float d = Vector3.Dot(v.position - planePoint, planeNormal);
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
                Debug.LogWarning("Face had more than 2 edges cut by plane, this should not happen.");
        }

        return newSplitVertices;
    }

    public void SplitFace(Vertex vA, Vertex vB, Face face, float foldAngle = 180f, float foldOffset = 0f) {
        int idxA = face.vertices.IndexOf(vA);
        int idxB = face.vertices.IndexOf(vB);

        // Ensure idxA < idxB for consistent slicing
        if (idxA > idxB) {
            int tmp = idxA; idxA = idxB; idxB = tmp;
            Vertex vtmp = vA; vA = vB; vB = vtmp;
        }

        int n = face.vertices.Count;

        // Create the splitting edge between the two vertices
        // Both foldAngle and foldOffset are stored on the crease edge so the
        // graph is self-describing and CreateHinge() can read them directly.
        Edge splitEdge = new Edge(vA, vB);
        splitEdge.foldAngle = foldAngle;
        splitEdge.foldOffset = foldOffset;
        edges.Add(splitEdge);

        // --- Build face1: vertices[idxA..idxB], closed by splitEdge ---
        List<Vertex> verts1 = new List<Vertex>();
        List<Edge> edges1 = new List<Edge>();
        for (int i = idxA; i <= idxB; i++) {
            verts1.Add(face.vertices[i]);
        }
        for (int i = idxA; i < idxB; i++) {
            edges1.Add(face.edges[i]);
        }
        edges1.Add(splitEdge); // closing edge: vB -> vA

        // --- Build face2: vertices[idxB..idxA] wrapping around, closed by splitEdge ---
        List<Vertex> verts2 = new List<Vertex>();
        List<Edge> edges2 = new List<Edge>();
        for (int i = idxB; i != idxA; i = (i + 1) % n) {
            verts2.Add(face.vertices[i]);
            edges2.Add(face.edges[i]);
        }
        verts2.Add(face.vertices[idxA]);
        edges2.Add(splitEdge); // closing edge: vA -> vB

        // Create the new second face
        Face face2 = new Face(verts2, edges2);
        faces.Add(face2);

        // Update the existing face in-place to become face1
        face.vertices = verts1;
        face.edges = edges1;

        // Assign both faces to the split edge
        splitEdge.face1 = face;
        splitEdge.face2 = face2;

        // Update all edges that moved to face2
        foreach (Edge e in face2.edges) {
            if (e == splitEdge) continue;
            if (e.face1 == face) e.face1 = face2;
            else if (e.face2 == face) e.face2 = face2;
        }
    }

    // ─── Hinge helpers ───────────────────────────────────────────────

    /// <summary>
    /// Rewires an edge so that oldV is replaced by newV.
    /// Updates the edge's v1/v2 and the vertex edge-lists.
    /// </summary>
    private void RewireEdgeVertex(Edge e, Vertex oldV, Vertex newV) {
        oldV.edges.Remove(e);
        newV.edges.Add(e);
        if (e.v1 == oldV) e.v1 = newV;
        else e.v2 = newV;
    }

    /// <summary>
    /// Replaces every occurrence of oldV with newV in a face's vertex list.
    /// </summary>
    private void ReplaceFaceVertex(Face face, Vertex oldV, Vertex newV) {
        for (int i = 0; i < face.vertices.Count; i++) {
            if (face.vertices[i] == oldV)
                face.vertices[i] = newV;
        }
    }

    /// <summary>
    /// Replaces oldE with newE in a face's edge list and updates the edge's face references.
    /// </summary>
    private void ReplaceFaceEdge(Face face, Edge oldE, Edge newE) {
        int idx = face.edges.IndexOf(oldE);
        if (idx >= 0) face.edges[idx] = newE;

        // Update face references on the old and new edges
        if (oldE.face1 == face) oldE.face1 = null;
        else if (oldE.face2 == face) oldE.face2 = null;

        if (newE.face1 == null) newE.face1 = face;
        else if (newE.face2 == null) newE.face2 = face;
    }

    /// <summary>
    /// Returns true if any vertex of the face is in the movedSet.
    /// </summary>
    private bool FaceHasMovedVertex(Face face, HashSet<Vertex> movedSet) {
        foreach (Vertex v in face.vertices) {
            if (movedSet.Contains(v)) return true;
        }
        return false;
    }

    /// <summary>
    /// Assigns a face to the first available slot (face1 or face2) on an edge.
    /// </summary>
    private void AssignFaceToEdge(Edge e, Face f) {
        if (e.face1 == null) e.face1 = f;
        else if (e.face2 == null) e.face2 = f;
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
            Vertex dup = new Vertex(v.position);
            dup.uv = v.uv;
            vertices.Add(dup);
            dupMap[v] = dup;

            // Propagate all tags from original to duplicate
            foreach (var kvp in tags) {
                if (kvp.Value.Contains(v) && !kvp.Value.Contains(dup))
                    kvp.Value.Add(dup);
            }
        }

        // 2. Rewire non-crease edges from split vertex to duplicate on the moved side
        foreach (Vertex splitV in splitVertices) {
            Vertex dupV = dupMap[splitV];
            List<Edge> snapshot = new List<Edge>(splitV.edges);
            foreach (Edge e in snapshot) {
                Vertex other = (e.v1 == splitV) ? e.v2 : e.v1;
                if (splitSet.Contains(other)) continue; // crease edge — handled in step 3
                if (!movedSet.Contains(other)) continue; // static side — keep as-is

                if (e.face1 != null && e.face1.ContainsVertex(splitV))
                    ReplaceFaceVertex(e.face1, splitV, dupV);
                if (e.face2 != null && e.face2.ContainsVertex(splitV))
                    ReplaceFaceVertex(e.face2, splitV, dupV);

                RewireEdgeVertex(e, splitV, dupV);
            }
        }

        // 3. Handle crease edges (edges between two split vertices)
        HashSet<Edge> processedCrease = new HashSet<Edge>();
        foreach (Vertex splitV in splitVertices) {
            List<Edge> snapshot = new List<Edge>(splitV.edges);
            foreach (Edge e in snapshot) {
                if (processedCrease.Contains(e)) continue;
                Vertex other = (e.v1 == splitV) ? e.v2 : e.v1;
                if (!dupMap.ContainsKey(other)) continue;
                processedCrease.Add(e);

                Vertex dupV = dupMap[splitV];
                Vertex dupOther = dupMap[other];

                // Create duplicate crease edge for the moved side
                Edge dupEdge = new Edge(dupV, dupOther);
                dupEdge.foldAngle = e.foldAngle;
                edges.Add(dupEdge);

                // Find the moved-side faces and rewire them
                if (e.face1 != null && FaceHasMovedVertex(e.face1, movedSet)) {
                    ReplaceFaceVertex(e.face1, splitV, dupV);
                    ReplaceFaceVertex(e.face1, other, dupOther);
                    ReplaceFaceEdge(e.face1, e, dupEdge);
                }
                
                if (e.face2 != null && FaceHasMovedVertex(e.face2, movedSet)) {
                    ReplaceFaceVertex(e.face2, splitV, dupV);
                    ReplaceFaceVertex(e.face2, other, dupOther);
                    ReplaceFaceEdge(e.face2, e, dupEdge);
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
            foreach (Edge e in v.edges) {
                Vertex other = (e.v1 == v) ? e.v2 : e.v1;
                if (splitSet.Contains(other) && Mathf.Abs(e.foldOffset) > 0.00001f) {
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
                edges.Add(ce);
                connectEdges[splitV] = ce;
            }

            List<Edge> snapshot = new List<Edge>(splitV.edges);
            foreach (Edge e in snapshot) {
                if (processedCrease.Contains(e)) continue;
                Vertex other = (e.v1 == splitV) ? e.v2 : e.v1;
                if (!splitSet.Contains(other)) continue;
                processedCrease.Add(e);

                if (!foldDupMap.ContainsKey(other)) continue;
                Vertex dupOther = foldDupMap[other];

                // Ensure connecting edge for the other vertex
                if (!connectEdges.ContainsKey(other)) {
                    Edge ce = new Edge(other, dupOther);
                    edges.Add(ce);
                    connectEdges[other] = ce;
                }

                // Find the duplicate crease edge (between fold duplicates)
                Edge dupCreaseEdge = null;
                foreach (Edge de in dupV.edges) {
                    if ((de.v1 == dupV && de.v2 == dupOther) || (de.v1 == dupOther && de.v2 == dupV)) {
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
                faces.Add(hingeFace);

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
        v0.uv = new Vector2(0f, 0f);
        Vertex v1 = new Vertex(new Vector3(width/2, 0, -height/2));  // bottom-right
        v1.uv = new Vector2(1f, 0f);
        Vertex v2 = new Vertex(new Vector3(width/2, 0, height/2));   // top-right
        v2.uv = new Vector2(1f, 1f);
        Vertex v3 = new Vertex(new Vector3(-width/2, 0, height/2));  // top-left
        v3.uv = new Vector2(0f, 1f);
        
        vertices.AddRange(new[] { v0, v1, v2, v3 });
        
        // Create 4 boundary edges (connecting corners in order)
        Edge e0 = new Edge(v0, v1); // bottom
        Edge e1 = new Edge(v1, v2); // right
        Edge e2 = new Edge(v2, v3); // top
        Edge e3 = new Edge(v3, v0); // left
        
        edges.AddRange(new[] { e0, e1, e2, e3 });

        // Create the single face for the sheet
        Face face = new Face(
            new List<Vertex> { v0, v1, v2, v3 },
            new List<Edge> { e0, e1, e2, e3 }
        );
        faces.Add(face);

        // Assign face to all boundary edges (single face, so face1 only)
        e0.face1 = face;
        e1.face1 = face;
        e2.face1 = face;
        e3.face1 = face;
    }

    /// <summary>
    /// Undoes the last fold, restoring the previous state.
    /// </summary>
    public void Undo() {
        if (undoStack.Count == 0) {
            Debug.Log("Nothing to undo.");
            return;
        }

        // Save current state to redo stack before restoring
        redoStack.Add(CreateSnapshot());

        PaperGraphSnapshot snapshot = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);
        RestoreSnapshot(snapshot);
    }

    /// <summary>
    /// Redoes the last undone fold. Only works if no new folds have been made since the last undo.
    /// </summary>
    public void Redo() {
        if (redoStack.Count == 0) {
            Debug.Log("Nothing to redo.");
            return;
        }

        // Save current state to undo stack before restoring
        undoStack.Add(CreateSnapshot());

        PaperGraphSnapshot snapshot = redoStack[redoStack.Count - 1];
        redoStack.RemoveAt(redoStack.Count - 1);
        RestoreSnapshot(snapshot);
    }

    /// <summary>
    /// Creates a deep-copy snapshot of the current graph state.
    /// </summary>
    public PaperGraphSnapshot CreateSnapshot() {
        // Clone vertices
        Dictionary<Vertex, Vertex> vertexMap = new Dictionary<Vertex, Vertex>();
        List<Vertex> clonedVertices = new List<Vertex>();
        foreach (Vertex v in vertices) {
            Vertex clone = new Vertex(v.position);
            clone.uv = v.uv;
            vertexMap[v] = clone;
            clonedVertices.Add(clone);
        }

        // Clone edges (using cloned vertices)
        Dictionary<Edge, Edge> edgeMap = new Dictionary<Edge, Edge>();
        List<Edge> clonedEdges = new List<Edge>();
        foreach (Edge e in edges) {
            Edge clone = new Edge(vertexMap[e.v1], vertexMap[e.v2]);
            clone.foldAngle = e.foldAngle;
            clone.foldOffset = e.foldOffset;
            edgeMap[e] = clone;
            clonedEdges.Add(clone);
        }

        // Clone faces (using cloned vertices and edges)
        Dictionary<Face, Face> faceMap = new Dictionary<Face, Face>();
        List<Face> clonedFaces = new List<Face>();
        foreach (Face f in faces) {
            List<Vertex> fVerts = new List<Vertex>();
            foreach (Vertex v in f.vertices) fVerts.Add(vertexMap[v]);
            List<Edge> fEdges = new List<Edge>();
            foreach (Edge e in f.edges) fEdges.Add(edgeMap[e]);
            Face clone = new Face(fVerts, fEdges);
            faceMap[f] = clone;
            clonedFaces.Add(clone);
        }

        // Wire up face references on cloned edges
        foreach (Edge e in edges) {
            Edge clone = edgeMap[e];
            clone.face1 = e.face1 != null && faceMap.ContainsKey(e.face1) ? faceMap[e.face1] : null;
            clone.face2 = e.face2 != null && faceMap.ContainsKey(e.face2) ? faceMap[e.face2] : null;
        }

        // Clone tags (remap vertex references to cloned vertices)
        Dictionary<string, List<Vertex>> clonedTags = new Dictionary<string, List<Vertex>>();
        foreach (var kvp in tags) {
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
        vertices = snapshot.vertices;
        edges = snapshot.edges;
        faces = snapshot.faces;
        tags = snapshot.tags;
    }

    /// <summary>
    /// Generates a double-sided Mesh from the current faces.
    /// Each face is fan-triangulated from its first vertex.
    /// Back faces are appended with reversed winding and flipped normals.
    /// </summary>
    public Mesh GenerateMesh() {
        // Build a mapping from Vertex -> index
        Dictionary<Vertex, int> vertexIndex = new Dictionary<Vertex, int>();
        List<Vector3> positions = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        for (int i = 0; i < vertices.Count; i++) {
            vertexIndex[vertices[i]] = i;
            positions.Add(vertices[i].position);
            uvs.Add(vertices[i].uv);
        }

        // Triangulate each face using a fan from vertex 0
        List<int> frontTriangles = new List<int>();
        foreach (Face face in faces) {
            if (face.vertices.Count < 3) continue;
            int i0 = vertexIndex[face.vertices[0]];
            for (int i = 1; i < face.vertices.Count - 1; i++) {
                int i1 = vertexIndex[face.vertices[i]];
                int i2 = vertexIndex[face.vertices[i + 1]];
                
                // Native graph is configured Counter-Clockwise. 
                // We must swap i1 and i2 to generate Clockwise triangles for Unity's front-faces (pointing +Y)
                frontTriangles.Add(i0);
                frontTriangles.Add(i2);
                frontTriangles.Add(i1);
            }
        }

        // Duplicate vertices for the back side (offset by vertex count)
        int vertCount = positions.Count;
        for (int i = 0; i < vertCount; i++) {
            positions.Add(positions[i]);
            uvs.Add(new Vector2(1f - uvs[i].x, uvs[i].y)); // Mirror horizontal UVs for the back side
        }

        // Back-face triangles: reversed winding
        List<int> backTriangles = new List<int>();
        for (int i = 0; i < frontTriangles.Count; i += 3) {
            backTriangles.Add(frontTriangles[i] + vertCount);
            backTriangles.Add(frontTriangles[i + 2] + vertCount);
            backTriangles.Add(frontTriangles[i + 1] + vertCount);
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(positions);
        mesh.SetUVs(0, uvs);
        
        // Define submeshes (0 = front, 1 = back)
        mesh.subMeshCount = 2;
        mesh.SetTriangles(frontTriangles, 0);
        mesh.SetTriangles(backTriangles, 1);
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

public class Vertex {
    public Vector3 position;
    public Vector2 uv;
    public List<Edge> edges = new List<Edge>();
    
    public Vertex(Vector3 pos) {
        position = pos;
    }
}

public class Edge {
    public Vertex v1, v2;
    public float foldAngle = 180f;
    /// <summary>Hinge thickness offset stored on crease edges during flat folds. Zero for non-hinge edges.</summary>
    public float foldOffset = 0f;
    public Face face1;  // One adjacent face
    public Face face2;  // Other adjacent face (null for boundary)

    public Edge(Vertex vertex1, Vertex vertex2) {
        v1 = vertex1;
        v2 = vertex2;
        v1.edges.Add(this);
        v2.edges.Add(this);
    }
}

public class Face {
    public List<Vertex> vertices;  // Ordered around the face
    public List<Edge> edges;       // edges[i] connects vertices[i] to vertices[(i+1) % n]

    public Face(List<Vertex> verts, List<Edge> edgeList) {
        vertices = new List<Vertex>(verts);
        edges = new List<Edge>(edgeList);
    }

    /// <summary>
    /// Replaces oldEdge with edgeA (v1->vNew) and edgeB (vNew->v2),
    /// inserting vNew into the vertex list at the correct position.
    /// </summary>
    public void ReplaceSplitEdge(Edge oldEdge, Edge edgeA, Edge edgeB, Vertex vNew) {
        int edgeIndex = edges.IndexOf(oldEdge);
        if (edgeIndex < 0) return;

        // Determine which direction the face traverses this edge
        bool forwardOrder = (vertices[edgeIndex] == edgeA.v1);

        // Insert vNew between the two endpoint vertices
        int insertAt = (edgeIndex + 1) % vertices.Count;
        if (insertAt == 0) insertAt = vertices.Count;
        vertices.Insert(insertAt, vNew);

        // Replace the old edge with the two new ones in the correct order
        edges.RemoveAt(edgeIndex);
        if (forwardOrder) {
            edges.Insert(edgeIndex, edgeB);
            edges.Insert(edgeIndex, edgeA);
        } else {
            edges.Insert(edgeIndex, edgeA);
            edges.Insert(edgeIndex, edgeB);
        }
    }

    public bool ContainsVertex(Vertex v) {
        return vertices.Contains(v);
    }

    public bool ContainsEdge(Edge e) {
        return edges.Contains(e);
    }
}

/// <summary>
/// Stores a deep-copy snapshot of a PaperGraph's state for undo/redo.
/// </summary>
public class PaperGraphSnapshot {
    public List<Vertex> vertices;
    public List<Edge> edges;
    public List<Face> faces;
    public Dictionary<string, List<Vertex>> tags;

    public PaperGraphSnapshot(List<Vertex> vertices, List<Edge> edges, List<Face> faces, Dictionary<string, List<Vertex>> tags) {
        this.vertices = vertices;
        this.edges = edges;
        this.faces = faces;
        this.tags = tags;
    }
}