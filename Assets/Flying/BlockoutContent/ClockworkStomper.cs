using UnityEngine;
using DG.Tweening;

namespace Crease.Flying.BlockoutContent
{
    public class ClockworkStomper : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _dropDistance = 3f;
        [SerializeField] private float _dropDuration = 0.25f;
        [SerializeField] private float _riseDuration = 1.2f;
        [SerializeField] private float _bottomWait = 0.5f;
        [SerializeField] private float _topWait = 0.5f;

        [Header("Settings")]
        [SerializeField] private bool _triggerOnStart = true;
        [SerializeField] private bool _loop = true;

        [Header("Easing")]
        [SerializeField] private Ease _dropEase = Ease.InQuad;
        [SerializeField] private Ease _riseEase = Ease.OutQuad;

        private Vector3 _startLocalPos;
        private Sequence _stompSequence;

        private void Awake()
        {
            _startLocalPos = transform.localPosition;
        }

        private void Start()
        {
            if (_triggerOnStart)
                TriggerStomp();
        }

        private void OnDisable()
        {
            _stompSequence?.Kill();
            _stompSequence = null;
        }

        private void OnDestroy()
        {
            _stompSequence?.Kill();
        }

        public void TriggerStomp()
        {
            _stompSequence?.Kill();

            transform.localPosition = _startLocalPos;

            Vector3 bottomPos = _startLocalPos + Vector3.down * _dropDistance;

            _stompSequence = DOTween.Sequence()
                .SetLink(gameObject);

            _stompSequence.Append(
                transform.DOLocalMove(bottomPos, _dropDuration)
                    .SetEase(_dropEase)
            );

            _stompSequence.AppendInterval(_bottomWait);

            _stompSequence.Append(
                transform.DOLocalMove(_startLocalPos, _riseDuration)
                    .SetEase(_riseEase)
            );

            _stompSequence.AppendInterval(_topWait);

            if (_loop)
                _stompSequence.SetLoops(-1, LoopType.Restart);
        }

        public void StopStomp()
        {
            _stompSequence?.Kill();
            _stompSequence = null;
        }
    }
}
