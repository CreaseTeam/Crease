using UnityEngine;

public class RisingBubbleSpawner : MonoBehaviour
{
    public GameObject bubblePrefab;

    [Header("Spawn Area")]
    public Vector3 spawnAreaSize = new Vector3(6f, 0f, 6f);

    [Header("Spawn Timing")]
    public float spawnInterval = 0.5f;
    public int maxBubbles = 20;

    [Header("Bubble Size")]
    public float minSize = 0.3f;
    public float maxSize = 1.2f;

    private int currentBubbles = 0;

    void Start()
    {
        InvokeRepeating(nameof(SpawnBubble), 0f, spawnInterval);
    }

    void SpawnBubble()
    {
        if (currentBubbles >= maxBubbles) return;

        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f),
            0f,
            Random.Range(-spawnAreaSize.z / 2f, spawnAreaSize.z / 2f)
        );

        GameObject bubble = Instantiate(
            bubblePrefab,
            transform.position + randomOffset,
            Quaternion.identity
        );

        float randomScale = Random.Range(minSize, maxSize);
        bubble.transform.localScale = Vector3.one * randomScale;

        RisingBubble rb = bubble.GetComponent<RisingBubble>();
        if (rb != null)
        {
            rb.spawner = this;
        }

        currentBubbles++;
    }

    public void BubbleDestroyed()
    {
        currentBubbles--;
        currentBubbles = Mathf.Max(currentBubbles, 0);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireCube(transform.position, spawnAreaSize);
    }
}