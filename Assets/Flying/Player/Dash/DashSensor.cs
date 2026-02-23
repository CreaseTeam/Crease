using System;
using UnityEngine;

public class DashSensor : MonoBehaviour
{
    [SerializeField] private DashController dashController;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle") || other.CompareTag("Ground"))
        {
            dashController.ModifyObjectsInRange(1);
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Obstacle") || other.CompareTag("Ground"))
        {
            dashController.ModifyObjectsInRange(-1);
        }
    }
}
