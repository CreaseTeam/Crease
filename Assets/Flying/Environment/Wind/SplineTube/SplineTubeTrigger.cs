using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Crease.Flying.Environment.Wind.SplineTube
{
    /// <summary>
    /// A physics helper that generates a tube-shaped trigger collider following an arbitrary Spline.
    /// Uses a procedurally generated mesh and a non-convex (trigger-only) MeshCollider.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class SplineTubeTrigger : MonoBehaviour
    {
        /// <summary>
        /// Cached per-ring sample data along the spline, shared with other systems
        /// (e.g. SplineWindZone) so the spline only needs to be sampled once per rebuild.
        /// </summary>
        public struct TubeRing
        {
            public float T;             // 0-1 position along the spline
            public Vector3 Position;    // world-space center of this ring
            public Vector3 Tangent;     // world-space forward direction of the tube at this ring
            public Vector3 Up;          // world-space up/normal used to orient the ring
            public float Radius;        // tube radius at this ring
        }

        [Header("Tube Settings")]
        [Tooltip("Base radius of the tube.")]
        [Min(0.01f)]
        public float Radius = 1.5f;

        [Tooltip("Optional curve to taper the radius along the length of the tube (0 = start, 1 = end). Multiplies the base Radius.")]
        public AnimationCurve RadiusCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Number of rings sampled along the spline. Higher = smoother bends, more triangles.")]
        [Range(2, 128)]
        public int RingCount = 24;

        [Tooltip("Number of segments for the circle approximation at each ring.")]
        [Range(3, 64)]
        public int Segments = 12;

        [Header("Physics Settings")]
        [Tooltip("If true, the Rigidbody will be set to IsKinematic automatically.")]
        public bool AutoConfigureRigidbody = true;

        [Tooltip("Triangle count above which a warning is logged about non-convex trigger collision cost. Tune per-project based on profiling.")]
        public int TriangleWarningThreshold = 3000;

        [Header("Events")]
        public UnityEvent<Collider> OnTriggerEntered;
        public UnityEvent<Collider> OnTriggerExited;

        private SplineContainer _splineContainer;
        private MeshCollider _meshCollider;
        private Rigidbody _rigidbody;
        private Mesh _generatedMesh;

        private TubeRing[] _rings;

        /// <summary>
        /// Read-only access to the most recently generated ring data, in world space.
        /// </summary>
        public IReadOnlyList<TubeRing> Rings => _rings;

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
            _splineContainer = GetComponent<SplineContainer>();
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
        /// Resamples the spline and regenerates the mesh and assigns it to the collider.
        /// </summary>
        public void RebuildMesh()
        {
            if (_meshCollider == null || _splineContainer == null) return;
            if (_splineContainer.Spline == null || _splineContainer.Spline.Count < 2) return;

            SampleRings();

            if (_generatedMesh == null)
            {
                _generatedMesh = new Mesh();
                _generatedMesh.name = "SplineTubeTriggerMesh";
            }

            GenerateTubeMesh(_generatedMesh, _rings, Segments);

            // Non-convex is required here: a turny spline can easily exceed the
            // 255-triangle limit Unity imposes on convex MeshColliders. Trigger-only
            // colliders do not need to be convex, since we only need overlap tests,
            // not physical collision response.
            _meshCollider.sharedMesh = null;
            _meshCollider.sharedMesh = _generatedMesh;
            _meshCollider.convex = false;
            _meshCollider.isTrigger = true;

            WarnIfMeshIsExpensive();
        }

        private void WarnIfMeshIsExpensive()
        {
            if (_generatedMesh == null) return;

            int triCount = _generatedMesh.triangles.Length / 3;
            if (triCount > TriangleWarningThreshold)
            {
                Debug.LogWarning(
                    $"[SplineTubeTrigger] '{name}' generated a non-convex trigger mesh with {triCount} triangles " +
                    $"(threshold: {TriangleWarningThreshold}). Non-convex MeshCollider trigger tests are more " +
                    $"expensive than convex ones; consider lowering RingCount/Segments, or splitting this tube " +
                    $"into multiple lower-density SplineTubeTrigger objects if this causes performance issues.",
                    this);
            }
        }

        private void SampleRings()
        {
            Spline spline = _splineContainer.Spline;
            _rings = new TubeRing[RingCount];

            for (int i = 0; i < RingCount; i++)
            {
                float t = (float)i / (RingCount - 1);

                spline.Evaluate(t, out float3 localPos, out float3 localTangent, out float3 localUp);

                Vector3 worldPos = _splineContainer.transform.TransformPoint(localPos);
                Vector3 worldTangent = _splineContainer.transform.TransformDirection(((Vector3)localTangent).normalized);
                Vector3 worldUp = _splineContainer.transform.TransformDirection(((Vector3)localUp).normalized);

                float radius = Radius * Mathf.Max(0.001f, RadiusCurve.Evaluate(t));

                _rings[i] = new TubeRing
                {
                    T = t,
                    Position = worldPos,
                    Tangent = worldTangent,
                    Up = worldUp,
                    Radius = radius
                };
            }
        }

        private void GenerateTubeMesh(Mesh mesh, TubeRing[] rings, int seg)
        {
            mesh.Clear();

            int ringCount = rings.Length;
            int vertexCount = ringCount * (seg + 1);
            Vector3[] vertices = new Vector3[vertexCount];

            for (int r = 0; r < ringCount; r++)
            {
                TubeRing ring = rings[r];

                // Build an orthonormal basis around the tangent so the ring is
                // oriented consistently (avoids twisting between rings).
                Vector3 fwd = ring.Tangent.sqrMagnitude > 0.0001f ? ring.Tangent.normalized : Vector3.forward;
                Vector3 up = ring.Up.sqrMagnitude > 0.0001f ? ring.Up.normalized : Vector3.up;
                Vector3 right = Vector3.Cross(up, fwd).normalized;
                up = Vector3.Cross(fwd, right).normalized;

                // Convert ring center to local space of this GameObject for mesh storage.
                Vector3 localCenter = transform.InverseTransformPoint(ring.Position);
                Vector3 localRight = transform.InverseTransformDirection(right);
                Vector3 localUp = transform.InverseTransformDirection(up);

                for (int i = 0; i <= seg; i++)
                {
                    float angle = (float)i / seg * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    Vector3 offset = (localRight * cos + localUp * sin) * ring.Radius;
                    vertices[r * (seg + 1) + i] = localCenter + offset;
                }
            }

            mesh.vertices = vertices;

            List<int> tris = new List<int>();

            for (int r = 0; r < ringCount - 1; r++)
            {
                int ringStart = r * (seg + 1);
                int nextRingStart = (r + 1) * (seg + 1);

                for (int i = 0; i < seg; i++)
                {
                    int a = ringStart + i;
                    int b = ringStart + i + 1;
                    int c = nextRingStart + i;
                    int d = nextRingStart + i + 1;

                    tris.Add(a);
                    tris.Add(c);
                    tris.Add(b);

                    tris.Add(b);
                    tris.Add(c);
                    tris.Add(d);
                }
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
            if (_splineContainer == null)
            {
                _splineContainer = GetComponent<SplineContainer>();
            }

            if (_splineContainer == null || _splineContainer.Spline == null) return;

            Gizmos.color = new Color(0.54f, 0.76f, 0.96f, 1.0f);

            // Lightweight gizmo sampling so editing the spline in-scene feels responsive,
            // independent of the (potentially heavier) collider RingCount/Segments.
            int gizmoRings = Mathf.Clamp(RingCount, 8, 32);
            TubeRing[] previewRings = new TubeRing[gizmoRings];
            Spline spline = _splineContainer.Spline;

            for (int i = 0; i < gizmoRings; i++)
            {
                float t = (float)i / (gizmoRings - 1);
                spline.Evaluate(t, out float3 localPos, out float3 localTangent, out float3 localUp);

                Vector3 worldPos = _splineContainer.transform.TransformPoint(localPos);
                Vector3 worldTangent = _splineContainer.transform.TransformDirection(((Vector3)localTangent).normalized);
                Vector3 worldUp = _splineContainer.transform.TransformDirection(((Vector3)localUp).normalized);
                float radius = Radius * Mathf.Max(0.001f, RadiusCurve.Evaluate(t));

                previewRings[i] = new TubeRing { T = t, Position = worldPos, Tangent = worldTangent, Up = worldUp, Radius = radius };

                DrawRing(previewRings[i]);
            }

            for (int i = 0; i < gizmoRings - 1; i++)
            {
                DrawRingConnections(previewRings[i], previewRings[i + 1]);
            }
        }

        private void DrawRing(TubeRing ring)
        {
            Vector3 fwd = ring.Tangent.sqrMagnitude > 0.0001f ? ring.Tangent.normalized : Vector3.forward;
            Vector3 up = ring.Up.sqrMagnitude > 0.0001f ? ring.Up.normalized : Vector3.up;
            Vector3 right = Vector3.Cross(up, fwd).normalized;
            up = Vector3.Cross(fwd, right).normalized;

            int div = 24;
            Vector3 prev = ring.Position + right * ring.Radius;
            for (int i = 1; i <= div; i++)
            {
                float angle = (float)i / div * Mathf.PI * 2f;
                Vector3 next = ring.Position + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * ring.Radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void DrawRingConnections(TubeRing a, TubeRing b)
        {
            // A handful of longitudinal lines so the tube reads as a tube, not a stack of circles.
            int lines = 8;
            Vector3 aFwd = a.Tangent.sqrMagnitude > 0.0001f ? a.Tangent.normalized : Vector3.forward;
            Vector3 aUp = a.Up.sqrMagnitude > 0.0001f ? a.Up.normalized : Vector3.up;
            Vector3 aRight = Vector3.Cross(aUp, aFwd).normalized;
            aUp = Vector3.Cross(aFwd, aRight).normalized;

            Vector3 bFwd = b.Tangent.sqrMagnitude > 0.0001f ? b.Tangent.normalized : Vector3.forward;
            Vector3 bUp = b.Up.sqrMagnitude > 0.0001f ? b.Up.normalized : Vector3.up;
            Vector3 bRight = Vector3.Cross(bUp, bFwd).normalized;
            bUp = Vector3.Cross(bFwd, bRight).normalized;

            for (int i = 0; i < lines; i++)
            {
                float angle = (float)i / lines * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                Vector3 pointA = a.Position + (aRight * cos + aUp * sin) * a.Radius;
                Vector3 pointB = b.Position + (bRight * cos + bUp * sin) * b.Radius;

                Gizmos.DrawLine(pointA, pointB);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_generatedMesh != null)
            {
                Gizmos.color = new Color(0.54f, 0.76f, 0.96f, 0.2f);
                Gizmos.DrawMesh(_generatedMesh, transform.position, transform.rotation, transform.lossyScale);
            }
            else if (!Application.isPlaying)
            {
                Initialize();
            }
        }
    }
}