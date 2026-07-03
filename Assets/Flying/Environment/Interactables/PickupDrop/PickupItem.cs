using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Environment.Interactables.PickupDrop
{
    [RequireComponent(typeof(Collider))]
    public class PickupItem : MonoBehaviour
    {
        [FormerlySerializedAs("_pickupScale")]
        [SerializeField] private Vector3 _attachedLocalScale = Vector3.one;

        private Rigidbody _rb;
        private Collider _collider;

        public bool IsHeld { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
        }

        public bool TryPickUp(Transform attachPoint)
        {
            if (IsHeld || attachPoint == null)
                return false;

            IsHeld = true;
            transform.SetParent(attachPoint);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = _attachedLocalScale;

            if (_rb != null)
                _rb.isKinematic = true;

            if (_collider != null)
                _collider.enabled = false;

            return true;
        }

        public bool TryRelease(Vector3 velocity)
        {
            if (!IsHeld)
                return false;

            IsHeld = false;
            transform.SetParent(null);

            if (_collider != null)
                _collider.enabled = true;

            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
                _rb.linearVelocity = velocity;
            }

            return true;
        }
    }
}
