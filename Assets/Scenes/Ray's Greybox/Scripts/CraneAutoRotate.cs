using UnityEngine;

public class CraneAutoRotate : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 1f;
    [SerializeField] private float maxAngle = 45f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [SerializeField] private int direction = 1; // 1 or -1

    private Quaternion initialRotation;

    void Start()
    {
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        float angle = Mathf.PingPong(Time.time * rotationSpeed, maxAngle * 2f) - maxAngle;

        transform.localRotation =
            initialRotation *
            Quaternion.AngleAxis(angle * direction, rotationAxis.normalized);
    }
}