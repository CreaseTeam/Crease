using System.Collections;
using Crease.Events;
using UnityEngine;

public class WaterParkBucket : MonoBehaviour
{
    [Header("Bucket")]
    [SerializeField] private Transform bucketPivot;

    [Tooltip("Bucket rotation while resting.")]
    [SerializeField] private Vector3 uprightRotation = Vector3.zero;

    [Tooltip("Maximum rotation when pouring.")]
    [SerializeField]
    private Vector3 pourRotation =
        new Vector3(0f, 0f, 110f);

    [Header("Cycle Timing")]
    [SerializeField] private float waitBeforePour = 8f;
    [SerializeField] private float tippingDuration = 1.2f;
    [SerializeField] private float pouringDuration = 2.5f;
    [SerializeField] private float returnDuration = 1.4f;

    [Tooltip("When water starts during tipping. 0 = immediately, 0.5 = halfway, 1 = fully tipped.")]
    [Range(0f, 1f)]
    [SerializeField] private float waterStartTipProgress = 0.65f;

    [Header("Pouring Swing")]
    [Tooltip("How far the bucket swings while pouring.")]
    [SerializeField] private float pouringSwingAngle = 10f;

    [Tooltip("How quickly the bucket swings while pouring.")]
    [SerializeField] private float pouringSwingSpeed = 3f;

    [Header("Return Wobble")]
    [Tooltip("How far the bucket swings after returning upright.")]
    [SerializeField] private float returnWobbleAngle = 12f;

    [SerializeField] private int returnWobbleCount = 3;
    [SerializeField] private float returnWobbleDuration = 1.5f;

    [Header("Sphere Water")]
    [Tooltip("Place this empty GameObject near the bucket opening.")]
    [SerializeField] private Transform pourPoint;

    [Tooltip("Optional sphere prefab. Leave empty to use default Unity spheres.")]
    [SerializeField] private GameObject waterSpherePrefab;

    [Tooltip("How many spheres spawn each second.")]
    [SerializeField] private float spheresPerSecond = 25f;

    [Tooltip("Minimum sphere size.")]
    [SerializeField] private float minimumSphereSize = 0.25f;

    [Tooltip("Maximum sphere size.")]
    [SerializeField] private float maximumSphereSize = 0.45f;

    [Tooltip("How fast spheres leave the bucket.")]
    [SerializeField] private float pourForce = 2f;

    [Tooltip("Random sideways movement.")]
    [SerializeField] private float spread = 0.35f;

    [Tooltip("How far in front of the Pour Point spheres appear.")]
    [SerializeField] private float spawnForwardOffset = 0.3f;

    [Tooltip("How long spheres stay before being destroyed.")]
    [SerializeField] private float sphereLifetime = 6f;

    [Tooltip("Maximum number of spheres allowed at once.")]
    [SerializeField] private int maximumActiveSpheres = 150;

    [Header("Sphere Physics")]
    [SerializeField] private float sphereMass = 0.05f;
    [SerializeField] private float linearDamping = 0.05f;
    [SerializeField] private float angularDamping = 0.05f;
    [SerializeField] private bool useGravity = true;

    [Header("Sphere Appearance")]
    [Tooltip("Color used when no sphere prefab is assigned.")]
    [SerializeField]
    private Color sphereColor =
        new Color(0.1f, 0.5f, 1f, 1f);

    [Header("Optional Water Effects")]
    [SerializeField] private ParticleSystem waterParticles;
    [SerializeField] private GameObject waterStream;

    private Quaternion uprightQuaternion;
    private Quaternion pourQuaternion;

    private Coroutine spherePourCoroutine;
    private Material generatedSphereMaterial;

    private bool isPouring;
    private int activeSphereCount;

    private void Start()
    {
        if (bucketPivot == null)
        {
            Debug.LogError(
                "Bucket Pivot has not been assigned.",
                this
            );

            enabled = false;
            return;
        }

        if (pourPoint == null)
        {
            Debug.LogWarning(
                "Pour Point has not been assigned. " +
                "Sphere water will not spawn.",
                this
            );
        }

        uprightQuaternion = Quaternion.Euler(uprightRotation);
        pourQuaternion = Quaternion.Euler(pourRotation);

        bucketPivot.localRotation = uprightQuaternion;

        CreateSphereMaterial();
        StopWater();

        StartCoroutine(BucketCycle());
    }

    private void CreateSphereMaterial()
    {
        Shader selectedShader = null;

        // Try shaders commonly used by different Unity render pipelines.
        selectedShader = Shader.Find(
            "Universal Render Pipeline/Lit"
        );

        if (selectedShader == null)
        {
            selectedShader = Shader.Find("Standard");
        }

        if (selectedShader == null)
        {
            selectedShader = Shader.Find(
                "HDRP/Lit"
            );
        }

        if (selectedShader == null)
        {
            Debug.LogWarning(
                "Could not find a supported shader for water spheres.",
                this
            );

            return;
        }

        generatedSphereMaterial =
            new Material(selectedShader);

        generatedSphereMaterial.color = sphereColor;
        generatedSphereMaterial.name =
            "Generated Water Sphere Material";
    }

    private IEnumerator BucketCycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                waitBeforePour
            );

            // Tip the bucket forward and begin pouring at the selected point.
            GameEvents.OnWaterBucketTipped?.Invoke();
            yield return TipBucketAndStartWater();

            // Swing while pouring.
            yield return SwingWhilePouring();

            StopWater();

            // Return upright.
            yield return RotateBucket(
                bucketPivot.localRotation,
                uprightQuaternion,
                returnDuration
            );

            // Wobble after returning.
            yield return ReturnWobble();

            bucketPivot.localRotation =
                uprightQuaternion;
        }
    }

    private IEnumerator TipBucketAndStartWater()
    {
        Quaternion startRotation =
            bucketPivot.localRotation;

        float elapsedTime = 0f;
        bool waterStarted = false;

        if (tippingDuration <= 0f)
        {
            bucketPivot.localRotation =
                pourQuaternion;

            StartWater();
            yield break;
        }

        while (elapsedTime < tippingDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsedTime / tippingDuration
                );

            float smoothProgress =
                progress *
                progress *
                (3f - 2f * progress);

            bucketPivot.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    pourQuaternion,
                    smoothProgress
                );

            if (
                !waterStarted &&
                progress >= waterStartTipProgress
            )
            {
                StartWater();
                waterStarted = true;
            }

            yield return null;
        }

        bucketPivot.localRotation =
            pourQuaternion;

        // Safety fallback if the selected point was not reached.
        if (!waterStarted)
        {
            StartWater();
        }
    }

    private IEnumerator SwingWhilePouring()
    {
        float elapsedTime = 0f;

        while (elapsedTime < pouringDuration)
        {
            elapsedTime += Time.deltaTime;

            float remainingStrength =
                1f - Mathf.Clamp01(
                    elapsedTime / pouringDuration
                );

            float swing =
                Mathf.Sin(
                    elapsedTime *
                    pouringSwingSpeed *
                    Mathf.PI *
                    2f
                )
                * pouringSwingAngle
                * remainingStrength;

            bucketPivot.localRotation =
                pourQuaternion *
                Quaternion.Euler(
                    0f,
                    0f,
                    swing
                );

            yield return null;
        }

        bucketPivot.localRotation =
            pourQuaternion;
    }

    private IEnumerator ReturnWobble()
    {
        float elapsedTime = 0f;

        while (elapsedTime < returnWobbleDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsedTime /
                    returnWobbleDuration
                );

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
                uprightQuaternion *
                Quaternion.Euler(
                    0f,
                    0f,
                    swing
                );

            yield return null;
        }

        bucketPivot.localRotation =
            uprightQuaternion;
    }

    private IEnumerator RotateBucket(
        Quaternion startRotation,
        Quaternion endRotation,
        float duration)
    {
        if (duration <= 0f)
        {
            bucketPivot.localRotation =
                endRotation;

            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsedTime / duration
                );

            float smoothProgress =
                progress *
                progress *
                (3f - 2f * progress);

            bucketPivot.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    endRotation,
                    smoothProgress
                );

            yield return null;
        }

        bucketPivot.localRotation =
            endRotation;
    }

    private void StartWater()
    {
        isPouring = true;

        if (waterStream != null)
        {
            waterStream.SetActive(true);
        }

        if (waterParticles != null)
        {
            waterParticles.Play();
        }

        if (
            pourPoint != null &&
            spherePourCoroutine == null
        )
        {
            spherePourCoroutine =
                StartCoroutine(
                    SpawnWaterSpheres()
                );
        }
    }

    private void StopWater()
    {
        isPouring = false;

        if (spherePourCoroutine != null)
        {
            StopCoroutine(
                spherePourCoroutine
            );

            spherePourCoroutine = null;
        }

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

    private IEnumerator SpawnWaterSpheres()
    {
        float spawnRate =
            Mathf.Max(1f, spheresPerSecond);

        float spawnInterval =
            1f / spawnRate;

        float spawnTimer = 0f;

        while (isPouring)
        {
            spawnTimer += Time.deltaTime;

            while (
                spawnTimer >= spawnInterval &&
                activeSphereCount <
                maximumActiveSpheres
            )
            {
                SpawnSphere();
                spawnTimer -= spawnInterval;
            }

            yield return null;
        }

        spherePourCoroutine = null;
    }

    private void SpawnSphere()
    {
        if (pourPoint == null)
        {
            return;
        }

        Vector3 spawnPosition =
            pourPoint.position +
            pourPoint.forward *
            spawnForwardOffset;

        GameObject sphere;

        if (waterSpherePrefab != null)
        {
            sphere = Instantiate(
                waterSpherePrefab,
                spawnPosition,
                Random.rotation
            );
        }
        else
        {
            sphere =
                GameObject.CreatePrimitive(
                    PrimitiveType.Sphere
                );

            sphere.transform.position =
                spawnPosition;

            sphere.transform.rotation =
                Random.rotation;

            Renderer sphereRenderer =
                sphere.GetComponent<Renderer>();

            if (
                sphereRenderer != null &&
                generatedSphereMaterial != null
            )
            {
                sphereRenderer.sharedMaterial =
                    generatedSphereMaterial;
            }
        }

        sphere.name = "Water Sphere";
        sphere.layer = gameObject.layer;

        float randomSize =
            Random.Range(
                minimumSphereSize,
                maximumSphereSize
            );

        sphere.transform.localScale =
            Vector3.one * randomSize;

        Rigidbody sphereRigidbody =
            sphere.GetComponent<Rigidbody>();

        if (sphereRigidbody == null)
        {
            sphereRigidbody =
                sphere.AddComponent<Rigidbody>();
        }

        sphereRigidbody.mass =
            sphereMass;

        sphereRigidbody.useGravity =
            useGravity;

#if UNITY_6000_0_OR_NEWER
        sphereRigidbody.linearDamping =
            linearDamping;

        sphereRigidbody.angularDamping =
            angularDamping;
#else
        sphereRigidbody.drag =
            linearDamping;

        sphereRigidbody.angularDrag =
            angularDamping;
#endif

        sphereRigidbody.collisionDetectionMode =
            CollisionDetectionMode.Continuous;

        sphereRigidbody.interpolation =
            RigidbodyInterpolation.Interpolate;

        Vector3 randomSpread =
            pourPoint.right *
            Random.Range(-spread, spread) +
            pourPoint.up *
            Random.Range(-spread, spread);

        Vector3 pourVelocity =
            pourPoint.forward *
            pourForce +
            randomSpread;

#if UNITY_6000_0_OR_NEWER
        sphereRigidbody.linearVelocity =
            pourVelocity;
#else
        sphereRigidbody.velocity =
            pourVelocity;
#endif

        activeSphereCount++;

        StartCoroutine(
            DestroySphereAfterDelay(
                sphere,
                sphereLifetime
            )
        );
    }

    private IEnumerator DestroySphereAfterDelay(
        GameObject sphere,
        float delay)
    {
        yield return new WaitForSeconds(delay);

        if (sphere != null)
        {
            Destroy(sphere);
        }

        activeSphereCount =
            Mathf.Max(
                0,
                activeSphereCount - 1
            );
    }

    private void OnDisable()
    {
        isPouring = false;

        if (spherePourCoroutine != null)
        {
            StopCoroutine(
                spherePourCoroutine
            );

            spherePourCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        if (generatedSphereMaterial != null)
        {
            Destroy(generatedSphereMaterial);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (pourPoint == null)
        {
            return;
        }

        Vector3 spawnPosition =
            pourPoint.position +
            pourPoint.forward *
            spawnForwardOffset;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(
            spawnPosition,
            0.12f
        );

        Gizmos.DrawLine(
            spawnPosition,
            spawnPosition +
            pourPoint.forward
        );
    }
}