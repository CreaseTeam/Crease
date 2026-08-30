using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Crease.Flying.Environment
{
    /// <summary>
    /// Plays a sequential "ink drop" reveal along a spline. Call <see cref="TriggerBreadcrumbs"/>
    /// to start the animation. Requires a <see cref="SplineContainer"/> on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class InkBreadcrumbs : MonoBehaviour
    {
        [Header("Ink Drop")]
        [Tooltip("Prefab instantiated at each breadcrumb position along the spline.")]
        [SerializeField] private GameObject _inkDropPrefab;

        [Tooltip("Scale multiplier applied to each spawned drop.")]
        [SerializeField, Min(0.01f)] private float _dropScale = 1f;

        [Tooltip("If true, each drop is rotated to face along the spline tangent.")]
        [SerializeField] private bool _alignToSpline = true;

        [Tooltip("Random offset (world units) applied perpendicular to the spline so drops do not sit on a perfect line.")]
        [SerializeField, Min(0f)] private float _positionJitter = 0f;

        [Header("Layout")]
        [Tooltip("Distance along the spline between consecutive drops.")]
        [SerializeField, Min(0.01f)] private float _dropSpacing = 2f;

        [Tooltip("Distance from the spline start before the first drop is placed.")]
        [SerializeField, Min(0f)] private float _startDistance = 0f;

        [Tooltip("Distance from the spline end left empty.")]
        [SerializeField, Min(0f)] private float _endPadding = 0f;

        [Tooltip("Safety cap on how many drops a single reveal can spawn.")]
        [SerializeField, Min(1)] private int _maxDropCount = 256;

        [Header("Animation")]
        [Tooltip("How quickly the reveal travels along the spline, in units per second.")]
        [SerializeField, Min(0.01f)] private float _revealSpeed = 8f;

        [Tooltip("Seconds each drop takes to scale in after it is spawned. 0 snaps to full scale.")]
        [SerializeField, Min(0f)] private float _appearDuration = 0.2f;

        [Tooltip("Scale-over-time curve for the appear animation. X = normalized time, Y = scale factor.")]
        [SerializeField] private AnimationCurve _appearScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Seconds to wait after TriggerBreadcrumbs before the first drop appears.")]
        [SerializeField, Min(0f)] private float _startDelay = 0f;

        [Tooltip("If true, a new trigger destroys already-spawned drops before playing again.")]
        [SerializeField] private bool _clearOnRetrigger = true;

        private SplineContainer _splineContainer;
        private readonly List<GameObject> _spawnedDrops = new List<GameObject>();
        private Coroutine _revealRoutine;
        private Vector3 _prefabLocalScale = Vector3.one;

        public bool IsRevealing { get; private set; }

        private void Awake()
        {
            _splineContainer = GetComponent<SplineContainer>();
        }

        /// <summary>
        /// Starts the sequential ink-drop reveal along the spline.
        /// </summary>
        [ContextMenu("Trigger Breadcrumbs")]
        public void TriggerBreadcrumbs()
        {
            if (_inkDropPrefab == null)
            {
                Debug.LogWarning($"{nameof(InkBreadcrumbs)} on {name} has no ink drop prefab assigned.", this);
                return;
            }

            if (_splineContainer == null)
                _splineContainer = GetComponent<SplineContainer>();

            if (_splineContainer == null || _splineContainer.Spline == null || _splineContainer.Spline.Count < 2)
            {
                Debug.LogWarning($"{nameof(InkBreadcrumbs)} on {name} needs a SplineContainer with at least 2 knots.", this);
                return;
            }

            StopReveal();

            if (_clearOnRetrigger)
                DestroySpawnedDrops();

            _prefabLocalScale = _inkDropPrefab.transform.localScale;
            _revealRoutine = StartCoroutine(RevealRoutine());
        }

        /// <summary>
        /// Stops a running reveal and destroys all spawned drops.
        /// </summary>
        [ContextMenu("Clear Breadcrumbs")]
        public void ClearBreadcrumbs()
        {
            StopReveal();
            DestroySpawnedDrops();
        }

        private IEnumerator RevealRoutine()
        {
            IsRevealing = true;

            if (_startDelay > 0f)
                yield return new WaitForSeconds(_startDelay);

            float splineLength = _splineContainer.CalculateLength();
            float endDistance = Mathf.Max(_startDistance, splineLength - _endPadding);
            float delay = _dropSpacing / _revealSpeed;

            if (splineLength <= 0.0001f || endDistance < _startDistance)
            {
                SpawnDropAtDistance(0f, Mathf.Max(splineLength, 0.0001f));
                FinishReveal();
                yield break;
            }

            int spawned = 0;
            float distance = _startDistance;

            while (distance <= endDistance + 0.0001f && spawned < _maxDropCount)
            {
                SpawnDropAtDistance(distance, splineLength);
                spawned++;

                float nextDistance = distance + _dropSpacing;
                if (nextDistance > endDistance + 0.0001f || spawned >= _maxDropCount)
                    break;

                yield return new WaitForSeconds(delay);
                distance = nextDistance;
            }

            FinishReveal();
        }

        private IEnumerator AppearRoutine(Transform drop, Vector3 targetScale)
        {
            if (_appearDuration <= 0f)
            {
                if (drop != null)
                    drop.localScale = targetScale;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < _appearDuration)
            {
                if (drop == null)
                    yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _appearDuration);
                drop.localScale = targetScale * _appearScaleCurve.Evaluate(t);
                yield return null;
            }

            if (drop != null)
                drop.localScale = targetScale;
        }

        private void SpawnDropAtDistance(float distance, float splineLength)
        {
            float t = SplineUtility.GetNormalizedInterpolation(
                _splineContainer.Spline,
                Mathf.Clamp(distance, 0f, splineLength),
                PathIndexUnit.Distance);

            _splineContainer.Evaluate(t, out float3 position, out float3 tangent, out float3 up);

            Quaternion rotation = Quaternion.identity;
            if (_alignToSpline && math.lengthsq(tangent) > 0.0001f)
            {
                Vector3 tangentDir = math.normalize(tangent);
                Vector3 upDir = math.lengthsq(up) > 0.0001f ? (Vector3)math.normalize(up) : Vector3.up;
                rotation = Quaternion.LookRotation(tangentDir, upDir);
            }

            if (_positionJitter > 0f)
                position += (float3)RandomPerpendicularOffset(tangent);

            GameObject drop = Instantiate(_inkDropPrefab, position, rotation, transform);
            Vector3 targetScale = drop.transform.localScale;
            targetScale.x *= _dropScale;
            targetScale.y *= _dropScale;
            targetScale.z *= _dropScale;

            if (_appearDuration > 0f)
                drop.transform.localScale = Vector3.zero;

            _spawnedDrops.Add(drop);
            StartCoroutine(AppearRoutine(drop.transform, targetScale));
        }

        private Vector3 RandomPerpendicularOffset(float3 tangent)
        {
            Vector3 random = UnityEngine.Random.insideUnitSphere * _positionJitter;
            if (math.lengthsq(tangent) <= 0.0001f)
                return random;

            Vector3 tangentDir = math.normalize(tangent);
            return random - tangentDir * Vector3.Dot(random, tangentDir);
        }

        private void StopReveal()
        {
            StopAllCoroutines();
            _revealRoutine = null;
            IsRevealing = false;

            Vector3 targetScale = _prefabLocalScale * _dropScale;
            foreach (GameObject drop in _spawnedDrops)
            {
                if (drop != null)
                    drop.transform.localScale = targetScale;
            }
        }

        private void FinishReveal()
        {
            IsRevealing = false;
            _revealRoutine = null;
        }

        private void DestroySpawnedDrops()
        {
            foreach (GameObject drop in _spawnedDrops)
            {
                if (drop == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(drop);
                else
                    DestroyImmediate(drop);
            }

            _spawnedDrops.Clear();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            StopReveal();
        }

        private void OnDestroy()
        {
            DestroySpawnedDrops();
        }

        private void OnDrawGizmosSelected()
        {
            if (_splineContainer == null)
                _splineContainer = GetComponent<SplineContainer>();

            if (_splineContainer == null || _splineContainer.Spline == null || _splineContainer.Spline.Count < 2)
                return;

            float splineLength = _splineContainer.CalculateLength();
            if (splineLength <= 0f)
                return;

            float endDistance = Mathf.Max(_startDistance, splineLength - _endPadding);
            Gizmos.color = new Color(0.12f, 0.08f, 0.16f, 0.9f);

            int drawn = 0;
            for (float distance = _startDistance; distance <= endDistance + 0.0001f && drawn < _maxDropCount; distance += _dropSpacing)
            {
                float t = SplineUtility.GetNormalizedInterpolation(
                    _splineContainer.Spline,
                    Mathf.Clamp(distance, 0f, splineLength),
                    PathIndexUnit.Distance);
                float3 position = _splineContainer.EvaluatePosition(t);
                Gizmos.DrawSphere(position, 0.12f * _dropScale);
                drawn++;
            }
        }
    }
}
