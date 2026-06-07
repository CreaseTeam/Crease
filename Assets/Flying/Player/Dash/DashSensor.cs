using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player.Dash
{
    public class DashSensor : MonoBehaviour
    {
        [FormerlySerializedAs("dashController")]
        [SerializeField] private DashController _dashController;

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Obstacle") || other.CompareTag("Ground"))
            {
                _dashController.ModifyObjectsInRange(1);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Obstacle") || other.CompareTag("Ground"))
            {
                _dashController.ModifyObjectsInRange(-1);
            }
        }
    }
}
