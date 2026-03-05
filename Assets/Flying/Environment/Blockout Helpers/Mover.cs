using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Moves an object through a series of waypoints using DOTween.
/// All positions are relative to the transform's initial rotation.
/// </summary>
public class Mover : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Positions relative to the starting position and oriented by starting rotation")]
    [SerializeField] private List<Vector3> relativeWaypoints = new List<Vector3> 
    { 
        new Vector3(0f, 5f, 0f) 
    };
    
    [Tooltip("Total duration to complete the entire path")]
    [SerializeField] private float duration = 2f;
    
    #pragma warning disable 0414 // Field assigned but never used - used for inspector convenience to sync with duration
    [Tooltip("Movement speed in units per second")]
    [SerializeField] private float speed = 2.5f;
    #pragma warning restore 0414
    
    [Header("DOTween Settings")]
    [Tooltip("Start movement automatically when the scene starts")]
    [SerializeField] private bool triggerOnStart = true;
    
    [Tooltip("Easing function for smooth animation transitions (e.g., Linear, InOutQuad, OutBounce)")]
    [SerializeField] private Ease easeType = Ease.InOutQuad;
    
    [Tooltip("Restart: Jump back to start | Yoyo: Reverse back and forth | Incremental: Continue in same direction")]
    [SerializeField] private LoopType loopType = LoopType.Restart;
    
    [Tooltip("Number of times to loop. Set to -1 for infinite loops, 1 to play once")]
    [SerializeField] private int loops = 1;
    
    [Tooltip("Delay in seconds before the movement starts")]
    [SerializeField] private float delay = 0f;
    
    [Tooltip("Linear: Straight lines between points | CatmullRom: Smooth curved path")]
    [SerializeField] private PathType pathType = PathType.Linear;
    
    [Tooltip("Close the path by connecting the last waypoint back to the first")]
    [SerializeField] private bool closePath = false;
    
    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color pathColor = new Color(0f, 1f, 1f, 0.8f);
    [SerializeField] private Color waypointColor = new Color(1f, 0.5f, 0f, 0.8f);
    [SerializeField] private float waypointSize = 0.3f;
    #if UNITY_EDITOR
    [SerializeField] private bool showWaypointNumbers = true;
    #endif
    
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3[] localWaypoints;
    private Tweener movementTween;
    
    #if UNITY_EDITOR
    private float previousDuration;
    private float previousSpeed;
    private float cachedPathDistance = -1f;
    private int previousWaypointCount = -1;
    private bool validationScheduled = false;
    #endif
    
    private void Awake()
    {
        startPosition = transform.localPosition;
        startRotation = transform.localRotation;
        CalculateLocalWaypoints();
    }
    
    private void Start()
    {
        if (triggerOnStart)
        {
            TriggerMovement();
        }
    }
    
    private void OnDestroy()
    {
        // Kill tween on destroy to prevent errors
        movementTween?.Kill();
    }
    
    private void CalculateLocalWaypoints()
    {
        List<Vector3> waypointList = new List<Vector3>();
        
        // DOTween paths need to include the starting position as the first waypoint
        waypointList.Add(startPosition);
        
        foreach (Vector3 relativePos in relativeWaypoints)
        {
            // Rotate the relative position by the starting rotation
            Vector3 rotatedOffset = startRotation * relativePos;
            waypointList.Add(startPosition + rotatedOffset);
        }
        
        localWaypoints = waypointList.ToArray();
    }
    
    private float CalculatePathDistance()
    {
        float totalDistance = 0f;
        
        // Calculate distance in edit mode or play mode
        if (Application.isPlaying && localWaypoints != null && localWaypoints.Length > 0)
        {
            // Use local waypoints in play mode (first waypoint is start position)
            for (int i = 0; i < localWaypoints.Length - 1; i++)
            {
                totalDistance += Vector3.Distance(localWaypoints[i], localWaypoints[i + 1]);
            }
            
            // Add distance back to start if closed path
            if (closePath && localWaypoints.Length > 1)
            {
                totalDistance += Vector3.Distance(localWaypoints[localWaypoints.Length - 1], localWaypoints[0]);
            }
        }
        else
        {
            // Calculate from relative waypoints in edit mode
            if (relativeWaypoints.Count == 0)
                return 0f;
            
            Vector3 currentPos = Vector3.zero; // Start position
            foreach (Vector3 relativePos in relativeWaypoints)
            {
                Vector3 rotatedOffset = transform.localRotation * relativePos;
                Vector3 worldPos = rotatedOffset;
                totalDistance += Vector3.Distance(currentPos, worldPos);
                currentPos = worldPos;
            }
            
            // Add distance back to start if closed path
            if (closePath)
            {
                totalDistance += Vector3.Distance(currentPos, Vector3.zero);
            }
        }
        
        return totalDistance;
    }
    
    public void TriggerMovement()
    {
        // Initialize DOTween (safe to call multiple times)
        DOTween.Init();
        
        // Kill existing tween
        movementTween?.Kill();
        
        // Reset to start position
        transform.localPosition = startPosition;
        
        // Need at least two waypoints (start + at least one target)
        if (localWaypoints == null || localWaypoints.Length < 2)
        {
            Debug.LogWarning($"Mover has insufficient waypoints to move to! Found {localWaypoints?.Length ?? 0} waypoints, need at least 2.", this);
            return;
        }
        
        // Create path tween
        movementTween = transform.DOLocalPath(localWaypoints, duration, pathType)
            .SetEase(easeType)
            .SetLoops(loops, loopType)
            .SetDelay(delay)
            .SetOptions(closePath)
            .SetAutoKill(true);
            
        // Debug.Log($"Mover started with {localWaypoints.Length} waypoints over {duration}s", this);
    }
    
    public void StopMovement()
    {
        movementTween?.Kill();
    }
    
    public void PauseMovement()
    {
        movementTween?.Pause();
    }
    
    public void ResumeMovement()
    {
        movementTween?.Play();
    }
    
    public void ResetPosition()
    {
        movementTween?.Kill();
        transform.localPosition = startPosition;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        // Calculate waypoints for gizmos
        List<Vector3> gizmoWaypoints = new List<Vector3>();
        
        // Add starting position
        Vector3 gizmoStartPos = Application.isPlaying ? 
            (transform.parent != null ? transform.parent.TransformPoint(startPosition) : startPosition) :
            transform.position;
        gizmoWaypoints.Add(gizmoStartPos);
        
        // Calculate waypoint positions
        if (Application.isPlaying && localWaypoints != null)
        {
            // Use calculated local waypoints in play mode (skip first one as it's the start position)
            for (int i = 1; i < localWaypoints.Length; i++)
            {
                Vector3 worldPos = transform.parent != null 
                    ? transform.parent.TransformPoint(localWaypoints[i]) 
                    : localWaypoints[i];
                gizmoWaypoints.Add(worldPos);
            }
        }
        else
        {
            // Calculate waypoints in edit mode
            foreach (Vector3 relativePos in relativeWaypoints)
            {
                Vector3 rotatedOffset = transform.rotation * relativePos;
                gizmoWaypoints.Add(gizmoStartPos + rotatedOffset);
            }
        }
        
        // Draw waypoints
        for (int i = 0; i < gizmoWaypoints.Count; i++)
        {
            Gizmos.color = i == 0 ? Color.green : waypointColor;
            Gizmos.DrawWireSphere(gizmoWaypoints[i], waypointSize);
            
            // Draw waypoint number
            #if UNITY_EDITOR
            if (showWaypointNumbers && i > 0)
            {
                UnityEditor.Handles.Label(gizmoWaypoints[i] + Vector3.up * waypointSize, $"{i}");
            }
            #endif
        }
        
        // Draw path lines
        Gizmos.color = pathColor;
        for (int i = 0; i < gizmoWaypoints.Count - 1; i++)
        {
            Gizmos.DrawLine(gizmoWaypoints[i], gizmoWaypoints[i + 1]);
            
            // Draw direction arrows
            Vector3 direction = (gizmoWaypoints[i + 1] - gizmoWaypoints[i]).normalized;
            Vector3 midPoint = (gizmoWaypoints[i] + gizmoWaypoints[i + 1]) * 0.5f;
            DrawArrow(midPoint, direction, 0.5f);
        }
        
        // Draw loop indicator
        if (closePath && gizmoWaypoints.Count > 1)
        {
            Gizmos.color = pathColor;
            Gizmos.DrawLine(gizmoWaypoints[gizmoWaypoints.Count - 1], gizmoWaypoints[0]);
            
            Vector3 direction = (gizmoWaypoints[0] - gizmoWaypoints[gizmoWaypoints.Count - 1]).normalized;
            Vector3 midPoint = (gizmoWaypoints[gizmoWaypoints.Count - 1] + gizmoWaypoints[0]) * 0.5f;
            DrawArrow(midPoint, direction, 0.5f);
        }
        else if (loopType == LoopType.Yoyo && gizmoWaypoints.Count > 1)
        {
            // Draw return path with dashed line effect
            Gizmos.color = new Color(pathColor.r, pathColor.g, pathColor.b, pathColor.a * 0.3f);
            for (int i = gizmoWaypoints.Count - 1; i > 0; i--)
            {
                Vector3 start = gizmoWaypoints[i];
                Vector3 end = gizmoWaypoints[i - 1];
                Vector3 direction = (end - start).normalized;
                Vector3 midPoint = (start + end) * 0.5f;
                DrawArrow(midPoint, direction, 0.4f);
            }
        }
    }
    
    private void DrawArrow(Vector3 position, Vector3 direction, float size)
    {
        Vector3 right = Vector3.Cross(direction, Vector3.up);
        if (right.sqrMagnitude < 0.01f)
        {
            right = Vector3.Cross(direction, Vector3.forward);
        }
        right = right.normalized;
        
        Vector3 arrowTip = position + direction * size * 0.3f;
        Vector3 arrowBase = position - direction * size * 0.3f;
        
        Gizmos.DrawLine(arrowBase, arrowTip);
        Gizmos.DrawLine(arrowTip, arrowTip - (direction * size * 0.2f) + (right * size * 0.15f));
        Gizmos.DrawLine(arrowTip, arrowTip - (direction * size * 0.2f) - (right * size * 0.15f));
    }
    
    private void OnValidate()
    {
        #if UNITY_EDITOR
        // Ensure at least one waypoint
        if (relativeWaypoints.Count == 0)
        {
            relativeWaypoints.Add(new Vector3(0f, 5f, 0f));
        }
        
        // Debounce validation to prevent excessive calculations
        // This prevents lag when adjusting values in inspector
        if (!validationScheduled)
        {
            validationScheduled = true;
            UnityEditor.EditorApplication.delayCall += PerformValidation;
        }
        #endif
    }
    
    #if UNITY_EDITOR
    private void PerformValidation()
    {
        validationScheduled = false;
        
        if (this == null) return; // Object was destroyed
        
        // Only recalculate path distance if waypoints changed or cache is invalid
        bool waypointsChanged = (relativeWaypoints.Count != previousWaypointCount || cachedPathDistance < 0f);
        
        // Only calculate path distance when needed
        if (waypointsChanged || 
            !Mathf.Approximately(duration, previousDuration) || 
            !Mathf.Approximately(speed, previousSpeed))
        {
            float pathDistance = CalculatePathDistance();
            cachedPathDistance = pathDistance;
            previousWaypointCount = relativeWaypoints.Count;
            
            if (pathDistance <= 0f)
                pathDistance = 0.01f; // Prevent division by zero
            
            // Sync duration and speed based on path distance
            if (!Mathf.Approximately(duration, previousDuration))
            {
                // Duration changed, update speed
                if (duration > 0f)
                {
                    speed = pathDistance / duration;
                    previousSpeed = speed;
                }
                previousDuration = duration;
            }
            else if (!Mathf.Approximately(speed, previousSpeed))
            {
                // Speed changed, update duration
                if (speed > 0f)
                {
                    duration = pathDistance / speed;
                    previousDuration = duration;
                }
                previousSpeed = speed;
            }
            
            // Ensure minimum values
            if (duration <= 0f) duration = 0.01f;
            if (speed <= 0f) speed = 0.01f;
        }
    }
    #endif
}