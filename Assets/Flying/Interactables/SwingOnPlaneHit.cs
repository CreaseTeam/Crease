using System.Collections;
using UnityEngine;

public class SwingOnPlaneHit : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string planeTag = "Player";

    [Header("Rotation")]
    [SerializeField] private Transform pivot;
    
    [SerializeField] private Vector3 closedLocalEuler;

    [Tooltip("How much to rotate from CLOSED")]
    [SerializeField] private Vector3 openLocalEuler = new Vector3(0f, 90f, 0f);

    [SerializeField] private float swingTime = 0.2f;

    [Tooltip("If true, it only triggers once.")]
    [SerializeField] private bool oneShot = true;

    [Header("Debug")]
    [SerializeField] private bool logOnHit = true;
    [SerializeField] private string debugMessage = "Interactable triggered!";

    private bool triggered = false;
    private Coroutine swingRoutine;
    
    private Quaternion closedRot;
    private Quaternion openRot;

    private void Reset()
    {
        pivot = transform;
        closedLocalEuler = transform.localEulerAngles;
    }

    private void Awake()
    {
        if (pivot == null) pivot = transform;
        
        closedRot = Quaternion.Euler(closedLocalEuler);
        
        openRot = closedRot * Quaternion.Euler(openLocalEuler);
        
        // pivot.localRotation = closedRot;
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

        if (logOnHit)
            Debug.Log(debugMessage, this);

        if (swingRoutine != null)
            StopCoroutine(swingRoutine);

        if (swingTime <= 0f)
        {
            pivot.localRotation = openRot;
        }
        else
        {
            swingRoutine = StartCoroutine(SwingTo(openRot, swingTime));
        }
    }

    public void ResetToClosed()
    {
        triggered = false;

        if (swingRoutine != null)
            StopCoroutine(swingRoutine);
        
        pivot.localRotation = closedRot;
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
}