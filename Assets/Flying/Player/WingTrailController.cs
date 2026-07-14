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
    }
}
