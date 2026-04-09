using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform segmentContainer;
    [SerializeField] private RectTransform barRoot;
    [SerializeField] private GameObject segmentPrefab;

    [Header("Settings")]
    private float BarPixelWidth => barRoot != null ? barRoot.rect.width : 0f;

    private static readonly Dictionary<DamageType, Color> SegmentColors = new()
    {
        { DamageType.Impact, new Color(0.9f, 0.2f, 0.2f) },
        { DamageType.Fire,   new Color(1f,   0.5f, 0f)   },
        { DamageType.Tear, new Color(0.3f, 0.9f, 0.3f) },
        { DamageType.Nasty,   new Color(0.6f, 0.4f, 1f)   },
        { DamageType.Water,  new Color(0.8f, 0f,   0.3f) },
    };

    private readonly Dictionary<DamageType, HealthSegment> _segmentUIs = new();

    public void HandleDamage(DamageType type, float normalizedTotal)
    {
        float barWidth = BarPixelWidth;
        float targetWidth = normalizedTotal * barWidth;
        Debug.Log($"HealthBar.HandleDamaged: type={type}, normalizedTotal={normalizedTotal}, barWidth={barWidth}, targetWidth={targetWidth}");

        if (_segmentUIs.TryGetValue(type, out HealthSegment existing))
        {
            Debug.Log($"HealthBar updating existing segment for {type}");
            existing.AnimateToWidth(targetWidth);
        }
        else
        {
            Debug.Log($"HealthBar creating new segment for {type}");
            var go = Instantiate(segmentPrefab, segmentContainer);
            var segUI = go.GetComponent<HealthSegment>();
            segUI.SetColor(SegmentColors[type]);
            segUI.SetWidth(targetWidth);
            segUI.PlaySpawnAnimation();
            _segmentUIs[type] = segUI;
            Debug.Log($"HealthBar now has {_segmentUIs.Count} segments");
        }

        StartCoroutine(ShakeBar());
    }

    public void HandleHeal(float normalizedTotal, DamageType? type = null)
    {
        float barWidth = BarPixelWidth;
        float targetWidth = normalizedTotal * barWidth;

        if (type.HasValue)
        {
            if (_segmentUIs.TryGetValue(type.Value, out HealthSegment existing))
            {
                existing.AnimateToWidth(targetWidth);
            }
        }
        else
        {
            if (segmentContainer.childCount > 0)
            {
                HealthSegment first = segmentContainer.GetChild(0).GetComponent<HealthSegment>();
                if (first != null) first.AnimateToWidth(targetWidth);
            }
        }
    }

    private IEnumerator ShakeBar()
    {
        Vector3 origin = barRoot.anchoredPosition3D;
        Debug.Log("HealthBar.ShakeBar start");
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / 0.25f;
            float offset = Mathf.Sin(t * 80f) * 5f * (1f - t);
            barRoot.anchoredPosition3D = origin + new Vector3(offset, 0f, 0f);
            yield return null;
        }

        barRoot.anchoredPosition3D = origin;
        Debug.Log("HealthBar.ShakeBar end");
    }
}