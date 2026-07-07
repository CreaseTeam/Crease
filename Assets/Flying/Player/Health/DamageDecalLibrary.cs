using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crease.Flying.Player.Health
{
    [Serializable]
    public class DamageDecalEntry
    {
        public Texture2D Texture;
        [Tooltip("Size relative to paper width (1 = full width).")]
        public float DefaultScale = 0.1f;
    }

    [Serializable]
    public class DamageTypeDecalGroup
    {
        public DamageType Type;
        public List<DamageDecalEntry> Decals = new();
    }

    [CreateAssetMenu(fileName = "DamageDecalLibrary", menuName = "Crease/Damage Decal Library")]
    public class DamageDecalLibrary : ScriptableObject
    {
        public List<DamageTypeDecalGroup> Groups = new();

        public bool TryGetRandomEntry(DamageType type, out DamageDecalEntry entry)
        {
            entry = null;
            DamageTypeDecalGroup group = Groups.Find(g => g.Type == type);
            if (group == null || group.Decals.Count == 0)
                return false;

            var valid = new List<DamageDecalEntry>();
            foreach (DamageDecalEntry candidate in group.Decals)
            {
                if (candidate?.Texture != null)
                    valid.Add(candidate);
            }

            if (valid.Count == 0)
                return false;

            entry = valid[UnityEngine.Random.Range(0, valid.Count)];
            return true;
        }
    }
}
