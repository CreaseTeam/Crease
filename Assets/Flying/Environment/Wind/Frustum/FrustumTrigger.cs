using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace PhysicsHelpers
{
    /// <summary>
    /// A physics helper that generates a frustum (truncated cone) shaped trigger collider.
    /// Uses a procedurally generated mesh and a convex MeshCollider.
    /// </summary>
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(Rigidbody))] // Rigidbody is usually needed for triggers to catch static colliders, or vice-versa, depending on setup.
    public class FrustumTrigger : MonoBehaviour
    {
        [Header("Frustum Settings")]
        [Tooltip("Radius of the top circle of the frustum (larger end).")]
        [Min(0)] public float topRadius = 1.0f;

        [Tooltip("Radius of the bottom circle of the frustum (smaller end).")]
        [Min(0)] public float bottomRadius = 2.0f;

        [Tooltip("Total height of the frustum.")]
        [Min(0)] public float height = 3.0f;

        [Tooltip("Number of segments for the circle approximation.")]
        [Range(3, 64)] public int segments = 18;

        [Header("Physics Settings")]
        [Tooltip("If true, the Rigidbody will be set to IsKinematic automatically.")]
        public bool autoConfigureRigidbody = true;

        [Header("Events")]
        public UnityEvent<Collider> onTriggerEnter;
        public UnityEvent<Collider> onTriggerExit;

        private MeshCollider _meshCollider;
        private Rigidbody _rigidbody;
        private Mesh _generatedMesh;

        private void Awake()
        {
            Initialize();
        }

        private void OnValidate()
        {
            // Allows real-time visualization in the Editor
            // Only rebuild if the component is already initialized/has references
            if (_meshCollider != null)
            {
                RebuildMesh();
            }
        }

        private void Initialize()
        {
            _meshCollider = GetComponent<MeshCollider>();
            _rigidbody = GetComponent<Rigidbody>();

            if (autoConfigureRigidbody && _rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
            }

            RebuildMesh();
        }

        /// <summary>
        /// Regenerates the mesh and assigns it to the collider.
        /// </summary>
        public void RebuildMesh()
        {
            if (_meshCollider == null) return;

            if (_generatedMesh == null)
            {
                _generatedMesh = new Mesh();
                _generatedMesh.name = "FrustumTriggerMesh";
            }

            GenerateFrustumMesh(_generatedMesh, topRadius, bottomRadius, height, segments);

            _meshCollider.sharedMesh = null; // Clear first to force update if needed
            _meshCollider.sharedMesh = _generatedMesh;
            _meshCollider.convex = true;
            _meshCollider.isTrigger = true;
        }

        private void GenerateFrustumMesh(Mesh mesh, float rTop, float rBottom, float h, int seg)
        {
            mesh.Clear();
        
            int vertexCount = (seg + 1) * 2 + 2; // +1 for UV wrapping, +2 for caps centers
            Vector3[] vertices = new Vector3[vertexCount];
            // We don't strictly need UVs/Normals for a trigger collider, but good practice.
            
            int vIndex = 0;
        
            // 0: Bottom Center (at transform origin, smaller radius)
            vertices[vIndex++] = new Vector3(0, 0, 0);
            // 1: Top Center (at full height, larger radius)
            vertices[vIndex++] = new Vector3(0, h, 0);
        
            // Ring vertices
            // We generate (seg + 1) vertices for the rings to handle closed loop if we were doing texturing,
            // but for a collider, distinct vertices are fine.
            // Vertices 2 to 2+seg: Bottom Ring (at origin, smaller radius)
            // Vertices 2+seg+1 to ...: Top Ring (at height, larger radius)
        
            int bottomRingStart = vIndex;
            for (int i = 0; i <= seg; i++)
            {
                float angle = (float)i / seg * Mathf.PI * 2;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
        
                vertices[vIndex++] = new Vector3(cos * rBottom, 0, sin * rBottom);
            }
        
            int topRingStart = vIndex;
            for (int i = 0; i <= seg; i++)
            {
                float angle = (float)i / seg * Mathf.PI * 2;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
        
                vertices[vIndex++] = new Vector3(cos * rTop, h, sin * rTop);
            }
        
            mesh.vertices = vertices;
        
            // Triangles
            List<int> tris = new List<int>();
        
            // Bottom Cap (Triangle Fan) - Center is index 0
            // Ring indices: bottomRingStart to bottomRingStart + seg
            for (int i = 0; i < seg; i++)
            {
                tris.Add(0);
                tris.Add(bottomRingStart + i + 1);
                tris.Add(bottomRingStart + i);
            }
        
            // Top Cap - Center is index 1
            // Winding order reversed for top
            for (int i = 0; i < seg; i++)
            {
                tris.Add(1);
                tris.Add(topRingStart + i);
                tris.Add(topRingStart + i + 1);
            }
        
            // Sides (Quads -> 2 Tris)
            for (int i = 0; i < seg; i++)
            {
                int currentBottom = bottomRingStart + i;
                int nextBottom = bottomRingStart + i + 1;
                int currentTop = topRingStart + i;
                int nextTop = topRingStart + i + 1;
        
                // Triangle 1
                tris.Add(currentBottom);
                tris.Add(nextTop);
                tris.Add(nextBottom);
        
                // Triangle 2
                tris.Add(currentBottom);
                tris.Add(currentTop);
                tris.Add(nextTop);
            }
        
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals(); // Fix: Gizmos.DrawMesh requires normals
            mesh.RecalculateBounds();
        }

        // Native Unity Trigger Callbacks
        private void OnTriggerEnter(Collider other)
        {
            onTriggerEnter?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            onTriggerExit?.Invoke(other);
        }

        // Editor Visualization
        private void OnDrawGizmos()
        {
            // Use Unity standard collider green color
            Gizmos.color = new Color(0.56f, 0.96f, 0.54f, 1.0f);
            Gizmos.matrix = transform.localToWorldMatrix;

            // Draw bottom and top rings
            DrawCircle(new Vector3(0, 0, 0), bottomRadius);
            DrawCircle(new Vector3(0, height, 0), topRadius);

            // Draw 8 side lines to make the shape clearer
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                Gizmos.DrawLine(
                    new Vector3(cos * bottomRadius, 0, sin * bottomRadius),
                    new Vector3(cos * topRadius, height, sin * topRadius)
                );
            }

            // Draw direction arrow from bottom to top center
            DrawDirectionArrow();
        }

        private void DrawDirectionArrow()
        {
            Gizmos.color = Color.white;
            Vector3 start = new Vector3(0, 0, 0);
            Vector3 end = new Vector3(0, height, 0);
            
            // Draw the main shaft
            Gizmos.DrawLine(start, end);

            // Draw arrow head (simple lines)
            float arrowHeadSize = height * 0.15f;
            
            Gizmos.DrawLine(end, end + new Vector3(arrowHeadSize, -arrowHeadSize, 0));
            Gizmos.DrawLine(end, end + new Vector3(-arrowHeadSize, -arrowHeadSize, 0));
            Gizmos.DrawLine(end, end + new Vector3(0, -arrowHeadSize, arrowHeadSize));
            Gizmos.DrawLine(end, end + new Vector3(0, -arrowHeadSize, -arrowHeadSize));
        }

        private void OnDrawGizmosSelected()
        {
            // Add a semi-transparent fill effect when selected
            if (_generatedMesh != null)
            {
                Gizmos.color = new Color(0.56f, 0.96f, 0.54f, 0.2f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawMesh(_generatedMesh);
            }
            else if (!Application.isPlaying)
            {
                // Try to generate mesh once in editor mode if not already generated
                Initialize();
            }
        }

        private void DrawCircle(Vector3 center, float radius)
        {
            Vector3 prev = center + new Vector3(radius, 0, 0);
            int div = 24;
            for(int i = 1; i <= div; i++)
            {
                float angle = (float)i / div * Mathf.PI * 2;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
