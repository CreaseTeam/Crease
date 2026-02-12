using UnityEngine;
using UnityEngine.Events; // Required for UnityEvent

public class TriggerHandler : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Only objects with this tag will trigger the event.")]
    public string targetTag = "Player";

    [Space]
    [Tooltip("Drag and drop objects here to trigger their functions.")]
    public UnityEvent onTriggerEnter;

    private void OnTriggerEnter(Collider other)
    {
        // Check if the object entering is the Player
        if (other.CompareTag(targetTag))
        {
            // Fire all events hooked up in the Inspector
            onTriggerEnter.Invoke();
        }
    }
}