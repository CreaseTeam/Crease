using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Crease.Flying.Environment.Wind.SplineTube
{
    /// <summary>
    /// Generates a tube-shaped trigger volume following an arbitrary Spline.
    /// Automatically segments the tube based on curvature, giving each segment its own
    /// small convex MeshCollider (Unity requires convex MeshColliders for trigger overlap tests).
    /// Segment count is determined automatically: straight sections get long segments (cheap),
    /// tight bends get short segments (accurate), without any manual tuning required.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(Rigidbody))]
    public class SplineTubeTrigger : MonoBehaviour
    {
        /// <summary>
        /// Cached per-ring sample data along the spline, shared with SplineWindZone
        /// so the spline only needs to be sampled once per rebuild.
        /// </summary>
        public struct TubeRing
        {
            public float T;
            public Vector3 Position;
            public Vector3 Tangent;
            public Vector3 Up;
            public float Radius;
        }

        [Header("Tube Settings")]
        [Tooltip("Base radius of the tube.")]
        [Min(0.01f)]
        public float Radius = 1.5f;

        [Tooltip("Optional curve to taper the radius along the length of the tube (0 = start, 1 = end). Multiplies the base Radius.")]
        public AnimationCurve RadiusCurve = AnimationCurve.Constant(0f, 1f, 1f);

        [Tooltip("Number of rings sampled along the spline. Higher = smoother bends and more accurate curvature detection.")]
        [Range(2, 128)]
        public int RingCount = 24;

        [Tooltip("Number of segments for the circle approximation at each ring cross-section.")]
        [Range(3, 32)]
        public int Segments = 12;

        [Header("Curvature Settings")]
        [Tooltip(
            "Maximum cumulative angle change (degrees) allowed within a single convex collider segment. " +
            "Lower values = more segments on bends (more accurate trigger boundary, more objects). " +
            "Higher values = fewer segments (cheaper, but convex hulls may be inaccurate on sharp turns). " +
            "25-35 degrees is a good range for most tubes.")]
        [Range(5f, 90f)]
        public float MaxSegmentAngle = 25f;

        [Header("Physics Settings")]
        [Tooltip("If true, the Rigidbody will be set to IsKinematic automatically.")]
        public bool AutoConfigureRigidbody = true;

        [Header("Events")]
        public UnityEvent<Collider> OnTriggerEntered;
        public UnityEvent<Collider> OnTriggerExited;

        private SplineContainer _splineContainer;
        private Rigidbody _rigidbody;
        private TubeRing[] _rings;

        // Tracks how many segment children the player is currently overlapping.
        // Entry fires when this goes 0→1. Exit fires when it goes 1→0.
        // This prevents false exit/enter pairs when crossing between adjacent segments.
        private int _activeSegmentCount = 0;

        // Child GameObjects holding per-segment convex MeshColliders.
        private readonly List<GameObject> _segmentObjects = new List<GameObject>();

        public IReadOnlyList<TubeRing> Rings => _rings;

        private void Awake()
        {
            Initialize();
        }

        private void OnValidate()
        {
            if (_splineContainer != null)
            {
                RebuildMesh();
            }
        }

        private void Initialize()
        {
            _splineContainer = GetComponent<SplineContainer>();
            _rigidbody = GetComponent<Rigidbody>();

            if (AutoConfigureRigidbody && _rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
            }

            RebuildMesh();
        }

        /// <summary>
        /// Resamples the spline, determines curvature-based segments, and rebuilds all
        /// child convex MeshCollider objects.
        /// </summary>
        public void RebuildMesh()
        {
            if (_splineContainer == null || _splineContainer.Spline == null) return;
            if (_splineContainer.Spline.Count < 2) return;

            SampleRings();
            RebuildSegmentColliders();
        }

        private void SampleRings()
        {
            Spline spline = _splineContainer.Spline;
            _rings = new TubeRing[RingCount];

            for (int i = 0; i < RingCount; i++)
            {
                float t = (float)i / (RingCount - 1);
                spline.Evaluate(t, out float3 localPos, out float3 localTangent, out float3 localUp);

                Vector3 worldPos      = _splineContainer.transform.TransformPoint(localPos);
                Vector3 worldTangent  = _splineContainer.transform.TransformDirection(((Vector3)localTangent).normalized);
                Vector3 worldUp       = _splineContainer.transform.TransformDirection(((Vector3)localUp).normalized);
                float   radius        = Radius * Mathf.Max(0.001f, RadiusCurve.Evaluate(t));

                _rings[i] = new TubeRing
                {
                    T        = t,
                    Position = worldPos,
                    Tangent  = worldTangent,
                    Up       = worldUp,
                    Radius   = radius
                };
            }
        }

        private void RebuildSegmentColliders()
        {
            // Destroy existing segment children before rebuilding.
            foreach (GameObject seg in _segmentObjects)
            {
                if (seg != null)
                {
                    if (Application.isPlaying)
                        Destroy(seg);
                    else
                        DestroyImmediate(seg);
                }
            }
            _segmentObjects.Clear();
            _activeSegmentCount = 0;

            // Walk the rings, grouping them into segments based on cumulative angle change.
            // Each time the cumulative angle exceeds MaxSegmentAngle, close the current
            // segment and start a new one from the last ring.
            List<int> segmentStartIndices = new List<int>();
            segmentStartIndices.Add(0);

            float cumulativeAngle = 0f;

            for (int i = 1; i < _rings.Length - 1; i++)
            {
                float angle = Vector3.Angle(_rings[i - 1].Tangent, _rings[i].Tangent);
                cumulativeAngle += angle;

                if (cumulativeAngle >= MaxSegmentAngle)
                {
                    segmentStartIndices.Add(i);
                    cumulativeAngle = 0f;
                }
            }

            // Build one convex MeshCollider child per segment.
            for (int s = 0; s < segmentStartIndices.Count; s++)
            {
                int startIndex = segmentStartIndices[s];
                int endIndex   = (s < segmentStartIndices.Count - 1)
                    ? segmentStartIndices[s + 1]
                    : _rings.Length - 1;

                if (endIndex <= startIndex) continue;

                CreateSegmentCollider(startIndex, endIndex, s);
            }
        }

        private void CreateSegmentCollider(int startRing, int endRing, int segmentIndex)
        {
            GameObject segObj = new GameObject($"TubeSegment_{segmentIndex:D3}");
            segObj.transform.SetParent(transform, worldPositionStays: true);
            segObj.layer = gameObject.layer;

            // Each segment needs its own Rigidbody (kinematic) for trigger detection to work
            // correctly alongside the parent's Rigidbody.
            Rigidbody rb = segObj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;

            MeshCollider mc = segObj.AddComponent<MeshCollider>();
            Mesh segmentMesh = GenerateSegmentMesh(startRing, endRing);
            mc.sharedMesh = segmentMesh;
            mc.convex     = true;
            mc.isTrigger  = true;

            // Wire up a SegmentTriggerRelay on each child so enter/exit events
            // route back to this parent with reference counting.
            SegmentTriggerRelay relay = segObj.AddComponent<SegmentTriggerRelay>();
            relay.Initialize(this);

            _segmentObjects.Add(segObj);
        }

        private Mesh GenerateSegmentMesh(int startRing, int endRing)
        {
            Mesh mesh = new Mesh();
            mesh.name = $"SplineTubeSegment_{startRing}_{endRing}";

            int ringCount   = endRing - startRing + 1;
            int vertexCount = ringCount * (Segments + 1);
            Vector3[] vertices = new Vector3[vertexCount];

            for (int r = 0; r < ringCount; r++)
            {
                TubeRing ring = _rings[startRing + r];

                Vector3 fwd   = ring.Tangent.sqrMagnitude > 0.0001f ? ring.Tangent.normalized : Vector3.forward;
                Vector3 up    = ring.Up.sqrMagnitude > 0.0001f ? ring.Up.normalized : Vector3.up;
                Vector3 right = Vector3.Cross(up, fwd).normalized;
                up            = Vector3.Cross(fwd, right).normalized;

                // Store in world space — segment children sit at world origin with no
                // local offset, so world == local for mesh vertex positions here.
                for (int i = 0; i <= Segments; i++)
                {
                    float angle  = (float)i / Segments * Mathf.PI * 2f;
                    Vector3 offset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * ring.Radius;
                    vertices[r * (Segments + 1) + i] = ring.Position + offset;
                }
            }

            mesh.vertices = vertices;

            List<int> tris = new List<int>();
            for (int r = 0; r < ringCount - 1; r++)
            {
                int ringStart     = r * (Segments + 1);
                int nextRingStart = (r + 1) * (Segments + 1);

                for (int i = 0; i < Segments; i++)
                {
                    int a = ringStart + i;
                    int b = ringStart + i + 1;
                    int c = nextRingStart + i;
                    int d = nextRingStart + i + 1;

                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Called by SegmentTriggerRelay children when a collider enters any segment.
        /// Increments the active-segment counter; fires OnTriggerEntered only on 0→1 transition
        /// so crossing between adjacent segments never produces a false exit/enter pair.
        /// </summary>
        public void OnChildTriggerEnter(Collider other)
        {
            _activeSegmentCount++;
            if (_activeSegmentCount == 1)
            {
                OnTriggerEntered?.Invoke(other);
            }
        }

        /// <summary>
        /// Called by SegmentTriggerRelay children when a collider exits any segment.
        /// Decrements the counter; fires OnTriggerExited only on 1→0 transition
        /// so the player can cross segment boundaries without triggering a false exit.
        /// </summary>
        public void OnChildTriggerExit(Collider other)
        {
            _activeSegmentCount = Mathf.Max(0, _activeSegmentCount - 1);
            if (_activeSegmentCount == 0)
            {
                OnTriggerExited?.Invoke(other);
            }
        }

        private void OnDrawGizmos()
        {
            if (_splineContainer == null)
                _splineContainer = GetComponent<SplineContainer>();

            if (_splineContainer == null || _splineContainer.Spline == null) return;

            Gizmos.color = new Color(0.54f, 0.76f, 0.96f, 1.0f);

            int gizmoRings = Mathf.Clamp(RingCount, 8, 32);
            TubeRing[] previewRings = new TubeRing[gizmoRings];
            Spline spline = _splineContainer.Spline;

            for (int i = 0; i < gizmoRings; i++)
            {
                float t = (float)i / (gizmoRings - 1);
                spline.Evaluate(t, out float3 localPos, out float3 localTangent, out float3 localUp);

                Vector3 worldPos     = _splineContainer.transform.TransformPoint(localPos);
                Vector3 worldTangent = _splineContainer.transform.TransformDirection(((Vector3)localTangent).normalized);
                Vector3 worldUp      = _splineContainer.transform.TransformDirection(((Vector3)localUp).normalized);
                float   radius       = Radius * Mathf.Max(0.001f, RadiusCurve.Evaluate(t));

                previewRings[i] = new TubeRing { T = t, Position = worldPos, Tangent = worldTangent, Up = worldUp, Radius = radius };
                DrawRing(previewRings[i]);
            }

            for (int i = 0; i < gizmoRings - 1; i++)
                DrawRingConnections(previewRings[i], previewRings[i + 1]);
        }

        private void DrawRing(TubeRing ring)
        {
            Vector3 fwd   = ring.Tangent.sqrMagnitude > 0.0001f ? ring.Tangent.normalized : Vector3.forward;
            Vector3 up    = ring.Up.sqrMagnitude > 0.0001f ? ring.Up.normalized : Vector3.up;
            Vector3 right = Vector3.Cross(up, fwd).normalized;
            up            = Vector3.Cross(fwd, right).normalized;

            int div = 24;
            Vector3 prev = ring.Position + right * ring.Radius;
            for (int i = 1; i <= div; i++)
            {
                float angle   = (float)i / div * Mathf.PI * 2f;
                Vector3 next  = ring.Position + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * ring.Radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void DrawRingConnections(TubeRing a, TubeRing b)
        {
            int lines = 8;
            Vector3 aFwd   = a.Tangent.sqrMagnitude > 0.0001f ? a.Tangent.normalized : Vector3.forward;
            Vector3 aUp    = a.Up.sqrMagnitude > 0.0001f ? a.Up.normalized : Vector3.up;
            Vector3 aRight = Vector3.Cross(aUp, aFwd).normalized;
            aUp            = Vector3.Cross(aFwd, aRight).normalized;

            Vector3 bFwd   = b.Tangent.sqrMagnitude > 0.0001f ? b.Tangent.normalized : Vector3.forward;
            Vector3 bUp    = b.Up.sqrMagnitude > 0.0001f ? b.Up.normalized : Vector3.up;
            Vector3 bRight = Vector3.Cross(bUp, bFwd).normalized;
            bUp            = Vector3.Cross(bFwd, bRight).normalized;

            for (int i = 0; i < lines; i++)
            {
                float angle    = (float)i / lines * Mathf.PI * 2f;
                float cos      = Mathf.Cos(angle);
                float sin      = Mathf.Sin(angle);
                Vector3 pointA = a.Position + (aRight * cos + aUp * sin) * a.Radius;
                Vector3 pointB = b.Position + (bRight * cos + bUp * sin) * b.Radius;
                Gizmos.DrawLine(pointA, pointB);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw filled segment meshes in editor so designers can see the actual
            // trigger volume, not just the ring outlines.
            if (_segmentObjects == null) return;
            Gizmos.color = new Color(0.54f, 0.76f, 0.96f, 0.15f);
            foreach (GameObject seg in _segmentObjects)
            {
                if (seg == null) continue;
                MeshCollider mc = seg.GetComponent<MeshCollider>();
                if (mc != null && mc.sharedMesh != null)
                    Gizmos.DrawMesh(mc.sharedMesh);
            }
        }
    }
}