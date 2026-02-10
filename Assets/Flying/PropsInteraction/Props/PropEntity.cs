using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public abstract class PropEntity : MonoBehaviour, IInteractable
{
    [HideInInspector] public string id;
    public string Id => id;
    public E_PropCategory category;
    public float weight;
    public bool isValid;
    protected bool isInAir;

    public virtual bool IsValid => isValid && !isInAir;
    public float Weight => weight;
    public E_PropCategory Category => category;

    protected PropEntity()
    {
        id = System.Guid.NewGuid().ToString();
        isInAir = false;
    }

    public abstract void OnPickUp(Transform mountingSocket);
    public abstract void OnThrow(Vector3 startVelocity);
    public abstract void OnLand();
}