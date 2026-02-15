using System.Collections.Generic;
using UnityEngine;

public class PaperGraph : MonoBehaviour
{
    public List<Vertex> vertices = new List<Vertex>();
    public List<Edge> edges = new List<Edge>();
    public List<Face> faces = new List<Face>();

    public float width = 1f;
    public float height = 1f;

    private List<PaperGraphSnapshot> undoStack = new List<PaperGraphSnapshot>();
    private List<PaperGraphSnapshot> redoStack = new List<PaperGraphSnapshot>();

    private void Start() {
        CreateSheet(width, height);
    }
    
    public void ExecuteFold(Vector3 foldPoint1, Vector3 foldPoint2, Vector3 planeVector, float degrees) {
        // Save state before the fold for undo
        undoStack.Add(CreateSnapshot());
        redoStack.Clear();

        Vector3 foldAxis = (foldPoint2 - foldPoint1).normalized;
        Vector3 planeNormal = Vector3.Cross(foldAxis, planeVector).normalized;

        // Split edges along the fold plane
        SplitEdgesCrossingPlane(foldPoint1, planeNormal, degrees);

        // Partition vertices on the positive side of the plane and rotate them
        Quaternion rotation = Quaternion.AngleAxis(degrees, foldAxis);
        foreach (Vertex v in vertices) {
            float side = Vector3.Dot(v.position - foldPoint1, planeNormal);
            if (side > 0.0001f) {
                // Rotate around the fold axis line (not just the direction)
                v.position = foldPoint1 + rotation * (v.position - foldPoint1);
            }
        }
    }

    public void SplitEdgesCrossingPlane(Vector3 planePoint, Vector3 planeNormal, float foldAngle = 180f) {
        List<Edge> edgeSnapshot = new List<Edge>(edges);
        Dictionary<Face, List<Vertex>> faceSplitVertices = new Dictionary<Face, List<Vertex>>();

        foreach (Edge oldEdge in edgeSnapshot) {
            float d1 = Vector3.Dot(oldEdge.v1.position - planePoint, planeNormal);
            float d2 = Vector3.Dot(oldEdge.v2.position - planePoint, planeNormal);

            // Only split edges that cross the plane (endpoints on opposite sides)
            if (d1 * d2 >= 0f)
                continue;

            // Compute intersection point
            float t = d1 / (d1 - d2);
            Vector3 intersectionPoint = oldEdge.v1.position + t * (oldEdge.v2.position - oldEdge.v1.position);

            Vertex vNew = new Vertex(intersectionPoint);
            vertices.Add(vNew);

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

        // Split any face that had exactly two edges cut by the plane
        foreach (var kvp in faceSplitVertices) {
            if (kvp.Value.Count == 2)
                SplitFace(kvp.Value[0], kvp.Value[1], kvp.Key, foldAngle);
            else if (kvp.Value.Count > 2)
                Debug.LogWarning("Face had more than 2 edges cut by plane, this should not happen.");
        }
    }

    public void SplitFace(Vertex vA, Vertex vB, Face face, float foldAngle = 180f) {
        int idxA = face.vertices.IndexOf(vA);
        int idxB = face.vertices.IndexOf(vB);

        // Ensure idxA < idxB for consistent slicing
        if (idxA > idxB) {
            int tmp = idxA; idxA = idxB; idxB = tmp;
            Vertex vtmp = vA; vA = vB; vB = vtmp;
        }

        int n = face.vertices.Count;

        // Create the splitting edge between the two vertices
        Edge splitEdge = new Edge(vA, vB);
        splitEdge.foldAngle = foldAngle;
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

    public void CreateSheet(float width, float height) {
        
        // Create 4 corner vertices
        // Centered at origin, lying flat in XZ plane
        Vertex v0 = new Vertex(new Vector3(-width/2, 0, -height/2)); // bottom-left
        Vertex v1 = new Vertex(new Vector3(width/2, 0, -height/2));  // bottom-right
        Vertex v2 = new Vertex(new Vector3(width/2, 0, height/2));   // top-right
        Vertex v3 = new Vertex(new Vector3(-width/2, 0, height/2));  // top-left
        
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
            vertexMap[v] = clone;
            clonedVertices.Add(clone);
        }

        // Clone edges (using cloned vertices)
        Dictionary<Edge, Edge> edgeMap = new Dictionary<Edge, Edge>();
        List<Edge> clonedEdges = new List<Edge>();
        foreach (Edge e in edges) {
            Edge clone = new Edge(vertexMap[e.v1], vertexMap[e.v2]);
            clone.foldAngle = e.foldAngle;
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

        return new PaperGraphSnapshot(clonedVertices, clonedEdges, clonedFaces);
    }

    /// <summary>
    /// Restores the graph state from a snapshot.
    /// </summary>
    public void RestoreSnapshot(PaperGraphSnapshot snapshot) {
        vertices = snapshot.vertices;
        edges = snapshot.edges;
        faces = snapshot.faces;
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
        for (int i = 0; i < vertices.Count; i++) {
            vertexIndex[vertices[i]] = i;
            positions.Add(vertices[i].position);
        }

        // Triangulate each face using a fan from vertex 0
        List<int> frontTriangles = new List<int>();
        foreach (Face face in faces) {
            if (face.vertices.Count < 3) continue;
            int i0 = vertexIndex[face.vertices[0]];
            for (int i = 1; i < face.vertices.Count - 1; i++) {
                int i1 = vertexIndex[face.vertices[i]];
                int i2 = vertexIndex[face.vertices[i + 1]];
                frontTriangles.Add(i0);
                frontTriangles.Add(i1);
                frontTriangles.Add(i2);
            }
        }

        // Duplicate vertices for the back side (offset by vertex count)
        int vertCount = positions.Count;
        for (int i = 0; i < vertCount; i++) {
            positions.Add(positions[i]);
        }

        // Back-face triangles: same triangles with reversed winding
        List<int> allTriangles = new List<int>(frontTriangles);
        for (int i = 0; i < frontTriangles.Count; i += 3) {
            allTriangles.Add(frontTriangles[i] + vertCount);
            allTriangles.Add(frontTriangles[i + 2] + vertCount);
            allTriangles.Add(frontTriangles[i + 1] + vertCount);
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(positions);
        mesh.SetTriangles(allTriangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

public class Vertex {
    public Vector3 position;
    public List<Edge> edges = new List<Edge>();
    
    public Vertex(Vector3 pos) {
        position = pos;
    }
}

public class Edge {
    public Vertex v1, v2;
    public float foldAngle = 180f;
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

    public PaperGraphSnapshot(List<Vertex> vertices, List<Edge> edges, List<Face> faces) {
        this.vertices = vertices;
        this.edges = edges;
        this.faces = faces;
    }
}