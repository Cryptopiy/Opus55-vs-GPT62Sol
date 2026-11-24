# Contraption Lab — Godot 4.7.2 (Jolt) physics puzzle

A 2.5D physics puzzle: place planks, boxes, levers, rope pendulums, spring pads, conveyors, a cloth hammock and fans
to deliver the ball into the glowing cup. Five hand-built levels, a full in-game editor with undo/redo, hints,
stars and saved progress. Everything (geometry, textures, sounds) is generated in code — no external assets.

## How to open and run
* **Editor:** Godot 4.7.2 → *Import* → `physics-playground/project.godot` → **Run (F5)**. The main scene
  `res://main.tscn` builds everything in code and opens on the level select screen (Level 1 is unlocked).
* **Command line:** `Godot_v4.7.2-stable_win64.exe --path physics-playground`
* **Automated tests (headless, no GPU):**
  `Godot_v4.7.2-stable_win64_console.exe --headless --fixed-fps 120 --path physics-playground -- --autotest=full`
  (prints `AUTOTEST PASS/FAIL ...` lines; single modes: `solutions|det|detall|editor|save|stack|lab|scale|worlds|ui`). Tests use their own
  save file, so the player's progress is never touched.
* **Visual check (GPU):** `... --path physics-playground -- --autotest=screens` writes screenshots to
  `tools/screens/` (level select, each level in build / solution / running / result state, menus, 3 aspect ratios,
  F1 overlay, F8 hidden-UI frame + F12 file check) and prints an FPS measurement of level 5. `--bench` adds one
  uncapped FPS run.
* `tools/run_godot.ps1` is the launcher used for every run during development: one process at a time, hard timeout,
  kill by PID only, and for GPU runs the launch gate + 5 s monitoring + hard stop from the task rules.

## Controls
| Action | Input |
|---|---|
| Pick an inventory item | click the slot in the bottom bar, or keys `1`–`9` (again / right-click to drop the tool) |
| Place the ghost | `LMB` (ghost green = valid, red = overlapping scenery, a forbidden zone or another object) |
| Rope pendulum | `LMB` on the anchor, drag to set length (1–6 m, 0.25 m steps) and start angle, release |
| Rotate | `Q` / `E` = ±15°, `Ctrl` + mouse wheel = ±5° (ghost or selected object) |
| Select / move / delete | `LMB` on a placed object (yellow outline), drag to move, `Del` removes it (item refunded) |
| Lever fulcrum / rope length | select the object, `Alt` + drag its yellow handle (fulcrum 0.5 m steps / rope bob) |
| Undo / redo | `Ctrl+Z` / `Ctrl+Y` (also `Ctrl+Shift+Z`), 100 operations deep |
| Play / pause simulation | `Space` or the Play button |
| Reset (back to build mode) | `R` or the Reset button |
| Simulation speed ×0.25 / ×1 / ×2 | buttons, or `[` / `]` |
| Camera | `MMB` drag orbit (±45° yaw), `Shift+MMB` pan, wheel zoom, `F` re-frame |
| Hint (after 60 s in a level) | Hint button — each press reveals one more object of the reference solution |
| Reference solution | `F9` (replaces your placements, undoable) then `Space` |
| Pause menu | `Esc` (Resume / Restart level / Quit game) |
| Debug overlay | `F1` (FPS, frame ms, draw calls, physics bodies + active, physics step ms, particles, rope stretch) |
| Dev menu | `F10` (determinism test ×5, load solution, 3-box stack test) |
| Screenshot | `F12` → `physics-playground/Screenshots/screenshot_<date>_<time>.png` |
| Hide / show all UI (clean footage) | `F8` (default: shown). Hides HUD, buttons, inventory bar, tooltips, banners, tutorial text, F1 overlay and F10 menu; the game keeps running and every hotkey above still works. Level select, pause menu and results screen are never hidden. The state survives restarts and level changes; `F12` while hidden saves a UI-free image. |

## Architecture (which file does what)
`physics-playground/`
* `main.tscn` + `scripts/game/main.gd` — entry point; level select ↔ level session, global keys (F1, F8, F10, F12, Esc), clean quit.
* `scripts/core/config.gd` — **every number** of the game (spec values marked with their IDs) and the 5 level layouts.
* `scripts/core/` — `save_data.gd` (autoload `Save`, `user://save.json`), `placement_model.gd` (placements, inventory,
  undo/redo ops), `footprint.gd` (2D shapes, SAT overlap + picking), `materials.gd` / `mesh_lib.gd` (procedural
  materials, meshes, cached and shared).
* `scripts/objects/` — one module per placeable (`obj_plank`, `obj_box`, `obj_lever`, `obj_rope`, `obj_spring`,
  `obj_conveyor`, `obj_cloth`, `obj_fan`): footprint, build-mode visual, simulation bodies + per-tick logic;
  `registry.gd` (names, tooltips, type → module); `ball.gd`, `goal_cup.gd` (cup + sensor + glow), `machinery.gd`
  (L5 moving platform, switch plate, hinged door).
* `scripts/game/` — `level_session.gd` (state machine BUILD/RUNNING/PAUSED/SUCCESS/FAILED, world swap, stars,
  hints, results), `sim_world.gd` (simulation built at every Play: fixed-order tick logic, goal hold, kill plane,
  timeout, NaN guard, impacts), `build_editor.gd` (ghost, snapping, validity, select/drag/rotate/delete, handles),
  `camera_rig.gd` (auto-frame, orbit/pan/zoom, shake, success zoom), `level_builder.gd` (static scenery, forbidden
  zones, backdrop, obstacle footprints), `environment_builder.gd` (light, SSAO, glow), `vfx.gd` (pooled dust,
  confetti, multimesh ball trail), `dev_tools.gd` (determinism + stack tests).
* `scripts/ui/` — `hud.gd`, `inventory_bar.gd`, `level_select.gd`, `overlays.gd` (results, tutorial, pause menu),
  `debug_overlay.gd`, `dev_menu.gd`, `widgets.gd` (vector-drawn stars, padlock, item icons), `ui_style.gd` (theme),
  `ui_visibility.gd` (F8: the one place that shows/hides the HUD and debug layers; level select, pause menu and
  results live on a separate screens layer that is never hidden).
* `scripts/audio/` — `sfx.gd` (autoload `Sfx`: player pool + loops), `synth.gd` (all sounds synthesized).
* `shaders/` — rounded-edge flat material, back-wall gradient + grid, forbidden-zone hatching, conveyor chevrons,
  selection outline, ghost, fan streaks, local snap grid.
* `levels/solutions/L1..L5.json` — reference solutions (type, position, rotation, parameters).
* `tests/autotest.gd` (headless acceptance suite), `tests/screens.gd` (GPU screenshots / FPS),
  `tests/playbot.gd` (windowed play driven by synthetic mouse/keyboard input, `--autotest=play --part=1|2|3`),
  `tests/check_scripts.gd` (loads every script/shader; with `tools/check_warnings.sh` all default-on GDScript
  warnings are escalated to errors, so "0 failed" means zero warnings).
* `tools/` (outside the project): `run_godot.ps1` (guarded launcher), `search.py` (solution search),
  `check_warnings.sh`, `screens/` (latest screenshots from the GPU run).

## Decisions
* **Determinism (P-02).** Jolt results depend on body IDs and creation order: rebuilding the same scene in the same
  physics space drifted up to 12 cm in a prototype (and joints still drifted ~1 cm with strict LIFO removal). Every
  Play therefore swaps in a **fresh World3D** (fresh physics space); the static scenery re-enters in tree order, then
  the ball, the level machinery and the player's objects (sorted canonically, independent of placement order) are
  created. All gameplay logic runs once per physics tick in one fixed-priority node, from simulation time only (no
  frame time, no RNG). Result: 5 runs differ by 0.000000 m on every level, headless and in the windowed game.
* **Retired physics worlds.** Destroying a Jolt space and then stepping physics right away intermittently triggers
  the engine warning "Jolt Physics job system exceeded the maximum number of jobs" (reproduced ~1 in 6 stress runs).
  Replaced worlds are therefore *retired* (space deactivated, ~0.3 MB each) and freed only after the level has been
  in build mode for 0.6 s (`RETIRE_CALM_TIME`). Afterwards: 0 warnings in 12 stress runs, 8 full suites and all GPU runs.
* **Cloth determinism.** Two engine behaviours made the hammock non-reproducible and are worked around:
  (1) `SoftBody3D` refreshes its render mesh from the physics body in `RenderingServer.frame_pre_draw`, i.e. at frame
  rate (never in headless), which perturbed windowed runs - that hook is disconnected and the cloth is drawn by
  `ClothView` from per-tick samples; (2) Jolt bakes a soft body's stiffness from the *current* ticks-per-second when
  it is created, so simulation objects are always built at the nominal 120 Hz before the speed setting is restored.
  Now ×0.25 / ×1 / ×2 / F10 turbo, headless and windowed all give the identical hammock result.
* **Time scale keeps the step fixed.** ×0.25/×2 change the number of 120 Hz ticks per second
  (`physics_ticks_per_second = 120 × s`, `time_scale = s`), so every step is exactly 1/120 s and ×1/×2/×0.25 give
  bit-identical runs (tested). Physics interpolation smooths the slow-motion rendering.
* **Build mode is physics-free.** Placed objects are visual only; validity uses 2D footprints (SAT) against scenery,
  forbidden zones, the ball's start, the L5 machinery sweep areas and other objects; the physics server is inactive.
* **Hole coordinate** = centre of the cup interior / sensor (1.2 × 1.0 m). Cup walls are lined with an absorbent
  material (Godot `PhysicsMaterial.absorbent`) so a delivered ball stops instead of bouncing out, and a backboard on
  the far side catches fast balls. Lip heights per level are in `config.gd`.
* **Completion time** for stars = simulation time when the ball entered the cup for the successful 1.0 s hold
  (the HUD timer freezes at that value).
* **Ball start:** the ball is held at its start point until Play. L1 and L5 start platforms are tilted (−12°, −8°)
  so the ball starts rolling; L3's ball (−7, 1) is 0.7 m above the ground and drops onto whatever is placed below.
* **Lever:** limits are absolute ±35° from horizontal (plank created flat, hinge made, then the initial tilt applied);
  placement rotation = initial tilt; fulcrum movable in 0.5 m steps (implemented, OPTIONAL in the spec).
* **Rope pendulum:** rotation = start angle of the rope from vertical (±90°); the 4 kg bob is a cradle (floor + two
  lips) so a ball can ride in it. Jolt solver raised to 32 velocity / 12 position steps to keep stretch < 5 %
  (measured 4.26 % worst case: 6 m rope released horizontally; 7.6 % with the default 10/2 steps). After placing,
  `Alt`+drag on the bob handle re-adjusts length/angle (same gesture as the placement drag).
* **Spring pad:** the impulse sets the body's velocity component along the pad normal to exactly 12 m/s.
* **Fan:** 15 N per kg of the body (= "15 N on a 1 kg object"), linear falloff over the 4 m field length.
* **Cloth:** the spec says "2 × 2 m plane" and "depth 1 m"; the hammock is 2 m along X × 1 m deep (Z) with 10 × 10
  vertices. A small pre-shaped dip in the rest mesh plus damping gives ~0.35–0.38 m sag under a 1 kg ball and a
  stable rest (a flat cloth was bouncy and only sagged ~0.2 m).
* **Level layouts** fill in what the spec leaves open (platform sizes, ledges, forbidden zones, L5 platforms).
  Reference solutions were found with a headless parameter search (`tools/search.py`) and are re-verified by the
  autotest. F9-loaded completions count for progress (needed to unlock levels in acceptance test 1).
* **Pause menu "Quit"** quits the game; the HUD "Levels" button returns to the level select. The game opens on the
  level select (one click to play), which is the main scene's playable state.
* **Hints** unlock after 60 s of real time spent in the level (build + simulation), counted per visit.
* **Physics interpolation** is on (smooth motion at ×0.25 and on high-refresh displays); visual-only nodes that are
  moved per frame (camera, editor, VFX, rope tube) opt out.

## Known limitations
* The hammock is 2 m × 1 m (X × depth) rather than a literal 2 × 2 m plane (see Decisions); CHECKLIST marks P-11 PARTIAL.
* The cloth is drawn from per-tick samples (not interpolated), so at ×0.25 it visibly updates at 30 Hz.
* "Zero errors/warnings during 5 minutes of play" was verified as 4 consecutive windowed runs (~3.8 min of scripted
  play through the real input paths) plus many headless suites, because each test run is capped at 120 s by the
  PC-safety rules - not as one continuous manual 5-minute session.
* The engine warning about the Jolt job pool is avoided by the retire-then-free scheme, not fixed in the engine; a
  release that happens in the very instant before the player presses Play remains a (small, unobserved) risk.
* Sounds are synthesized and play without errors, but their quality was not checked by ear.
* Reference solutions do not use every inventory item (e.g. L5 is solved with 4 of 10 items).
* Determinism is bit-exact for the same build on the same machine; cross-platform determinism was not tested.
* Aspect ratios tested: 16:9 (1600×900), 16:10 (1728×1080), 21:9 (1920×823); minimum window 960×540.

## Performance on this PC
RTX 5070 12 GB, Ryzen 7 5700X3D, 64 GB RAM, shared with other agents. Values from `tools/run_godot.ps1` monitoring
(nvidia-smi is system-wide, so VRAM includes other processes; ~1.5–2.0 GB was already in use before each launch).
* Peak VRAM during any of my runs: **2.78 GB** (system total); peak GPU temperature: **59 °C**; free RAM never below 38.9 GB.
* Level 5 fully built and simulating, 1600×900, VSync on (180 Hz display): **180 FPS** average, worst frame 5.55 ms,
  longest physics frame 2.7 ms.
* Same scene, one uncapped 10 s benchmark (VSync off): **800 FPS** average, worst frame 5.3 ms, physics ≤ 1.04 ms.
* Headless: the full acceptance suite (20+ checks, ~70 simulated runs) completes in ~20–26 s.

## Time spent (approx.)
* Jolt prototypes (restitution, rope stretch, cloth, conveyor, hinge, determinism): ~20 min
* Core code (objects, simulation, editor, camera, UI, audio, VFX): ~1 h
* Level design + solution search: ~40 min
* Determinism hardening (world swap, engine job-pool warning, soft-body issues): ~60 min
* GPU verification, play bot, docs: ~35 min
