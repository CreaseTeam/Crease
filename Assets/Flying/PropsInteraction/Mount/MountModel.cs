using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MountModel
{
    public List<SlotBase> mountingSlots; //List to store all mounting slots as prop's parent
    public Dictionary<SlotBase, IInteractable> propSlotDict;
    public int AvailableSlots => mountingSlots.Count - propSlotDict.Count;
    public bool IsFull => AvailableSlots == 0;
    public bool IsEmpty => propSlotDict.Count == 0;

    public MountModel(params SlotBase[] slots)
    {
        propSlotDict = new();
        mountingSlots = slots.ToList();
    }

    public List<SlotBase> GetAvailableSlots() => mountingSlots.FindAll(s => s.IsEmpty && s.IsValid);
}
