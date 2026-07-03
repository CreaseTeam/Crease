using System.Collections.Generic;
using Crease.Flying.Player;
using Crease.Managers.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Environment.Interactables.PickupDrop
{
    public class MountController : MonoBehaviour
    {
        [Tooltip("Transform on the player where picked-up items are parented.")]
        [FormerlySerializedAs("slotContainer")]
        [SerializeField] private Transform _attachPoint;

        private PickupItem _heldItem;
        private KinematicBody _body;
        private readonly HashSet<PickupItem> _ignoreUntilExit = new();

        private void Awake()
        {
            _body = GetComponentInParent<KinematicBody>();

            if (_attachPoint != null && _attachPoint.childCount > 0)
                _attachPoint = _attachPoint.GetChild(0);
        }

        private void Update()
        {
            SyncHeldItemReference();

            if (InputManager.Instance == null || !InputManager.Instance.DropTriggered)
                return;

            DropHeldItem();
        }

        private void SyncHeldItemReference()
        {
            if (_heldItem != null && _heldItem.IsHeld)
                return;

            _heldItem = null;

            if (_attachPoint == null)
                return;

            PickupItem attachedItem = _attachPoint.GetComponentInChildren<PickupItem>();
            if (attachedItem != null && attachedItem.IsHeld)
                _heldItem = attachedItem;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_heldItem != null)
                return;

            if (!other.TryGetComponent<PickupItem>(out PickupItem item) || item.IsHeld)
                return;

            if (_ignoreUntilExit.Contains(item))
                return;

            if (!item.TryPickUp(_attachPoint))
                return;

            _heldItem = item;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<PickupItem>(out PickupItem item))
                _ignoreUntilExit.Remove(item);
        }

        public void DropHeldItem()
        {
            SyncHeldItemReference();

            if (_heldItem == null)
                return;

            PickupItem item = _heldItem;
            Vector3 velocity = _body != null ? _body.Velocity : Vector3.zero;

            if (!item.TryRelease(velocity))
                return;

            _heldItem = null;
            _ignoreUntilExit.Add(item);
        }
    }
}
