namespace Crease.Flying.Player.FlightSettings
{
    /// <summary>
    /// Must stay in sync with public float fields on <see cref="FlightSettings"/>.
    /// Each enum member name must exactly match its field name.
    /// </summary>
    public enum FlightStatType
    {
        Mass,
        DivingGravity,
        ClimbingGravity,
        Lift,
        DiveRate,
        ClimbRate,
        ClimbEfficiency,
        TurnInterpolation,
        StabilityStrength,
        StabilityReferenceSpeed,
        StabilityInputSuppression,
        XDrag,
        YDrag,
        ZDrag,
        PitchSpeed,
        MaxPitch,
        YawSpeed,
        RollSpeed,
        RollBackSpeed,
        MaxRoll,
        BoostSpeed,
        InitialSpeed,
        MinimumVelocity,
        WindForceMultiplier
    }
}
