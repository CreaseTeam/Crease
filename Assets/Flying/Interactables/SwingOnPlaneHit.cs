using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class SwingOnPlaneHit : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string planeTag = "Player";

    [Header("Rotation")]
    [SerializeField] private Transform pivot;

    [Tooltip("Rotation offset from starting rotation")]
    [SerializeField] private Vector3 endLocalEuler = new Vector3(0f, 90f, 0f);

    [SerializeField] private float swingTime = 0.2f;

    [Tooltip("If true, it only triggers once.")]
    [SerializeField] private bool oneShot = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onDoorOpened;

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color startColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color endColor = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private Color swingPathColor = new Color(1f, 1f, 0f, 0.3f);
    [SerializeField] private int arcSegments = 20;
    [SerializeField] private float gizmoSize = 5f;

    private bool triggered = false;
    private Coroutine swingRoutine;
    
    private Quaternion initialRot;
    private Quaternion endRot;

    private void Reset()
    {
        pivot = transform;
    }

    private void Awake()
    {
        if (pivot == null) pivot = transform;
        
        // Store initial rotation and calculate end rotation relative to it
        initialRot = pivot.localRotation;
        endRot = initialRot * Quaternion.Euler(endLocalEuler);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (oneShot && triggered) return;

        if (!string.IsNullOrEmpty(planeTag) && !other.CompareTag(planeTag))
            return;

        Trigger();
    }

    public void Trigger()
    {
        if (oneShot && triggered) return;
        triggered = true;

        onDoorOpened?.Invoke();

        if (swingRoutine != null)
            StopCoroutine(swingRoutine);

        if (swingTime <= 0f)
        {
            pivot.localRotation = endRot;
        }
        else
        {
            swingRoutine = StartCoroutine(SwingTo(endRot, swingTime));
        }
    }

    public void ResetToStart()
    {
        triggered = false;

        if (swingRoutine != null)
            StopCoroutine(swingRoutine);
        
        pivot.localRotation = initialRot;
    }
    
    private IEnumerator SwingTo(Quaternion targetRot, float duration)
    {
        Quaternion startRot = pivot.localRotation;
        Quaternion endRot = targetRot;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / duration);
            pivot.localRotation = Quaternion.Slerp(startRot, endRot, alpha);
            yield return null;
        }

        pivot.localRotation = endRot;
        swingRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Transform gizmoPivot = pivot != null ? pivot : transform;
        
        // Calculate rotations relative to current rotation (in edit mode) or initial rotation (in play mode)
        Quaternion baseRot = Application.isPlaying ? initialRot : gizmoPivot.localRotation;
        Quaternion gizmoStartRot = baseRot;
        Quaternion gizmoEndRot = baseRot * Quaternion.Euler(endLocalEuler);
        
        // Get world positions
        Vector3 pivotPos = gizmoPivot.position;
        
        // Draw pivot point
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(pivotPos, 0.1f * gizmoSize);
        
        // Draw start direction
        Vector3 startDir = gizmoPivot.parent != null 
            ? gizmoPivot.parent.rotation * gizmoStartRot * Vector3.forward 
            : gizmoStartRot * Vector3.forward;
        Gizmos.color = startColor;
        Gizmos.DrawLine(pivotPos, pivotPos + startDir * gizmoSize);
        Gizmos.DrawWireSphere(pivotPos + startDir * gizmoSize, 0.15f * gizmoSize);
        
        // Draw end direction
        Vector3 endDir = gizmoPivot.parent != null 
            ? gizmoPivot.parent.rotation * gizmoEndRot * Vector3.forward 
            : gizmoEndRot * Vector3.forward;
        Gizmos.color = endColor;
        Gizmos.DrawLine(pivotPos, pivotPos + endDir * gizmoSize);
        Gizmos.DrawWireSphere(pivotPos + endDir * gizmoSize, 0.15f * gizmoSize);
        
        // Draw swing path arc
        DrawSwingArc(pivotPos, gizmoPivot, gizmoStartRot, gizmoEndRot);
        
        // Draw rotation axis
        Vector3 rotationAxis = Vector3.Cross(startDir, endDir).normalized;
        if (rotationAxis.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pivotPos - rotationAxis * 0.3f * gizmoSize, 
                           pivotPos + rotationAxis * 0.3f * gizmoSize);
        }
    }

    private void DrawSwingArc(Vector3 pivotPos, Transform gizmoPivot, Quaternion startRot, Quaternion endRot)
    {
        Gizmos.color = swingPathColor;
        
        Vector3 previousPoint = Vector3.zero;
        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            Quaternion currentRot = Quaternion.Slerp(startRot, endRot, t);
            
            Vector3 direction = gizmoPivot.parent != null 
                ? gizmoPivot.parent.rotation * currentRot * Vector3.forward 
                : currentRot * Vector3.forward;
            
            Vector3 currentPoint = pivotPos + direction * gizmoSize;
            
            if (i > 0)
            {
                Gizmos.DrawLine(previousPoint, currentPoint);
            }
            
            previousPoint = currentPoint;
        }
    }
}