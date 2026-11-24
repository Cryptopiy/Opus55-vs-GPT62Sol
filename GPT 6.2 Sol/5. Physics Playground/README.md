# Contraption Lab

Contraption Lab is a five-board physics puzzle built in the existing `physics-playground` project. Place parts on the workbench, then run the mechanism to guide the ball into the receiver.

## Open and play

Open [physics-playground/project.godot](physics-playground/project.godot) in **Godot 4.7.2 stable** and press **F6** with the main scene open or **F5** from any scene. The project opens directly into First Roll with a dismissible tutorial. Jolt and Forward+ are configured in the project. All models, textures, particles, and audio are generated from Godot primitives, shaders, and GDScript.

Level progress lives in `user://save.json`. The next board unlocks when you complete the current one. A win earns one star, meeting the part par earns a second, and meeting both part and time pars earns a third. The game keeps the best rating.

## Controls

| Control | Action |
|---|---|
| **1–9** or click a tile | Pick a part with stock remaining. The tile shows its key and remaining count. |
| **Left click** | Place the preview or select a placed part. For rope, press at the anchor and drag toward the bob before releasing. |
| **Left drag** | Move a selected part. |
| **Q / E** | Rotate the preview or selection by 15°. |
| **Ctrl + wheel** | Fine rotate in 5° increments. |
| **Right click** | Clear the active tool or selection. |
| **Delete** | Remove the selection and refund it. |
| **Ctrl+Z / Ctrl+Y** | Undo or redo a build edit. |
| **Middle drag / Shift + middle drag / wheel** | Orbit, pan, or zoom. |
| **F** | Reframe the board. |
| **Space / R** | Start or pause the simulation, or reset all bodies to their build positions. |
| **[ / ]** or speed controls | Choose ¼×, 1×, or 2× simulation speed. |
| **Hint** | After 60 seconds on the board, reveal one reference part. Click again for the next. |
| **Esc** | Pause menu with Resume, Restart, and Quit. |
| **F1 / F12** | Toggle diagnostics or save a timestamped PNG to `physics-playground/Screenshots/`. |
| **F8** | Toggle gameplay UI for recording and screenshots. Starts visible and carries across attempts and levels. Menus and results stay available. |
| **F9** | Replace the current layout with the level reference. Press Space to run it. |
| **F10** | Open the determinism and resting-stack tests. |

## Project structure

| Files | Responsibility |
|---|---|
| `scripts/config.gd`, `project.godot` | Level layouts, inventories, physics constants, and engine boot settings. |
| `scripts/lab.gd`, `scripts/board.gd`, `scripts/machine.gd` | Attempt lifecycle, ball and receiver, fixed-step feedback, shuttle, switch, and gate. |
| `scripts/mechanisms.gd`, `scripts/rope.gd`, `scripts/hammock.gd` | Physical parts, hinge and pin joints, cloth mesh, belts, launch pads, and airflow. |
| `scripts/blueprint.gd`, `scripts/workbench.gd`, `scripts/view_rig.gd` | Placement validation, capped undo/redo, cursor and camera controls, selection and hints. |
| `scripts/hud.gd`, `scripts/modal.gd`, `scripts/progress.gd` | Inventory, level selection, results, pause, developer menu, and save file. |
| `scripts/geometry.gd`, `scripts/studio.gd`, `scripts/effects.gd`, `scripts/sound.gd`, `shaders/` | Procedural visual materials, lighting, pooled effects, and synthesized audio. |
| `levels/solutions/`, `tests/`, `../Run-LabTest.ps1` | Reference builds, game-driven acceptance tests, and the shared-PC test launcher. |

## Design decisions and limits

- The shelf and approach angles use the coordinates and sizes in the brief. Where the brief left dimensions or angles open, I tuned them in Jolt so the solutions follow clear trajectories.
- Each attempt recreates the physics board in a fixed insertion order. The speed controls change tick cadence and time scale together so each solver step still advances 1/120 simulation second.
- The receiver has a physical liner that dissipates motion below the rim. The goal requires the ball center to remain inside its sensor for one continuous second. The hinge pivot is fixed at the center, as allowed by the brief.
- The cloth starts as a 2 × 2 m pattern with a 10 × 10 vertex grid. Its installed depth is 1 m. The steel-ball drop measured 0.374 m of peak sag. The physical rope suspension runs behind the cradle, leaving the ball room to ride in it.
- Swing Time’s included route uses a plank and spring. Hammock’s included route uses the spring. The rope and cloth are available in the editor for alternate builds. The Machine’s reference uses the moving platform and a crate to open the pressure gate.
- F10 checks five repetitions on the current machine. Physics results can vary across engine versions or hardware.

## Reference runs

| Board | Parts | Simulation time |
|---|---:|---:|
| First Roll | 2 | 5.650 s |
| Swing Time | 2 | 4.233 s |
| Counterweight | 3 | 5.608 s |
| Hammock | 1 | 4.783 s |
| The Machine | 1 | 6.317 s |

## Performance on this PC

The renderer was **D3D12 Forward+** on the RTX 5070 with VSync enabled. The full-kit benchmark used all nine placements in The Machine and kept the HUD and F1 diagnostics visible. Across monitored runs, shared GPU usage peaked at **4,135 MiB** and **58 °C**. The lowest sampled free RAM was about **27.7 GiB**. These figures include other running applications.

| Windowed test | Minimum FPS | Median FPS | Maximum FPS |
|---|---:|---:|---:|
| First Roll at 1440 × 900 | 169 | 180 | 180 |
| The Machine at 1600 × 900, 1440 × 900, and 1680 × 720 | 93 | 180 | 180 |
| The Machine at 1680 × 720 in a separate spot check | 177 | 180 | 180 |

Three capped sessions on the finished build logged **312.6 seconds of graphical play** without engine errors or warnings.

[Full-kit benchmark capture](physics-playground/Screenshots/Contraption-Lab_2026-11-23T22-09-50_166.png) · [21:9 layout capture](physics-playground/Screenshots/Contraption-Lab_2026-11-23T22-10-49_670.png)

## Repeat the checks

From this folder in PowerShell, run the following. The launcher confines test saves and logs to `test-logs/`, starts one owned Godot PID per run, and applies the project’s GPU gate when `-Graphics` is supplied.

```powershell
.\Run-LabTest.ps1 -Name import -Seconds 90 -Import
.\Run-LabTest.ps1 -Name acceptance -Seconds 45 -Profile checks -Flags @('--fixed-fps','120','--quit-after','45000','res://tests/acceptance.tscn')
.\Run-LabTest.ps1 -Name persist -Seconds 20 -Profile checks -Flags @('--quit-after','500','res://tests/acceptance.tscn','--','persist')
.\Run-LabTest.ps1 -Name mechanisms -Seconds 30 -Flags @('--fixed-fps','120','--quit-after','10000','res://tests/mechanism_probe.tscn')
```

See [CHECKLIST.md](CHECKLIST.md) for criterion-by-criterion evidence.
