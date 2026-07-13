using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Crease.UI.Flying
{
    public class HealthSegment : MonoBehaviour
    {
        [SerializeField]
        [FormerlySerializedAs("fill")]
        private Image _fill;
        [SerializeField]
        [FormerlySerializedAs("layoutElement")]
        private LayoutElement _layoutElement;

        public void SetWidth(float pixelWidth) =>
            _layoutElement.preferredWidth = pixelWidth;

        public void AnimateToWidth(float targetWidth) =>
            StartCoroutine(WidthRoutine(targetWidth));

        public void PlaySpawnAnimation() =>
            StartCoroutine(FlashRoutine());

        private IEnumerator WidthRoutine(float targetWidth)
        {
            float startWidth = _layoutElement.preferredWidth;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / 0.12f;
                _layoutElement.preferredWidth = Mathf.Lerp(startWidth, targetWidth, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            _layoutElement.preferredWidth = targetWidth;
        }

        private IEnumerator FlashRoutine()
        {
            Color baseColor = _fill.color;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / 0.15f;
                _fill.color = Color.Lerp(Color.white, baseColor, t);
                yield return null;
            }

            _fill.color = baseColor;
        }

        public void SetColor(Color color) => _fill.color = color;
    }
}
