using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player
{
    /// <summary>
    /// Scriptable flight stat values. To add a new stat:
    /// 1. Add a public float field here (with default and optional FormerlySerializedAs).
    /// 2. Add a matching name to <see cref="FlightStatType"/>.
    /// FlightStatAccessor validates the two stay in sync at startup.
    /// </summary>
    [CreateAssetMenu(fileName = "FlightSettings", menuName = "Crease/Flight Settings")]
    public class FlightSettings : ScriptableObject
    {
        [Header("Flight Physics")]
        [Tooltip("Mass used for force calculations on the player body.")]
        [FormerlySerializedAs("mass")]
        public float Mass = 1f;
        [FormerlySerializedAs("divingGravity")]
        public float DivingGravity = 0.12f;
        [FormerlySerializedAs("climbingGravity")]
        public float ClimbingGravity = 0.04f;
        [FormerlySerializedAs("lift")]
        public float Lift = 0.06f;
        [FormerlySerializedAs("diveRate")]
        public float DiveRate = 0.1f;
        [FormerlySerializedAs("climbRate")]
        public float ClimbRate = 0.04f;
        [FormerlySerializedAs("climbEfficiency")]
        public float ClimbEfficiency = 3.5f;
        [FormerlySerializedAs("turnInterpolation")]
        public float TurnInterpolation = 0.1f;

        [Header("Stability")]
        [Tooltip("How strongly the plane aligns its nose with its velocity vector. Higher = more weathervane effect.")]
        public float StabilityStrength = 2f;
        [Tooltip("Speed at which stability reaches full strength.")]
        public float StabilityReferenceSpeed = 15f;
        [Tooltip("How much active input reduces stability. 1 = fully suppressed while turning, 0 = always full stability.")]
        public float StabilityInputSuppression = 0.85f;
        [FormerlySerializedAs("xDrag")]
        public float XDrag = 0.99f;
        [FormerlySerializedAs("yDrag")]
        public float YDrag = 0.98f;
        [FormerlySerializedAs("zDrag")]
        public float ZDrag = 0.99f;

        [Header("Input Tuning")]
        [FormerlySerializedAs("pitchSpeed")]
        public float PitchSpeed = 45f;
        [FormerlySerializedAs("maxPitch")]
        public float MaxPitch = 90f;
        [FormerlySerializedAs("yawSpeed")]
        public float YawSpeed = 45f;
        [FormerlySerializedAs("rollSpeed")]
        public float RollSpeed = 45f;
        [FormerlySerializedAs("rollBackSpeed")]
        public float RollBackSpeed = 45f;

        [FormerlySerializedAs("maxRoll")]
        public float MaxRoll = 90f;
        [FormerlySerializedAs("boostSpeed")]
        public float BoostSpeed = 150f;

        [Header("Initial Speed")]
        [FormerlySerializedAs("initialSpeed")]
        public float InitialSpeed = 10f;
        [FormerlySerializedAs("minimumVelocity")]
        public float MinimumVelocity = 5f;

        [Header("Wind")]
        [Tooltip("Multiplier for how much the wind affects the physics.")]
        [FormerlySerializedAs("windForceMultiplier")]
        public float WindForceMultiplier = 1.0f;

        [Header("Simulation Speed")]
        [Tooltip("Scales all flight physics by this factor. 2 = twice as fast, 0.5 = half speed. Path is unchanged.")]
        [Min(0.01f)]
        public float SimulationSpeed = 1f;
    }
}
