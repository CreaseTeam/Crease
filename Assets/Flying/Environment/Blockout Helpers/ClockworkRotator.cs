using UnityEngine;
using DG.Tweening;

public class ClockworkRotator : MonoBehaviour
{
    public enum PivotMode { Self, AroundPivotTransform, AroundPoint }
    public enum AxisMode { X, Y, Z, Custom }

    [Header("Trigger")]
    [SerializeField] private bool triggerOnStart = true;

    [Header("Timing")]
    [Min(0.01f)]
    [SerializeField] private float duration = 2f;

    [Min(0f)]
    [SerializeField] private float delay = 0f;

    [Header("Rotation")]
    [Tooltip("Total degrees to rotate over the duration.")]
    [SerializeField] private float degrees = 360f;

    [SerializeField] private bool clockwise = true;

    [Tooltip("World: axis is world space | Local: axis follows the object.")]
    [SerializeField] private Space axisSpace = Space.Self;

    [SerializeField] private AxisMode axisMode = AxisMode.Y;

    [Tooltip("Used when AxisMode = Custom.")]
    [SerializeField] private Vector3 customAxis = Vector3.up;

    [Header("Pivot")]
    [SerializeField] private PivotMode pivotMode = PivotMode.Self;
    [SerializeField] private Transform pivotTransform;
    [SerializeField] private Vector3 pivotWorldPoint;

    [Header("DOTween Settings")]
    [SerializeField] private Ease easeType = Ease.InOutQuad;
    [SerializeField] private LoopType loopType = LoopType.Restart;
    [SerializeField] private int loops = 1;
    [SerializeField] private bool resetOnComplete = false;

    private Quaternion startLocalRotation;
    private Vector3 startLocalPosition;
    private Tweener rotateTween;

    private void Awake()
    {
        startLocalRotation = transform.localRotation;
        startLocalPosition = transform.localPosition;
    }

    private void Start()
    {
        if (triggerOnStart)
            TriggerRotation();
    }

    private void OnDestroy()
    {
        rotateTween?.Kill();
    }

    public void TriggerRotation()
    {
        DOTween.Init();
        rotateTween?.Kill();

        // Reset
        transform.localRotation = startLocalRotation;
        transform.localPosition = startLocalPosition;

        Vector3 axisLocalOrWorld = GetAxisNormalized();
        float signedDegrees = clockwise ? -degrees : degrees;

        // degrees per second so the motion is stable and loop-safe
        float degPerSec = signedDegrees / duration;

        if (pivotMode == PivotMode.Self)
        {
            // TRUE self rotation: changes rotation only, never position.
            rotateTween = DOVirtual.Float(0f, 1f, duration, _ =>
                {
                    float dt = Time.deltaTime;
                    Vector3 axis = AxisInWorldIfNeeded(axisLocalOrWorld);
                    // Rotate around axis in chosen space
                    if (axisSpace == Space.Self)
                        transform.Rotate(axisLocalOrWorld, degPerSec * dt, Space.Self);
                    else
                        transform.Rotate(axis, degPerSec * dt, Space.World);
                })
                .SetEase(easeType)
                .SetLoops(loops, loopType)
                .SetDelay(delay)
                .SetAutoKill(true);

            if (resetOnComplete && loops != -1)
                rotateTween.OnComplete(ResetTransform);

            return;
        }

        // Pivoted rotation: orbit around a pivot point (intended behavior)
        Vector3 pivot = GetPivotWorldPoint();
        rotateTween = DOVirtual.Float(0f, 1f, duration, _ =>
            {
                float dt = Time.deltaTime;
                Vector3 axis = AxisInWorldIfNeeded(axisLocalOrWorld);
                transform.RotateAround(pivot, axis, degPerSec * dt);
            })
            .SetEase(easeType)
            .SetLoops(loops, loopType)
            .SetDelay(delay)
            .SetAutoKill(true);

        if (resetOnComplete && loops != -1)
            rotateTween.OnComplete(ResetTransform);
    }

    public void StopRotation() => rotateTween?.Kill();
    public void PauseRotation() => rotateTween?.Pause();
    public void ResumeRotation() => rotateTween?.Play();

    public void ResetTransform()
    {
        rotateTween?.Kill();
        transform.localRotation = startLocalRotation;
        transform.localPosition = startLocalPosition;
    }

    private Vector3 GetAxisNormalized()
    {
        Vector3 axis = axisMode switch
        {
            AxisMode.X => Vector3.right,
            AxisMode.Y => Vector3.up,
            AxisMode.Z => Vector3.forward,
            AxisMode.Custom => customAxis,
            _ => Vector3.up
        };

        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.up;
        return axis.normalized;
    }

    private Vector3 AxisInWorldIfNeeded(Vector3 axis)
    {
        // For pivot rotations we need a world axis direction.
        return axisSpace == Space.Self ? transform.TransformDirection(axis).normalized : axis.normalized;
    }

    private Vector3 GetPivotWorldPoint()
    {
        return pivotMode switch
        {
            PivotMode.AroundPivotTransform => pivotTransform != null ? pivotTransform.position : transform.position,
            PivotMode.AroundPoint => pivotWorldPoint,
            _ => transform.position
        };
    }
}