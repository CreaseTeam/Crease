using UnityEngine;

[CreateAssetMenu(fileName = "FlightSettings", menuName = "Crease/Flight Settings")]
public class FlightSettings : ScriptableObject
{
    [Header("Flight Physics")]
    public float divingGravity = 0.12f;
    public float climbingGravity = 0.04f;
    public float lift = 0.06f;
    public float diveRate = 0.1f;
    public float climbRate = 0.04f;
    public float climbEfficiency = 3.5f;
    public float turnInterpolation = 0.1f;
    public float xDrag = 0.99f;
    public float yDrag = 0.98f;
    public float zDrag = 0.99f;

    [Header("Input Tuning")]
    public float pitchSpeed = 45f;
    public float maxPitch = 90f;
    public float yawSpeed = 45f;
    public float rollSpeed = 45f;
    public float rollBackSpeed = 45f;

    public float maxRoll = 90f;
    public float boostSpeed = 150f;

    [Header("Initial Speed")]
    public float initialSpeed = 10f;
    public float minimumVelocity = 5f;
}
