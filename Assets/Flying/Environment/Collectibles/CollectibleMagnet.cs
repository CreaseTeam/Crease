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
        [Tooltip("The minimum speed of the magnitized objects")]
        [SerializeField] private float _minSpeed = 0f;
        [Tooltip("Time to reach max speed")]
        [SerializeField] private float _totalTime = 2f;
        [Tooltip("Normalized speed floor over magnetization time (x: 0-1 elapsed, y: 0-1 from min to max speed).")]
        [SerializeField] private AnimationCurve _speedFloorCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

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

            coin.Magnetize(gameObject, _minSpeed, _maxSpeed, _totalTime, _speedFloorCurve);
        }
    }
}
