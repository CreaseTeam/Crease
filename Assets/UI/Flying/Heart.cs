using UnityEngine;
using UnityEngine.UI;

public class Heart : MonoBehaviour
{
    public Sprite heartImage;
    public Sprite brokenHeartImage;

    private Image heartRenderer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        heartRenderer = GetComponent<Image>();

        if (heartImage != null)
        {
            heartRenderer.sprite = heartImage;
        }
    }

    public void SetHealth(bool isHealthy)
    {
        if (heartRenderer != null)
        {
            heartRenderer.sprite = isHealthy ? heartImage : brokenHeartImage;
        }
    }
}
