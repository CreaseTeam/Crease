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

        [Header("Input Settings")]
        [Tooltip("If true, all player input (turning, abilities, etc.) is disabled while the player is inside this wind tube.")]
        public bool IgnoreInput = false;

        [Tooltip(
            "Centering force strength used while IgnoreInput is active — completely independent of " +
            "CenteringStrength above, which stays exactly as tuned for normal, non-locked tubes. " +
            "IgnoreInputPassiveAmount below is only a 0–1 multiplier on this value, so it can never " +
            "exceed whatever this is set to — raise THIS if centering feels weak, not that.")]
        public float IgnoreInputCenteringStrength = 150f;

        [Tooltip(
            "Multiplier (0–1) applied to IgnoreInputCenteringStrength above, replacing the normal " +
            "real-time-input-driven passiveAmount calculation (which would otherwise be permanently " +
            "pinned at 1 — its absolute max — since IgnoreInput locks MoveInput at zero for the whole " +
            "traversal). This can only ever reach parity with IgnoreInputCenteringStrength at best, " +
            "never exceed it — to increase overall centering strength beyond that, raise " +
            "IgnoreInputCenteringStrength itself instead.")]
        [Range(0f, 1f)]
        public float IgnoreInputPassiveAmount = 1f;

        [Header("Torque")]
        [Tooltip("If true, wind force can rotate the plane toward its direction when the plane is pointed away from the spline center.")]
        public bool ApplyTorqueFromWindForce = true;

        [Tooltip("How strongly wind force rotates the plane toward its direction.")]
        public float TorqueStrength = 1.5f;

        [Tooltip(
            "Multiplier applied to TorqueStrength while IgnoreInput is active. FlightController's " +
            "stability torque runs at full, uninterrupted strength every frame when there's no player " +
            "input to ever reduce it (normally, even idle players produce occasional incidental input " +
            "that momentarily weakens it) — this compensates so the tube's steering can still win out.")]
        public float IgnoreInputTorqueBoost = 2f;

        public override bool AppliesTorqueFromForce => ApplyTorqueFromWindForce;
        public override float WindTorqueStrength => IgnoreInput ? TorqueStrength * IgnoreInputTorqueBoost : TorqueStrength;

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

            // Normally, torque only fires when the player is facing away from center, so we
            // don't fight a well-aligned player who could otherwise self-correct. Under
            // IgnoreInput the player has no ability to self-correct at all, so there's no
            // reason to hold back — apply every frame instead of only on misaligned ones,
            // so it can actually contest FlightController's now-constant stability torque.
            if (IgnoreInput) return true;

            Vector3 toCenter = nearestPoint - worldPosition;
            float facingCenter = Vector3.Dot(_cachedPlayerTransform.forward, toCenter.normalized);
            return facingCenter < 0f;
        }

        private SplineTubeTrigger _shape;
        private Transform _cachedPlayerTransform;
        private KinematicBody _cachedPlayerBody;

        // +1 = wind blows in the spline's authored direction, -1 = reversed.
        // Only ever changes from +1 when _shape.Reversible is true and the player
        // crosses the "end" side end-cap trigger; see HandleEndCapEntered.
        private float _windDirectionSign = 1f;

        // Static, shared across all SplineWindZone instances, so multiple overlapping
        // IgnoreInput tubes don't step on each other — Player input only re-enables once
        // every zone holding a lock has released it. Kept here (rather than in InputManager)
        // so InputManager.cs doesn't need to change.
        private static readonly System.Collections.Generic.HashSet<SplineWindZone> _inputLockHolders = new System.Collections.Generic.HashSet<SplineWindZone>();

        // Tracks whether this specific zone currently holds the lock, so OnDestroy can
        // release it if the zone is destroyed (or disabled) while the player is still inside it.
        private bool _inputLockHeld = false;

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
                _shape.OnEndCapEntered.AddListener(HandleEndCapEntered);
            }

            AudioManager.Instance.PlayAtPosition("wind", transform.position, new AudioManager.PlaySettings { Loop = true });
        }

        private void OnDestroy()
        {
            if (_shape != null)
            {
                _shape.OnTriggerEntered.RemoveListener(OnEnterZone);
                _shape.OnTriggerExited.RemoveListener(OnExitZone);
                _shape.OnEndCapEntered.RemoveListener(HandleEndCapEntered);
            }

            // Safety net: if this zone is destroyed/unloaded while still holding an input
            // lock (e.g. player inside it during a scene unload), release it so the player
            // doesn't get stuck with permanently disabled input.
            if (_inputLockHeld)
            {
                SetPlayerInputLocked(false);
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

                if (IgnoreInput)
                {
                    SetPlayerInputLocked(true);
                }
            }
        }

        private void OnExitZone(Collider other)
        {
            FlightForceReceiver receiver = other.GetComponentInParent<FlightForceReceiver>();
            if (receiver == null) receiver = other.GetComponent<FlightForceReceiver>();

            if (receiver != null)
            {
                receiver.RemoveWindZone(this);

                if (IgnoreInput)
                {
                    SetPlayerInputLocked(false);
                }

                if (_cachedPlayerTransform == receiver.transform)
                {
                    _cachedPlayerTransform = null;
                    _cachedPlayerBody = null;
                    // Deliberately NOT resetting _windDirectionSign here — a reversal should
                    // persist across exits, and only change again when the player crosses an
                    // end cap (in either direction). See HandleEndCapEntered.
                }
            }
        }

        /// <summary>
        /// Locks/unlocks player input by disabling/enabling the Player action map directly
        /// (InputManager.Actions is public, same as SwitchToFolding already calls from
        /// outside InputManager). Reference-counted via the static _inputLockHolders set so
        /// multiple overlapping IgnoreInput tubes don't step on each other — input only
        /// re-enables once every zone holding a lock has released it.
        ///
        /// Note: unlike a lock owned by InputManager itself, this can't protect against
        /// something else (e.g. a scene-transition call to SwitchToPlayerAndDebug) also
        /// calling Actions.Player.Enable() while a tube still holds this lock. That's an
        /// acceptable trade-off for keeping InputManager.cs untouched, but worth knowing if
        /// scene transitions can happen while a player is mid-tube.
        /// </summary>
        private void SetPlayerInputLocked(bool locked)
        {
            if (InputManager.Instance == null || InputManager.Instance.Actions == null) return;

            if (locked)
            {
                bool wasUnlocked = _inputLockHolders.Count == 0;
                _inputLockHolders.Add(this);
                _inputLockHeld = true;

                if (wasUnlocked)
                {
                    InputManager.Instance.Actions.Player.Disable();
                }
            }
            else
            {
                _inputLockHolders.Remove(this);
                _inputLockHeld = false;

                if (_inputLockHolders.Count == 0)
                {
                    InputManager.Instance.Actions.Player.Enable();
                }
            }

            if (DebugLog)
                Debug.Log($"[SplineWindZone] SetPlayerInputLocked({locked})");
        }

        /// <summary>
        /// Fired by SplineTubeTrigger when the player crosses one of the reversible end caps.
        /// Sets which direction "forward along the tube" means for the rest of this traversal.
        /// No-ops if Reversible is off (SplineTubeTrigger won't generate end caps in that case,
        /// but this guard keeps things safe if Reversible is toggled off after entry).
        /// </summary>
        private void HandleEndCapEntered(Collider other, bool enteredFromEndSide)
        {
            if (_shape == null || !_shape.Reversible) return;

            FlightForceReceiver receiver = other.GetComponentInParent<FlightForceReceiver>();
            if (receiver == null) receiver = other.GetComponent<FlightForceReceiver>();
            if (receiver == null) return;

            _windDirectionSign = enteredFromEndSide ? -1f : 1f;

            if (DebugLog)
                Debug.Log($"[SplineWindZone] Player entered from {(enteredFromEndSide ? "END" : "START")} side. Wind direction sign = {_windDirectionSign}");
        }

        public override Vector3 GetWindForceAtPoint(Vector3 worldPosition)
        {
            if (_shape == null || _cachedPlayerTransform == null) return Vector3.zero;
            if (_shape.Rings == null || _shape.Rings.Count < 2) return Vector3.zero;

            if (!TryGetNearestTubeSample(worldPosition, out Vector3 tubeTangent, out float radiusAtPoint, out float distanceFromCenter, out Vector3 nearestPoint))
            {
                return Vector3.zero;
            }

            // Flip the tube's tangent when travelling the reverse direction, so alignment,
            // boost, and sideways-force calculations below all treat "along the wind" as
            // whichever way the player actually entered. No-op (sign is always +1) unless
            // Reversible is enabled and the player entered via the end-side cap.
            tubeTangent *= _windDirectionSign;

            if (distanceFromCenter > radiusAtPoint)
            {
                return Vector3.zero;
            }
            Vector3 toCenter = (nearestPoint - worldPosition);
            
            // Player is considered "idling" / "passive" if FlightController detects no input, and centeringForce is activated
            // Player can freely escape the wind tube once input is detected and centeringForce no longer applies
            float playerSpeed = _cachedPlayerBody != null ? _cachedPlayerBody.Speed : 0f;

            // This calculation assumes real-time, fluctuating input — an idle-but-not-locked player
            // occasionally nudges the stick, so passiveAmount normally dips below 1 sometimes, keeping
            // centering a soft, varying pull. IgnoreInput breaks that assumption: MoveInput is
            // permanently (0,0), so this would otherwise pin passiveAmount at its absolute max for the
            // entire traversal with zero relief. Give IgnoreInput its own dedicated, tunable value
            // instead of hijacking this one.
            float passiveAmount = IgnoreInput
                ? IgnoreInputPassiveAmount
                : Mathf.Clamp01(1f - InputManager.Instance.MoveInput.magnitude);

            // IgnoreInputPassiveAmount is only ever a 0–1 multiplier, so it can't push centering
            // past whatever base strength it's multiplying — use a fully independent strength value
            // for IgnoreInput tubes rather than capping them at the shared CenteringStrength ceiling
            // that normal, non-locked tubes are tuned around.
            float effectiveCenteringStrength = IgnoreInput ? IgnoreInputCenteringStrength : CenteringStrength;

            float towardsCenterVelocity = Vector3.Dot(_cachedPlayerBody.Velocity, toCenter.normalized);
            float dampening = Mathf.Clamp01(1f - (towardsCenterVelocity / effectiveCenteringStrength));
            Vector3 centeringForce = toCenter.normalized * effectiveCenteringStrength * (distanceFromCenter / radiusAtPoint) * passiveAmount * dampening;

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