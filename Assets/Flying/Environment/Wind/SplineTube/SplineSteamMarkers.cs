using System.Collections.Generic;
using UnityEngine;

namespace Crease.Flying.Environment.Wind.SplineTube
{
    [ExecuteAlways]
    public class SplineSteamMarkers : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SplineTubeTrigger _tubeTrigger;
        [SerializeField] private ParticleSystem _steamPrefab;

        [Header("Continuous Steam Placement")]
        [SerializeField] private float _verticalOffset = 0.25f;
        [SerializeField] private float _sideOffset = 0f;
        [SerializeField] private float _steamWidth = 1.5f;
        [SerializeField] private float _steamHeight = 1.5f;
        [SerializeField] private float _segmentLengthMultiplier = 1.15f;

        [Header("Steam Particle Settings")]
        [SerializeField] private float _startSpeed = 1.5f;
        [SerializeField] private float _startLifetime = 1.5f;
        [SerializeField] private float _emissionRate = 35f;

        private readonly List<ParticleSystem> _spawned = new List<ParticleSystem>();

        private void Reset()
        {
            _tubeTrigger = GetComponent<SplineTubeTrigger>();
        }

        private void Start()
        {
            RebuildSteam();
        }

        private void OnValidate()
        {
            RebuildSteam();
        }

        public void RebuildSteam()
        {
            if (_tubeTrigger == null)
                _tubeTrigger = GetComponent<SplineTubeTrigger>();

            if (_tubeTrigger == null || _steamPrefab == null) return;
            if (_tubeTrigger.Rings == null || _tubeTrigger.Rings.Count < 2) return;

            ClearSteam();

            for (int i = 0; i < _tubeTrigger.Rings.Count - 1; i++)
            {
                SplineTubeTrigger.TubeRing a = _tubeTrigger.Rings[i];
                SplineTubeTrigger.TubeRing b = _tubeTrigger.Rings[i + 1];

                Vector3 start = a.Position;
                Vector3 end = b.Position;

                Vector3 direction = end - start;
                float length = direction.magnitude;

                if (length <= 0.001f)
                    continue;

                direction.Normalize();

                Vector3 up = a.Up.sqrMagnitude > 0.001f
                    ? a.Up.normalized
                    : Vector3.up;

                Vector3 right = Vector3.Cross(up, direction).normalized;

                Vector3 mid =
                    (start + end) * 0.5f +
                    up * _verticalOffset +
                    right * _sideOffset;

                Quaternion rotation = Quaternion.LookRotation(direction, up);

                ParticleSystem steam = Instantiate(_steamPrefab, mid, rotation, transform);
                steam.name = $"SplineSteamSegment_{i:D3}";

                ConfigureSteamAsSegment(steam, length);

                _spawned.Add(steam);
            }
        }

        private void ConfigureSteamAsSegment(ParticleSystem steam, float length)
        {
            var main = steam.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            // Important: don't let prefab's default start speed shoot upward.
            main.startSpeed = 0f;
            main.startLifetime = _startLifetime;

            var emission = steam.emission;
            emission.enabled = true;
            emission.rateOverTime = _emissionRate;

            var shape = steam.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(
                _steamWidth,
                _steamHeight,
                length * _segmentLengthMultiplier
            );

            shape.randomDirectionAmount = 0f;
            shape.alignToDirection = false;

            // Force particles to move along this steam object's LOCAL Z.
            // Since the object is rotated with Quaternion.LookRotation(direction, up),
            // local Z = spline direction.
            var velocity = steam.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = 0f;
            velocity.y = 0f;
            velocity.z = _startSpeed;

            // Optional: prevent prefab settings from pushing it upward.
            var force = steam.forceOverLifetime;
            force.enabled = false;
        }

        private void ClearSteam()
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                if (_spawned[i] == null) continue;

                if (Application.isPlaying)
                    Destroy(_spawned[i].gameObject);
                else
                    DestroyImmediate(_spawned[i].gameObject);
            }

            _spawned.Clear();
        }

        private void OnDisable()
        {
            ClearSteam();
        }
    }
}