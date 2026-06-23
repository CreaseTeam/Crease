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
            main.startLifetime   = _startLifetime;

            // Zero out startSpeed so the shape module contributes no initial velocity —
            // velocityOverLifetime below is the sole driver of particle direction and speed.
            main.startSpeed = 0f;

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

            // Drive particles along the spline's start tangent direction in local space.
            // startSpeed is zeroed above so the shape module doesn't fight this velocity.
            Vector3 localDir = GetSplineStartDirectionLocal();
            var velocityOverLifetime = _particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space   = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x       = _startSpeed * localDir.x;
            velocityOverLifetime.y       = _startSpeed * localDir.y;
            velocityOverLifetime.z       = _startSpeed * localDir.z;

            WarnIfDriftExceedsRadius();
            UpdateSecondaryParticleSystem(emissionMesh);
        }

        // June 2026 NOTE: For steam particle system: rotation of Steam (child game object of SplineWindZone) is 
        // set to 90 in the x-direction otherwise it does not align properly with spline.
        // May need to be changed in the future depending on implementation, but should work for all Spline
        // shapes as far as I tested. Since it is basic VFX stage it should be good.
        // This is bc I copy-pasted the Steam particle system from the Wind Frustums for simplicity 
        // at this time of early development, but we should probably make a new particle system
        // that works better for varying spline shapes in the future.
        private void UpdateSecondaryParticleSystem(Mesh emissionMesh)
        {
            if (_secondaryParticleSystem == null || emissionMesh == null) return;

            // Mirror the primary system exactly — same mesh, same Local simulation space,
            // same velocity-over-lifetime along Z. Steam is visually differentiated purely
            // through its own Inspector values (Start Size, Start Color, material) and the
            // Secondary Speed/Duration fields below, not through different emission logic.
            // This is the same approach the frustum uses: same cone shape for both primary
            // and secondary, different visual settings per system.
            var secondaryMain = _secondaryParticleSystem.main;
            // Use Custom simulation space pointed at the ROOT transform, not the child's
            // own Local space. This makes the secondary system interpret the emission mesh
            // vertices relative to the same transform the primary system uses, so particles
            // spawn in exactly the same positions as the primary wind particles.
            secondaryMain.simulationSpace       = ParticleSystemSimulationSpace.Custom;
            secondaryMain.customSimulationSpace = transform;
            secondaryMain.scalingMode           = ParticleSystemScalingMode.Hierarchy;
            secondaryMain.maxParticles          = 500;

            // Zero out startSpeed so the shape module contributes no initial velocity.
            secondaryMain.startSpeed = 0f;

            var emission = _secondaryParticleSystem.emission;
            emission.enabled      = true;
            emission.rateOverTime = _emissionRate * 1.5f;

            var shape = _secondaryParticleSystem.shape;
            shape.enabled               = true;
            shape.shapeType             = ParticleSystemShapeType.Mesh;
            shape.meshShapeType         = ParticleSystemMeshShapeType.Triangle;
            shape.mesh                  = emissionMesh;
            shape.alignToDirection      = false;
            shape.randomDirectionAmount = 0f;

            // Drive particles along the spline's start tangent, same as primary system.
            Vector3 localDir = GetSplineStartDirectionLocal();
            var velocityOverLifetime = _secondaryParticleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space   = ParticleSystemSimulationSpace.Custom;

            if (_secondaryDriveMode == SecondaryDriveMode.Speed)
            {
                float speed = Mathf.Max(_secondarySpeed, 0.001f);
                secondaryMain.startLifetime     = speed > 0 ? _secondaryDuration : 1f;
                velocityOverLifetime.x          = speed * localDir.x;
                velocityOverLifetime.y          = speed * localDir.y;
                velocityOverLifetime.z          = speed * localDir.z;
            }
            else
            {
                float duration = Mathf.Max(_secondaryDuration, 0.001f);
                secondaryMain.startLifetime = duration;
                velocityOverLifetime.x      = _secondarySpeed * localDir.x;
                velocityOverLifetime.y      = _secondarySpeed * localDir.y;
                velocityOverLifetime.z      = _secondarySpeed * localDir.z;
            }
        }

        /// <summary>
        /// Returns the spline's start tangent converted into this root GameObject's local space.
        /// Used to drive velocityOverLifetime so particles flow along the actual spline direction
        /// rather than a fixed local axis, making flow direction correct regardless of tube orientation.
        /// Falls back to local forward (0,0,1) if rings are unavailable.
        /// </summary>
        private Vector3 GetSplineStartDirectionLocal()
        {
            if (_tubeTrigger == null || _tubeTrigger.Rings == null || _tubeTrigger.Rings.Count < 2)
                return Vector3.forward;

            Vector3 worldTangent = _tubeTrigger.Rings[0].Tangent;
            if (worldTangent.sqrMagnitude < 0.0001f) return Vector3.forward;

            return transform.InverseTransformDirection(worldTangent.normalized);
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
        }
    }
}