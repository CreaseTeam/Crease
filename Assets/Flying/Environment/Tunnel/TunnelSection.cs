using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crease.Flying.Environment
{
    [Serializable]
    public class TunnelSectionPair
    {
        public Mesh Mesh;
        public Material Material;
    }

    [CreateAssetMenu(fileName = "TunnelSection", menuName = "Crease/Tunnel Section")]
    public class TunnelSection : ScriptableObject
    {
        public GameObject BasePrefab;
        public string BaseName;
        [Min(1)]
        public int GroupSize = 3;
        public List<TunnelSectionPair> Pairs = new();

        public string GetPrefabName(int index)
        {
            int groupIndex = index / GroupSize;
            int placeInGroup = index % GroupSize + 1;
            char letter = (char)('A' + groupIndex);
            return $"{BaseName}_{letter}_{placeInGroup}";
        }

        void OnValidate()
        {
            if (GroupSize < 1)
                GroupSize = 1;
        }
    }
}
