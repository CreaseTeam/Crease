using UnityEngine.Serialization;

namespace Crease.Flying.Player.Health
{
    public enum DamageType
    {
        Impact,
        Tear,
        Fire,
        Water,
        Nasty,
    }

    [System.Serializable]
    public struct DamageSegment
    {
        [FormerlySerializedAs("amount")]
        public float Amount;
        [FormerlySerializedAs("type")]
        public DamageType Type;
        [FormerlySerializedAs("normalizedSize")]
        public float NormalizedSize;
    }
}
