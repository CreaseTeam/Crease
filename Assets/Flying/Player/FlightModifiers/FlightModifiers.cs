using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player.FlightModifiers
{
    public class FlightModifiers : MonoBehaviour
    {
        [Header("Simulation Speed")]
        [Tooltip("Default simulation speed when no active simulation-speed grants exist.")]
        [FormerlySerializedAs("_simulationSpeed")]
        [SerializeField]
        private float _baseSimulationSpeed = 1f;

        public float SimulationSpeed => ResolveSimulationSpeed();
        public float ScaledFixedDeltaTime => Time.fixedDeltaTime * SimulationSpeed;

        private struct Grant
        {
            public object Source;
            public bool IsTimed;
            public float ExpiresAt;
            public float Value;
        }

        private readonly Dictionary<FlightModifierType, List<Grant>> _grants = new();

        public bool IsActive(FlightModifierType type)
        {
            if (!_grants.TryGetValue(type, out List<Grant> grants))
                return false;

            float now = Time.time;
            for (int i = 0; i < grants.Count; i++)
            {
                Grant grant = grants[i];
                if (!grant.IsTimed || now < grant.ExpiresAt)
                    return true;
            }

            return false;
        }

        public void SetActive(FlightModifierType type, object source, bool active)
        {
            if (active)
            {
                AddGrant(type, new Grant { Source = source, IsTimed = false });
            }
            else
            {
                RemoveGrant(type, source);
            }
        }

        public void SetActive(FlightModifierType type, object source, float value)
        {
            AddGrant(type, new Grant { Source = source, IsTimed = false, Value = value });
        }

        public void ApplyForDuration(FlightModifierType type, object source, float duration)
        {
            if (duration <= 0f)
                return;

            if (IsActive(type))
                return;

            AddGrant(type, new Grant
            {
                Source = source,
                IsTimed = true,
                ExpiresAt = Time.time + duration,
            });
        }

        public void ApplyForDuration(FlightModifierType type, object source, float duration, float value)
        {
            if (duration <= 0f)
                return;

            if (!IsValueModifier(type) && IsActive(type))
                return;

            AddGrant(type, new Grant
            {
                Source = source,
                IsTimed = true,
                ExpiresAt = Time.time + duration,
                Value = value,
            });
        }

        public void Revoke(FlightModifierType type, object source)
        {
            RemoveGrant(type, source);
        }

        private void Update()
        {
            ExpireTimedGrants();
        }

        private float ResolveSimulationSpeed()
        {
            if (!_grants.TryGetValue(FlightModifierType.SimulationSpeed, out List<Grant> grants))
                return _baseSimulationSpeed;

            float now = Time.time;
            for (int i = grants.Count - 1; i >= 0; i--)
            {
                Grant grant = grants[i];
                if (!grant.IsTimed || now < grant.ExpiresAt)
                    return grant.Value;
            }

            return _baseSimulationSpeed;
        }

        private static bool IsValueModifier(FlightModifierType type)
        {
            return type == FlightModifierType.SimulationSpeed;
        }

        private void ExpireTimedGrants()
        {
            float now = Time.time;

            foreach (KeyValuePair<FlightModifierType, List<Grant>> entry in _grants)
            {
                List<Grant> grants = entry.Value;
                for (int i = grants.Count - 1; i >= 0; i--)
                {
                    Grant grant = grants[i];
                    if (grant.IsTimed && now >= grant.ExpiresAt)
                        grants.RemoveAt(i);
                }
            }
        }

        private void AddGrant(FlightModifierType type, Grant grant)
        {
            if (!_grants.TryGetValue(type, out List<Grant> grants))
            {
                grants = new List<Grant>();
                _grants[type] = grants;
            }

            RemoveGrant(type, grant.Source);
            grants.Add(grant);
        }

        private void RemoveGrant(FlightModifierType type, object source)
        {
            if (!_grants.TryGetValue(type, out List<Grant> grants))
                return;

            for (int i = grants.Count - 1; i >= 0; i--)
            {
                if (grants[i].Source == source)
                    grants.RemoveAt(i);
            }
        }
    }
}
