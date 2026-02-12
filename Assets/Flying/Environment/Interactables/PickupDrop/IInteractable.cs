using UnityEngine;

public interface IInteractable
{
    bool IsValid { get; }
    float Weight { get; }
    E_PropCategory Category { get; }

    public void OnPickUp(Transform mountingSocket);
    public void OnThrow(Vector3 startVelocity);
    public void OnLand();
}