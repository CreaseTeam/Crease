using UnityEngine;
using System.Collections;

public class Dragon : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float rotateDuration = 3.0f;
    [SerializeField] private float pauseDuration = 1f;

    [SerializeField] private float range = 180f;

    [Header("Fire Object")]
    [SerializeField] private GameObject fireObject; // Assign your child fire object

    private Quaternion rotationA;
    private Quaternion rotationB;

    void Start()
    {
        rotationA = transform.rotation;
        rotationB = transform.rotation * Quaternion.Euler(0f, 0f, range);  // Rotate around Z

        if (fireObject != null)
            fireObject.SetActive(false);

        StartCoroutine(RotateLoop());
    }

    IEnumerator RotateLoop()
    {
        while (true)
        {
            // Turn fire ON and rotate forward (A -> B)
            SetFire(true);
            yield return RotateOverTime(rotationA, rotationB, rotateDuration);

            // Turn fire OFF and pause
            SetFire(false);
            yield return new WaitForSeconds(pauseDuration);

            // Turn fire ON and rotate back (B -> A)
            SetFire(true);
            yield return RotateOverTime(rotationB, rotationA, rotateDuration);

            // Turn fire OFF and pause
            SetFire(false);
            yield return new WaitForSeconds(pauseDuration);
        }
    }

    IEnumerator RotateOverTime(Quaternion startRot, Quaternion endRot, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            // Smooth easing in/out
            float easedT = Mathf.SmoothStep(0f, 1f, t / duration);

            transform.rotation = Quaternion.Slerp(startRot, endRot, easedT);
            yield return null;
        }

        transform.rotation = endRot;
    }

    void SetFire(bool on)
    {
        if (fireObject != null)
            fireObject.SetActive(on);
    }
}
