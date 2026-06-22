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

        [Header("Velocity Lift Mode")]
        [Tooltip("If true, control the player's vertical velocity directly so they follow liftcurve up to the top of the frustum.")]
        public bool UseVelocityLiftMode = false;

        [Tooltip("Normalized height over normalized time")]
        public AnimationCurve LiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Normalized height (0 = bottom, 1 = top) below which the player's horizontal speed is frozen to 0, forcing them to follow the vertical lift curve. Once they climb past this height, horizontal velocity is released.")]
        [Range(0f, 1f)]
        public float HorizontalReleaseHeight = 1f;

        [Tooltip("Once past HorizontalReleaseHeight, the speed (m/s) pushed along the player's facing direction so they don't stall at the tip of the frustum after their horizontal speed was killed.")]
        public float ReleaseSpeed = 10f;

        [Min(0.001f)]
        public float LiftDuration = 2f;

        private FrustumTrigger _shape;

        private bool _liftActive;
        private float _liftProgress; // 0-1
        private Transform _liftReceiver;

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

                if (UseVelocityLiftMode && _shape != null)
                {
                    _liftReceiver = receiver.transform;
                    SeedLiftProgress(receiver.transform.position);
                }
            }
        }

        private void SeedLiftProgress(Vector3 worldPosition)
        {
            float heightFraction = GetHeightFraction(worldPosition);
            _liftProgress = InverseLiftCurve(heightFraction);
            _liftActive = true;

            if (DebugLog)
                Debug.Log($"[Lift] enter @ height {heightFraction:P0} -> seed progress {_liftProgress:F2} " +
                          $"({(1f - _liftProgress) * LiftDuration:F2}s of {LiftDuration:F2}s lift remaining)");
        }

        private void OnExitZone(Collider other)
        {
            FlightForceReceiver receiver = other.attachedRigidbody
                ? other.attachedRigidbody.GetComponent<FlightForceReceiver>()
                : other.GetComponent<FlightForceReceiver>();

            if (receiver != null)
            {
                receiver.RemoveWindZone(this);
                _liftActive = false;
                _liftReceiver = null;
            }
        }

        public override bool OverridesVelocity => UseVelocityLiftMode;

        public override Vector3 GetVelocityOverride(Vector3 worldPosition, Vector3 currentVelocity)
        {
            if (!UseVelocityLiftMode || _shape == null || !_liftActive)
            {
                return currentVelocity;
            }

            // Advance along the curve, clamping at the top so the player is held there.
            _liftProgress = Mathf.Clamp01(_liftProgress + Time.fixedDeltaTime / LiftDuration);

            float targetHeightFraction = Mathf.Clamp01(LiftCurve.Evaluate(_liftProgress));
            Vector3 localPos = transform.InverseTransformPoint(worldPosition);
            Vector3 targetWorld = transform.TransformPoint(
                new Vector3(localPos.x, targetHeightFraction * _shape.Height, localPos.z));

            // Replace only the along-axis component of velocity, leaving the perpendicular untouched.
            Vector3 axis = transform.up;
            Vector3 displacementNeeded = targetWorld - worldPosition;
            Vector3 velocityNeeded = displacementNeeded / Time.fixedDeltaTime;
            float alongSpeed = Vector3.Dot(velocityNeeded, axis);

            // Freeze horizontal velocity below HorizontalReleaseHeight so the player follows the lift curve up.
            Vector3 perpendicular = currentVelocity - Vector3.Project(currentVelocity, axis);
            if (GetHeightFraction(worldPosition) < HorizontalReleaseHeight)
            {
                perpendicular = Vector3.zero;
            }
            else if (_liftReceiver != null)
            {
                // Past the release height, push along facing so the player doesn't stall at the tip.
                Vector3 facing = Vector3.ProjectOnPlane(_liftReceiver.forward, axis);
                if (facing.sqrMagnitude > 0.0001f)
                {
                    perpendicular = facing.normalized * ReleaseSpeed;
                }
            }

            if (DebugLog)
                Debug.Log($"[Lift] progress {_liftProgress:F2} | target height {targetHeightFraction:P0} " +
                          $"| current height {GetHeightFraction(worldPosition):P0} | up speed {alongSpeed:F1} m/s");

            return perpendicular + axis * alongSpeed;
        }

        // Normalized 0-1 height of a world position within the frustum.
        private float GetHeightFraction(Vector3 worldPosition)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPosition);
            return _shape.Height > 0f ? Mathf.Clamp01(localPos.y / _shape.Height) : 0f;
        }

        // Finds the curve time whose height equals targetHeight by sampling and interpolating the crossing.
        private float InverseLiftCurve(float targetHeight)
        {
            int LiftInverseSamples = 32;

            targetHeight = Mathf.Clamp01(targetHeight);

            float prevT = 0f;
            float prevValue = LiftCurve.Evaluate(0f);
            if (targetHeight <= prevValue) return 0f;

            for (int i = 1; i <= LiftInverseSamples; i++)
            {
                float t = (float)i / LiftInverseSamples;
                float value = LiftCurve.Evaluate(t);

                if (value >= targetHeight)
                {
                    float span = value - prevValue;
                    float frac = span > 0.0001f ? (targetHeight - prevValue) / span : 0f;
                    return Mathf.Lerp(prevT, t, frac);
                }

                prevT = t;
                prevValue = value;
            }

            return 1f;
        }

        public override Vector3 GetWindForceAtPoint(Vector3 worldPosition)
        {
            // In lift mode the force path is fully disabled; vertical velocity is controlled directly.
            if (UseVelocityLiftMode) return Vector3.zero;

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
