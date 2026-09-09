using System.Collections;
using Crease.Events;
using Crease.Flying.Environment.Collectibles;
using UnityEngine;
using UnityEngine.Events;

namespace Crease.Flying.Environment
{
    /// <summary>
    /// Ink blot collectible. On pickup, shows handwritten text and draws a guide line to it.
    /// <see cref="TriggerInkFullyCollected"/> is intended to be wired from the text's disappear event.
    /// </summary>
    public class InkCollectible : Collectible
    {
        [Header("Ink")]
        [Tooltip("Handwritten text to show when this ink is collected.")]
        [SerializeField] private CollectibleText _handwrittenText;

        [Header("Guide Line")]
        [Tooltip("Local-space offset from this collectible for the line start.")]
        [SerializeField] private Vector3 _lineStartOffset;

        [Tooltip("Local-space offset from the handwritten text for the line end.")]
        [SerializeField] private Vector3 _lineEndOffset;

        [Tooltip("Seconds the line stays visible after collection. 0 keeps it until this object is disabled.")]
        [SerializeField, Min(0f)] private float _lineLingerTime = 3f;

        [Tooltip("Material applied to the guide line.")]
        [SerializeField] private Material _lineMaterial;

        [Header("Events")]
        [Tooltip("Invoked when TriggerInkFullyCollected is called, typically after the handwritten text finishes disappearing.")]
        public UnityEvent OnInkFullyCollected;

        private LineRenderer _lineRenderer;
        private Coroutine _lineRoutine;
        private bool _lineVisible;

        private void Reset()
        {
            _destroyOnCollect = false;
            _hideOnCollect = true;
            _magnetize = false;
        }

        protected override void HandlePlayerCollected()
        {
            if (_handwrittenText != null)
                _handwrittenText.Show();

            ShowGuideLine();
            GameEvents.OnInkCollected?.Invoke();
        }

        /// <summary>
        /// Invokes <see cref="OnInkFullyCollected"/>. Wire this from the handwritten text disappear event.
        /// </summary>
        public void TriggerInkFullyCollected()
        {
            OnInkFullyCollected?.Invoke();
        }

        private void LateUpdate()
        {
            if (_lineVisible)
                UpdateLinePositions();
        }

        private void OnDisable()
        {
            HideGuideLine();
        }

        private void ShowGuideLine()
        {
            if (_handwrittenText == null)
            {
                Debug.LogWarning($"{nameof(InkCollectible)} on {name} has no handwritten text to draw a line to.", this);
                return;
            }

            EnsureLineRenderer();
            _lineVisible = true;
            _lineRenderer.enabled = true;
            UpdateLinePositions();

            if (_lineRoutine != null)
                StopCoroutine(_lineRoutine);

            if (_lineLingerTime > 0f)
                _lineRoutine = StartCoroutine(LingerThenHideLine());
        }

        private IEnumerator LingerThenHideLine()
        {
            yield return new WaitForSeconds(_lineLingerTime);
            HideGuideLine();
        }

        private void HideGuideLine()
        {
            _lineVisible = false;
            _lineRoutine = null;

            if (_lineRenderer != null)
                _lineRenderer.enabled = false;
        }

        private void EnsureLineRenderer()
        {
            if (_lineRenderer == null)
                _lineRenderer = gameObject.AddComponent<LineRenderer>();

            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = 2;
            _lineRenderer.startWidth = 0.15f;
            _lineRenderer.endWidth = 0.15f;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.textureMode = LineTextureMode.Stretch;

            if (_lineMaterial != null)
                _lineRenderer.sharedMaterial = _lineMaterial;
        }

        private void UpdateLinePositions()
        {
            if (_lineRenderer == null || _handwrittenText == null)
                return;

            Vector3 start = transform.TransformPoint(_lineStartOffset);
            Vector3 end = _handwrittenText.transform.TransformPoint(_lineEndOffset);
            _lineRenderer.SetPosition(0, start);
            _lineRenderer.SetPosition(1, end);
        }
    }
}
