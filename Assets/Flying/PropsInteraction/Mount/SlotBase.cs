using System;
using UnityEngine;

[Flags]
public enum E_PropCategory
{
    Weapon = 1 << 0,
    Gasoline = 1 << 1,
    Bomb = 1 << 2,
    Others = 1 << 3
}

public class SlotBase : MonoBehaviour
{
    [Header("Filters")]
    [SerializeField] private float maxWeight;
    [SerializeField] private E_PropCategory categoryFilter;
    
    /// <summary>
    /// check if slot is empty
    /// </summary>
    private bool isEmpty;
    public bool IsEmpty => isEmpty;
    
    [SerializeField]private bool isValid = true;
    public virtual bool IsValid => isValid;
    
    [SerializeField] private PropEntity mountingProp;
    private IInteractable currentInteractable;

    private void Start()
    {
        if (mountingProp != null)
        {
            var instance = Instantiate(mountingProp, transform.position, Quaternion.identity, transform);
            if (instance.TryGetComponent<IInteractable>(out var interactable))
            {
                MountProp(interactable);
            }
        }
        else
        {
            isEmpty = true;
        }
    }

    public bool IsPickable(IInteractable prop)
    {
        if (prop.Weight > maxWeight) return false;
        if(!categoryFilter.HasFlag(prop.Category)) return false;
        return true;
    }

    public void MountProp(IInteractable prop)
    {
        if (!isEmpty) return;
        currentInteractable = prop;
        isEmpty = false;
    }

    public IInteractable DropProp()
    {
        if (isEmpty) return null;
        var prop = currentInteractable;
        currentInteractable = null;
        isEmpty = true;
        return prop;
    }
    
    public void SetActive(bool active) => isValid = active;
}