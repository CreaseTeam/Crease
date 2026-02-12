using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class MountController : MonoBehaviour
{
    [SerializeField] private Transform slotContainer;
    private MountModel model;
    private KinematicBody kinematicBody;
    public Action<IInteractable> OnMountInteractable;

    private void Start()
    {
        SlotBase[] slots = slotContainer.GetComponentsInChildren<SlotBase>();
        model = new MountModel(slots);
        kinematicBody = GetComponentInParent<KinematicBody>();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            DropOneProp();
        }
    }

    public void DropOneProp()
    {
        if (model == null || model.mountingSlots == null) return;

        foreach (var slot in model.mountingSlots)
        {
            if (!slot.IsEmpty)
            {
                var prop = slot.DropProp();
                if (prop != null)
                {
                    // Throw in the direction the mount is facing
                    prop.OnThrow(kinematicBody.Velocity);
                    return; // Drop only one per key press
                }
            }
        }
    }

    public List<SlotBase> GetAvailableSlotsForProp(IInteractable prop)
    {
        List<SlotBase> availableSlots = new();
        if (model.IsFull || !prop.IsValid) return null;
        foreach (var slot in model.mountingSlots)
        {
            if (slot.IsEmpty && slot.IsValid && slot.IsPickable(prop))
                availableSlots.Add(slot);
        }
        return availableSlots;
    }

    public bool TryAddPropToRandomSlot(IInteractable prop, out SlotBase slot)
    {
        slot = null;
        if (model.IsFull || !prop.IsValid) return false;
        foreach (var s in model.mountingSlots)
        {
            if (s.IsEmpty && s.IsValid && s.IsPickable(prop))
            {
                slot = s;
                return true;
            }
        }
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<IInteractable>(out var interactable))
        {
            if (TryAddPropToRandomSlot(interactable, out var slot))
            {
                interactable.OnPickUp(slot.transform);
                slot.MountProp(interactable);
                OnMountInteractable?.Invoke(interactable);
            }
        }
    }
}
