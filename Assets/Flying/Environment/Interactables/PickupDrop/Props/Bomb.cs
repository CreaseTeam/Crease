using System;
using UnityEngine;

public class BombPropEntry : PropEntity
{
    [SerializeField] private Vector3 pickupScale = Vector3.one;
    
    private Rigidbody rb;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public override void OnPickUp(Transform mountingSlot)
    {
        isInAir = true;
        transform.SetParent(mountingSlot);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = pickupScale;
        rb.isKinematic = true;
    }

    public override void OnThrow(Vector3 startVelocity)
    {
        transform.SetParent(null);
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = startVelocity;
    }

    public override void OnLand()
    {
        isInAir = false;
        Debug.Log($"Prop {gameObject.name} landed");
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Ground") && isInAir)
        {
            OnLand();
        }
    }
}