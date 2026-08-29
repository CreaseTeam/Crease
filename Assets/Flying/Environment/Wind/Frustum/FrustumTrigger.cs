using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        private const string GeneratedMeshName = "FrustumTriggerMesh";

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
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall -= RebuildMeshDeferred;
                EditorApplication.delayCall += RebuildMeshDeferred;
                return;
            }
#endif
            RebuildMesh();
        }

#if UNITY_EDITOR
        private void RebuildMeshDeferred()
        {
            if (this == null) return;
            RebuildMesh();
        }
#endif

        private void OnDestroy()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= RebuildMeshDeferred;
#endif
            if (_generatedMesh != null && ShouldDestroyGeneratedMesh(_generatedMesh))
            {
                if (Application.isPlaying)
                    Destroy(_generatedMesh);
                else
                    DestroyImmediate(_generatedMesh);
                _generatedMesh = null;
            }
        }

        private void Initialize()
        {
            EnsureComponents();
            ConfigureRigidbody();
            RebuildMesh();
        }

        private void EnsureComponents()
        {
            if (_meshCollider == null)
                _meshCollider = GetComponent<MeshCollider>();
            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();
        }

        private void ConfigureRigidbody()
        {
            if (!AutoConfigureRigidbody || _rigidbody == null)
                return;

            if (!_rigidbody.isKinematic)
                _rigidbody.isKinematic = true;
            if (_rigidbody.useGravity)
                _rigidbody.useGravity = false;
        }

        /// <summary>
        /// Regenerates the collider mesh only when the frustum shape has actually changed.
        /// Prefab instances keep the source mesh unless their dimensions differ.
        /// </summary>
        public void RebuildMesh()
        {
            EnsureComponents();
            if (_meshCollider == null) return;

            Mesh existing = _meshCollider.sharedMesh;
            if (MeshMatchesCurrentShape(existing))
            {
                _generatedMesh = existing;
                return;
            }

#if UNITY_EDITOR
            // Don't write a mesh onto a prefab instance whose shape still matches the source.
            // Assigning a new Mesh here is what registers a prefab override on every parent.
            if (!Application.isPlaying && !gameObject.scene.IsValid())
                return;

            if (!Application.isPlaying && IsPrefabInstanceWithSourceShape())
            {
                if (existing != null)
                    RevertInstanceMeshOverride();
                return;
            }
#endif

            Mesh targetMesh = GetWritableMesh(existing, out bool createdNewMesh);
            GenerateFrustumMesh(targetMesh, TopRadius, BottomRadius, Height, Segments);
            _generatedMesh = targetMesh;

            if (Application.isPlaying && createdNewMesh)
                targetMesh.hideFlags = HideFlags.DontSave;

            AssignColliderMesh(targetMesh);
        }

        private bool MeshMatchesCurrentShape(Mesh mesh)
        {
            if (mesh == null) return false;

            int expectedVertexCount = (Segments + 1) * 2 + 2;
            if (mesh.vertexCount != expectedVertexCount)
                return false;

            Vector3[] vertices = mesh.vertices;
            int topRingStart = 2 + (Segments + 1);
            return Mathf.Approximately(vertices[1].y, Height)
                && Mathf.Approximately(vertices[2].x, BottomRadius)
                && Mathf.Approximately(vertices[topRingStart].x, TopRadius)
                && Mathf.Approximately(vertices[topRingStart].y, Height);
        }

        private Mesh GetWritableMesh(Mesh existing, out bool createdNew)
        {
            if (_generatedMesh != null && CanMutateMesh(_generatedMesh))
            {
                createdNew = false;
                return _generatedMesh;
            }

            if (existing != null && CanMutateMesh(existing))
            {
                createdNew = false;
                return existing;
            }

            createdNew = true;
            Mesh mesh = new Mesh();
            mesh.name = GeneratedMeshName;
            return mesh;
        }

        private bool CanMutateMesh(Mesh mesh)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && _meshCollider != null)
            {
                MeshCollider sourceCollider = PrefabUtility.GetCorrespondingObjectFromSource(_meshCollider);
                if (sourceCollider != null && sourceCollider.sharedMesh == mesh)
                    return false;
            }
#endif
            return mesh != null;
        }

#if UNITY_EDITOR
        private bool IsPrefabInstanceWithSourceShape()
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(this))
                return false;

            FrustumTrigger source = PrefabUtility.GetCorrespondingObjectFromSource(this);
            if (source == null)
                return false;

            return Mathf.Approximately(source.TopRadius, TopRadius)
                && Mathf.Approximately(source.BottomRadius, BottomRadius)
                && Mathf.Approximately(source.Height, Height)
                && source.Segments == Segments;
        }

        private void RevertInstanceMeshOverride()
        {
            SerializedObject serializedCollider = new SerializedObject(_meshCollider);
            SerializedProperty meshProperty = serializedCollider.FindProperty("m_Mesh");
            if (meshProperty != null && meshProperty.prefabOverride)
                PrefabUtility.RevertPropertyOverride(meshProperty, InteractionMode.AutomatedAction);
        }
#endif

        private void AssignColliderMesh(Mesh mesh)
        {
            if (_meshCollider.sharedMesh != mesh)
            {
                _meshCollider.sharedMesh = mesh;
            }
            else
            {
                _meshCollider.sharedMesh = null;
                _meshCollider.sharedMesh = mesh;
            }

            if (!_meshCollider.convex)
                _meshCollider.convex = true;
            if (!_meshCollider.isTrigger)
                _meshCollider.isTrigger = true;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(_meshCollider);
                if (PrefabUtility.IsPartOfPrefabInstance(_meshCollider))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(_meshCollider);
            }
#endif
        }

        private static bool ShouldDestroyGeneratedMesh(Mesh mesh)
        {
            return (mesh.hideFlags & HideFlags.DontSave) != 0;
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
            Mesh meshToDraw = _generatedMesh;
            if (meshToDraw == null)
            {
                EnsureComponents();
                if (_meshCollider != null)
                    meshToDraw = _meshCollider.sharedMesh;
            }

            if (meshToDraw == null)
                return;

            Gizmos.color = new Color(0.56f, 0.96f, 0.54f, 0.2f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawMesh(meshToDraw);
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
