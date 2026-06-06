using System.Collections;
using System.Collections.Generic;
using Crease.Flying.Player.Health;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Crease.UI.Flying
{
    public class HealthBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [FormerlySerializedAs("segmentContainer")]
        private RectTransform _segmentContainer;
        [SerializeField]
        [FormerlySerializedAs("barRoot")]
        private RectTransform _barRoot;
        [SerializeField]
        [FormerlySerializedAs("segmentPrefab")]
        private GameObject _segmentPrefab;
        [SerializeField]
        [FormerlySerializedAs("remainingHealthSegment")]
        private HealthSegment _remainingHealthSegment;

        [Header("Settings")]
        private float BarPixelWidth => _barRoot != null ? _barRoot.rect.width : 0f;

        [System.Serializable]
        private struct DamageColorEntry
        {
            [FormerlySerializedAs("type")]
            public DamageType Type;
            [FormerlySerializedAs("color")]
            public Color Color;
        }

        [Header("Colors")]
        [SerializeField]
        [FormerlySerializedAs("segmentColorEntries")]
        private List<DamageColorEntry> _segmentColorEntries = new()
        {
            new DamageColorEntry { Type = DamageType.Impact, Color = new Color(0.9f, 0.2f, 0.2f) },
            new DamageColorEntry { Type = DamageType.Fire, Color = new Color(1f, 0.5f, 0f) },
            new DamageColorEntry { Type = DamageType.Tear, Color = new Color(0.3f, 0.9f, 0.3f) },
            new DamageColorEntry { Type = DamageType.Nasty, Color = new Color(0.6f, 0.4f, 1f) },
            new DamageColorEntry { Type = DamageType.Water, Color = new Color(0.8f, 0f, 0.3f) },
        };

        private readonly Dictionary<DamageType, Color> _segmentColors = new();
        private readonly Dictionary<DamageType, HealthSegment> _segmentUIs = new();
        private readonly Dictionary<DamageType, float> _normalizedDamageByType = new();
        private HorizontalLayoutGroup _segmentLayoutGroup;

        private void Awake()
        {
            if (_segmentContainer != null)
                _segmentLayoutGroup = _segmentContainer.GetComponent<HorizontalLayoutGroup>();
        }

        public void HandleDamage(DamageType type, float normalizedTotal)
        {
            bool isNew = !_segmentUIs.ContainsKey(type);
            _normalizedDamageByType[type] = normalizedTotal;

            if (isNew)
            {
                var go = Instantiate(_segmentPrefab, _segmentContainer);
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
            if (_segmentColors.Count == 0 && _segmentColorEntries != null)
            {
                foreach (var e in _segmentColorEntries)
                {
                    if (!_segmentColors.ContainsKey(e.Type))
                        _segmentColors[e.Type] = e.Color;
                }

                if (_segmentColors.TryGetValue(type, out c)) return c;
            }

            return Color.white;
        }

        public void HandleHeal(float normalizedTotal, DamageType? type = null)
        {
            if (type.HasValue)
            {
                if (normalizedTotal <= 0f)
                {
                    _normalizedDamageByType.Remove(type.Value);
                }
                else
                {
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
                if (_segmentContainer.childCount > 0)
                {
                    HealthSegment first = _segmentContainer.GetChild(0).GetComponent<HealthSegment>();
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

                        if (normalizedTotal <= 0f)
                        {
                            _normalizedDamageByType.Remove(targetType);
                        }
                        else
                        {
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

        private float Spacing =>
            _segmentLayoutGroup != null ? _segmentLayoutGroup.spacing : 0f;

        private float PaddingWidth =>
            _segmentLayoutGroup != null && _segmentLayoutGroup.padding != null
                ? _segmentLayoutGroup.padding.left + _segmentLayoutGroup.padding.right
                : 0f;

        private void UpdateAllSegmentWidths(DamageType? typeToSetInstantly = null)
        {
            int activeSegmentsCount = _segmentUIs.Count;
            if (_remainingHealthSegment != null && _remainingHealthSegment.gameObject.activeSelf)
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
            if (_remainingHealthSegment == null) return;

            float totalDamage = 0f;
            foreach (var damage in _normalizedDamageByType.Values)
            {
                totalDamage += damage;
            }

            bool hasRemainingHealth = totalDamage <= 0.999f;

            if (_remainingHealthSegment.gameObject.activeSelf != hasRemainingHealth)
            {
                _remainingHealthSegment.gameObject.SetActive(hasRemainingHealth);
            }
        }

        private IEnumerator ShakeBar()
        {
            Vector3 origin = _barRoot.anchoredPosition3D;
            // Debug.Log("HealthBar.ShakeBar start");
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / 0.25f;
                float offset = Mathf.Sin(t * 80f) * 5f * (1f - t);
                _barRoot.anchoredPosition3D = origin + new Vector3(offset, 0f, 0f);
                yield return null;
            }

            _barRoot.anchoredPosition3D = origin;
            // Debug.Log("HealthBar.ShakeBar end");
        }
    }
}
