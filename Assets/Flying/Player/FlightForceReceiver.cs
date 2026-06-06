using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Crease.Flying.Environment.Wind;

namespace Crease.Flying.Player
{
    [RequireComponent(typeof(KinematicBody))]
    [RequireComponent(typeof(FlightController))]
    public class FlightForceReceiver : MonoBehaviour
    {
        [Header("Wind Source")]
        [Tooltip("Current active wind zones affecting this plane. Automatically managed by triggers.")]
        [FormerlySerializedAs("activeWindZones")]
        public List<WindProvider> ActiveWindZones = new List<WindProvider>();

        [Header("Settings")]
        [Tooltip("Multiplier for how much the wind affects the physics.")]
        [FormerlySerializedAs("windForceMultiplier")]
        public float WindForceMultiplier = 1.0f;

        private KinematicBody _body;

        private void Awake()
        {
            _body = GetComponent<KinematicBody>();
        }

        public void AddWindZone(WindProvider zone)
        {
            if (!ActiveWindZones.Contains(zone))
            {
                ActiveWindZones.Add(zone);
            }
        }

        public void RemoveWindZone(WindProvider zone)
        {
            if (ActiveWindZones.Contains(zone))
            {
                ActiveWindZones.Remove(zone);
            }
        }

        private void FixedUpdate()
        {
            if (ActiveWindZones.Count == 0) return;

            Vector3 totalWindForce = Vector3.zero;

            for (int i = ActiveWindZones.Count - 1; i >= 0; i--)
            {
                WindProvider zone = ActiveWindZones[i];
                if (zone == null)
                {
                    ActiveWindZones.RemoveAt(i);
                    continue;
                }
                totalWindForce += zone.GetWindForceAtPoint(transform.position);
            }

            if (totalWindForce.sqrMagnitude > 0.01f)
            {
                Vector3 finalForce = totalWindForce * WindForceMultiplier;
                _body.AddForce(finalForce);
            }
        }

        public void AddImpact(Vector3 force)
        {
            _body.AddImpulse(force);
        }

        public void AddExternalForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            _body.AddForce(force, mode);
        }

        public void AddExplosionForce(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier = 0.0f)
        {
            Vector3 dir = transform.position - explosionPosition;
            float distance = dir.magnitude;

            if (distance > explosionRadius || distance < 0.001f) return;

            float falloff = 1f - (distance / explosionRadius);
            Vector3 force = dir.normalized * explosionForce * falloff;
            force.y += upwardsModifier * falloff;

            _body.AddImpulse(force);
        }
    }
}
