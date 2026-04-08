using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HealthSegment : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private LayoutElement layoutElement;

    public void SetWidth(float pixelWidth) =>
        layoutElement.preferredWidth = pixelWidth;

    public void AnimateToWidth(float targetWidth) =>
        StartCoroutine(WidthRoutine(targetWidth));

    public void PlaySpawnAnimation() =>
        StartCoroutine(FlashRoutine());

    private IEnumerator WidthRoutine(float targetWidth)
    {
        float startWidth = layoutElement.preferredWidth;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / 0.12f;
            layoutElement.preferredWidth = Mathf.Lerp(startWidth, targetWidth, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        layoutElement.preferredWidth = targetWidth;
    }

    private IEnumerator FlashRoutine()
    {
        Color baseColor = fill.color;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / 0.15f;
            fill.color = Color.Lerp(Color.white, baseColor, t);
            yield return null;
        }

        fill.color = baseColor;
    }

    public void SetColor(Color color) => fill.color = color;
}