using Crease.Events;
using Crease.Flying.Environment.Collectibles;
using UnityEngine;
using UnityEngine.Events;

namespace Crease.Flying.Environment
{
    /// <summary>
    /// Ink blot collectible. On pickup, starts the breadcrumb trail and shows handwritten text.
    /// <see cref="TriggerInkFullyCollected"/> is intended to be wired from the text's disappear event.
    /// </summary>
    public class InkCollectible : Collectible
    {
        [Header("Ink")]
        [Tooltip("Breadcrumb trail to play when this ink is collected.")]
        [SerializeField] private InkBreadcrumbs _breadcrumbs;

        [Tooltip("Handwritten text to show when this ink is collected.")]
        [SerializeField] private CollectibleText _handwrittenText;

        [Header("Events")]
        [Tooltip("Invoked when TriggerInkFullyCollected is called, typically after the handwritten text finishes disappearing.")]
        public UnityEvent OnInkFullyCollected;

        private void Reset()
        {
            _destroyOnCollect = false;
            _hideOnCollect = true;
            _magnetize = false;
        }

        protected override void HandlePlayerCollected()
        {
            if (_breadcrumbs != null)
                _breadcrumbs.TriggerBreadcrumbs();

            if (_handwrittenText != null)
                _handwrittenText.Show();

            GameEvents.OnInkCollected?.Invoke();
        }

        /// <summary>
        /// Invokes <see cref="OnInkFullyCollected"/>. Wire this from the handwritten text disappear event.
        /// </summary>
        public void TriggerInkFullyCollected()
        {
            OnInkFullyCollected?.Invoke();
        }
    }
}
