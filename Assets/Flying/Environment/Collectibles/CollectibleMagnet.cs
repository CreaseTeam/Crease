using Crease.Flying.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Environment.Collectibles
{
    public class CollectibleMagnet : MonoBehaviour
    {
        /// <summary>
        /// a simple script controlling the magnitized behavior of collectibles
        /// </summary>
        [Header("Settings")]
        [Tooltip("Seconds for a magnetized collectible to arc all the way into the player.")]
        [FormerlySerializedAs("_totalTime")]
        [SerializeField] private float _arcDuration = 0.5f;

        [Tooltip("Eases arc progress over time (x: 0-1 elapsed, y: 0-1 progress along the arc). An ease-in curve makes the coin appear to accelerate as it comes in.")]
        [FormerlySerializedAs("_speedFloorCurve")]
        [SerializeField] private AnimationCurve _progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Interception Arc")]
        [Tooltip("Shapes the parabolic arc the coin follows in, so it approaches from the side instead of chasing directly behind the plane.")]
        [SerializeField] private MagnetArcSettings _arc = new MagnetArcSettings();

        private KinematicBody _playerBody;

        private void Awake()
        {
            // The magnet is a trigger on (or under) the plane; grab the body so we can read its velocity.
            _playerBody = GetComponentInParent<KinematicBody>();
        }

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

            GameObject player = _playerBody != null ? _playerBody.gameObject : gameObject;
            coin.Magnetize(player, _playerBody, _arcDuration, _progressCurve, _arc);
        }
    }
}
