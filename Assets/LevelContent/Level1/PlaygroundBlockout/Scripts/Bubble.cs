using UnityEngine;

public class Bubble : MonoBehaviour
{
    [HideInInspector] public BubbleSpawner spawner;

    [Header("Lifetime")]
    public float lifetime = 5f;

    [Header("Floating Motion")]
    public float minFloatSpeed = 0.6f;
    public float maxFloatSpeed = 1.4f;
    public float floatHeightMultiplier = 0.4f;

    [Header("Pop Effect")]
    public GameObject popEffect;

    private Vector3 startPosition;
    private float floatSpeed;
    private float floatHeight;
    private bool destroyed = false;

    void Start()
    {
        startPosition = transform.position;

        float size = transform.localScale.x;

        float normalizedSize = Mathf.InverseLerp(0.3f, 1.2f, size);

        floatSpeed = Mathf.Lerp(maxFloatSpeed, minFloatSpeed, normalizedSize);
        floatHeight = size * floatHeightMultiplier;

        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;

        transform.position = new Vector3(
            transform.position.x,
            newY,
            transform.position.z
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
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

    private void OnDestroy()
    {
        if (!destroyed && spawner != null)
        {
            spawner.BubbleDestroyed();
        }
    }
}