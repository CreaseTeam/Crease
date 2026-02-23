using System.Collections.Generic;
using UnityEngine;

public class PaperGraphVisualizer : MonoBehaviour {
    public PaperGraph graph;
    
    [Header("Visualization Settings")]
    public Color vertexColor = Color.green;
    public Color edgeColor = Color.white;
    public Color foldedEdgeColor = Color.red;
    public float vertexSize = 0.1f;
    public float edgeThickness = 2f;
    
    [Header("Debug Info")]
    public bool showVertexLabels = true;
    public bool showFoldAngles = true;
    public bool showMesh = true;
    public Material meshMaterial;

    [Header("Tag Highlight")]
    [HideInInspector] public int selectedTagIndex = 0;
    public Color tagHighlightColor = Color.yellow;
    public float tagHighlightSize = 0.15f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    void OnDrawGizmos() {
        if (graph == null) return;

        UpdateMesh();
        
        // Draw edges
        foreach (Edge edge in graph.edges) {
            // Choose color based on whether edge is folded
            bool isFolded = Mathf.Abs(edge.foldAngle - 180f) > 0.01f;
            Gizmos.color = isFolded ? foldedEdgeColor : edgeColor;
            
            Gizmos.DrawLine(edge.v1.position, edge.v2.position);
            
            // Optionally draw fold angle at midpoint
            if (showFoldAngles && isFolded) {
                Vector3 midpoint = (edge.v1.position + edge.v2.position) / 2f;
                DrawLabel(midpoint, $"{edge.foldAngle:F1}°");
            }
        }
        
        // Draw vertices
        Gizmos.color = vertexColor;
        for (int i = 0; i < graph.vertices.Count; i++) {
            Vertex v = graph.vertices[i];
            Gizmos.DrawSphere(v.position, vertexSize);
            
            // Optionally draw vertex index
            if (showVertexLabels) {
                DrawLabel(v.position + Vector3.up * 0.2f, i.ToString());
            }
        }

        // Draw tag-highlighted vertices
        if (graph.tags != null && graph.tags.Count > 0) {
            List<string> tagKeys = new List<string>(graph.tags.Keys);
            if (selectedTagIndex > 0 && selectedTagIndex <= tagKeys.Count) {
                string selectedTag = tagKeys[selectedTagIndex - 1];
                List<Vertex> taggedVerts = graph.GetVerticesForTag(selectedTag);
                Gizmos.color = tagHighlightColor;
                foreach (Vertex tv in taggedVerts) {
                    Gizmos.DrawSphere(tv.position, tagHighlightSize);
                }
            }
        }
    }
    
    // Helper to draw text labels in scene view
    void DrawLabel(Vector3 position, string text) {
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(position, text);
        #endif
    }

    void UpdateMesh() {
        if (meshFilter == null) {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = gameObject.AddComponent<MeshFilter>();
        }
        if (meshRenderer == null) {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        if (showMesh && graph.faces.Count > 0) {
            meshFilter.sharedMesh = graph.GenerateMesh();
            meshRenderer.enabled = true;
            if (meshMaterial != null)
                meshRenderer.sharedMaterial = meshMaterial;
        } else {
            meshFilter.sharedMesh = null;
            meshRenderer.enabled = false;
        }
    }
}