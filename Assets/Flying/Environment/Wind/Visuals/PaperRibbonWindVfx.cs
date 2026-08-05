using Crease.Flying.Player;
using UnityEngine;
using UnityEngine.VFX;

namespace Crease.Flying.Environment.Wind.Visuals
{
    /// <summary>
    /// Drives the ambient paper ribbon visual effect.
    ///
    /// Keeps the spawn volume anchored to the player, biased ahead of travel so ribbons
    /// appear in front and stream past rather than being left behind. Blends an ambient
    /// breeze with any wind zones the player is currently inside, and subtracts a share
    /// of the player's own velocity so speed reads as airflow.
    ///
    /// When the blended flow direction swings past a threshold, a subset of ribbons
    /// (flagged per particle inside the graph) spiral into loop-de-loops that decay back
    /// to ordinary drift.
    ///
    /// The graph does the simulation. This only computes and pushes exposed properties.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(VisualEffect))]
    public class PaperRibbonWindVfx : MonoBehaviour
    {
        // Exposed property names on PaperRibbonAmbient.vfx. These must match the graph
        // blackboard exactly. Anything missing is skipped rather than warned about every
        // frame, so a partly hand finished graph still runs.
        private static readonly int SpawnRateId = Shader.PropertyToID("SpawnRate");
        private static readonly int SpawnCenterId = Shader.PropertyToID("SpawnCenter");
        private static readonly int SpawnBoxSizeId = Shader.PropertyToID("SpawnBoxSize");
        private static readonly int FlowVelocityId = Shader.PropertyToID("FlowVelocity");
        private static readonly int FlowStrengthId = Shader.PropertyToID("FlowStrength");
        private static readonly int TurbulenceIntensityId = Shader.PropertyToID("TurbulenceIntensity");
        private static readonly int LoopAxisId = Shader.PropertyToID("LoopAxis");
        private static readonly int LoopOmegaId = Shader.PropertyToID("LoopOmega");
        private static readonly int LoopIntensityId = Shader.PropertyToID("LoopIntensity");
        private static readonly int SizeScaleId = Shader.PropertyToID("SizeScale");

        [Header("Follow")]
        [Tooltip("Transform the spawn volume is anchored to. Defaults to the main camera.")]
        [SerializeField] private Transform _followTarget;

        [Tooltip("Optional. Player body, used to bias spawning ahead of travel and to make speed read as airflow.")]
        [SerializeField] private KinematicBody _body;

        [Tooltip("Optional. Player force receiver, used to pick up the wind zones the player is currently inside.")]
        [SerializeField] private FlightForceReceiver _receiver;

        [Header("Flow")]
        [Tooltip("Ambient breeze direction in world space. Does not need to be normalised.")]
        [SerializeField] private Vector3 _ambientWindDirection = new Vector3(1f, 0.05f, 0.3f);

        [Tooltip("Ambient breeze speed in metres per second.")]
        [SerializeField] private float _ambientWindSpeed = 4f;

        [Tooltip("How much the wind zones the player is inside push the ribbons around.")]
        [Range(0f, 1f)]
        [SerializeField] private float _zoneWindInfluence = 0.6f;

        [Tooltip("Share of the player's own velocity subtracted from the flow. This is what makes ribbons stream past when flying fast.")]
        [Range(0f, 1f)]
        [SerializeField] private float _relativeStreamFactor = 0.5f;

        [Tooltip("Seconds for the flow direction to catch up to changes. Higher is lazier.")]
        [Range(0.02f, 2f)]
        [SerializeField] private float _flowSmoothing = 0.35f;

        [Tooltip("How hard the flow drags ribbons toward its own velocity.")]
        [SerializeField] private float _flowStrength = 0.8f;

        [Tooltip("Noise strength that keeps the drift from reading as a conveyor belt.")]
        [SerializeField] private float _turbulenceIntensity = 1.2f;

        [Header("Loop de loop")]
        [Tooltip("Degrees the flow direction must swing before flagged ribbons start looping.")]
        [Range(20f, 140f)]
        [SerializeField] private float _loopAngleThreshold = 70f;

        [Tooltip("Minimum seconds between loop bursts.")]
        [Range(0f, 20f)]
        [SerializeField] private float _loopCooldown = 6f;

        [Tooltip("Seconds for a loop burst to reach full strength.")]
        [Range(0.05f, 2f)]
        [SerializeField] private float _loopAttack = 0.35f;

        [Tooltip("Seconds for a loop burst to fade back to nothing.")]
        [Range(0.2f, 8f)]
        [SerializeField] private float _loopDecay = 2.5f;

        [Tooltip("Angular rate of the loops in radians per second. Loop radius is speed divided by this, so higher means tighter.")]
        [Range(0.2f, 6f)]
        [SerializeField] private float _loopOmega = 1.6f;

        [Header("Density")]
        [Tooltip("Master dial for the whole effect. This is the knob to reach for if it ever feels busy.")]
        [Range(0f, 1f)]
        [SerializeField] private float _density = 0.5f;

        [Tooltip("Spawn rate per second at full density.")]
        [SerializeField] private float _maxSpawnRate = 16f;

        [Tooltip("Size of the spawn volume in metres.")]
        [SerializeField] private Vector3 _spawnBoxSize = new Vector3(45f, 24f, 45f);

        [Tooltip("How far ahead of travel the spawn volume is pushed, as a fraction of its own depth.")]
        [Range(0f, 1f)]
        [SerializeField] private float _spawnAhead = 0.55f;

        [Tooltip("Uniform scale applied to every ribbon.")]
        [Range(0.1f, 4f)]
        [SerializeField] private float _sizeScale = 1f;

        [Tooltip("Stop spawning while the game is paused.")]
        [SerializeField] private bool _disableWhenPaused = true;

        // Time constant for the reference direction the fast flow is compared against.
        // Long enough that a sustained turn registers as a shift but a wobble does not.
        private const float SlowFlowSmoothing = 2f;

        private VisualEffect _vfx;

        private bool _hasSpawnRate;
        private bool _hasSpawnCenter;
        private bool _hasSpawnBoxSize;
        private bool _hasFlowVelocity;
        private bool _hasFlowStrength;
        private bool _hasTurbulenceIntensity;
        private bool _hasLoopAxis;
        private bool _hasLoopOmega;
        private bool _hasLoopIntensity;
        private bool _hasSizeScale;

        private Vector3 _flowFast;
        private Vector3 _flowSlow;
        private Vector3 _loopAxis = Vector3.up;
        private float _loopIntensity;
        private float _loopTarget;
        private float _nextLoopTime;
        private bool _flowInitialised;

        /// <summary>
        /// Current loop burst strength, 0 to 1. Exposed for debugging and for other
        /// systems that want to react to the same beat.
        /// </summary>
        public float LoopIntensity => _loopIntensity;

        /// <summary>
        /// Triggers a loop burst immediately, ignoring the angle threshold and cooldown.
        /// </summary>
        public void TriggerLoopBurst(Vector3 axis)
        {
            if (axis.sqrMagnitude > 1e-6f)
            {
                _loopAxis = axis.normalized;
            }

            _loopTarget = 1f;
            _nextLoopTime = Time.time + _loopCooldown;
        }

        private void OnEnable()
        {
            _vfx = GetComponent<VisualEffect>();
            CacheProperties();
            ResolveReferences();
            _flowInitialised = false;
        }

        private void OnValidate()
        {
            if (_vfx == null) _vfx = GetComponent<VisualEffect>();
            CacheProperties();
        }

        // The graph may be regenerated or hand finished, so check once on enable rather
        // than assuming every property exists.
        private void CacheProperties()
        {
            if (_vfx == null) return;

            _hasSpawnRate = _vfx.HasFloat(SpawnRateId);
            _hasSpawnCenter = _vfx.HasVector3(SpawnCenterId);
            _hasSpawnBoxSize = _vfx.HasVector3(SpawnBoxSizeId);
            _hasFlowVelocity = _vfx.HasVector3(FlowVelocityId);
            _hasFlowStrength = _vfx.HasFloat(FlowStrengthId);
            _hasTurbulenceIntensity = _vfx.HasFloat(TurbulenceIntensityId);
            _hasLoopAxis = _vfx.HasVector3(LoopAxisId);
            _hasLoopOmega = _vfx.HasFloat(LoopOmegaId);
            _hasLoopIntensity = _vfx.HasFloat(LoopIntensityId);
            _hasSizeScale = _vfx.HasFloat(SizeScaleId);
        }

        // Resolved at play time only. Doing this in edit mode would write serialized
        // fields and dirty every scene the prefab sits in.
        private void ResolveReferences()
        {
            if (!Application.isPlaying) return;

            if (_followTarget == null && Camera.main != null)
            {
                _followTarget = Camera.main.transform;
            }

            if (_body == null || _receiver == null)
            {
                // Only one player exists, and this is a visual only component, so a
                // find on enable is cheaper than wiring references in every scene.
                var body = FindFirstObjectByType<KinematicBody>();
                if (body != null)
                {
                    if (_body == null) _body = body;
                    if (_receiver == null) _receiver = body.GetComponent<FlightForceReceiver>();
                }
            }
        }

        // LateUpdate so the camera has already been moved this frame and the spawn
        // volume does not trail it by one frame.
        private void LateUpdate()
        {
            if (_vfx == null) return;

            bool playing = Application.isPlaying;

            // In edit mode the effect previews around wherever the object has been
            // placed. Following, and therefore moving the transform, is a play mode
            // behaviour only, so scenes never get dirtied by the preview.
            Transform anchorSource = playing && _followTarget != null ? _followTarget : transform;

            // Unscaled so pausing and unpausing does not snap the smoothed direction.
            float dt = playing ? Time.unscaledDeltaTime : 1f / 60f;
            if (dt <= 0f) return;

            Vector3 anchor = anchorSource.position;
            Vector3 playerVelocity = playing && _body != null ? _body.Velocity : Vector3.zero;

            Vector3 travelDir = playing && _body != null && _body.Speed > 0.5f
                ? playerVelocity.normalized
                : anchorSource.forward;

            Vector3 rawFlow = ComputeRawFlow(anchor, playerVelocity);
            UpdateFlow(rawFlow, dt);
            UpdateLoopEnvelope(dt);

            if (playing && _followTarget != null)
            {
                transform.position = anchor;
            }

            Push(anchor, travelDir);
        }

        private Vector3 ComputeRawFlow(Vector3 anchor, Vector3 playerVelocity)
        {
            Vector3 flow = _ambientWindDirection.sqrMagnitude > 1e-6f
                ? _ambientWindDirection.normalized * _ambientWindSpeed
                : Vector3.zero;

            if (_receiver != null && _zoneWindInfluence > 0f)
            {
                Vector3 zoneWind = Vector3.zero;
                var zones = _receiver.ActiveWindZones;

                for (int i = 0; i < zones.Count; i++)
                {
                    WindProvider zone = zones[i];
                    if (zone == null) continue;
                    zoneWind += zone.GetWindForceAtPoint(anchor);
                }

                flow += zoneWind * _zoneWindInfluence;
            }

            // Subtracting the player's own velocity is most of the sensation: fly fast
            // and the ribbons rush past you, coast and they hang in the air.
            flow -= playerVelocity * _relativeStreamFactor;

            return flow;
        }

        private void UpdateFlow(Vector3 rawFlow, float dt)
        {
            if (!_flowInitialised)
            {
                _flowFast = rawFlow;
                _flowSlow = rawFlow;
                _flowInitialised = true;
                return;
            }

            // Frame rate independent exponential smoothing.
            float fastK = 1f - Mathf.Exp(-dt / Mathf.Max(_flowSmoothing, 0.001f));
            float slowK = 1f - Mathf.Exp(-dt / SlowFlowSmoothing);

            _flowFast = Vector3.Lerp(_flowFast, rawFlow, fastK);
            _flowSlow = Vector3.Lerp(_flowSlow, rawFlow, slowK);

            if (!Application.isPlaying) return;

            // A sustained swing between the responsive and lazy directions means the
            // general flow has genuinely changed, not that it wobbled for a frame.
            if (Time.time < _nextLoopTime) return;
            if (_flowSlow.sqrMagnitude < 0.01f || _flowFast.sqrMagnitude < 0.01f) return;

            if (Vector3.Angle(_flowFast, _flowSlow) > _loopAngleThreshold)
            {
                // Loop about the axis the flow is actually turning around, so the loops
                // orbit in the plane of the turn instead of some arbitrary plane.
                Vector3 axis = Vector3.Cross(_flowSlow, _flowFast);
                TriggerLoopBurst(axis.sqrMagnitude > 1e-6f ? axis : Vector3.up);
            }
        }

        private void UpdateLoopEnvelope(float dt)
        {
            float rate = _loopTarget > _loopIntensity
                ? dt / Mathf.Max(_loopAttack, 0.001f)
                : dt / Mathf.Max(_loopDecay, 0.001f);

            _loopIntensity = Mathf.MoveTowards(_loopIntensity, _loopTarget, rate);

            // Once a burst peaks, start it falling again.
            if (_loopTarget > 0f && _loopIntensity >= 0.999f)
            {
                _loopTarget = 0f;
            }
        }

        private void Push(Vector3 anchor, Vector3 travelDir)
        {
            bool paused = _disableWhenPaused && Application.isPlaying && Time.timeScale == 0f;
            float spawnRate = paused ? 0f : _density * _maxSpawnRate;

            if (_hasSpawnRate) _vfx.SetFloat(SpawnRateId, spawnRate);
            if (_hasSpawnBoxSize) _vfx.SetVector3(SpawnBoxSizeId, _spawnBoxSize);
            if (_hasSpawnCenter) _vfx.SetVector3(SpawnCenterId, anchor + travelDir * (_spawnBoxSize.z * _spawnAhead));
            if (_hasFlowVelocity) _vfx.SetVector3(FlowVelocityId, _flowFast);
            if (_hasFlowStrength) _vfx.SetFloat(FlowStrengthId, _flowStrength);
            if (_hasTurbulenceIntensity) _vfx.SetFloat(TurbulenceIntensityId, _turbulenceIntensity);
            if (_hasLoopAxis) _vfx.SetVector3(LoopAxisId, _loopAxis);
            if (_hasLoopOmega) _vfx.SetFloat(LoopOmegaId, _loopOmega);
            if (_hasLoopIntensity) _vfx.SetFloat(LoopIntensityId, _loopIntensity);
            if (_hasSizeScale) _vfx.SetFloat(SizeScaleId, _sizeScale);
        }

        private void OnDrawGizmosSelected()
        {
            bool playing = Application.isPlaying;
            Transform anchorSource = playing && _followTarget != null ? _followTarget : transform;

            Vector3 travelDir = playing && _body != null && _body.Speed > 0.5f
                ? _body.Velocity.normalized
                : anchorSource.forward;

            Vector3 centre = anchorSource.position + travelDir * (_spawnBoxSize.z * _spawnAhead);

            Gizmos.color = new Color(0.6f, 0.85f, 1f, 0.35f);
            Gizmos.DrawWireCube(centre, _spawnBoxSize);

            if (_flowFast.sqrMagnitude > 1e-6f)
            {
                Gizmos.color = new Color(0.6f, 0.85f, 1f, 0.9f);
                Gizmos.DrawRay(centre, _flowFast);
            }
        }
    }
}
