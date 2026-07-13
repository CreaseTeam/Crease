# Welcome to Crease!

*Crease* is a **3D Flying Puzzle Adventure Game** where the player controls a daughter's letter to her father, which has been folded into a paper plane and must deliver itself safely.

Crease is still in-progress, however is planned for release on Steam for Windows, Mac, and Linux in **May 2027**

## Updates Since June 2026

- Plane selection screen with 5 plane types (Dart, Glider, Heavyweight, Spinner, Stunt), each with unique stats, fold pattern, and abilities
- Ability system with primary/secondary slots — Dash, Triple Dash, Time Stop, Corkscrew Deflection
- Loadout system ties plane type to flight settings, fold instructions, and abilities
- Folding starts automatically on level start; level-end unfold reveals the letter
- Accordion folds, shader-based crease lines, fold-edge highlighting on in-flight mesh
- Wwise audio integration (2025.1.8.9170); Unity built-in audio disabled
- Spline wind zones, wind tube centering force, and torque on the plane
- Combined level 1 blockout (canyon, grass/lake, forest, playground, etc.)
- Sun/moon sky system
- Pickup/drop items, magnetized coins, cosmetic damage decals
- Revised guide line system and handwritten text display
- Soft height ceiling, perpendicular collision behavior
- Settings menu updates, debug UI toggle, pause menu fixed during folding

## Controls

### Flight

| Keyboard     | Controller     | Action                                       |
|--------------|----------------|----------------------------------------------|
| W            | LStick U       | Push nose down                               |
| A / D        | LStick L / R   | Turn left / right                            |
| S            | LStick D       | Push nose up                                 |
| Mouse        | RStick         | Camera pan                                   |
| Arrow Keys   | —              | Also camera pan                              |
| X            | RStick Press   | Recenter camera                              |
| Space        | East Btn       | Primary ability                              |
| Left Shift   | L Trigger      | Secondary ability                            |
| F            | West Btn       | Drop item                                    |
| Scroll       | DPad           | Zoom camera                                  |
| C            | Select         | Go to folding mode                           |
| Esc          | Start          | Pause / Settings Menu                        |

### Folding

| Keyboard           | Controller     | Action                          |
|--------------------|----------------|---------------------------------|
| Space              | East Btn       | Apply fold                      |
| C                  | South Btn (A)  | Recenter screen                 |
| Right Mouse / Arrows | L Trigger    | Toggle paper rotation           |
| Mouse / Arrows     | RStick         | Rotate paper                    |
| W / S              | DPad U / D     | Scale sticker                   |
| A / D              | DPad L / R     | Rotate sticker                  |
| Esc                | Start          | Pause / Settings Menu           |

### Debug Controls (Dev Only)

| Keyboard | Controller | Action     |
|----------|------------|------------|
| R        | North Btn  | Reset      |
| Caps Lock | L Shoulder | Dev boost |

Built with Unity 6000.3.17f1.
