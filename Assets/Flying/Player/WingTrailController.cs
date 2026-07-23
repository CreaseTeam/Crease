using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player
{
    public class WingTrailController : MonoBehaviour
    {
        [Header("Wing Tip Transforms")]
        [FormerlySerializedAs("wingTipLeft")]
        public Transform WingTipLeft;
        [FormerlySerializedAs("wingTipRight")]
        public Transform WingTipRight;

        [Header("Trail Settings")]
        [FormerlySerializedAs("trailTime")]
        public float TrailTime = 0.5f;
        [FormerlySerializedAs("startWidth")]
        public float StartWidth = 0.3f;
        [FormerlySerializedAs("endWidth")]
        public float EndWidth = 0f;
        [FormerlySerializedAs("startColor")]
        public Color StartColor = new Color(1f, 1f, 1f, 0.8f);
        [FormerlySerializedAs("endColor")]
        public Color EndColor = new Color(1f, 1f, 1f, 0f);
        [FormerlySerializedAs("trailMaterial")]
        public Material TrailMaterial;

        private TrailRenderer _trailLeft;
        private TrailRenderer _trailRight;

        private bool _isTrailActive = true;

        void Start()
        {
            _trailLeft = CreateTrail(WingTipLeft);
            _trailRight = CreateTrail(WingTipRight);

            SetTrailEnabled(_isTrailActive);
        }

        TrailRenderer CreateTrail(Transform wingTip)
        {
            GameObject trailObj = new GameObject("WingTrail");
            trailObj.transform.SetParent(wingTip);
            trailObj.transform.localPosition = Vector3.zero;

            TrailRenderer trail = trailObj.AddComponent<TrailRenderer>();
            trail.time = TrailTime;
            trail.startWidth = StartWidth;
            trail.endWidth = EndWidth;
            trail.material = TrailMaterial != null ? TrailMaterial
                : new Material(Shader.Find("Sprites/Default"));

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(StartColor, 0f),
                    new GradientColorKey(EndColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(StartColor.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            trail.colorGradient = gradient;

            return trail;
        }

        public void SetTrailTime(float time)
        {
            TrailTime = time;
            if (_trailLeft) _trailLeft.time = time;
            if (_trailRight) _trailRight.time = time;
        }

        public void SetTrailEnabled(bool enabled)
        {
            _isTrailActive = enabled;
            if (_trailLeft) _trailLeft.emitting = enabled;
            if (_trailRight) _trailRight.emitting = enabled;
        }

        /// <summary>
        /// Places wing tip transforms on the rear-left and rear-right mesh corners.
        /// Mesh vertices are expected in the flight frame (+Z = front).
        /// </summary>
        public void PositionWingTipsFromMesh(Mesh mesh, Transform meshTransform)
        {
            if (mesh == null || meshTransform == null || WingTipLeft == null || WingTipRight == null)
                return;

            if (!TryGetBackWingTipLocalPositions(mesh, out Vector3 backLeft, out Vector3 backRight))
                return;

            WingTipLeft.SetParent(meshTransform, false);
            WingTipRight.SetParent(meshTransform, false);
            WingTipLeft.localPosition = backLeft;
            WingTipRight.localPosition = backRight;
        }

        private static bool TryGetBackWingTipLocalPositions(Mesh mesh, out Vector3 backLeft, out Vector3 backRight)
        {
            backLeft = default;
            backRight = default;

            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
                return false;

            Vector3 extents = mesh.bounds.extents;
            float zScale = 1f / Mathf.Max(extents.z, 1e-5f);
            float xScale = 1f / Mathf.Max(extents.x, 1e-5f);

            backLeft = vertices[0];
            backRight = vertices[0];
            float bestBackLeftScore = ScoreBackLeft(vertices[0], zScale, xScale);
            float bestBackRightScore = ScoreBackRight(vertices[0], zScale, xScale);

            for (int i = 1; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];

                float backLeftScore = ScoreBackLeft(vertex, zScale, xScale);
                if (backLeftScore < bestBackLeftScore)
                {
                    bestBackLeftScore = backLeftScore;
                    backLeft = vertex;
                }

                float backRightScore = ScoreBackRight(vertex, zScale, xScale);
                if (backRightScore < bestBackRightScore)
                {
                    bestBackRightScore = backRightScore;
                    backRight = vertex;
                }
            }

            return true;
        }

        private static float ScoreBackLeft(Vector3 vertex, float zScale, float xScale) =>
            vertex.z * zScale + vertex.x * xScale;

        private static float ScoreBackRight(Vector3 vertex, float zScale, float xScale) =>
            vertex.z * zScale - vertex.x * xScale;
    }
}
