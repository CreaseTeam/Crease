using Crease.Folding.Decals;
using Crease.Folding.PaperGraph;
using UnityEngine;

namespace Crease.Flying.Player.Health
{
    /// <summary>
    /// Places damage decals on the paper graph during flight using the same folding-space
    /// placement pipeline as stickers, then displays them on the player mesh immediately.
    /// </summary>
    public class DamageDecalApplier : MonoBehaviour
    {
        [SerializeField] private DamageDecalLibrary _library;
        [SerializeField] private PaperDecalManager _decalManager;
        [SerializeField] private Health _health;
        [SerializeField] private float _decalCooldown = 0.75f;
        [SerializeField] private float _randomRotationRange = 180f;

        private readonly float[] _lastPlacementTimeByType = new float[5];

        private void Awake()
        {
            for (int i = 0; i < _lastPlacementTimeByType.Length; i++)
                _lastPlacementTimeByType[i] = float.NegativeInfinity;

            if (_health == null)
                _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            Health.OnDamageTaken += HandleDamageTaken;
            Health.OnDamageHealed += HandleDamageHealed;

            PaperDecalManager decalManager = ResolveDecalManager();
            if (decalManager != null)
                decalManager.OnDecalsCleared += HandleDecalsCleared;
        }

        private void OnDisable()
        {
            Health.OnDamageTaken -= HandleDamageTaken;
            Health.OnDamageHealed -= HandleDamageHealed;

            PaperDecalManager decalManager = ResolveDecalManager();
            if (decalManager != null)
                decalManager.OnDecalsCleared -= HandleDecalsCleared;
        }

        private void HandleDamageTaken(float amount, DamageType type)
        {
            if (amount <= 0f || _library == null)
                return;

            FoldingManager foldingManager = FoldingManager.Instance;
            if (foldingManager == null || foldingManager.IsFolding)
                return;

            PaperDecalManager decalManager = ResolveDecalManager();
            if (decalManager == null)
                return;

            int typeIndex = (int)type;
            if (typeIndex < 0 || typeIndex >= _lastPlacementTimeByType.Length)
                return;

            if (Time.time - _lastPlacementTimeByType[typeIndex] < _decalCooldown)
                return;

            if (!_library.TryGetRandomEntry(type, out DamageDecalEntry entry))
                return;

            float rotationUv = Random.Range(-_randomRotationRange, _randomRotationRange);
            if (!decalManager.PlaceDecalAtRandomOuterSurface(
                    entry.Texture,
                    entry.DefaultScale,
                    rotationUv,
                    isDamageDecal: true,
                    damageSourceType: typeIndex))
                return;

            _lastPlacementTimeByType[typeIndex] = Time.time;
            _health?.RegisterDamageDecal(type);
        }

        private void HandleDamageHealed(float amount, DamageType type)
        {
            if (amount <= 0f)
                return;

            PaperDecalManager decalManager = ResolveDecalManager();
            if (decalManager == null)
                return;

            if (!decalManager.TryRemoveNewestDamageDecalOfType((int)type))
                return;

            _health?.UnregisterDamageDecal(type);
        }

        private void HandleDecalsCleared()
        {
            _health?.ClearDamageDecalTracking();
        }

        private PaperDecalManager ResolveDecalManager()
        {
            if (_decalManager != null)
                return _decalManager;

            FoldingManager foldingManager = FoldingManager.Instance;
            if (foldingManager?.PaperGraph == null)
                return null;

            PaperGraphController controller = foldingManager.PaperGraph.GetComponent<PaperGraphController>();
            return controller?.DecalManager;
        }
    }
}
