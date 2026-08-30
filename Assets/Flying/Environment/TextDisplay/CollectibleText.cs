using System.Collections;
using Crease.Flying.Player;
using Crease.Handwritting;
using UnityEngine;
using UnityEngine.Events;

namespace Crease.Flying.Environment
{
    /// <summary>
    /// Placed on a handwritten text display. After <see cref="Show"/>, a player collision
    /// dismisses the text. Does not auto-hide — turn off Disappear After Linger on
    /// <see cref="HandwrittenTextPlayer"/> so the text stays until collected.
    /// </summary>
    [RequireComponent(typeof(HandwrittenTextPlayer))]
    [RequireComponent(typeof(Collider))]
    public class CollectibleText : MonoBehaviour
    {
        [Header("Events")]
        [Tooltip("Invoked after the disappear animation finishes.")]
        public UnityEvent OnDisappeared;

        private HandwrittenTextPlayer _textPlayer;
        private Collider _collider;
        private bool _isShowing;
        private bool _isCollecting;
        private Coroutine _disappearRoutine;

        private void Awake()
        {
            _textPlayer = GetComponent<HandwrittenTextPlayer>();
            _collider = GetComponent<Collider>();
            _collider.enabled = false;
        }

        /// <summary>
        /// Starts the write-in animation and enables collection.
        /// </summary>
        public void Show()
        {
            if (_textPlayer == null)
            {
                Debug.LogWarning($"{nameof(CollectibleText)} on {name} is missing a {nameof(HandwrittenTextPlayer)}.", this);
                return;
            }

            _isShowing = true;
            _isCollecting = false;
            _collider.enabled = true;
            _textPlayer.PlayWriteIn();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isShowing || _isCollecting)
                return;

            KinematicBody body = other.GetComponent<KinematicBody>();
            if (body == null)
                return;

            _isCollecting = true;
            _collider.enabled = false;

            if (_disappearRoutine != null)
                StopCoroutine(_disappearRoutine);

            _disappearRoutine = StartCoroutine(CollectRoutine());
        }

        private IEnumerator CollectRoutine()
        {
            _textPlayer.PlayDisappear();

            while (_textPlayer != null && _textPlayer.IsPlaying)
                yield return null;

            _isShowing = false;
            _disappearRoutine = null;
            OnDisappeared?.Invoke();
        }
    }
}
