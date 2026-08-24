using UnityEngine;

public class LotusFlower : MonoBehaviour
{
    [Header("Petals")]
    [SerializeField] private Transform[] petals;

    [Header("Rotation")]
    [SerializeField] private float closedAngle = 65f;
    [SerializeField] private float rotationSpeed = 3f;

    [Header("Wind")]
    [SerializeField] private GameObject windFrustum;

    private Quaternion[] openRotations;
    private Quaternion[] closedRotations;

    private bool playerNearby = false;

    private void Start()
    {
        openRotations = new Quaternion[petals.Length];
        closedRotations = new Quaternion[petals.Length];

        for (int i = 0; i < petals.Length; i++)
        {
            openRotations[i] = petals[i].localRotation;

            closedRotations[i] =
                openRotations[i] *
                Quaternion.Euler(-closedAngle, 0f, 0f);
        }

        // Wind starts ON
        if (windFrustum != null)
        {
            windFrustum.SetActive(true);
        }
    }

    private void Update()
    {
        for (int i = 0; i < petals.Length; i++)
        {
            Quaternion targetRotation =
                playerNearby
                ? closedRotations[i]
                : openRotations[i];

            petals[i].localRotation = Quaternion.Slerp(
                petals[i].localRotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = true;

            // Player approaches → turn wind OFF
            if (windFrustum != null)
            {
                windFrustum.SetActive(false);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = false;

            // Player leaves → turn wind back ON
            if (windFrustum != null)
            {
                windFrustum.SetActive(true);
            }
        }
    }
}