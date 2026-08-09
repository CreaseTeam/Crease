# Spline Path Placer — Designer Guide

Place any number of copies of a prefab along a curvy path you draw right in the Scene view.
Good for coin trails, obstacle rows, decorative arcs, pickup snakes — anything that follows a line.

---

## Quick Start (30 seconds)

1. **Create an empty GameObject** (`GameObject ▸ Create Empty`) and name it e.g. `Coin Arc`.
2. **Add Component ▸ Crease ▸ Tools ▸ Spline Path Placer**. A gentle default arc appears with
   placeholder copies once you assign a prefab.
3. In the inspector, drag your prefab into **Prefab** (e.g. `Coin`) and set **Count** (e.g. `20`).
4. Copies now sit evenly along the arc. Done.

To reshape the path: keep the object selected, click the **Spline** tool in the Scene view's
tools overlay, then drag the round knots. The copies redistribute **live** as you drag.

> The placed copies are a **live preview** and are not written into the scene file. When you're
> happy, press **Bake To Permanent Objects** to turn them into real, saveable objects, then delete
> the placer.

---

## Inspector Reference

| Field | What it does |
|---|---|
| **Prefab** | The object to copy along the path. |
| **Count** | How many copies to place. |
| **Uniform Spacing** | On = even by real distance along the curve. Off = even by curve parameter (bunches on tight bends). Leave on unless you have a reason. |
| **Start / End Normalized** | Trim where copies begin and end along the path (0 = start, 1 = end). Use to leave a gap at either end. |
| **Align To Path** | Rotate each copy to face along the path direction. |
| **Up Source** | Which "up" to use when aligning — the spline's own up, or world up. |
| **Rotation Offset** | Extra Euler rotation added to every copy (e.g. spin coins to face the camera). |
| **Position Offset** | Nudge every copy in its own local space (e.g. lift objects off the line). |

**Buttons**

- **Rebuild Now** — force a refresh (rarely needed; it updates automatically).
- **Reset Path To Default Arc** — throw away the current path and start from the default arc.
- **Bake To Permanent Objects** — commit the current preview into real objects under a new
  `… (Baked)` GameObject. In the editor these keep their prefab link. Delete the placer afterward.

---

## Editing the Path

- **Move a knot:** select the object, choose the Spline tool, drag a knot.
- **Add a knot:** Ctrl (Windows) / Cmd (Mac) + click on the path.
- **Delete a knot:** select it and press Delete.
- **Sharper vs. smoother bends:** select a knot and change its tangent mode in the Element
  Inspector overlay (Auto is smooth; Linear makes straight segments).
- **Make a loop:** enable **Closed** on the Spline Container component (copies wrap around with no
  overlap at the seam).

---

## Benchmark 1 — A simple arc of coins

1. Create empty GameObject → add **Spline Path Placer**.
2. Prefab = `Coin`, Count = `15`.

That's it — the default path *is* an arc, so you immediately get an arc of 15 evenly spaced coins.
Drag the middle knot up for a taller arc, or the end knots apart for a wider one.

## Benchmark 2 — A more complicated snake of coins

Start from the arc above, then:

1. Select the object and activate the **Spline** tool.
2. Ctrl/Cmd-click the path two or three times to add extra knots.
3. Drag the knots into an alternating left-right S / zig-zag — a "snake".
4. Bump **Count** up to `40` so the snake reads as a continuous stream.

The coins flow through every curve, evenly spaced, updating as you drag. To make the snake wind
through 3D (up-and-over as well as side-to-side), drag knots on the Y axis too.

When the shape looks right, press **Bake To Permanent Objects** and delete the placer.

---

## Notes

- Uniform spacing assumes roughly uniform scale on the placer object; extreme non-uniform scale can
  make distance spacing approximate.
- The preview regenerates whenever the scene loads, so the placer must stay in the scene to keep the
  live copies — or Bake them to make them permanent.
