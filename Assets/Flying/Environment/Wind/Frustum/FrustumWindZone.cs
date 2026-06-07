using Crease.Audio;
using Crease.Flying.Environment.Wind;
using Crease.Flying.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Environment.Wind.Frustum
{
    [RequireComponent(typeof(FrustumTrigger))]
    public class FrustumWindZone : WindProvider
    {
        [Header("Wind Settings")]
        [Tooltip("The strength of the wind force pushing from bottom to top.")]
        [FormerlySerializedAs("windStrength")]
        public float WindStrength = 10f;

        [Tooltip("If true, wind strength fades out near the edges of the cone.")]
        [FormerlySerializedAs("featherEdges")]
        public bool FeatherEdges = true;

        [FormerlySerializedAs("useHeightCurve")]
        public bool UseHeightCurve = true;

        [FormerlySerializedAs("heightStrengthCurve")]
        public AnimationCurve HeightStrengthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f);

        private FrustumTrigger _shape;

        [Header("Debug")]
        [FormerlySerializedAs("debugLog")]
        public bool DebugLog = false;

        private void Awake()
        {
            _shape = GetComponent<FrustumTrigger>();
        }

        private void Start()
        {
            if (_shape != null)
            {
                _shape.OnTriggerEntered.AddListener(OnEnterZone);
                _shape.OnTriggerExited.AddListener(OnExitZone);
            }

            AudioManager.Instance.PlayAtPosition("wind", transform.position, new AudioManager.PlaySettings { Loop = true });
        }

        private void OnDestroy()
        {
            if (_shape != null)
            {
                _shape.OnTriggerEntered.RemoveListener(OnEnterZone);
                _shape.OnTriggerExited.RemoveListener(OnExitZone);
            }
        }

        private void OnEnterZone(Collider other)
        {
            FlightForceReceiver receiver = other.attachedRigidbody
                ? other.attachedRigidbody.GetComponent<FlightForceReceiver>()
                : other.GetComponent<FlightForceReceiver>();

            if (receiver != null)
            {
                receiver.AddWindZone(this);
            }
        }

        private void OnExitZone(Collider other)
        {
            FlightForceReceiver receiver = other.attachedRigidbody
                ? other.attachedRigidbody.GetComponent<FlightForceReceiver>()
                : other.GetComponent<FlightForceReceiver>();

            if (receiver != null)
            {
                receiver.RemoveWindZone(this);
            }
        }

        public override Vector3 GetWindForceAtPoint(Vector3 worldPosition)
        {
            if (_shape == null) return Vector3.zero;

            Vector3 localPos = transform.InverseTransformPoint(worldPosition);

            if (localPos.y < 0 || localPos.y > _shape.Height)
            {
                return Vector3.zero;
            }

            float t = Mathf.InverseLerp(0, _shape.Height, localPos.y);
            float maxRadiusAtY = Mathf.Lerp(_shape.BottomRadius, _shape.TopRadius, t);
            float distSq = localPos.x * localPos.x + localPos.z * localPos.z;

            if (distSq > maxRadiusAtY * maxRadiusAtY)
            {
                return Vector3.zero;
            }

            Vector3 forceDirection = transform.up;
            float strength = WindStrength;

            if (FeatherEdges)
            {
                float dist = Mathf.Sqrt(distSq);
                float normalizedDist = dist / maxRadiusAtY;
                strength *= Mathf.Clamp01(1.0f - normalizedDist);
            }

            if (UseHeightCurve && HeightStrengthCurve != null)
            {
                strength *= Mathf.Clamp01(HeightStrengthCurve.Evaluate(t));
            }

            Vector3 finalForce = forceDirection * strength;
            if (DebugLog) Debug.Log($"height={t:F2} | wind={finalForce.magnitude:F1}");
            return finalForce;
        }
    }
}
