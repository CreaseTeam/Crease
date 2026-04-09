using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform segmentContainer;
    [SerializeField] private RectTransform barRoot;
    [SerializeField] private GameObject segmentPrefab;
    [SerializeField] private HealthSegment remainingHealthSegment;

    [Header("Settings")]
    private float BarPixelWidth => barRoot != null ? barRoot.rect.width : 0f;

    [System.Serializable]
    private struct DamageColorEntry
    {
        public DamageType type;
        public Color color;
    }

    [Header("Colors")]
    [SerializeField]
    private List<DamageColorEntry> segmentColorEntries = new()
    {
        new DamageColorEntry { type = DamageType.Impact, color = new Color(0.9f, 0.2f, 0.2f) },
        new DamageColorEntry { type = DamageType.Fire,   color = new Color(1f,   0.5f, 0f) },
        new DamageColorEntry { type = DamageType.Tear,   color = new Color(0.3f, 0.9f, 0.3f) },
        new DamageColorEntry { type = DamageType.Nasty,  color = new Color(0.6f, 0.4f, 1f) },
        new DamageColorEntry { type = DamageType.Water,  color = new Color(0.8f, 0f,   0.3f) },
    };

    private readonly Dictionary<DamageType, Color> _segmentColors = new();
    private readonly Dictionary<DamageType, HealthSegment> _segmentUIs = new();
    private readonly Dictionary<DamageType, float> _normalizedDamageByType = new();

    public void HandleDamage(DamageType type, float normalizedTotal)
    {
        bool isNew = !_segmentUIs.ContainsKey(type);
        _normalizedDamageByType[type] = normalizedTotal;

        if (isNew)
        {
            var go = Instantiate(segmentPrefab, segmentContainer);
            var segUI = go.GetComponent<HealthSegment>();
            segUI.SetColor(GetColorFor(type));
            segUI.PlaySpawnAnimation();
            _segmentUIs[type] = segUI;
        }

        UpdateRemainingHealthState();
        UpdateAllSegmentWidths(isNew ? type : null);

        StartCoroutine(ShakeBar());
    }

    private Color GetColorFor(DamageType type)
    {
        if (_segmentColors.TryGetValue(type, out Color c)) return c;

        // Build map lazily if empty
        if (_segmentColors.Count == 0 && segmentColorEntries != null)
        {
            foreach (var e in segmentColorEntries)
            {
                if (!_segmentColors.ContainsKey(e.type))
                    _segmentColors[e.type] = e.color;
            }

            if (_segmentColors.TryGetValue(type, out c)) return c;
        }

        return Color.white;
    }

    public void HandleHeal(float normalizedTotal, DamageType? type = null)
    {
        if (type.HasValue)
        {
            if (normalizedTotal <= 0f) {
                _normalizedDamageByType.Remove(type.Value);
            } else {
                _normalizedDamageByType[type.Value] = normalizedTotal;
            }

            if (_segmentUIs.TryGetValue(type.Value, out HealthSegment existing))
            {
                if (normalizedTotal <= 0f)
                {
                    // Animate to zero then destroy the segment, and remove mapping
                    existing.AnimateToWidth(0f);
                    Destroy(existing.gameObject, 0.2f);
                    _segmentUIs.Remove(type.Value);
                    Debug.Log($"HealthBar removed segment for {type.Value}");
                }
            }
        }
        else
        {
            if (segmentContainer.childCount > 0)
            {
                HealthSegment first = segmentContainer.GetChild(0).GetComponent<HealthSegment>();
                if (first != null)
                {
                    DamageType targetType = DamageType.Impact;
                    foreach (var kv in _segmentUIs)
                    {
                        if (kv.Value == first)
                        {
                            targetType = kv.Key;
                            break;
                        }
                    }

                    if (normalizedTotal <= 0f) {
                        _normalizedDamageByType.Remove(targetType);
                    } else {
                        _normalizedDamageByType[targetType] = normalizedTotal;
                    }

                    if (normalizedTotal <= 0f)
                    {
                        first.AnimateToWidth(0f);
                        Destroy(first.gameObject, 0.2f);
                        // Try to remove from dictionary if present
                        _segmentUIs.Remove(targetType);
                    }
                }
            }
        }

        UpdateRemainingHealthState();
        UpdateAllSegmentWidths();
    }

    private float Spacing
    {
        get
        {
            if (segmentContainer != null)
            {
                var hlg = segmentContainer.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                if (hlg != null) return hlg.spacing;
            }
            return 0f;
        }
    }

    private float PaddingWidth
    {
        get
        {
            if (segmentContainer != null)
            {
                var hlg = segmentContainer.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                if (hlg != null && hlg.padding != null) return hlg.padding.left + hlg.padding.right;
            }
            return 0f;
        }
    }

    private void UpdateAllSegmentWidths(DamageType? typeToSetInstantly = null)
    {
        int activeSegmentsCount = _segmentUIs.Count;
        if (remainingHealthSegment != null && remainingHealthSegment.gameObject.activeSelf)
        {
            activeSegmentsCount++;
        }

        float totalSpacing = Mathf.Max(0, activeSegmentsCount - 1) * Spacing;
        float totalPadding = PaddingWidth;
        float availableWidth = Mathf.Max(0f, BarPixelWidth - totalSpacing - totalPadding);

        foreach (var kvp in _segmentUIs)
        {
            if (!_normalizedDamageByType.TryGetValue(kvp.Key, out float normAmount))
                continue;

            float target = normAmount * availableWidth;
            
            if (typeToSetInstantly.HasValue && typeToSetInstantly.Value == kvp.Key)
            {
                kvp.Value.SetWidth(target);
            }
            else
            {
                kvp.Value.AnimateToWidth(target);
            }
        }
    }

    private void UpdateRemainingHealthState()
    {
        if (remainingHealthSegment == null) return;

        float totalDamage = 0f;
        foreach (var damage in _normalizedDamageByType.Values)
        {
            totalDamage += damage;
        }

        bool hasRemainingHealth = totalDamage <= 0.999f;
        
        if (remainingHealthSegment.gameObject.activeSelf != hasRemainingHealth)
        {
            remainingHealthSegment.gameObject.SetActive(hasRemainingHealth);
        }
    }

    private IEnumerator ShakeBar()
    {
        Vector3 origin = barRoot.anchoredPosition3D;
        // Debug.Log("HealthBar.ShakeBar start");
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / 0.25f;
            float offset = Mathf.Sin(t * 80f) * 5f * (1f - t);
            barRoot.anchoredPosition3D = origin + new Vector3(offset, 0f, 0f);
            yield return null;
        }

        barRoot.anchoredPosition3D = origin;
        // Debug.Log("HealthBar.ShakeBar end");
    }
}