using UnityEngine;

namespace Crease.Flying.Player.Abilities
{
    public class AbilityProximitySensor : MonoBehaviour
    {
        [SerializeField] private AbilityController _abilityController;

        private void Awake()
        {
            if (_abilityController == null)
                _abilityController = GetComponentInParent<AbilityController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Obstacle") || other.CompareTag("Ground"))
                _abilityController?.AdjustProximitySourceCount(1);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Obstacle") || other.CompareTag("Ground"))
                _abilityController?.AdjustProximitySourceCount(-1);
        }
    }
}
