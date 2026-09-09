using System;
using System.Collections;
using Crease.Flying.Environment.Obstacle;
using Crease.Flying.Player;
using Crease.Flying.Player.Abilities;
using Crease.Folding.Paper;
using PlayerHealth = Crease.Flying.Player.Health.Health;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Crease.Managers
{
    [Serializable]
    public struct RespawnPose
    {
        public Vector3 Location;
        public Quaternion Rotation;

        public RespawnPose(Vector3 location, Quaternion rotation)
        {
            Location = location;
            Rotation = rotation;
        }

        public static RespawnPose FromTransform(Transform source)
        {
            return new RespawnPose(source.position, source.rotation);
        }
    }

    /// <summary>
    /// Singleton that owns flight-gameplay state: respawn pose and death/respawn flow.
    /// </summary>
    public class FlyingManager : MonoBehaviour
    {
        public static FlyingManager Instance { get; private set; }

        public RespawnPose Respawn { get; private set; } = new RespawnPose(Vector3.zero, Quaternion.identity);
        public bool IsDying { get; private set; }

        [SerializeField]
        [Tooltip("Seconds to wait after the plane crashes before respawning.")]
        private float _respawnDelay = 2f;

        private Transform _player;
        private KinematicBody _body;
        private FlightController _flightController;
        private FlightCollisionController _collisionController;
        private PlayerCrashHandler _crashHandler;
        private PlayerHealth _health;
        private AbilityController _abilityController;
        private Coroutine _respawnRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
            CachePlayer();
            InitializeRespawnFromPlayer();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;

            StopDeathRoutine();
            CachePlayer();
            InitializeRespawnFromPlayer();
        }

        public void SetRespawn(RespawnPose respawn)
        {
            Respawn = respawn;
        }

        public void HandleDeath()
        {
            if (IsDying)
                return;

            CachePlayer();
            if (_player == null)
            {
                Debug.LogWarning("FlyingManager: cannot handle death because no player was found.");
                return;
            }

            IsDying = true;

            if (_collisionController != null)
                _collisionController.StopRecovery();

            if (_abilityController != null)
                _abilityController.enabled = false;

            if (_crashHandler != null)
                _crashHandler.Crash();
            else
                Debug.LogWarning("FlyingManager: no PlayerCrashHandler on the player; crash fall will be skipped.");

            _respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(_respawnDelay);
            _respawnRoutine = null;
            RespawnPlayer();
        }

        private void RespawnPlayer()
        {
            CachePlayer();
            if (_player == null)
            {
                IsDying = false;
                Debug.LogWarning("FlyingManager: cannot respawn because no player was found.");
                return;
            }

            if (_body != null)
                _body.Teleport(Respawn.Location, Respawn.Rotation);
            else
                _player.SetPositionAndRotation(Respawn.Location, Respawn.Rotation);

            if (_crashHandler != null)
                _crashHandler.ResetCrash();

            if (_flightController != null)
            {
                _flightController.ResetOrientationFromTransform();
                _flightController.ResetVelocityToInitial();
            }

            if (_collisionController != null)
                _collisionController.StopRecovery();

            if (_health != null)
                _health.RestoreFullHealth();

            if (_abilityController != null)
                _abilityController.enabled = true;

            IsDying = false;
        }

        private void StopDeathRoutine()
        {
            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
            }

            IsDying = false;
        }

        private void InitializeRespawnFromPlayer()
        {
            if (_player == null)
            {
                Debug.LogWarning("FlyingManager: no player found; respawn pose was not initialized.");
                return;
            }

            SetRespawn(RespawnPose.FromTransform(_player));
        }

        private void CachePlayer()
        {
            Transform player = FindPlayerTransform();
            if (player == _player && _player != null)
                return;

            _player = player;
            _body = null;
            _flightController = null;
            _collisionController = null;
            _crashHandler = null;
            _health = null;
            _abilityController = null;

            if (_player == null)
                return;

            _body = _player.GetComponent<KinematicBody>();
            _flightController = _player.GetComponent<FlightController>();
            _collisionController = _player.GetComponent<FlightCollisionController>();
            _crashHandler = _player.GetComponent<PlayerCrashHandler>();
            _health = _player.GetComponent<PlayerHealth>();
            _abilityController = _player.GetComponent<AbilityController>();
        }

        private static Transform FindPlayerTransform()
        {
            if (FoldingManager.Instance != null && FoldingManager.Instance.Player != null)
                return FoldingManager.Instance.Player.transform;

            GameObject playerObject = GameObject.FindWithTag("Player");
            return playerObject != null ? playerObject.transform : null;
        }
    }
}
