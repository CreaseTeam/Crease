using UnityEngine;

public class CarouselSpin : MonoBehaviour
{
    public float rotationSpeed = 30f; // Degrees per second

    void Update()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }
}