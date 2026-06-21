using Crease.Audio;
using Crease.Flying.Environment.Wind;
using Crease.Flying.Player;
using UnityEngine;

namespace Crease.Flying.Environment.Wind.SplineTube
{
    [RequireComponent(typeof(SplineTubeTrigger))]
    public class SplineWindZone : WindProvider
    {
        [Header("Wind Settings")]
        [Tooltip("The maximum strength of the boost force, applied when the player is fully aligned with the tube (dot product = 1).")]
        public float BoostStrength = 10f;

        [Tooltip(
            "Maps alignment between the player's forward vector and the tube's tangent (dot product, -1 to 1) " +
            "to a boost multiplier. X axis: -1 = flying exactly backwards through the tube, 0 = perpendicular, " +
            "1 = flying exactly with the tube. Y axis: boost multiplier at that alignment, typically 0 to 1, " +
            "but negative values are allowed if you want misalignment (or flying backwards) to actively slow the player.")]
        public AnimationCurve AlignmentCurve = new AnimationCurve(
            new Keyframe(-1f, 0f),
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f)
        );

        [Tooltip("If true, boost strength fades out near the edges of the tube radius, same as FrustumWindZone's FeatherEdges.")]
        public bool FeatherEdges = true;

        private SplineTubeTrigger _shape;
        private Transform _cachedPlayerTransform;

        [Header("Debug")]
        public bool DebugLog = false;

        private void Awake()
        {
            _shape = GetComponent<SplineTubeTrigger>();
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
                // GetWindForceAtPoint only receives a world position (see WindProvider),
                // so we cache the player's transform here in order to read their live
                // forward vector for the alignment dot product later.
                _cachedPlayerTransform = receiver.transform;
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

                if (_cachedPlayerTransform == receiver.transform)
                {
                    _cachedPlayerTransform = null;
                }
            }
        }

        public override Vector3 GetWindForceAtPoint(Vector3 worldPosition)
        {
            if (_shape == null || _cachedPlayerTransform == null) return Vector3.zero;
            if (_shape.Rings == null || _shape.Rings.Count < 2) return Vector3.zero;

            if (!TryGetNearestTubeSample(worldPosition, out Vector3 tubeTangent, out float radiusAtPoint, out float distanceFromCenter))
            {
                return Vector3.zero;
            }

            if (distanceFromCenter > radiusAtPoint)
            {
                return Vector3.zero;
            }

            float dot = Vector3.Dot(_cachedPlayerTransform.forward, tubeTangent);
            float boostMultiplier = AlignmentCurve.Evaluate(dot);

            float strength = BoostStrength * boostMultiplier;

            if (FeatherEdges)
            {
                float normalizedDist = distanceFromCenter / radiusAtPoint;
                strength *= Mathf.Clamp01(1.0f - normalizedDist);
            }

            Vector3 finalForce = tubeTangent * strength;
            if (DebugLog) Debug.Log($"dot={dot:F2} | boostMult={boostMultiplier:F2} | wind={finalForce.magnitude:F1}");
            return finalForce;
        }

        /// <summary>
        /// Finds the two nearest cached rings to worldPosition and interpolates between them
        /// to approximate the nearest point on the tube's spline. This reuses the ring data
        /// already sampled by SplineTubeTrigger rather than re-evaluating the spline directly.
        /// </summary>
        private bool TryGetNearestTubeSample(Vector3 worldPosition, out Vector3 tangent, out float radius, out float distanceFromCenter)
        {
            tangent = Vector3.forward;
            radius = 0f;
            distanceFromCenter = float.MaxValue;

            var rings = _shape.Rings;
            int nearestIndex = -1;
            float nearestSqrDist = float.MaxValue;

            for (int i = 0; i < rings.Count; i++)
            {
                float sqrDist = (rings[i].Position - worldPosition).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearestIndex = i;
                }
            }

            if (nearestIndex < 0) return false;

            // Compare against the neighbor on either side to figure out which segment
            // worldPosition actually falls between, then interpolate within that segment.
            int neighborIndex = nearestIndex;
            if (nearestIndex == 0)
            {
                neighborIndex = 1;
            }
            else if (nearestIndex == rings.Count - 1)
            {
                neighborIndex = rings.Count - 2;
            }
            else
            {
                float distToPrev = (rings[nearestIndex - 1].Position - worldPosition).sqrMagnitude;
                float distToNext = (rings[nearestIndex + 1].Position - worldPosition).sqrMagnitude;
                neighborIndex = distToPrev < distToNext ? nearestIndex - 1 : nearestIndex + 1;
            }

            var ringA = neighborIndex < nearestIndex ? rings[neighborIndex] : rings[nearestIndex];
            var ringB = neighborIndex < nearestIndex ? rings[nearestIndex] : rings[neighborIndex];

            Vector3 segment = ringB.Position - ringA.Position;
            float segmentLengthSqr = segment.sqrMagnitude;

            float segT = segmentLengthSqr > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(worldPosition - ringA.Position, segment) / segmentLengthSqr)
                : 0f;

            Vector3 nearestPointOnSegment = ringA.Position + segment * segT;

            tangent = Vector3.Slerp(ringA.Tangent, ringB.Tangent, segT).normalized;
            radius = Mathf.Lerp(ringA.Radius, ringB.Radius, segT);
            distanceFromCenter = Vector3.Distance(worldPosition, nearestPointOnSegment);

            return true;
        }
    }
}