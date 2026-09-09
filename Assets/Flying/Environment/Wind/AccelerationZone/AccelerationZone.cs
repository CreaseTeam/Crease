using Crease.Flying.Player;
using UnityEngine;

namespace Crease.Flying.Environment.Wind
{
    /// <summary>
    /// While the player is inside this trigger, applies a constant acceleration
    /// in world space or in the player's local space. An optional speed cap
    /// refuses any part of that acceleration that would raise total speed above the cap.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Crease/Acceleration Zone")]
    public class AccelerationZone : MonoBehaviour
    {
        public enum DirectionSpace
        {
            [InspectorName("World")]
            World,
            [InspectorName("Player")]
            Player
        }

        [Header("Acceleration")]
        [Tooltip("Acceleration applied each physics step, in meters per second squared.")]
        [SerializeField] private float _acceleration = 10f;

        [Tooltip("Axis of the acceleration. World space uses the zone transform; Player space uses the player's transform.")]
        [SerializeField] private Vector3 _direction = Vector3.forward;

        [Tooltip("World aims this direction through the zone's orientation. Player aims it through the player's local space.")]
        [SerializeField] private DirectionSpace _directionSpace = DirectionSpace.World;

        [Header("Speed Cap")]
        [Tooltip("If enabled, this zone will not raise the player's speed above Speed Cap. It will not slow them down if they are already faster.")]
        [SerializeField] private bool _limitSpeed;

        [Tooltip("Maximum speed this zone is allowed to accelerate the player to.")]
        [SerializeField] [Min(0f)] private float _speedCap = 40f;

        public float Acceleration => _acceleration;
        public Vector3 Direction => _direction;
        public DirectionSpace Space => _directionSpace;
        public bool LimitSpeed => _limitSpeed;
        public float SpeedCap => _speedCap;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnValidate()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;

            if (_direction.sqrMagnitude < 0.0001f)
                _direction = Vector3.forward;

            _speedCap = Mathf.Max(0f, _speedCap);
        }

        private void OnTriggerEnter(Collider other)
        {
            FlightForceReceiver receiver = FindReceiver(other);
            if (receiver != null)
                receiver.AddAccelerationZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            FlightForceReceiver receiver = FindReceiver(other);
            if (receiver != null)
                receiver.RemoveAccelerationZone(this);
        }

        /// <summary>
        /// World-space acceleration for this step, with the speed cap already applied.
        /// </summary>
        public Vector3 EvaluateAcceleration(Transform player, Vector3 velocity, float deltaTime)
        {
            Vector3 acceleration = ResolveDirection(player) * _acceleration;
            if (!_limitSpeed)
                return acceleration;

            return LimitAccelerationToSpeedCap(velocity, acceleration, _speedCap, deltaTime);
        }

        private Vector3 ResolveDirection(Transform player)
        {
            Vector3 axis = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Vector3.forward;
            if (_directionSpace == DirectionSpace.Player && player != null)
                return player.TransformDirection(axis);
            return transform.TransformDirection(axis);
        }

        /// <summary>
        /// Drops only the part of <paramref name="acceleration"/> that would raise speed above
        /// <paramref name="speedCap"/>. Already-faster motion is left alone.
        /// </summary>
        public static Vector3 LimitAccelerationToSpeedCap(
            Vector3 velocity,
            Vector3 acceleration,
            float speedCap,
            float deltaTime)
        {
            if (speedCap <= 0f || deltaTime <= 0f)
                return Vector3.zero;

            Vector3 proposed = velocity + acceleration * deltaTime;
            float capSq = speedCap * speedCap;
            if (proposed.sqrMagnitude <= capSq)
                return acceleration;

            float currentSpeedSq = velocity.sqrMagnitude;
            if (currentSpeedSq >= capSq)
            {
                if (currentSpeedSq < 0.0001f)
                    return Vector3.zero;

                Vector3 velocityDir = velocity / Mathf.Sqrt(currentSpeedSq);
                float accelAlongVelocity = Vector3.Dot(acceleration, velocityDir);
                if (accelAlongVelocity > 0f)
                    acceleration -= velocityDir * accelAlongVelocity;
                return acceleration;
            }

            float proposedSpeed = Mathf.Sqrt(proposed.sqrMagnitude);
            if (proposedSpeed < 0.0001f)
                return Vector3.zero;

            Vector3 cappedVelocity = proposed * (speedCap / proposedSpeed);
            return (cappedVelocity - velocity) / deltaTime;
        }

        private static FlightForceReceiver FindReceiver(Collider other)
        {
            if (other.attachedRigidbody != null)
            {
                FlightForceReceiver onBody = other.attachedRigidbody.GetComponent<FlightForceReceiver>();
                if (onBody != null)
                    return onBody;
            }

            return other.GetComponentInParent<FlightForceReceiver>();
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.25f);
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawCube(col.bounds.center, col.bounds.size);
                Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.85f);
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }

            Vector3 origin = transform.position;
            Vector3 previewDir = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Vector3.forward;
            previewDir = transform.TransformDirection(previewDir);

            float length = 2f + Mathf.Abs(_acceleration) * 0.05f;
            Gizmos.color = _directionSpace == DirectionSpace.Player
                ? new Color(0.3f, 0.85f, 1f, 0.95f)
                : new Color(1f, 0.75f, 0.2f, 0.95f);
            DrawArrow(origin, previewDir, length);
        }

        static void DrawArrow(Vector3 origin, Vector3 direction, float length)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;

            Vector3 tip = origin + direction.normalized * length;
            Gizmos.DrawLine(origin, tip);

            Vector3 back = -direction.normalized;
            Vector3 right = Vector3.Cross(direction, Vector3.up);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.Cross(direction, Vector3.right);
            right.Normalize();
            Vector3 up = Vector3.Cross(right, direction).normalized;
            float head = length * 0.2f;
            Gizmos.DrawLine(tip, tip + (back + right) * head);
            Gizmos.DrawLine(tip, tip + (back - right) * head);
            Gizmos.DrawLine(tip, tip + (back + up) * head);
            Gizmos.DrawLine(tip, tip + (back - up) * head);
        }
#endif
    }
}
