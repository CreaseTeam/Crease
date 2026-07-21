using System.Collections;
using UnityEngine;

public class WaterParkBucket : MonoBehaviour
{
    [Header("Bucket")]
    [SerializeField] private Transform bucketPivot;

    [Tooltip("Bucket rotation while resting.")]
    [SerializeField] private Vector3 uprightRotation = Vector3.zero;

    [Tooltip("Maximum rotation when pouring.")]
    [SerializeField] private Vector3 pourRotation = new Vector3(0f, 0f, 110f);

    [Header("Cycle Timing")]
    [SerializeField] private float waitBeforePour = 8f;
    [SerializeField] private float tippingDuration = 1.2f;
    [SerializeField] private float pouringDuration = 2.5f;
    [SerializeField] private float returnDuration = 1.4f;

    [Header("Pouring Swing")]
    [Tooltip("How far the bucket swings while pouring.")]
    [SerializeField] private float pouringSwingAngle = 10f;

    [Tooltip("How quickly it swings while pouring.")]
    [SerializeField] private float pouringSwingSpeed = 3f;

    [Header("Return Wobble")]
    [Tooltip("How far it swings after returning upright.")]
    [SerializeField] private float returnWobbleAngle = 12f;

    [SerializeField] private int returnWobbleCount = 3;
    [SerializeField] private float returnWobbleDuration = 1.5f;

    [Header("Water Effects")]
    [SerializeField] private ParticleSystem waterParticles;
    [SerializeField] private GameObject waterStream;

    private Quaternion uprightQuaternion;
    private Quaternion pourQuaternion;

    private void Start()
    {
        if (bucketPivot == null)
        {
            Debug.LogError("Bucket Pivot has not been assigned.", this);
            enabled = false;
            return;
        }

        uprightQuaternion = Quaternion.Euler(uprightRotation);
        pourQuaternion = Quaternion.Euler(pourRotation);

        bucketPivot.localRotation = uprightQuaternion;
        StopWater();

        StartCoroutine(BucketCycle());
    }

    private IEnumerator BucketCycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(waitBeforePour);

            // Tip forward.
            yield return RotateBucket(
                bucketPivot.localRotation,
                pourQuaternion,
                tippingDuration
            );

            StartWater();

            // Swing backward and forward while pouring.
            yield return SwingWhilePouring();

            StopWater();

            // Return toward the upright position.
            yield return RotateBucket(
                bucketPivot.localRotation,
                uprightQuaternion,
                returnDuration
            );

            // Small back-and-forth wobble after returning.
            yield return ReturnWobble();

            bucketPivot.localRotation = uprightQuaternion;
        }
    }

    private IEnumerator SwingWhilePouring()
    {
        float elapsedTime = 0f;

        while (elapsedTime < pouringDuration)
        {
            elapsedTime += Time.deltaTime;

            // Gradually reduce the swing toward the end.
            float remainingStrength =
                1f - Mathf.Clamp01(elapsedTime / pouringDuration);

            float swing =
                Mathf.Sin(elapsedTime * pouringSwingSpeed * Mathf.PI * 2f)
                * pouringSwingAngle
                * remainingStrength;

            // This swings around the bucket's local Z axis.
            bucketPivot.localRotation =
                pourQuaternion * Quaternion.Euler(0f, 0f, swing);

            yield return null;
        }
    }

    private IEnumerator ReturnWobble()
    {
        float elapsedTime = 0f;

        while (elapsedTime < returnWobbleDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsedTime / returnWobbleDuration);

            // The wobble becomes smaller over time.
            float damping = 1f - progress;

            float swing =
                Mathf.Sin(
                    progress *
                    returnWobbleCount *
                    Mathf.PI *
                    2f
                )
                * returnWobbleAngle
                * damping;

            bucketPivot.localRotation =
                uprightQuaternion * Quaternion.Euler(0f, 0f, swing);

            yield return null;
        }

        bucketPivot.localRotation = uprightQuaternion;
    }

    private IEnumerator RotateBucket(
        Quaternion startRotation,
        Quaternion endRotation,
        float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsedTime / duration);

            float smoothProgress =
                progress * progress * (3f - 2f * progress);

            bucketPivot.localRotation = Quaternion.Slerp(
                startRotation,
                endRotation,
                smoothProgress
            );

            yield return null;
        }

        bucketPivot.localRotation = endRotation;
    }

    private void StartWater()
    {
        if (waterStream != null)
        {
            waterStream.SetActive(true);
        }

        if (waterParticles != null)
        {
            waterParticles.Play();
        }
    }

    private void StopWater()
    {
        if (waterStream != null)
        {
            waterStream.SetActive(false);
        }

        if (waterParticles != null)
        {
            waterParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting
            );
        }
    }
}