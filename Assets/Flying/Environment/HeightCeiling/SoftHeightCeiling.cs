using UnityEngine;
using Crease.Flying.Player;

namespace Crease.Flying.Environment
{

    /// Soft height ceiling: bleeds off upward speed with a stopping-distance force
    /// so the plane feels like it ran out of climb. 
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(BoxCollider))]
    public class SoftHeightCeiling : MonoBehaviour
    {
        [Header("Soft Force")]
        [Tooltip("Multiplier on the kinematic stopping acceleration (v² / 2s). 1 = stop exactly at the ceiling in continuous time.")]
        [SerializeField] private float _strength = 1.1f;

        [Tooltip("Minimum stopping distance used in the force formula. Avoids division-by-zero and softens the last centimeters.")]
        [SerializeField] private float _epsilon = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool _debugLog = false;

        private BoxCollider _collider;
        private KinematicBody _playerBody;
        private Transform _playerTransform;
        private Rigidbody _playerRigidbody;
        private bool _holdAfterTopExit;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_playerBody != null) return;

            if (!other.TryGetComponent(out KinematicBody body)) return;

            _playerBody = body;
            _playerTransform = other.transform;
            _playerRigidbody = other.attachedRigidbody;
            _holdAfterTopExit = false;

            if (_debugLog) Debug.Log("[SoftHeightCeiling] Player entered ceiling zone.");
        }

        private void OnTriggerExit(Collider other)
        {
            if (_playerTransform == null || other.gameObject != _playerTransform.gameObject) return;

            GetCeilingBounds(out float ceilingY, out _, out _);

            // Exited through the top (e.g. tunneled in one physics step) — keep clamping
            // until the player is back below the ceiling or leaves the horizontal footprint.
            if (_playerRigidbody != null && _playerRigidbody.position.y >= ceilingY - 0.01f)
            {
                _holdAfterTopExit = true;
                if (_debugLog) Debug.Log("[SoftHeightCeiling] Player exited through top — holding for clamp.");
                return;
            }

            ClearPlayer();
            if (_debugLog) Debug.Log("[SoftHeightCeiling] Player exited ceiling zone.");
        }

        private void FixedUpdate()
        {
            if (_playerBody == null)
            {
                if (_playerTransform != null) ClearPlayer();
                return;
            }

            GetCeilingBounds(out float ceilingY, out float bottomY, out _);

            Vector3 playerPos = _playerRigidbody != null
                ? _playerRigidbody.position
                : _playerTransform.position;
            float playerY = playerPos.y;

            if (_holdAfterTopExit)
            {
                // Release only once the player leaves the footprint or falls below the volume.
                if (!IsInsideHorizontalBounds(playerPos) || playerY < bottomY)
                {
                    ClearPlayer();
                    return;
                }

                // Back below the ceiling — resume normal in-volume tracking.
                if (playerY < ceilingY - 0.01f)
                    _holdAfterTopExit = false;
            }

            float remainingDist = ceilingY - playerY;
            float dt = Time.fixedDeltaTime * _playerBody.SimulationSpeed;
            if (dt < 1e-6f) dt = Time.fixedDeltaTime;

            // Soft force — kinematic brake: a = v² / (2s). No force when not climbing.
            float upwardVel = _playerBody.Velocity.y;
            if (upwardVel > 0f && remainingDist > 0f)
            {
                float stoppingDist = Mathf.Max(remainingDist, _epsilon);
                float forceMag = (upwardVel * upwardVel) / (2f * stoppingDist) * _strength;

                if (_debugLog)
                    Debug.Log($"[SoftHeightCeiling] Soft force: {forceMag:F2} down | remaining={remainingDist:F2} | vy={upwardVel:F2}");

                _playerBody.AddAcceleration(Vector3.down * forceMag);
            }

            // Hard stop — if this frame would breach the ceiling, limit vy and clamp position.
            Vector3 vel = _playerBody.Velocity;
            float predictedY = playerY + vel.y * dt;

            if (playerY > ceilingY || predictedY > ceilingY)
            {
                if (playerY >= ceilingY)
                {
                    if (vel.y > 0f)
                        vel.y = 0f;

                    Vector3 clamped = playerPos;
                    clamped.y = ceilingY;
                    if (_playerRigidbody != null)
                        _playerRigidbody.position = clamped;
                    else
                        _playerTransform.position = clamped;
                }
                else
                {
                    // Reach exactly the ceiling this step instead of overshooting.
                    vel.y = (ceilingY - playerY) / dt;
                }

                _playerBody.Velocity = vel;

                if (_debugLog)
                    Debug.Log($"[SoftHeightCeiling] HARD STOP — vy={vel.y:F2}, playerY={playerY:F2}, ceilingY={ceilingY:F2}");
            }
        }

        private void GetCeilingBounds(out float ceilingY, out float bottomY, out float height)
        {
            Vector3 worldCenter = transform.TransformPoint(_collider.center);
            float halfH = _collider.size.y * transform.lossyScale.y * 0.5f;
            ceilingY = worldCenter.y + halfH;
            bottomY = worldCenter.y - halfH;
            height = ceilingY - bottomY;
        }

        private bool IsInsideHorizontalBounds(Vector3 worldPosition)
        {
            Bounds bounds = _collider.bounds;
            return worldPosition.x >= bounds.min.x && worldPosition.x <= bounds.max.x
                && worldPosition.z >= bounds.min.z && worldPosition.z <= bounds.max.z;
        }

        private void ClearPlayer()
        {
            _playerBody = null;
            _playerTransform = null;
            _playerRigidbody = null;
            _holdAfterTopExit = false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_collider == null) _collider = GetComponent<BoxCollider>();
            if (_collider == null) return;

            Vector3 worldCenter = transform.TransformPoint(_collider.center);
            Vector3 size = new Vector3(
                _collider.size.x * transform.lossyScale.x,
                _collider.size.y * transform.lossyScale.y,
                _collider.size.z * transform.lossyScale.z);

            float halfH = size.y * 0.5f;
            float ceilingY = worldCenter.y + halfH;

            // Ceiling plane — red
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.45f);
            Gizmos.DrawCube(
                new Vector3(worldCenter.x, ceilingY, worldCenter.z),
                new Vector3(size.x, 0.05f, size.z));

            // Full trigger volume — cyan
            Gizmos.color = new Color(0f, 0.9f, 1f, 0.35f);
            Gizmos.DrawCube(worldCenter, size);
        }
#endif
    }
}
