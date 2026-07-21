using Crease.Flying.Player;

using Crease.Flying.Player.Animation;

using Crease.Managers.Input;

using UnityEngine;

using UnityEngine.Serialization;



namespace Crease.Flying.Player.Abilities

{

    /// <summary>

    /// Owns the equipped primary and secondary abilities, player references abilities can use, and the update loop.

    /// </summary>

    public class AbilityController : MonoBehaviour

    {

        [FormerlySerializedAs("_equippedAbility")]

        [SerializeField] private Ability _primaryAbility;

        [SerializeField] private Ability _secondaryAbility;



        [Header("References For Abilities")]

        [SerializeField] private AnimationController _animationController;

        [SerializeField] private KinematicBody _body;

        [SerializeField] private WingTrailController _wingTrail;

        [SerializeField] private GameObject _rechargeProximityIndicator;

        [SerializeField] private FlightStats _flightStats;

        [SerializeField] private FlightModifiers.FlightModifiers _flightModifiers;



        private Ability.Runtime _primaryRuntime;

        private Ability.Runtime _secondaryRuntime;



        public Ability PrimaryEquippedAbility { get; private set; }

        public Ability SecondaryEquippedAbility { get; private set; }



        public Transform PlayerTransform => transform;

        public KinematicBody Body => _body;

        public AnimationController AnimationController => _animationController;

        public FlightStats FlightStats => _flightStats;

        public FlightModifiers.FlightModifiers FlightModifiers => _flightModifiers;

        public WingTrailController WingTrail => _wingTrail;

        public GameObject RechargeProximityIndicator => _rechargeProximityIndicator;

        public int ProximitySourceCount { get; private set; }



        public bool IsActive =>

            (_primaryRuntime != null && _primaryRuntime.IsActive)

            || (_secondaryRuntime != null && _secondaryRuntime.IsActive);



        public float RechargeNormalized => _primaryRuntime?.RechargeNormalized ?? 0f;

        public bool CanActivate => _primaryRuntime != null && _primaryRuntime.CanActivate;



        public float SecondaryRechargeNormalized => _secondaryRuntime?.RechargeNormalized ?? 0f;

        public bool SecondaryCanActivate => _secondaryRuntime != null && _secondaryRuntime.CanActivate;



        private void Awake()

        {

            if (_body == null) _body = GetComponent<KinematicBody>();

            if (_flightStats == null) _flightStats = GetComponent<FlightStats>();

            if (_flightModifiers == null) _flightModifiers = GetComponent<FlightModifiers.FlightModifiers>();

        }



        private void Start()

        {

            Equip(_primaryAbility, _secondaryAbility);

        }



        private void Update()

        {

            if (InputManager.Instance != null)

            {

                _primaryRuntime?.SetInputHeld(InputManager.Instance.Actions.Player.PrimaryAbility.IsPressed());

                _secondaryRuntime?.SetInputHeld(InputManager.Instance.Actions.Player.SecondaryAbility.IsPressed());

                if (InputManager.Instance.PrimaryAbilityPressed)

                    _primaryRuntime?.TryActivate();



                if (InputManager.Instance.SecondaryAbilityPressed)

                    _secondaryRuntime?.TryActivate();

            }



            _primaryRuntime?.Tick(Time.deltaTime);

            _secondaryRuntime?.Tick(Time.deltaTime);

        }



        private void FixedUpdate()

        {

            _primaryRuntime?.FixedTick(Time.fixedDeltaTime);

            _secondaryRuntime?.FixedTick(Time.fixedDeltaTime);

        }



        public void Equip(Ability primaryAbility, Ability secondaryAbility = null)

        {

            _primaryRuntime?.OnUnequipped();

            _secondaryRuntime?.OnUnequipped();

            ProximitySourceCount = 0;



            PrimaryEquippedAbility = primaryAbility;

            SecondaryEquippedAbility = secondaryAbility;

            _primaryAbility = primaryAbility;

            _secondaryAbility = secondaryAbility;



            _primaryRuntime = primaryAbility != null ? primaryAbility.Begin(this) : null;

            _secondaryRuntime = secondaryAbility != null ? secondaryAbility.Begin(this) : null;



            _primaryRuntime?.OnEquipped();

            _secondaryRuntime?.OnEquipped();

        }



        public void Refresh()

        {

            _primaryRuntime?.Refresh();

            _secondaryRuntime?.Refresh();

        }



        public void ActivatePrimaryAbility() => _primaryRuntime?.TryActivate();

        public void ActivateSecondaryAbility() => _secondaryRuntime?.TryActivate();



        public void AdjustProximitySourceCount(int delta)

        {

            ProximitySourceCount = Mathf.Max(0, ProximitySourceCount + delta);

        }

    }

}


