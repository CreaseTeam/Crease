using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Crease.Flying.Environment.BlockoutHelpers
{
    /// <summary>
    /// Generates an invisible dome-shaped MeshCollider from a closed Spline outline. Used to keep
    /// the player contained inside a play area (e.g. the water sprouts arena) without a visible
    /// wall. Edit the Spline's knots directly in the Scene view to reshape the base outline - the
    /// dome mesh and its MeshCollider rebuild automatically to match.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SplineContainer))]
    [RequireComponent(typeof(MeshCollider))]
    public class SplineDomeBoundary : MonoBehaviour
    {
        private const int MinRadialResolution = 3;
        private const int MinHeightSegments = 1;

        [Header("Dome Shape")]
        [Tooltip("Height of the dome apex above the Spline outline.")]
        [SerializeField] private float domeHeight = 40f;

        [Tooltip("How far the boundary wall extends straight down below the Spline outline. Must reach below the lowest terrain/ground point in the area, otherwise the player can dive under the wall and escape through the gap.")]
        [SerializeField] private float skirtDepth = 150f;

        [Tooltip("Number of points sampled around the closed Spline to form the base ring. Higher values follow tight curves in the outline more closely.")]
        [SerializeField, Range(MinRadialResolution, 128)] private int radialResolution = 32;

        [Tooltip("Number of rings between the base outline and the dome apex. Higher values produce a smoother curvature.")]
        [SerializeField, Range(MinHeightSegments, 24)] private int heightSegments = 8;

        [Header("Collider")]
        [Tooltip("Cook the collider as a convex hull. Unity's physics engine only sweep-tests fast-moving Rigidbodies (Continuous/Continuous Dynamic collision detection) against convex colliders - a concave MeshCollider lets a fast player tunnel straight through it in a single physics step. Enabling this trades exact concave detail in the outline (dents get filled in by the convex hull) for a boundary the player physically cannot pass through.")]
        [SerializeField] private bool useConvexCollider = true;

        [Header("Behavior")]
        [Tooltip("Rebuild the dome mesh automatically whenever the Spline is edited in the Scene view.")]
        [SerializeField] private bool rebuildOnSplineChange = true;

        [Header("Gizmos")]
        [Tooltip("Draw the invisible dome as a wireframe when this object is selected, so it stays editable without being visible in Play mode.")]
        [SerializeField] private bool showGizmo = true;
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.6f, 1f, 0.35f);

        private SplineContainer _splineContainer;
        private MeshCollider _meshCollider;
        private Mesh _mesh;

        private SplineContainer SplineContainerRef => _splineContainer ??= GetComponent<SplineContainer>();
        private MeshCollider MeshColliderRef => _meshCollider ??= GetComponent<MeshCollider>();

        private void OnEnable()
        {
            Spline.Changed += OnSplineChanged;
            RebuildDome();
        }

        private void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
        }

        private void OnValidate()
        {
            RebuildDome();
        }

        private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            if (!rebuildOnSplineChange) return;
            if (SplineContainerRef == null || SplineContainerRef.Spline != spline) return;
            RebuildDome();
        }

        /// <summary>
        /// Regenerates the dome mesh and MeshCollider from the current Spline outline. Call this
        /// manually after editing the Spline via script, or via the context menu in the Inspector.
        /// </summary>
        [ContextMenu("Rebuild Dome")]
        public void RebuildDome()
        {
            SplineContainer container = SplineContainerRef;
            if (container == null || container.Splines.Count == 0) return;

            Spline spline = container.Spline;
            if (spline == null || spline.Count < MinRadialResolution) return;

            spline.Closed = true;

            Vector3[] baseRing = SampleBaseRing(spline);
            Vector3 baseCenter = ComputeCentroid(baseRing);

            BuildDomeGeometry(baseRing, baseCenter, out Vector3[] vertices, out int[] triangles);

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "SplineDomeBoundary" };
            }
            else
            {
                _mesh.Clear();
            }

            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            MeshCollider meshCollider = MeshColliderRef;
            meshCollider.sharedMesh = null;
            meshCollider.convex = useConvexCollider;
            meshCollider.sharedMesh = _mesh;
        }

        private Vector3[] SampleBaseRing(Spline spline)
        {
            var ring = new Vector3[radialResolution];
            for (int i = 0; i < radialResolution; i++)
            {
                float t = i / (float)radialResolution;
                float3 point = spline.EvaluatePosition(t);
                ring[i] = point;
            }
            return ring;
        }

        private static Vector3 ComputeCentroid(Vector3[] points)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 point in points)
            {
                sum += point;
            }
            return sum / points.Length;
        }

        /// <summary>
        /// Builds a fully enclosed boundary: a straight-walled skirt that drops from the base outline
        /// down past any terrain in the area, followed by a dome that lofts upward from the outline
        /// using a quarter-circle profile (cosine falloff for the radius, sine falloff for the height),
        /// so the outline scales smoothly down to a single apex point directly above its centroid. The
        /// skirt guarantees there is no gap under the wall for the player to dive through and escape.
        /// </summary>
        private void BuildDomeGeometry(Vector3[] baseRing, Vector3 baseCenter, out Vector3[] vertices, out int[] triangles)
        {
            int domeRingCount = heightSegments + 1;
            int ringCount = domeRingCount + 1; // + skirt bottom ring
            vertices = new Vector3[ringCount * radialResolution];

            for (int i = 0; i < radialResolution; i++)
            {
                Vector3 basePoint = baseRing[i];
                vertices[i] = new Vector3(basePoint.x, basePoint.y - skirtDepth, basePoint.z);
            }

            for (int domeRing = 0; domeRing < domeRingCount; domeRing++)
            {
                int ring = domeRing + 1;
                float theta = (domeRing / (float)heightSegments) * (Mathf.PI * 0.5f);
                float radialScale = Mathf.Cos(theta);
                float verticalOffset = domeHeight * Mathf.Sin(theta);

                for (int i = 0; i < radialResolution; i++)
                {
                    Vector3 basePoint = baseRing[i];
                    Vector3 outward = basePoint - baseCenter;
                    outward.y = 0f;

                    Vector3 vertex = baseCenter + outward * radialScale;
                    vertex.y = basePoint.y + verticalOffset;
                    vertices[(ring * radialResolution) + i] = vertex;
                }
            }

            var triangleList = new List<int>((ringCount - 1) * radialResolution * 6);
            for (int ring = 0; ring < ringCount - 1; ring++)
            {
                for (int i = 0; i < radialResolution; i++)
                {
                    int next = (i + 1) % radialResolution;

                    int a = (ring * radialResolution) + i;
                    int b = (ring * radialResolution) + next;
                    int c = ((ring + 1) * radialResolution) + i;
                    int d = ((ring + 1) * radialResolution) + next;

                    triangleList.Add(a);
                    triangleList.Add(c);
                    triangleList.Add(b);

                    triangleList.Add(b);
                    triangleList.Add(c);
                    triangleList.Add(d);
                }
            }
            triangles = triangleList.ToArray();
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmo || _mesh == null) return;

            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireMesh(_mesh);
        }
    }
}
