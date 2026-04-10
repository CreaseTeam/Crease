using UnityEngine;

[CreateAssetMenu(fileName = "CameraSettings", menuName = "Crease/Camera Settings")]
public class CameraSettings : ScriptableObject
{
    [Header("Offset")]
    [Tooltip("Offset behind and above the plane (in rig-local space).")]
    public Vector3 defaultOffset = new Vector3(0f, 2f, -8f);

    [Header("Camera Zoom")]
    [Tooltip("How fast the camera zooms in/out per scroll tick.")]
    public float zoomSpeed = 2f;
    public float minZoomOffset = -3f;
    public float maxZoomOffset = -20f;

    [Header("Camera Panning (Orbital)")]
    [Tooltip("Maximum horizontal angle the camera can orbit.")]
    public float maxPanAngleX = 30f;
    [Tooltip("Maximum vertical angle the camera can orbit.")]
    public float maxPanAngleY = 20f;
    [Tooltip("Speed at which the camera reaches the target pan angle (degrees per second).")]
    public float panSpeed = 90f;

    [Header("Mouse")]
    [Tooltip("Mouse sensitivity for camera panning. Higher values make small mouse movements produce larger pan. Edges still map to full -1..1.")]
    [Min(0.01f)]
    public float mouseSensitivity = 1f;

    [Header("Follow Speeds")]
    public float yawSpeed = 5f;
    public float pitchSpeed = 5f;
    public float positionSmoothing = 10f;

    [Header("Pitch Profile (Velocity-Driven)")]
    public float profileStrength = 0.25f;
    public float maxProfileOffset = 30f;
    public float pitchRateSmoothing = 8f;
    public float profileDecay = 3f;

    [Header("Look At")]
    public float lookAheadDistance = 5f;
    public float lookSmoothing = 8f;
    [Range(0f, 1f)]
    public float lookAheadBlend = 0.5f;

    [Header("Horizon Stabilization")]
    [Range(0f, 1f)]
    public float horizonRollStabilization = 0.85f;
}
