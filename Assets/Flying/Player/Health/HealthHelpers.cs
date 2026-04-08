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
    public float amount;
    public DamageType type;
    public float normalizedSize;
}