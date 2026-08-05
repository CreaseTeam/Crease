# Paper ribbon wind VFX

Tearaway-style strips of paper drifting through the air, with a subset spiralling
into loop-de-loops when the general wind direction swings.

## Setup

Everything is generated. Nothing here needs hand authoring in the editor.

Run **Tools > Crease > Paper Ribbons > Set Up Everything**. That runs, in order:

1. `Generate Ribbon Meshes` writes three curved strip meshes to `Meshes/`.
2. `Generate Ambient Ribbon VFX` writes `PaperRibbonAmbient.vfx`.
3. `Create Prefab And Test Scene` writes `PaperRibbonWind.prefab` and
   `Assets/Scenes/Test Scenes/PaperWindVFX.unity`.

Then open the test scene, press Play, and fly.

### If step 2 fails

The VFX Graph authoring model is internal to `Unity.VisualEffectGraph.Editor`, so
`PaperRibbonVfxGenerator` drives it through reflection by name, and those names can
move between package versions.

Run **Dump VFX Model API**, open `Temp/VfxApiDump.txt`, and reconcile the `Names`
region at the top of `Editor/PaperRibbonVfxGenerator.cs` against it. The console
error from a failed run lists exactly which names did not resolve.

Nothing downstream depends on the graph internals, only on the exposed blackboard
property names, so building the graph by hand is also a valid escape hatch. The
failure message prints the full property list.

## Using it in a real scene

Drag `PaperRibbonWind.prefab` in. It resolves the camera, `KinematicBody` and
`FlightForceReceiver` on its own at play time, so usually there is nothing to wire.

## Tuning

`_density` on `PaperRibbonWindVfx` is the master dial. At the shipped 0.5 the effect
settles around 36 ribbons on screen (6/s spawn rate against a 4 to 8 second
lifetime, hard capped at 64). If it ever feels busy, lower density rather than
shrinking the ribbons; small ribbons at high density read as dirt on the lens.

Other things worth knowing:

- `_relativeStreamFactor` subtracts a share of the player's own velocity from the
  flow. This is most of the sensation: fly fast and ribbons rush past, coast and
  they hang. Turning it to 0 makes the effect feel dead.
- `_loopOmega` sets loop tightness. Loop radius is particle speed divided by omega,
  so 1.6 rad/s at 5 m/s gives roughly a 3 m loop. Raise it to 3.0 temporarily when
  testing to make loops unmistakable.
- `_loopFraction` is how many ribbons take part. Keep it low. Loops read as a few
  showing off, not the whole field turning at once.

## Files

| File | What it is |
|---|---|
| `PaperRibbonWindVfx.cs` | Runtime driver. Computes flow, detects direction shifts, pushes exposed properties. |
| `PaperRibbonForces.hlsl` | The simulation. Flow drag plus the loop-de-loop, in one function. |
| `Editor/PaperRibbonMeshGenerator.cs` | Builds the three ribbon meshes. |
| `Editor/PaperRibbonVfxGenerator.cs` | Builds the VFX graph. |
| `Editor/PaperRibbonSetup.cs` | Builds the prefab and test scene, and the run-everything command. |
| `Editor/VfxModelProbe.cs` | Diagnostic. Dumps the VFX authoring API to `Temp/VfxApiDump.txt`. |

## Notes

- VFX Graph needs compute shaders, so this will not run on a WebGL build.
- The `Mobile` quality level has depth and opaque textures disabled. Do not add any
  scene depth or scene colour sampling to the ribbon shading or it will break there.
