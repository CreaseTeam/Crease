using Crease.Flying.Player;
using Crease.Flying.Player.Animation;
using Crease.Folding.PaperGraph;
using Crease.Managers.Input;
using UnityEngine;

namespace Crease.Flying.Player.Abilities
{
    /// <summary>
    /// Owns the equipped ability, player references abilities can use, and the update loop.
    /// </summary>
    public class AbilityController : MonoBehaviour
    {
        [SerializeField] private Ability _equippedAbility;

        [Header("References For Abilities")]
        [SerializeField] private AnimationController _animationController;
        [SerializeField] private KinematicBody _body;
        [SerializeField] private WingTrailController _wingTrail;
        [SerializeField] private GameObject _rechargeProximityIndicator;
        [SerializeField] private FlightStats _flightStats;
        [SerializeField] private FlightModifiers.FlightModifiers _flightModifiers;

        [Header("Loadout (Optional)")]
        [SerializeField] private FoldInstructionRunner _foldInstructionRunner;

        private Ability.Runtime _runtime;

        public Ability EquippedAbility { get; private set; }

        public Transform PlayerTransform => transform;
        public KinematicBody Body => _body;
        public AnimationController AnimationController => _animationController;
        public FlightStats FlightStats => _flightStats;
        public FlightModifiers.FlightModifiers FlightModifiers => _flightModifiers;
        public WingTrailController WingTrail => _wingTrail;
        public GameObject RechargeProximityIndicator => _rechargeProximityIndicator;
        public int ProximitySourceCount { get; private set; }

        public bool IsActive => _runtime != null && _runtime.IsActive;
        public float RechargeNormalized => _runtime?.RechargeNormalized ?? 0f;
        public bool CanActivate => _runtime != null && _runtime.CanActivate;

        private void Awake()
        {
            if (_body == null) _body = GetComponent<KinematicBody>();
            if (_flightStats == null) _flightStats = GetComponent<FlightStats>();
            if (_flightModifiers == null) _flightModifiers = GetComponent<FlightModifiers.FlightModifiers>();
        }

        private void Start()
        {
            if (_equippedAbility != null)
                Equip(_equippedAbility);
        }

        private void Update()
        {
            if (_runtime == null)
                return;

            if (InputManager.Instance != null && InputManager.Instance.ActivateAbilityPressed)
                _runtime.TryActivate();

            _runtime.Tick(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            _runtime?.FixedTick(Time.fixedDeltaTime);
        }

        public void Equip(Ability ability)
        {
            _runtime?.OnUnequipped();
            ProximitySourceCount = 0;

            EquippedAbility = ability;
            _equippedAbility = ability;
            _runtime = ability != null ? ability.Begin(this) : null;
            _runtime?.OnEquipped();
        }

        public void ApplyLoadout(PlaneLoadout loadout)
        {
            if (loadout == null)
                return;

            if (loadout.FoldInstruction != null && _foldInstructionRunner != null)
                _foldInstructionRunner.LoadInstruction(loadout.FoldInstruction);

            if (loadout.FlightSettings != null && _flightStats != null)
                _flightStats.SetBaseSettings(loadout.FlightSettings);

            if (loadout.Ability != null)
                Equip(loadout.Ability);
        }

        public void Refresh() => _runtime?.Refresh();
        public void ActivateAbility() => _runtime?.TryActivate();

        public void AdjustProximitySourceCount(int delta)
        {
            ProximitySourceCount = Mathf.Max(0, ProximitySourceCount + delta);
        }
    }
}
