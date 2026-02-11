using UnityEngine;
using DG.Tweening;

/// <summary>
/// Rotates an object from its starting rotation to an end rotation using DOTween.
/// </summary>
public class Rotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Rotation offset from starting rotation")]
    [SerializeField] private Vector3 endRotationOffset = new Vector3(0f, 90f, 0f);
    
    [SerializeField] private float duration = 2f;
    
    [Header("DOTween Settings")]
    [Tooltip("Start rotation automatically when the scene starts")]
    [SerializeField] private bool triggerOnStart = true;
    
    [Tooltip("Easing function for smooth animation transitions (e.g., Linear, InOutQuad, OutBounce)")]
    [SerializeField] private Ease easeType = Ease.InOutQuad;
    
    [Tooltip("Restart: Jump back to start | Yoyo: Reverse back and forth | Incremental: Continue rotating")]
    [SerializeField] private LoopType loopType = LoopType.Restart;
    
    [Tooltip("Number of times to loop. Set to -1 for infinite loops, 1 to play once")]
    [SerializeField] private int loops = 1;
    
    [Tooltip("Delay in seconds before the rotation starts")]
    [SerializeField] private float delay = 0f;
    
    [Tooltip("Fast: Shortest path | FastBeyond360: Shortest allowing >360° | WorldAxisAdd/LocalAxisAdd: Rotate on specific axis")]
    [SerializeField] private RotateMode rotateMode = RotateMode.Fast;
    
    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color startColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color endColor = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private int arcSegments = 20;
    [SerializeField] private float gizmoSize = 10f;
    
    private Quaternion startRotation;
    private Quaternion endRotation;
    private Tweener rotationTween;
    
    private void Awake()
    {
        startRotation = transform.localRotation;
        endRotation = startRotation * Quaternion.Euler(endRotationOffset);
    }
    
    private void Start()
    {
        if (triggerOnStart)
        {
            TriggerRotation();
        }
    }
    
    private void OnDestroy()
    {
        // Kill tween on destroy to prevent errors
        rotationTween?.Kill();
    }
    
    public void TriggerRotation()
    {
        // Kill existing tween
        rotationTween?.Kill();
        
        // Reset to start rotation
        transform.localRotation = startRotation;
        
        // Create new tween
        rotationTween = transform.DOLocalRotate(endRotationOffset, duration, rotateMode)
            .SetRelative(true)
            .SetEase(easeType)
            .SetLoops(loops, loopType)
            .SetDelay(delay)
            .SetAutoKill(true);
    }
    
    public void StopRotation()
    {
        rotationTween?.Kill();
    }
    
    public void PauseRotation()
    {
        rotationTween?.Pause();
    }
    
    public void ResumeRotation()
    {
        rotationTween?.Play();
    }
    
    public void ResetRotation()
    {
        rotationTween?.Kill();
        transform.localRotation = startRotation;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        Quaternion gizmoStartRot = Application.isPlaying ? startRotation : transform.localRotation;
        Quaternion gizmoEndRot = Application.isPlaying ? endRotation : (transform.localRotation * Quaternion.Euler(endRotationOffset));
        
        Vector3 position = transform.position;
        
        // Draw pivot point
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(position, 0.1f * gizmoSize);
        
        // Draw start direction
        Vector3 startDir = transform.parent != null 
            ? transform.parent.rotation * gizmoStartRot * Vector3.forward 
            : gizmoStartRot * Vector3.forward;
        Gizmos.color = startColor;
        Gizmos.DrawLine(position, position + startDir * gizmoSize);
        Gizmos.DrawWireSphere(position + startDir * gizmoSize, 0.15f * gizmoSize);
        
        // Draw end direction
        Vector3 endDir = transform.parent != null 
            ? transform.parent.rotation * gizmoEndRot * Vector3.forward 
            : gizmoEndRot * Vector3.forward;
        Gizmos.color = endColor;
        Gizmos.DrawLine(position, position + endDir * gizmoSize);
        Gizmos.DrawWireSphere(position + endDir * gizmoSize, 0.15f * gizmoSize);
        
        // Draw rotation arc
        DrawRotationArc(position, gizmoStartRot, gizmoEndRot);
        
        // Draw rotation axis
        Vector3 rotationAxis = Vector3.Cross(startDir, endDir).normalized;
        if (rotationAxis.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(position - rotationAxis * 0.3f * gizmoSize, 
                           position + rotationAxis * 0.3f * gizmoSize);
        }
    }
    
    private void DrawRotationArc(Vector3 position, Quaternion startRot, Quaternion endRot)
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        
        Vector3 previousPoint = Vector3.zero;
        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            Quaternion currentRot = Quaternion.Slerp(startRot, endRot, t);
            
            Vector3 direction = transform.parent != null 
                ? transform.parent.rotation * currentRot * Vector3.forward 
                : currentRot * Vector3.forward;
            
            Vector3 currentPoint = position + direction * gizmoSize;
            
            if (i > 0)
            {
                Gizmos.DrawLine(previousPoint, currentPoint);
            }
            
            previousPoint = currentPoint;
        }
    }
}
