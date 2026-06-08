using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Folding.PaperGraph
{
    public class PaperGraphVisualizer : MonoBehaviour
    {
        [FormerlySerializedAs("graph")]
        public PaperGraph Graph;

        [Header("Visualization Settings")]
        [FormerlySerializedAs("vertexColor")]
        public Color VertexColor = Color.green;
        [FormerlySerializedAs("edgeColor")]
        public Color EdgeColor = Color.white;
        [FormerlySerializedAs("foldedEdgeColor")]
        public Color FoldedEdgeColor = Color.red;
        [FormerlySerializedAs("vertexSize")]
        public float VertexSize = 0.1f;
        [FormerlySerializedAs("edgeThickness")]
        public float EdgeThickness = 2f;

        [Header("Debug Info")]
        [FormerlySerializedAs("showVertexLabels")]
        public bool ShowVertexLabels = true;
        [FormerlySerializedAs("showFoldAngles")]
        public bool ShowFoldAngles = true;
        [FormerlySerializedAs("showMesh")]
        public bool ShowMesh = true;
        [FormerlySerializedAs("meshMaterial")]
        public Material MeshMaterial;
        [Tooltip("If populated, materials are applied to submeshes (e.g. Element 0 = Front, Element 1 = Back)")]
        [FormerlySerializedAs("meshMaterials")]
        public Material[] MeshMaterials;

        [Header("Tag Highlight")]
        [HideInInspector]
        [FormerlySerializedAs("selectedTagIndex")]
        public int SelectedTagIndex = 0;
        [FormerlySerializedAs("tagHighlightColor")]
        public Color TagHighlightColor = Color.yellow;
        [FormerlySerializedAs("tagHighlightSize")]
        public float TagHighlightSize = 0.15f;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshCollider _meshCollider;
        private Mesh _colliderMesh;

        private void Awake() {
            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();

            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            _meshCollider = GetComponent<MeshCollider>();
            if (_meshCollider == null)
                _meshCollider = gameObject.AddComponent<MeshCollider>();

            _colliderMesh = new Mesh { name = $"{name}_ColliderMesh" };
        }

        private void Start() {
            UpdateMesh();
        }

        private void LateUpdate() {
            UpdateMesh();
        }

        private void OnDrawGizmos() {
            if (Graph == null) return;

            UpdateMesh();

            Gizmos.matrix = Graph.transform.localToWorldMatrix;

            foreach (Edge edge in Graph.Edges) {
                bool isFolded = Mathf.Abs(edge.FoldAngle - 180f) > 0.01f;
                Gizmos.color = isFolded ? FoldedEdgeColor : EdgeColor;

                Gizmos.DrawLine(edge.V1.Position, edge.V2.Position);

                if (ShowFoldAngles && isFolded) {
                    Vector3 midpoint = (edge.V1.Position + edge.V2.Position) / 2f;
                    DrawLabel(Graph.transform.TransformPoint(midpoint), $"{edge.FoldAngle:F1}°");
                }
            }

            Gizmos.color = VertexColor;
            for (int i = 0; i < Graph.Vertices.Count; i++) {
                Vertex v = Graph.Vertices[i];
                Gizmos.DrawSphere(v.Position, VertexSize);

                if (ShowVertexLabels) {
                    DrawLabel(Graph.transform.TransformPoint(v.Position + Vector3.up * 0.2f), i.ToString());
                }
            }

            if (Graph.Tags != null && Graph.Tags.Count > 0) {
                List<string> tagKeys = new List<string>(Graph.Tags.Keys);
                if (SelectedTagIndex > 0 && SelectedTagIndex <= tagKeys.Count) {
                    string selectedTag = tagKeys[SelectedTagIndex - 1];
                    List<Vertex> taggedVerts = Graph.GetVerticesForTag(selectedTag);
                    Gizmos.color = TagHighlightColor;
                    foreach (Vertex tv in taggedVerts) {
                        Gizmos.DrawSphere(tv.Position, TagHighlightSize);
                    }
                }
            }

            Gizmos.matrix = Matrix4x4.identity;
        }

        private void DrawLabel(Vector3 position, string text) {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
#endif
        }

        public void UpdateMesh() {
            if (Graph == null || _meshFilter == null || _meshRenderer == null) return;

            if (ShowMesh && Graph.Faces.Count > 0) {
                Mesh generatedMesh = Graph.GenerateMesh();
                _meshFilter.sharedMesh = generatedMesh;
                _meshRenderer.enabled = true;

                if (MeshMaterials != null && MeshMaterials.Length >= generatedMesh.subMeshCount) {
                    _meshRenderer.sharedMaterials = MeshMaterials;
                } else if (MeshMaterial != null) {
                    Material[] fallbackMats = new Material[generatedMesh.subMeshCount];
                    for (int i = 0; i < generatedMesh.subMeshCount; i++) {
                        fallbackMats[i] = MeshMaterial;
                    }
                    _meshRenderer.sharedMaterials = fallbackMats;
                }

                if (_meshCollider != null)
                {
                    CopyMeshToCollider(generatedMesh);
                    _meshCollider.convex = false;
                }
            } else {
                _meshFilter.sharedMesh = null;
                _meshRenderer.enabled = false;
                if (_meshCollider != null)
                    _meshCollider.sharedMesh = null;
            }
        }

        private void CopyMeshToCollider(Mesh source)
        {
            _colliderMesh.Clear();
            _colliderMesh.vertices = source.vertices;
            _colliderMesh.normals = source.normals;
            _colliderMesh.uv = source.uv;
            _colliderMesh.subMeshCount = source.subMeshCount;
            for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
                _colliderMesh.SetTriangles(source.GetTriangles(subMesh), subMesh, true);
            _colliderMesh.RecalculateBounds();
            _meshCollider.sharedMesh = _colliderMesh;
        }
    }
}
