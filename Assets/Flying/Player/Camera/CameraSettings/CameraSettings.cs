using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player.Camera
{
    [CreateAssetMenu(fileName = "CameraSettings", menuName = "Crease/Camera Settings")]
    public class CameraSettings : ScriptableObject
    {
        [Header("Offset")]
        [Tooltip("Offset behind and above the plane (in rig-local space).")]
        [FormerlySerializedAs("defaultOffset")]
        public Vector3 DefaultOffset = new Vector3(0f, 2f, -8f);

        [Header("Camera Zoom")]
        [Tooltip("How fast the camera zooms in/out per scroll tick.")]
        [FormerlySerializedAs("zoomSpeed")]
        public float ZoomSpeed = 2f;
        [FormerlySerializedAs("minZoomOffset")]
        public float MinZoomOffset = -3f;
        [FormerlySerializedAs("maxZoomOffset")]
        public float MaxZoomOffset = -20f;

        [Header("Camera Panning (Orbital)")]
        [Tooltip("Maximum horizontal angle the camera can orbit.")]
        [FormerlySerializedAs("maxPanAngleX")]
        public float MaxPanAngleX = 30f;
        [Tooltip("Maximum vertical angle the camera can orbit.")]
        [FormerlySerializedAs("maxPanAngleY")]
        public float MaxPanAngleY = 20f;
        [Tooltip("Speed at which the camera reaches the target pan angle (degrees per second).")]
        [FormerlySerializedAs("panSpeed")]
        public float PanSpeed = 90f;

        [Header("Mouse")]
        [Tooltip("Mouse sensitivity for camera panning. Higher values make small mouse movements produce larger pan. Edges still map to full -1..1.")]
        [Min(0.01f)]
        [FormerlySerializedAs("mouseSensitivity")]
        public float MouseSensitivity = 1f;
        [Tooltip("Radial deadzone for mouse panning in normalized screen space (0 = no deadzone, 1 = full).")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("mouseDeadzone")]
        public float MouseDeadzone = 0.05f;
        [Tooltip("Time in seconds before the camera auto-centers when mouse is inside the deadzone.")]
        [FormerlySerializedAs("mouseInactivityTimeout")]
        public float MouseInactivityTimeout = 2f;

        [Header("Follow Speeds")]
        [FormerlySerializedAs("yawSpeed")]
        public float YawSpeed = 5f;
        [FormerlySerializedAs("pitchSpeed")]
        public float PitchSpeed = 5f;
        [FormerlySerializedAs("positionSmoothing")]
        public float PositionSmoothing = 10f;

        [Header("Pitch Profile (Velocity-Driven)")]
        [FormerlySerializedAs("profileStrength")]
        public float ProfileStrength = 0.25f;
        [FormerlySerializedAs("maxProfileOffset")]
        public float MaxProfileOffset = 30f;
        [FormerlySerializedAs("pitchRateSmoothing")]
        public float PitchRateSmoothing = 8f;
        [FormerlySerializedAs("profileDecay")]
        public float ProfileDecay = 3f;

        [Header("Look At")]
        [FormerlySerializedAs("lookAheadDistance")]
        public float LookAheadDistance = 5f;
        [FormerlySerializedAs("lookSmoothing")]
        public float LookSmoothing = 8f;
        [Range(0f, 1f)]
        [FormerlySerializedAs("lookAheadBlend")]
        public float LookAheadBlend = 0.5f;

        [Header("Horizon Stabilization")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("horizonRollStabilization")]
        public float HorizonRollStabilization = 0.85f;
    }
}
