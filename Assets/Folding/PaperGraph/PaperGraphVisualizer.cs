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

        [Header("Coordinate Rulers")]
        [Tooltip("Draw local X/Z rulers along the paper edges to help place drag handles.")]
        public bool ShowCoordinateRulers = false;
        [Tooltip("Ruler along local X (horizontal on the sheet).")]
        public bool ShowHorizontalRuler = true;
        [Tooltip("Ruler along local Z (vertical on the sheet).")]
        public bool ShowVerticalRuler = true;
        [Tooltip("Distance between labeled tick marks in local units.")]
        public float RulerTickInterval = 0.1f;
        [Tooltip("How far outside the sheet edge the ruler sits.")]
        public float RulerEdgeOffset = 0.05f;
        [Tooltip("Lift above the paper surface in local Y.")]
        public float RulerHeightOffset = 0.002f;
        [Tooltip("Length of tick marks perpendicular to the ruler line.")]
        public float RulerTickSize = 0.02f;
        public Color RulerColor = new Color(0.35f, 0.75f, 1f, 0.95f);

        [FormerlySerializedAs("meshMaterial")]
        public Material MeshMaterial;
        [Tooltip("If populated, materials are applied to submeshes (e.g. Element 0 = Front, Element 1 = Back)")]
        [FormerlySerializedAs("meshMaterials")]
        public Material[] MeshMaterials;

        [Header("Debug Materials")]
        [Tooltip("Alternate material used when debug mode is enabled (single-material fallback).")]
        public Material DebugMeshMaterial;
        [Tooltip("Alternate materials for submeshes when debug mode is enabled (e.g. Element 0 = Front, Element 1 = Back).")]
        public Material[] DebugMeshMaterials;

        [Header("Tag Highlight")]
        [HideInInspector]
        [FormerlySerializedAs("selectedTagIndex")]
        public int SelectedTagIndex = 0;
        [FormerlySerializedAs("tagHighlightColor")]
        public Color TagHighlightColor = Color.yellow;
        [FormerlySerializedAs("tagHighlightSize")]
        public float TagHighlightSize = 0.15f;

        /// <summary>When true, mesh collider is not updated (avoids PhysX errors during animated preview).</summary>
        public bool SkipColliderUpdate = false;

        public bool DebugPaperTexture { get; private set; }

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

            DrawCoordinateRulers();

            Gizmos.matrix = Matrix4x4.identity;
        }

        private void DrawCoordinateRulers() {
            if (!ShowCoordinateRulers || Graph == null) return;
            if (!ShowHorizontalRuler && !ShowVerticalRuler) return;

            float halfWidth = Graph.Width * 0.5f;
            float halfHeight = Graph.Height * 0.5f;
            if (halfWidth <= 0f || halfHeight <= 0f) return;

            float interval = Mathf.Max(0.01f, RulerTickInterval);
            float y = RulerHeightOffset;

            Gizmos.color = RulerColor;

            if (ShowHorizontalRuler) {
                float rulerZ = -halfHeight - RulerEdgeOffset;
                Vector3 start = new Vector3(-halfWidth, y, rulerZ);
                Vector3 end = new Vector3(halfWidth, y, rulerZ);
                Gizmos.DrawLine(start, end);

                float firstTick = Mathf.Ceil(-halfWidth / interval) * interval;
                for (float x = firstTick; x <= halfWidth + interval * 0.001f; x += interval) {
                    if (x < -halfWidth - 0.0001f || x > halfWidth + 0.0001f) continue;

                    Vector3 tickBase = new Vector3(x, y, rulerZ);
                    Gizmos.DrawLine(tickBase, tickBase + new Vector3(0f, 0f, RulerTickSize));

                    Vector3 labelPos = tickBase + new Vector3(0f, 0f, -RulerTickSize * 1.5f);
                    DrawLabel(Graph.transform.TransformPoint(labelPos), FormatRulerCoordinate(x));
                }
            }

            if (ShowVerticalRuler) {
                float rulerX = -halfWidth - RulerEdgeOffset;
                Vector3 start = new Vector3(rulerX, y, -halfHeight);
                Vector3 end = new Vector3(rulerX, y, halfHeight);
                Gizmos.DrawLine(start, end);

                float firstTick = Mathf.Ceil(-halfHeight / interval) * interval;
                for (float z = firstTick; z <= halfHeight + interval * 0.001f; z += interval) {
                    if (z < -halfHeight - 0.0001f || z > halfHeight + 0.0001f) continue;

                    Vector3 tickBase = new Vector3(rulerX, y, z);
                    Gizmos.DrawLine(tickBase, tickBase + new Vector3(RulerTickSize, 0f, 0f));

                    Vector3 labelPos = tickBase + new Vector3(-RulerTickSize * 1.5f, 0f, 0f);
                    DrawLabel(Graph.transform.TransformPoint(labelPos), FormatRulerCoordinate(z));
                }
            }
        }

        private static string FormatRulerCoordinate(float value) {
            if (Mathf.Approximately(value, 0f)) return "0";
            if (Mathf.Abs(value - Mathf.Round(value)) < 0.001f)
                return Mathf.RoundToInt(value).ToString();
            return value.ToString("0.##");
        }

        private void DrawLabel(Vector3 position, string text) {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position, text);
#endif
        }

        public void SetDebugPaperTexture(bool enabled) {
            if (DebugPaperTexture == enabled)
                return;

            DebugPaperTexture = enabled;
            UpdateMesh();
        }

        /// <summary>
        /// Swaps the FRONT submesh material (submesh 0 — see PaperGraph.GenerateMesh)
        /// and re-applies it. If the material array isn't sized for both faces yet,
        /// it is normalized to [front, back] so the swap actually takes effect.
        /// </summary>
        public void SetFrontMaterial(Material material) {
            if (material == null) return;

            const int subMeshCount = 2; // front + back
            if (MeshMaterials == null || MeshMaterials.Length < subMeshCount) {
                Material back = (MeshMaterials != null && MeshMaterials.Length > 1) ? MeshMaterials[1]
                              : (MeshMaterials != null && MeshMaterials.Length == 1) ? MeshMaterials[0]
                              : MeshMaterial;
                MeshMaterials = new[] { material, back };
            } else {
                MeshMaterials[0] = material;
            }

            UpdateMesh();
        }

        public void UpdateMesh() {
            if (Graph == null || _meshFilter == null || _meshRenderer == null) return;

            if (ShowMesh && Graph.Faces.Count > 0) {
                Mesh generatedMesh = Graph.GenerateMesh();
                _meshFilter.sharedMesh = generatedMesh;
                _meshRenderer.enabled = true;

                Material[] meshMaterials = DebugPaperTexture ? DebugMeshMaterials : MeshMaterials;
                Material meshMaterial = DebugPaperTexture ? DebugMeshMaterial : MeshMaterial;

                if (meshMaterials != null && meshMaterials.Length >= generatedMesh.subMeshCount) {
                    _meshRenderer.sharedMaterials = meshMaterials;
                } else if (meshMaterial != null) {
                    Material[] fallbackMats = new Material[generatedMesh.subMeshCount];
                    for (int i = 0; i < generatedMesh.subMeshCount; i++) {
                        fallbackMats[i] = meshMaterial;
                    }
                    _meshRenderer.sharedMaterials = fallbackMats;
                }

                if (_meshCollider != null && !SkipColliderUpdate) {
                    CopyMeshToCollider(generatedMesh);
                    _meshCollider.convex = false;
                }

                PaperEdgeShading.Apply(_meshRenderer, Graph);
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
