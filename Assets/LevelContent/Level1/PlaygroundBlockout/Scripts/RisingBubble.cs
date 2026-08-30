using UnityEngine;

public class RisingBubble : MonoBehaviour
{
    [HideInInspector] public RisingBubbleSpawner spawner;

    [Header("Rise Motion")]
    public float riseAmount = 4f;
    public float riseSpeed = 6f;

    [Header("Floating Motion")]
    public float floatHeight = 0.25f;
    public float floatSpeed = 1f;

    [Header("Lifetime After Rising")]
    public float floatLifetime = 3f;

    [Header("Pop")]
    public GameObject popEffect;
    public string playerTag = "Player";

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private bool hasReachedTop = false;
    private bool destroyed = false;
    private float floatTimer = 0f;

    void Start()
    {
        startPosition = transform.position;
        targetPosition = startPosition + Vector3.up * riseAmount;
    }

    void Update()
    {
        if (!hasReachedTop)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                riseSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                hasReachedTop = true;
                startPosition = transform.position;
            }
        }
        else
        {
            floatTimer += Time.deltaTime;

            float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            if (floatTimer >= floatLifetime)
            {
                Pop();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            Pop();
        }
    }

    void Pop()
    {
        if (destroyed) return;
        destroyed = true;

        if (popEffect != null)
        {
            Instantiate(popEffect, transform.position, Quaternion.identity);
        }

        if (spawner != null)
        {
            spawner.BubbleDestroyed();
        }

        Destroy(gameObject);
    }
}