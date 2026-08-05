// Used by the Custom HLSL block in the Update context of PaperRibbonAmbient.vfx.
// deltaTime is a global provided by the VFX update pass.

#ifndef CREASE_PAPER_RIBBON_FORCES
#define CREASE_PAPER_RIBBON_FORCES

// Stable per particle value in [0,1).
float PaperRibbonHash(uint id, float salt)
{
    return frac(sin((float)id * 12.9898 + salt * 78.233) * 43758.5453);
}

void PaperRibbonFlow(
    inout VFXAttributes attributes,
    in float3 flowVelocity,
    in float flowStrength,
    in float3 loopAxis,
    in float loopOmega,
    in float loopIntensity,
    in float loopFraction)
{
    float3 v = attributes.velocity;

    // Drag toward the flow, which gives terminal velocity without a drag block.
    float3 accel = (flowVelocity - v) * flowStrength;

    float flagged = step(1.0 - loopFraction, PaperRibbonHash(attributes.particleId, 3.7));
    float turn = loopOmega * loopIntensity * flagged;

    // Acceleration perpendicular to velocity of magnitude omega * |v| turns the
    // velocity vector at rate omega, giving a circle of radius |v| / omega.
    accel += cross(loopAxis, v) * turn;

    attributes.velocity = v + accel * deltaTime;
}

#endif // CREASE_PAPER_RIBBON_FORCES
