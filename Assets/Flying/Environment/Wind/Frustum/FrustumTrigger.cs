using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using System.Collections.Generic;

namespace Crease.Flying.Environment.Wind.Frustum
{
    /// <summary>
    /// A physics helper that generates a frustum (truncated cone) shaped trigger collider.
    /// Uses a procedurally generated mesh and a convex MeshCollider.
    /// </summary>
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class FrustumTrigger : MonoBehaviour
    {
        [Header("Frustum Settings")]
        [Tooltip("Radius of the top circle of the frustum (larger end).")]
        [Min(0)]
        [FormerlySerializedAs("topRadius")]
        public float TopRadius = 1.0f;

        [Tooltip("Radius of the bottom circle of the frustum (smaller end).")]
        [Min(0)]
        [FormerlySerializedAs("bottomRadius")]
        public float BottomRadius = 2.0f;

        [Tooltip("Total height of the frustum.")]
        [Min(0)]
        [FormerlySerializedAs("height")]
        public float Height = 3.0f;

        [Tooltip("Number of segments for the circle approximation.")]
        [Range(3, 64)]
        [FormerlySerializedAs("segments")]
        public int Segments = 18;

        [Header("Physics Settings")]
        [Tooltip("If true, the Rigidbody will be set to IsKinematic automatically.")]
        [FormerlySerializedAs("autoConfigureRigidbody")]
        public bool AutoConfigureRigidbody = true;

        [Header("Events")]
        [FormerlySerializedAs("onTriggerEnter")]
        public UnityEvent<Collider> OnTriggerEntered;
        [FormerlySerializedAs("onTriggerExit")]
        public UnityEvent<Collider> OnTriggerExited;

        private MeshCollider _meshCollider;
        private Rigidbody _rigidbody;
        private Mesh _generatedMesh;

        private void Awake()
        {
            Initialize();
        }

        private void OnValidate()
        {
            if (_meshCollider != null)
            {
                RebuildMesh();
            }
        }

        private void Initialize()
        {
            _meshCollider = GetComponent<MeshCollider>();
            _rigidbody = GetComponent<Rigidbody>();

            if (AutoConfigureRigidbody && _rigidbody != null)
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

            GenerateFrustumMesh(_generatedMesh, TopRadius, BottomRadius, Height, Segments);

            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = _generatedMesh;
            _meshCollider.convex = true;
            _meshCollider.isTrigger = true;
        }

        private void GenerateFrustumMesh(Mesh mesh, float rTop, float rBottom, float h, int seg)
        {
            mesh.Clear();

            int vertexCount = (seg + 1) * 2 + 2;
            Vector3[] vertices = new Vector3[vertexCount];

            int vIndex = 0;

            vertices[vIndex++] = new Vector3(0, 0, 0);
            vertices[vIndex++] = new Vector3(0, h, 0);

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

            List<int> tris = new List<int>();

            for (int i = 0; i < seg; i++)
            {
                tris.Add(0);
                tris.Add(bottomRingStart + i + 1);
                tris.Add(bottomRingStart + i);
            }

            for (int i = 0; i < seg; i++)
            {
                tris.Add(1);
                tris.Add(topRingStart + i);
                tris.Add(topRingStart + i + 1);
            }

            for (int i = 0; i < seg; i++)
            {
                int currentBottom = bottomRingStart + i;
                int nextBottom = bottomRingStart + i + 1;
                int currentTop = topRingStart + i;
                int nextTop = topRingStart + i + 1;

                tris.Add(currentBottom);
                tris.Add(nextTop);
                tris.Add(nextBottom);

                tris.Add(currentBottom);
                tris.Add(currentTop);
                tris.Add(nextTop);
            }

            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void OnTriggerEnter(Collider other)
        {
            OnTriggerEntered?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            OnTriggerExited?.Invoke(other);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.56f, 0.96f, 0.54f, 1.0f);
            Gizmos.matrix = transform.localToWorldMatrix;

            DrawCircle(new Vector3(0, 0, 0), BottomRadius);
            DrawCircle(new Vector3(0, Height, 0), TopRadius);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                Gizmos.DrawLine(
                    new Vector3(cos * BottomRadius, 0, sin * BottomRadius),
                    new Vector3(cos * TopRadius, Height, sin * TopRadius)
                );
            }

            DrawDirectionArrow();
        }

        private void DrawDirectionArrow()
        {
            Gizmos.color = Color.white;
            Vector3 start = new Vector3(0, 0, 0);
            Vector3 end = new Vector3(0, Height, 0);

            Gizmos.DrawLine(start, end);

            float arrowHeadSize = Height * 0.15f;

            Gizmos.DrawLine(end, end + new Vector3(arrowHeadSize, -arrowHeadSize, 0));
            Gizmos.DrawLine(end, end + new Vector3(-arrowHeadSize, -arrowHeadSize, 0));
            Gizmos.DrawLine(end, end + new Vector3(0, -arrowHeadSize, arrowHeadSize));
            Gizmos.DrawLine(end, end + new Vector3(0, -arrowHeadSize, -arrowHeadSize));
        }

        private void OnDrawGizmosSelected()
        {
            if (_generatedMesh != null)
            {
                Gizmos.color = new Color(0.56f, 0.96f, 0.54f, 0.2f);
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawMesh(_generatedMesh);
            }
            else if (!Application.isPlaying)
            {
                Initialize();
            }
        }

        private void DrawCircle(Vector3 center, float radius)
        {
            Vector3 prev = center + new Vector3(radius, 0, 0);
            int div = 24;
            for (int i = 1; i <= div; i++)
            {
                float angle = (float)i / div * Mathf.PI * 2;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
