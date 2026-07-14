using UnityEngine;

public class BubbleSpawner : MonoBehaviour
{
    [Header("Bubble Prefab")]
    public GameObject bubblePrefab;

    [Header("Spawn Area")]
    public Vector3 spawnAreaSize = new Vector3(10f, 5f, 10f);

    [Header("Spawn Timing")]
    public float spawnInterval = 1f;
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
            Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f),
            Random.Range(-spawnAreaSize.z / 2f, spawnAreaSize.z / 2f)
        );

        Vector3 spawnPosition = transform.position + randomOffset;

        GameObject bubble = Instantiate(bubblePrefab, spawnPosition, Quaternion.identity);

        float randomScale = Random.Range(minSize, maxSize);
        bubble.transform.localScale = Vector3.one * randomScale;

        Bubble bubbleScript = bubble.GetComponent<Bubble>();
        if (bubbleScript != null)
        {
            bubbleScript.spawner = this;
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