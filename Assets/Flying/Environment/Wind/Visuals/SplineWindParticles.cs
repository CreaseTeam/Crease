using UnityEngine;

namespace Crease.Flying.Environment.Wind.SplineTube
{
    [RequireComponent(typeof(ParticleSystem))]
    [RequireComponent(typeof(SplineTubeTrigger))]
    [ExecuteAlways]
    public class SplineWindParticles : MonoBehaviour
    {
        [Header("Emission Shape")]
        [Tooltip(
            "Mesh used for particle emission. If left empty, a combined mesh built from all " +
            "convex segment colliders is used automatically. Assign a custom mesh here to " +
            "decouple VFX density from collider geometry if needed.")]
        [SerializeField] private Mesh _emissionMeshOverride;

        [Header("Particle Settings")]
        [Tooltip("Speed particles travel along their initial direction.")]
        [SerializeField] private float _startSpeed = 4f;

        [Tooltip(
            "How long each particle lives before respawning. Kept short deliberately: since a single " +
            "particle system cannot bend per-particle velocity around the tube's turns, short lifetimes " +
            "keep straight-line travel distance small enough that particles don't visibly clip through " +
            "the tube wall on tight bends.")]
        [SerializeField] private float _startLifetime = 0.6f;

        [Tooltip("Particle emission rate (particles per second).")]
        [SerializeField] private float _emissionRate = 80f;

        [Tooltip(
            "Safety margin for the drift warning: travel distance (speed * lifetime) is compared " +
            "against (tube radius * this multiplier). Lower = stricter warning.")]
        [SerializeField] private float _driftWarningRadiusMultiplier = 1.5f;

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

        private ParticleSystem _particleSystem;
        private SplineTubeTrigger _tubeTrigger;

        // Combined mesh built from all segment children — rebuilt only when tube geometry changes,
        // not every frame.
        private Mesh _combinedSegmentMesh;

        // Cached relay array — refreshed inside UpdateParticleShape when geometry changes.
        // Avoids calling GetComponentsInChildren every frame in Update.
        private SegmentTriggerRelay[] _cachedRelays;

        private int _lastSegmentCount = -1;
        private float _lastRadius;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
            _tubeTrigger    = GetComponent<SplineTubeTrigger>();
        }

        private void Start()
        {
            // Start is used instead of OnEnable/Awake to guarantee SplineTubeTrigger.Awake
            // has already run and built all segment children before we try to read them.
            // Using OnEnable would risk GetComponentsInChildren finding zero relays if this
            // script's OnEnable fires before SplineTubeTrigger's Awake.
            UpdateParticleShape();
        }

        private void OnValidate()
        {
            UpdateParticleShape();
        }

        private void Update()
        {
            if (_tubeTrigger != null && HasTubeChanged())
            {
                UpdateParticleShape();
            }
        }

        private bool HasTubeChanged()
        {
            // GetComponentsInChildren is still called here each frame, but only to get
            // the count for a cheap int comparison. The expensive part (building the combined
            // mesh, caching the relay array) only happens inside UpdateParticleShape when
            // the count or radius actually changed.
            SegmentTriggerRelay[] current = GetComponentsInChildren<SegmentTriggerRelay>();
            int currentCount = current != null ? current.Length : 0;
            return currentCount != _lastSegmentCount || _tubeTrigger.Radius != _lastRadius;
        }

        private Mesh GetEmissionMesh()
        {
            if (_emissionMeshOverride != null) return _emissionMeshOverride;
            return BuildCombinedMesh();
        }

        /// <summary>
        /// Combines all segment children's convex meshes into one mesh for the particle
        /// system's shape module. Uses _cachedRelays so GetComponentsInChildren is not
        /// called here — the cache is refreshed in UpdateParticleShape before this runs.
        /// </summary>
        private Mesh BuildCombinedMesh()
        {
            if (_cachedRelays == null || _cachedRelays.Length == 0) return null;

            CombineInstance[] combine = new CombineInstance[_cachedRelays.Length];
            bool anyValid = false;

            for (int i = 0; i < _cachedRelays.Length; i++)
            {
                MeshCollider mc = _cachedRelays[i].GetComponent<MeshCollider>();
                if (mc == null || mc.sharedMesh == null) continue;

                // Segment mesh verts are in world space. Transform them into this root
                // GameObject's local space so the ParticleSystem (running in Local simulation
                // space) emits from the correct positions rather than double-applying the
                // root transform's offset on top of already-world-space coordinates.
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

        private void UpdateParticleShape()
        {
            if (_particleSystem == null || _tubeTrigger == null) return;

            // Refresh the relay cache here, once per geometry change, not every frame.
            _cachedRelays     = GetComponentsInChildren<SegmentTriggerRelay>();
            _lastSegmentCount = _cachedRelays != null ? _cachedRelays.Length : 0;
            _lastRadius       = _tubeTrigger.Radius;

            Mesh emissionMesh = GetEmissionMesh();
            if (emissionMesh == null) return;

            var main = _particleSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode     = ParticleSystemScalingMode.Hierarchy;
            main.startSpeed      = _startSpeed;
            main.startLifetime   = _startLifetime;

            var emission = _particleSystem.emission;
            emission.enabled      = true;
            emission.rateOverTime = _emissionRate;

            var shape = _particleSystem.shape;
            shape.enabled       = true;
            shape.shapeType     = ParticleSystemShapeType.Mesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.mesh          = emissionMesh;

            shape.randomDirectionAmount = 0f;
            shape.alignToDirection      = false;

            var velocityOverLifetime = _particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space   = ParticleSystemSimulationSpace.Local;
            // Drives particles along this object's local forward axis. Known v1 limitation:
            // a single particle system cannot bend per-particle velocity around sharp turns.
            // Short StartLifetime keeps this visually acceptable on moderate curves.
            velocityOverLifetime.z = _startSpeed;
            velocityOverLifetime.x = 0f;
            velocityOverLifetime.y = 0f;

            WarnIfDriftExceedsRadius();
            UpdateSecondaryParticleSystem(emissionMesh);
        }

        private void UpdateSecondaryParticleSystem(Mesh emissionMesh)
        {
            if (_secondaryParticleSystem == null || emissionMesh == null) return;

            var secondaryMain = _secondaryParticleSystem.main;
            // World simulation space avoids coordinate mismatch: the emission mesh verts
            // are in the root GameObject's local space, but the secondary ParticleSystem
            // lives on a child GameObject with its own transform. World space sidesteps
            // this entirely — particles emit from world-space positions directly, so no
            // transform double-application occurs regardless of where the child sits.
            secondaryMain.simulationSpace = ParticleSystemSimulationSpace.World;
            secondaryMain.scalingMode     = ParticleSystemScalingMode.Hierarchy;

            // Build a world-space version of the emission mesh for the secondary system.
            // The primary system uses root-local-space verts (for Local sim space);
            // the secondary needs world-space verts (for World sim space).
            Mesh worldSpaceMesh = BuildWorldSpaceMesh();
            if (worldSpaceMesh == null) return;

            var shape = _secondaryParticleSystem.shape;
            shape.enabled       = true;
            shape.shapeType     = ParticleSystemShapeType.Mesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            shape.mesh          = worldSpaceMesh;

            shape.randomDirectionAmount = 0f;
            shape.alignToDirection      = false;

            if (_secondaryDriveMode == SecondaryDriveMode.Speed)
            {
                float speed = Mathf.Max(_secondarySpeed, 0.001f);
                secondaryMain.startSpeed    = speed;
                secondaryMain.startLifetime = speed > 0 ? _secondaryDuration : 1f;
            }
            else
            {
                float duration = Mathf.Max(_secondaryDuration, 0.001f);
                secondaryMain.startLifetime = duration;
                secondaryMain.startSpeed    = _secondarySpeed;
            }
        }

        /// <summary>
        /// Builds a version of the combined segment mesh with vertices in world space,
        /// for use with the secondary ParticleSystem running in World simulation space.
        /// Kept separate from _combinedSegmentMesh (which is root-local-space) so the
        /// two systems do not interfere with each other.
        /// </summary>
        private Mesh _worldSpaceSegmentMesh;
        private Mesh BuildWorldSpaceMesh()
        {
            if (_cachedRelays == null || _cachedRelays.Length == 0) return null;

            CombineInstance[] combine = new CombineInstance[_cachedRelays.Length];
            bool anyValid = false;

            for (int i = 0; i < _cachedRelays.Length; i++)
            {
                MeshCollider mc = _cachedRelays[i].GetComponent<MeshCollider>();
                if (mc == null || mc.sharedMesh == null) continue;

                // Identity matrix: verts are already in world space from GenerateSegmentMesh.
                combine[i].mesh      = mc.sharedMesh;
                combine[i].transform = Matrix4x4.identity;
                anyValid = true;
            }

            if (!anyValid) return null;

            if (_worldSpaceSegmentMesh == null)
            {
                _worldSpaceSegmentMesh      = new Mesh();
                _worldSpaceSegmentMesh.name = "SplineTubeWorldSpaceEmissionMesh";
            }

            _worldSpaceSegmentMesh.Clear();
            _worldSpaceSegmentMesh.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: true);
            _worldSpaceSegmentMesh.RecalculateBounds();
            return _worldSpaceSegmentMesh;
        }

        private void WarnIfDriftExceedsRadius()
        {
            if (_tubeTrigger == null) return;

            float travelDistance = _startSpeed * _startLifetime;
            float safeDistance   = _tubeTrigger.Radius * _driftWarningRadiusMultiplier;

            if (travelDistance > safeDistance)
            {
                Debug.LogWarning(
                    $"[SplineWindParticles] '{name}' particles may travel {travelDistance:F2}m " +
                    $"(StartSpeed * StartLifetime) before dying, which exceeds a safe margin around " +
                    $"the tube radius of {_tubeTrigger.Radius:F2}m. On tight turns this can cause " +
                    $"particles to visibly clip through the tube wall. Consider lowering StartSpeed " +
                    $"or StartLifetime, or increasing the tube Radius.",
                    this);
            }
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

            if (_worldSpaceSegmentMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_worldSpaceSegmentMesh);
                else
                    DestroyImmediate(_worldSpaceSegmentMesh);
            }
        }
    }
}