using UnityEngine;

namespace Crease.Flying.Environment.Wind.SplineTube
{
    [RequireComponent(typeof(SplineTubeTrigger))]
    [ExecuteAlways]
    public class SplineWindParticles : MonoBehaviour
    {
        [Header("Particle Settings")]
        [Tooltip("Speed particles travel along the tube direction.")]
        [SerializeField] private float _startSpeed = 50f;

        [Tooltip("How long each particle lives before respawning.")]
        [SerializeField] private float _startLifetime = 0.3f;

        [Tooltip("Particle emission rate per segment (particles per second).")]
        [SerializeField] private float _emissionRate = 8f;

        [Tooltip("Material applied to each segment's particle system renderer.")]
        [SerializeField] private Material _particleMaterial;

        [Tooltip("How much particle speed contributes to stretch length.")]
        [SerializeField] private float _stretchVelocityScale = 0f;

        [Tooltip("Base length scale for stretched billboard particles.")]
        [SerializeField] private float _stretchLengthScale = 50f;

        [Tooltip("Width of each particle. Lower = thinner streaks.")]
        [SerializeField] private float _startSize = 0.1f;

        [Header("Secondary Particle System (Steam)")]
        [Tooltip("Optional secondary particle system (e.g. Steam child GameObject) whose shape is synced " +
            "with the tube mesh. Assign the child ParticleSystem here to make it emit across the full tube.")]
        [SerializeField] private ParticleSystem _secondaryParticleSystem;

        [Tooltip("Start speed for the secondary particle system.")]
        [SerializeField] private float _secondarySpeed = 5f;

        [Tooltip("Start lifetime for the secondary particle system.")]
        [SerializeField] private float _secondaryDuration = 2f;

        [Tooltip("Control whether the secondary system is driven by speed or duration.")]
        [SerializeField] private SecondaryDriveMode _secondaryDriveMode = SecondaryDriveMode.Speed;

        public enum SecondaryDriveMode { Speed, Duration }

        private SplineTubeTrigger _tubeTrigger;

        // Combined mesh for the secondary (steam) system — still uses one system for steam
        // since direction matters less for the ambient steam look than for the wind particles.
        private Mesh _combinedSegmentMesh;

        private SegmentTriggerRelay[] _cachedRelays;
        private int _lastSegmentCount = -1;
        private float _lastRadius;

        private void Awake()
        {
            _tubeTrigger = GetComponent<SplineTubeTrigger>();
        }

        private void Start()
        {
            // Start guarantees SplineTubeTrigger.Awake has already run and built
            // all segment children before we try to add particle systems to them.
            UpdateParticleSystems();
        }

        private void OnValidate()
        {
            // Only reconfigure during Play mode — segment particle systems don't
            // exist in edit mode since they're created at runtime by SplineTubeTrigger.
            if (Application.isPlaying)
            {
                UpdateParticleSystems();
            }
        }

        private void Update()
        {
            if (_tubeTrigger != null && HasTubeChanged())
            {
                UpdateParticleSystems();
            }
        }

        private bool HasTubeChanged()
        {
            SegmentTriggerRelay[] current = GetComponentsInChildren<SegmentTriggerRelay>();
            int currentCount = current != null ? current.Length : 0;
            return currentCount != _lastSegmentCount || _tubeTrigger.Radius != _lastRadius;
        }

        private void UpdateParticleSystems()
        {
            if (_tubeTrigger == null) return;

            _cachedRelays     = GetComponentsInChildren<SegmentTriggerRelay>();
            _lastSegmentCount = _cachedRelays != null ? _cachedRelays.Length : 0;
            _lastRadius       = _tubeTrigger.Radius;

            SetupSegmentParticleSystems();
            UpdateSecondaryParticleSystem();
        }

        /// <summary>
        /// Adds or updates a ParticleSystem on each segment child, oriented to that
        /// segment's midpoint tangent direction so particles flow correctly through turns.
        /// </summary>
        private void SetupSegmentParticleSystems()
        {
            if (_cachedRelays == null || _tubeTrigger.Rings == null) return;

            foreach (SegmentTriggerRelay relay in _cachedRelays)
            {
                if (relay == null) continue;

                // Get or add a ParticleSystem on this segment child.
                ParticleSystem ps = relay.GetComponent<ParticleSystem>();
                if (ps == null) ps = relay.gameObject.AddComponent<ParticleSystem>();

                // Get the midpoint ring index for this segment to sample tangent direction.
                int midRing = (relay.StartRingIndex + relay.EndRingIndex) / 2;
                midRing = Mathf.Clamp(midRing, 0, _tubeTrigger.Rings.Count - 1);
                Vector3 worldTangent = _tubeTrigger.Rings[midRing].Tangent;

                // Segment mesh verts are in world space. The segment child sits at world
                // origin (no local offset), so world space == local space for this child.
                // We orient the ParticleSystem's velocity along the world-space tangent
                // directly since Local simulation space on a world-origin child = world space.
                ConfigureSegmentParticleSystem(ps, relay, worldTangent);
            }
        }

        private void ConfigureSegmentParticleSystem(ParticleSystem ps, SegmentTriggerRelay relay, Vector3 worldTangent)
        {
            MeshCollider mc = relay.GetComponent<MeshCollider>();
            if (mc == null || mc.sharedMesh == null) return;

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode     = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = _startLifetime;
            main.startSpeed      = 0f; // velocity driven entirely by velocityOverLifetime below
            main.startSize       = new ParticleSystem.MinMaxCurve(_startSize);

            // Stagger each segment's particle timing based on its position along the spline
            // so particles appear to flow continuously from tube start to end, rather than
            // each segment emitting in its own synchronized clump.
            int totalRings = _tubeTrigger.Rings != null ? _tubeTrigger.Rings.Count : 1;
            float phase = (float)relay.StartRingIndex / Mathf.Max(totalRings - 1, 1);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed        = (uint)(phase * uint.MaxValue);
            ps.Play();

            var emission = ps.emission;
            emission.enabled      = true;
            emission.rateOverTime = _emissionRate;

            // Emit from this segment's own mesh surface.
            var shape = ps.shape;
            shape.enabled       = true;
            shape.shapeType     = ParticleSystemShapeType.Mesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.mesh          = mc.sharedMesh;

            shape.alignToDirection      = false;
            shape.randomDirectionAmount = 0f;

            // Drive particles along this segment's actual tangent direction in world space.
            // Each segment has its own ParticleSystem so the direction is correct for that
            // specific piece of the tube — this is the core fix vs. one system for the whole tube.
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space   = ParticleSystemSimulationSpace.World;
            vol.x       = _startSpeed * worldTangent.x;
            vol.y       = _startSpeed * worldTangent.y;
            vol.z       = _startSpeed * worldTangent.z;

            // Apply material and stretched billboard render mode so particles
            // look like elongated streaks flowing in the wind direction,
            // matching the frustum wind particle visual style.
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                if (_particleMaterial != null)
                    renderer.material = _particleMaterial;
                renderer.renderMode        = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale     = _stretchVelocityScale;
                renderer.lengthScale       = _stretchLengthScale;
            }
        }

        // June 2026 NOTE: For steam particle system: rotation of Steam (child game object of SplineWindZone) is 
        // set to 90 in the x-direction otherwise it does not align properly with spline.
        // May need to be changed in the future depending on implementation, but should work for all Spline
        // shapes as far as I tested. Since it is basic VFX stage it should be good.
        // This is bc I copy-pasted the Steam particle system from the Wind Frustums for simplicity 
        // at this time of early development, but we should probably make a new particle system
        // that works better for varying spline shapes in the future.
        private void UpdateSecondaryParticleSystem()
        {
            if (_secondaryParticleSystem == null) return;

            // Build combined mesh for steam — steam still uses one system since
            // ambient steam doesn't need per-segment directional accuracy.
            Mesh steamMesh = BuildCombinedMesh();
            if (steamMesh == null) return;

            var secondaryMain = _secondaryParticleSystem.main;
            secondaryMain.simulationSpace       = ParticleSystemSimulationSpace.Custom;
            secondaryMain.customSimulationSpace = transform;
            secondaryMain.scalingMode           = ParticleSystemScalingMode.Hierarchy;
            secondaryMain.maxParticles          = 500;
            secondaryMain.startSpeed            = 0f;

            var emission = _secondaryParticleSystem.emission;
            emission.enabled      = true;
            emission.rateOverTime = _emissionRate * _cachedRelays.Length * 1.5f;

            var shape = _secondaryParticleSystem.shape;
            shape.enabled               = true;
            shape.shapeType             = ParticleSystemShapeType.Mesh;
            shape.meshShapeType         = ParticleSystemMeshShapeType.Triangle;
            shape.mesh                  = steamMesh;
            shape.alignToDirection      = false;
            shape.randomDirectionAmount = 0f;

            // Use first ring tangent as a general flow direction for steam.
            Vector3 worldTangent = _tubeTrigger.Rings != null && _tubeTrigger.Rings.Count > 0
                ? _tubeTrigger.Rings[0].Tangent
                : Vector3.forward;
            Vector3 localDir = transform.InverseTransformDirection(worldTangent.normalized);

            var vol = _secondaryParticleSystem.velocityOverLifetime;
            vol.enabled = true;
            vol.space   = ParticleSystemSimulationSpace.Custom;

            if (_secondaryDriveMode == SecondaryDriveMode.Speed)
            {
                float speed = Mathf.Max(_secondarySpeed, 0.001f);
                secondaryMain.startLifetime = speed > 0 ? _secondaryDuration : 1f;
                vol.x = speed * localDir.x;
                vol.y = speed * localDir.y;
                vol.z = speed * localDir.z;
            }
            else
            {
                float duration = Mathf.Max(_secondaryDuration, 0.001f);
                secondaryMain.startLifetime = duration;
                vol.x = _secondarySpeed * localDir.x;
                vol.y = _secondarySpeed * localDir.y;
                vol.z = _secondarySpeed * localDir.z;
            }
        }

        private Mesh BuildCombinedMesh()
        {
            if (_cachedRelays == null || _cachedRelays.Length == 0) return null;

            CombineInstance[] combine = new CombineInstance[_cachedRelays.Length];
            bool anyValid = false;

            for (int i = 0; i < _cachedRelays.Length; i++)
            {
                MeshCollider mc = _cachedRelays[i].GetComponent<MeshCollider>();
                if (mc == null || mc.sharedMesh == null) continue;

                combine[i].mesh      = mc.sharedMesh;
                combine[i].transform = transform.worldToLocalMatrix;
                anyValid = true;
            }

            if (!anyValid) return null;

            if (_combinedSegmentMesh == null)
            {
                _combinedSegmentMesh      = new Mesh();
                _combinedSegmentMesh.name = "SplineTubeCombinedEmissionMesh";
            }

            _combinedSegmentMesh.Clear();
            _combinedSegmentMesh.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: true);
            _combinedSegmentMesh.RecalculateBounds();
            return _combinedSegmentMesh;
        }

        private void OnDestroy()
        {
            if (_combinedSegmentMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_combinedSegmentMesh);
                else
                    DestroyImmediate(_combinedSegmentMesh);
            }
        }
    }
}