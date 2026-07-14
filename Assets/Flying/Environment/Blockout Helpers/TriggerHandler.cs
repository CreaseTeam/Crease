using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Crease.Flying.Environment.BlockoutHelpers
{
    public class TriggerHandler : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Only objects with this tag will trigger the event.")]
        [FormerlySerializedAs("targetTag")]
        public string TargetTag = "Player";

        [Space]
        [Tooltip("Drag and drop objects here to trigger their functions.")]
        [FormerlySerializedAs("onTriggerEnter")]
        public UnityEvent OnTriggerEntered;
        [FormerlySerializedAs("onTriggerExit")]
        public UnityEvent OnTriggerExited;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(TargetTag))
            {
                OnTriggerEntered.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(TargetTag))
            {
                OnTriggerExited.Invoke();
            }
        }
    }
}
