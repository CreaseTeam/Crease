// Motion for the ambient paper ribbon effect, used by a Custom HLSL block in the
// Update context of PaperRibbonAmbient.vfx.
//
// Doing this in one function rather than a chain of Force, Drag and operator nodes
// keeps the physics readable and reviewable as text, and removes the only part of the
// graph that would otherwise need operator wiring.
//
// `deltaTime` is a global provided by the VFX update pass. If a package version stops
// exposing it, add a `float deltaTime` parameter to the signature below and feed it
// from a Delta Time operator in the graph.

#ifndef CREASE_PAPER_RIBBON_FORCES
#define CREASE_PAPER_RIBBON_FORCES

// Stable per particle value in [0,1). Uses particleId so it costs no attribute
// storage and stays identical for the life of a particle.
float PaperRibbonHash(uint id, float salt)
{
    return frac(sin((float)id * 12.9898 + salt * 78.233) * 43758.5453);
}

// Drags each ribbon toward the ambient flow, and spirals a subset of them into
// loop-de-loops while a burst is active.
//
//   flowVelocity   world space velocity the air is moving at
//   flowStrength   how hard ribbons are pulled toward it, 1/seconds
//   loopAxis       axis the loops orbit about, normalised
//   loopOmega      angular rate of the loops in radians/second
//   loopIntensity  0 to 1 burst envelope driven by PaperRibbonWindVfx
//   loopFraction   share of ribbons that participate, 0 to 1
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

    // Drag toward the flow rather than a raw push. This gives terminal velocity
    // behaviour for free, so ribbons settle into the airflow instead of accelerating
    // without limit, and no separate drag block is needed.
    float3 accel = (flowVelocity - v) * flowStrength;

    // Only a subset of ribbons loop, so the effect reads as a few showing off rather
    // than the whole field turning at once.
    float flagged = step(1.0 - loopFraction, PaperRibbonHash(attributes.particleId, 3.7));
    float turn = loopOmega * loopIntensity * flagged;

    // An acceleration perpendicular to velocity, of magnitude omega * |v|, rotates the
    // velocity vector at angular rate omega. That is exactly circular motion of radius
    // |v| / omega. Explicit Euler integration lets the radius creep outward slightly
    // each revolution, which reads as a natural opening spiral, so it is left alone.
    accel += cross(loopAxis, v) * turn;

    attributes.velocity = v + accel * deltaTime;
}

#endif // CREASE_PAPER_RIBBON_FORCES
