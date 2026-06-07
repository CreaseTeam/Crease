using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Crease.UI.Flying
{
    public class Heart : MonoBehaviour
    {
        [FormerlySerializedAs("heartImage")]
        public Sprite HeartImage;
        [FormerlySerializedAs("brokenHeartImage")]
        public Sprite BrokenHeartImage;

        private Image _heartRenderer;

        private void Awake()
        {
            _heartRenderer = GetComponent<Image>();

            if (HeartImage != null)
            {
                _heartRenderer.sprite = HeartImage;
            }
        }

        public void SetHealth(bool isHealthy)
        {
            if (_heartRenderer != null)
            {
                _heartRenderer.sprite = isHealthy ? HeartImage : BrokenHeartImage;
            }
        }
    }
}
