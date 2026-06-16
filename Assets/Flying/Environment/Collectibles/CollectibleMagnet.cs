using Crease.Flying.Player;
using UnityEngine;

namespace Crease.Flying.Environment.Collectibles
{
    public class CollectibleMagnet : MonoBehaviour
    {
        /// <summary>
        /// a simple script controlling the magnitized behavior of collectibles
        /// </summary>
        [Header("Settings")]
        [Tooltip("The speed of the magnitized objects")]
        [SerializeField] private float _maxSpeed = 10f;

        private void OnTriggerEnter(Collider other)
        {
            Collectible coin = other.GetComponent<Collectible>();
            if (coin == null) return;

            Vector3 origin = transform.position;
            Vector3 target = coin.transform.position;
            Vector3 direction = target - origin;
            float distance = direction.magnitude;

            if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance))
            {
                if (hit.collider.GetComponent<Collectible>() == null)
                    return;
            }

            coin.Magnetize(gameObject, _maxSpeed);
        }
    }
}
