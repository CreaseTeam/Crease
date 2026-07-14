using Crease.Audio;
// using Crease.Flying.Environment.Wind;
using Crease.Flying.Player;
using Crease.Managers.Input;
using UnityEngine;

namespace Crease.Flying.Environment.Wind.SplineTube
{
    [RequireComponent(typeof(SplineTubeTrigger))]
    public class SplineWindZone : WindProvider
    {
        [Header("Wind Settings")]
        [Tooltip("The maximum strength of the boost force, applied when the player is fully aligned with the tube (dot product = 1).")]
        public float BoostStrength = 10f;
        [Tooltip("The maximum strength of the sideways boost force, applied when the player is fully perpendicular to the tube (dot product = 0).")]
        public float SidewaysStrength = 70f;
        [Tooltip("The maximum strength of the centering force, applied to gently pull the player towards the center of the wind tube.")]
        public float CenteringStrength = 150f;

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

        [Header("Torque")]
        [Tooltip("If true, wind force can rotate the plane toward its direction when the plane is pointed away from the spline center.")]
        public bool ApplyTorqueFromWindForce = true;

        [Tooltip("How strongly wind force rotates the plane toward its direction.")]
        public float TorqueStrength = 1.5f;

        public override bool AppliesTorqueFromForce => ApplyTorqueFromWindForce;
        public override float WindTorqueStrength => TorqueStrength;

        public override bool ShouldApplyTorqueAtPoint(Vector3 worldPosition)
        {
            if (!ApplyTorqueFromWindForce || _shape == null || _cachedPlayerTransform == null)
            {
                return false;
            }

            if (!TryGetNearestTubeSample(worldPosition, out _, out float radiusAtPoint, out float distanceFromCenter, out Vector3 nearestPoint))
            {
                return false;
            }

            if (distanceFromCenter > radiusAtPoint || distanceFromCenter < 0.001f)
            {
                return false;
            }

            Vector3 toCenter = nearestPoint - worldPosition;
            float facingCenter = Vector3.Dot(_cachedPlayerTransform.forward, toCenter.normalized);
            return facingCenter < 0f;
        }

        private SplineTubeTrigger _shape;
        private Transform _cachedPlayerTransform;
        private KinematicBody _cachedPlayerBody;

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
            // Search the collider's own GameObject and its parent hierarchy for
            // FlightForceReceiver. We cannot use other.attachedRigidbody here because
            // each TubeSegment child has its own Rigidbody — attachedRigidbody returns
            // the segment's Rigidbody instead of the player's, causing the lookup to fail.
            FlightForceReceiver receiver = other.GetComponentInParent<FlightForceReceiver>();
            if (receiver == null) receiver = other.GetComponent<FlightForceReceiver>();

            if (receiver != null)
            {
                receiver.AddWindZone(this);
                _cachedPlayerTransform = receiver.transform;
                _cachedPlayerBody = receiver.GetComponent<KinematicBody>();
            }
        }

        private void OnExitZone(Collider other)
        {
            FlightForceReceiver receiver = other.GetComponentInParent<FlightForceReceiver>();
            if (receiver == null) receiver = other.GetComponent<FlightForceReceiver>();

            if (receiver != null)
            {
                receiver.RemoveWindZone(this);

                if (_cachedPlayerTransform == receiver.transform)
                {
                    _cachedPlayerTransform = null;
                    _cachedPlayerBody = null;
                }
            }
        }

        public override Vector3 GetWindForceAtPoint(Vector3 worldPosition)
        {
            if (_shape == null || _cachedPlayerTransform == null) return Vector3.zero;
            if (_shape.Rings == null || _shape.Rings.Count < 2) return Vector3.zero;

            if (!TryGetNearestTubeSample(worldPosition, out Vector3 tubeTangent, out float radiusAtPoint, out float distanceFromCenter, out Vector3 nearestPoint))
            {
                return Vector3.zero;
            }

            if (distanceFromCenter > radiusAtPoint)
            {
                return Vector3.zero;
            }
            Vector3 toCenter = (nearestPoint - worldPosition);
            
            // Player is considered "idling" / "passive" if FlightController detects no input, and centeringForce is activated
            // Player can freely escape the wind tube once input is detected and centeringForce no longer applies
            float playerSpeed = _cachedPlayerBody != null ? _cachedPlayerBody.Speed : 0f;
            float passiveAmount = Mathf.Clamp01(1f - InputManager.Instance.MoveInput.magnitude);
            
            float towardsCenterVelocity = Vector3.Dot(_cachedPlayerBody.Velocity, toCenter.normalized);
            float dampening = Mathf.Clamp01(1f - (towardsCenterVelocity / CenteringStrength));
            Vector3 centeringForce = toCenter.normalized * CenteringStrength * (distanceFromCenter / radiusAtPoint) * passiveAmount * dampening;

            float dot = Vector3.Dot(_cachedPlayerTransform.forward, tubeTangent);
            float boostMultiplier = AlignmentCurve.Evaluate(dot);

            float strength = BoostStrength * boostMultiplier;
            
            // In the case that the player flies directly perpendicularly to the wind tube,
            // meaning the dot product between the players direction and wind tube's direction
            // is equal to zero, they should get pushed slightly in the direction of the wind tube
            // at that point (would either be left or right) by a slight sideways force.
            float perpendicularAmount = 1f - Mathf.Abs(dot);
            Vector3 sidewaysForce = tubeTangent * SidewaysStrength * perpendicularAmount;

            if (FeatherEdges)
            {
                float normalizedDist = distanceFromCenter / radiusAtPoint;
                float featherAmount = Mathf.Clamp01(1.0f - normalizedDist);
                strength *= featherAmount;
                sidewaysForce *= featherAmount;
            }

            // Per spec: force is applied in the direction the player is facing (not the tube's
            // tangent), scaled by how aligned that facing direction already is with the tube.
            // This rewards players for actively steering along the tube rather than the tube
            // forcibly carrying them along its path regardless of their heading.
            // The closer to the center the bigger the boost, as opposed to the edges of the tube.
            Vector3 finalForce = (_cachedPlayerTransform.forward * strength) + sidewaysForce + centeringForce;
            return finalForce;
        }
        /// <summary>
        /// Finds the two nearest cached rings to worldPosition and interpolates between them
        /// to approximate the nearest point on the tube's spline. This reuses the ring data
        /// already sampled by SplineTubeTrigger rather than re-evaluating the spline directly.
        /// </summary>
        private bool TryGetNearestTubeSample(Vector3 worldPosition, out Vector3 tangent, out float radius, out float distanceFromCenter, out Vector3 nearestPoint)
        {
            // default values before computation
            tangent = Vector3.forward;
            radius = 0f;
            distanceFromCenter = float.MaxValue;
            nearestPoint = Vector3.zero;

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
            nearestPoint = nearestPointOnSegment;

            return true;
        }
    }
}