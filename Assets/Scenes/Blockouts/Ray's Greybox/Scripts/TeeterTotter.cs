using UnityEngine;

public class TeeterTotter : MonoBehaviour
{
    [Header("Rocking")]
    [SerializeField] private float maxAngle = 15f;
    [SerializeField] private float speed = 0.25f;

    [Header("Axis")]
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;

    private Quaternion startRotation;

    private void Start()
    {
        startRotation = transform.localRotation;
    }

    private void Update()
    {
        float angle = Mathf.Sin(Time.time * speed * Mathf.PI * 2f) * maxAngle;

        transform.localRotation =
            startRotation * Quaternion.AngleAxis(angle, rotationAxis);
    }
}