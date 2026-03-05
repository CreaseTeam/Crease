using UnityEngine;
using DG.Tweening;

public class ClockworkStomper : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float dropDistance = 3f;
    [SerializeField] private float dropDuration = 0.25f;
    [SerializeField] private float riseDuration = 1.2f;
    [SerializeField] private float bottomWait = 0.5f;
    [SerializeField] private float topWait = 0.5f;

    [Header("Settings")]
    [SerializeField] private bool triggerOnStart = true;
    [SerializeField] private bool loop = true;

    [Header("Easing")]
    [SerializeField] private Ease dropEase = Ease.InQuad;
    [SerializeField] private Ease riseEase = Ease.OutQuad;

    private Vector3 startLocalPos;
    private Sequence stompSequence;

    private void Awake()
    {
        startLocalPos = transform.localPosition;
    }

    private void Start()
    {
        if (triggerOnStart)
            TriggerStomp();
    }

    private void OnDisable()
    {
        stompSequence?.Kill();
        stompSequence = null;
    }

    private void OnDestroy()
    {
        stompSequence?.Kill();
    }

    public void TriggerStomp()
    {
        stompSequence?.Kill();

        transform.localPosition = startLocalPos;

        Vector3 bottomPos = startLocalPos + Vector3.down * dropDistance;

        stompSequence = DOTween.Sequence()
            .SetLink(gameObject);   // auto-kill with GameObject

        stompSequence.Append(
            transform.DOLocalMove(bottomPos, dropDuration)
                .SetEase(dropEase)
        );

        stompSequence.AppendInterval(bottomWait);

        stompSequence.Append(
            transform.DOLocalMove(startLocalPos, riseDuration)
                .SetEase(riseEase)
        );

        stompSequence.AppendInterval(topWait);

        if (loop)
            stompSequence.SetLoops(-1, LoopType.Restart);
    }

    public void StopStomp()
    {
        stompSequence?.Kill();
        stompSequence = null;
    }
}