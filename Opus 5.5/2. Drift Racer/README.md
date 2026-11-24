# Drift Racer (Godot 4.7.2, GDScript, Forward+, Jolt Physics)

A 3D arcade racer on a procedurally generated closed circuit. The car uses a custom
raycast-suspension model on a `RigidBody3D` (not `VehicleBody3D`) with drift mechanics,
three AI opponents on the same physics, a 3-lap race, synthesized audio and a full HUD.
No external assets: all geometry is generated in code, every texture is a shader or a
runtime gradient, and every sound is synthesized in code.

## How to open and run

* Engine: **Godot 4.7.2** (`Godot_v4.7.2-stable_win64.exe`).
* Open the project folder `drift-racer/` (it contains `project.godot`) in the Godot editor and press **F5**.
  The main scene `res://scenes/main.tscn` builds everything in code and starts on the main menu.
* Command line (from this folder):
  `Godot_v4.7.2-stable_win64_console.exe --path drift-racer`
* Main menu: **Start Race**, track **seed** (type a number or use `-` / `+`, default 2024),
  **difficulty** (Easy / Normal / Hard), **Quit**.

## Controls

| Key | Action |
|---|---|
| `W` / `Up` | Throttle (in reverse: brake) |
| `S` / `Down` | Brake; when (nearly) stationary: reverse |
| `A` `D` / `Left` `Right` | Steer |
| `Space` | Handbrake (rear grip 40 %, rear wheels locked) |
| `R` | Respawn at the last checkpoint |
| `C` | Toggle camera (chase / hood) |
| `F3` | Toggle the counter-steer assist (default ON) |
| `Esc` | Pause menu (Resume / Restart / Main menu / Quit) |
| `F1` | Debug overlay |
| `F12` | Save a PNG screenshot to `drift-racer/Screenshots/` |
| `F8` | Hide / show **all** interface for clean footage (HUD, speedometer, minimap, timers, drift score, countdown, WRONG WAY, F1 overlay, loading text, mouse cursor). Default: shown |
| Gamepad (optional) | Left stick steer, RT throttle, LT brake/reverse, A handbrake, Y respawn, Back camera, Start pause |

Keys are bound by physical position, so WASD works with any keyboard layout.

**F8 / clean footage:** hiding the UI never pauses or changes the race. Every other key keeps
working, and the state survives restarts, respawns and new races until F8 is pressed again.
F12 screenshots match what you see, so with the UI hidden they contain only the game world.
Screens you need to go on (main menu, pause menu, results) always stay visible, so the game
can never get stuck. If you run the project from the Godot editor with the game embedded in
the editor's Game tab and F8 stops the game instead, set Editor Settings → Run → Window
Placement → Game Embed Mode to a separate window, or use the exported build.

**How to drift:** at 50-90 km/h steer into a corner and tap `Space`. When the tail steps
out, keep holding the steer key and the throttle. The assist counter-steers for you and
holds the slide. Throttle controls the angle; release the steer key to straighten the car.
A drift scores while the body slip is 15-80 deg above 40 km/h. It is banked 1 s after
the slide ends and lost if you touch a barrier.

## Architecture (which file does what)

| File | Responsibility |
|---|---|
| `scripts/config.gd` | **Every** tunable number (spec values tagged with their requirement ID). |
| `scripts/core/main.gd` | Entry point: menus, HUD, starting / restarting / leaving races, F1 / F12 / Esc, clean quit. |
| `scripts/core/input_setup.gd` | Builds the input map in code (keyboard + optional gamepad). |
| `scripts/core/screenshot.gd` | F12 PNG capture with timestamped file names. |
| `scripts/track/track_generator.gd` | Seed → 12 control points → closed centripetal Catmull-Rom → 2 m resampling → validation → derived-seed retries; start line on the straightest section. |
| `scripts/track/track_data.gd` | Sampled circuit: frames, curvature, distances, closest-sample / projection queries, spatial hash, spawn transforms. |
| `scripts/track/racing_line.gd` | AI racing line (elastic band, max 4 m) and speed profile (v = sqrt(9 R) + braking pass). |
| `scripts/track/road_builder.gd` | Road / curbs / run-off mesh and the trimesh road collider. |
| `scripts/track/barrier_builder.gd` | Alternating concrete / tyre-stack barriers, thick trimesh collider, catch fence. |
| `scripts/track/ground_builder.gd` | Height-field ground that follows the road elevation, outer ground disc, height-map collider. |
| `scripts/track/vegetation_builder.gd` | 720 trees in chunked MultiMeshes with LOD0/LOD1 visibility ranges. |
| `scripts/track/landmarks_builder.gd` | Start/finish arch with banner, grandstand with crowd, 12 distant hills. |
| `scripts/track/environment_builder.gd` | Procedural sky, sun with 150 m shadow distance, fog, tone mapping, glow. |
| `scripts/track/checkpoints.gd` | 8 invisible `Area3D` gates (gate 0 = start/finish). |
| `scripts/track/track_world.gd` | Assembles the whole world for a track. |
| `scripts/track/mesh_arrays.gd` | Mesh-building helper (auto front-face winding, trimesh faces). |
| `scripts/car/car.gd` | The car: `RigidBody3D` + 4 `RayCast3D`, suspension, tyre forces, steering + assist, contacts, flip / kill-plane respawn requests. Same script for player and AI. |
| `scripts/car/drivetrain.gd` | RWD engine, torque curve, 6-speed automatic box, torque cut, reverse, rev limiter. |
| `scripts/car/tyre_model.gd` | Slip-angle friction curve (peak 8 deg, 70 % beyond 25 deg) and load sensitivity. |
| `scripts/car/car_visuals.gd` | Procedural car body, wheels (travel / steer / spin), headlights, brake and reverse lights. |
| `scripts/car/car_fx.gd` | Feeds skid marks, rear-wheel tyre smoke, barrier sparks. |
| `scripts/car/car_controller.gd`, `player_controller.gd`, `ai_controller.gd` | Controllers that write the car's inputs (neutral / keyboard + gamepad / AI driver). |
| `scripts/fx/skid_marks.gd` | 600-segment ring-buffer MultiMesh of skid strips, faded in the shader over 20 s. |
| `scripts/fx/fx_factory.gd` | Shared particle materials / meshes for smoke and sparks. |
| `scripts/race/race.gd` | One race session: track, world, cars, controllers, camera, countdown beeps, R / C / F3. |
| `scripts/race/race_manager.gd` | Grid, countdown, ordered checkpoints, laps, timing, positions, wrong way, respawns, finish. |
| `scripts/race/drift_scorer.gd` | Drift detection, score, combo, banking, loss on wall hit. |
| `scripts/race/chase_camera.gd` | Chase camera (lagging, drift-aware, speed FOV) and hood camera. |
| `scripts/audio/engine_synth.gd` | Real-time wavetable engine voice for `AudioStreamGenerator`. |
| `scripts/audio/synth.gd` | Generated WAV sounds: countdown beeps, thud, squeal, wind and scrape loops. |
| `scripts/audio/car_audio.gd` | Per-car sound: engine, squeal, scrape, thud, wind (player). AI cars are 3D with Doppler. |
| `scripts/ui/*.gd` | Theme, main menu, HUD, speedometer + RPM bar, minimap, pause menu, results, F1 overlay. |
| `scripts/ui/ui_visibility.gd` | The one central F8 "hide all UI" switch. Every top-level interface item (including UI created later) goes on one canvas visibility layer, and hiding drops that layer from the viewport's cull mask. Menus, pause and results are exempt. |
| `scripts/debug/*.gd` | Automated self-tests (only run with `--autotest=...`, never in normal play). |
| `shaders/*.gdshader` | Procedural asphalt / lines, curbs, gravel, grass, concrete, trees, hills, checker, skid marks. |
| `../tools/run_monitored.ps1` | Test launcher enforcing the shared-PC GPU safety rules (gate, monitoring, hard stop, PID-only kill, timeout). |

## Decisions I made (interpretations of the spec)

* **Track (R-01/R-02):** 12 control points at equal angles. Base radius 220-320 m, then
  ±25 % radial noise and ±6 m elevation smoothed by two neighbour-average passes.
  The spline is a *centripetal* Catmull-Rom, which has no cusps or self-loops inside
  segments. It is resampled to N = round(L / 2) samples, so the spacing is about 2 m.
  "Self-intersection" is checked as a corridor overlap: two track parts more than 70 m
  apart along the track must be at least 26 m apart, so roads and barriers never overlap.
  Corner radius is the circumradius over ±6 m. Rejected layouts are regenerated from
  `hash(seed, attempt)`. Seeds 0-199 needed at most 7 attempts and never used the
  plain-circle fallback. The start line goes at the middle of the straightest 140 m.
  Seed 2024 gives a 1998 m clockwise circuit with a 43.5 m minimum radius.
* **Road cross-section (R-03/R-04):** 14 m asphalt + 0.3 m curbs + 1.2 m gravel run-off,
  so the barrier face is 8.5 m from the centreline. Barriers are 1.0 m tall and alternate
  concrete / tyre stacks every 32 m. Their collider is a closed 1.6 m thick prism with the
  same inner face as the visuals. An invisible catch fence (1-4 m) above the wall makes
  sure a car can never leave the track area. A height-map ground collider and a kill
  plane are extra safety nets.
* **Centre of mass (R-07):** the volume-weighted centre of the two collision boxes,
  lowered by 0.3 m. Inertia is set explicitly. Angular damping is 0.9, tuned with the
  `drift_sweep` test: no spin-outs, but slides can still be held.
* **Suspension (R-08):** spring 30 000 N/m and damper 2 500 N·s/m, with a bump stop
  near full travel and a mild anti-roll term. The force acts along the contact normal at
  the contact point.
* **Drivetrain (R-09):** the 9 000 N maximum is the rear-axle force at peak torque in
  1st gear. Higher gears scale it by their ratio. 6th gear reaches the 7 500 rpm limiter
  at about 190 km/h; measured top speed is 187.8 km/h. Upshift at 7 000 rpm, downshift at
  3 500 rpm, with a 0.18 s torque cut. Pull-away uses simulated clutch slip. Reverse
  engages when |v| < 1 m/s and `S` is held.
* **Tyres (R-10):** lateral force = curve(slip angle) × μ × load × load-sensitivity
  factor, with μ = 1.25. The curve peaks at 8 deg and falls to 70 % from 25 deg on.
  Longitudinal force comes from drive, engine braking, brakes and rolling resistance.
  A friction circle gives longitudinal force priority (throttle on the rear gives power
  oversteer). Loads come from the suspension, so weight transfer changes grip. The
  handbrake locks the rear wheels: their friction (40 % grip) opposes the whole sliding
  velocity of the contact patch.
* **Steering and assist (R-11):** maximum steering angle falls linearly from 32 deg at
  standstill to 8 deg at 190 km/h, and the wheels move at most 4 rad/s. The counter-steer
  assist adds 0.9 × body slip angle. While sliding it scales the driver's input to 50 %,
  so a held key holds the slide instead of killing it. Tuned with `drift_sweep`: a held
  key, or a stick at ≥ 0.4 with full throttle, holds a 25-55 deg slide for more than 5 s
  with no spin-outs, and releasing the key recovers within ~0.4 s. The total wheel angle
  never exceeds 32 deg.
* **Drift score (R-14):** slip factor = slip / 30 deg, clamped to 0.5-2.5. The combo grows
  every 2 s of drifting inside a chain. Gaps shorter than 1 s keep the chain. The chain is
  banked once no drift has happened for 1 s, but only if it contained at least 1 s of
  drifting. Any barrier contact loses the pending chain.
* **AI (R-21-R-23):** the racing line is an elastic-band relaxation clamped to ±4 m.
  Speed profile: v = sqrt(9 · R), then a backward braking pass at 6.5 m/s². Steering is
  pure pursuit with a look-ahead of 7 m + 0.55 s × v. Each car has its own slowly varying
  lane offset of up to ±1.5 m. Avoidance checks the 6 m gap ahead plus 1.1 s per m/s of
  closing speed, so it still reacts in time at racing speed. It dodges to the free side
  and brakes when closing faster than 3 m/s. The AI driver eases off the throttle when
  its rear slides (a driver skill, not different physics).
* **Race flow (R-25/R-26):** 2×2 staggered grid with the player at the back. The race
  clock starts at GO; the first crossing of the line starts lap counting. Positions come
  from progress along the track. Wrong way = heading opposite the track direction
  (dot < -0.5) for more than 2 s.
* **After the player finishes**, the AI drives the player's car on a cool-down lap. The
  results screen opens immediately, lists cars still racing as "lap x/3 ..." and then
  shows "Final standings" once everyone has finished. "New seed" starts a race on a
  random seed.
* **Pause menu** has Resume, Restart, Main menu and Quit. This combines both spec lists.
* **Physics interpolation** is on: 120 Hz physics renders smoothly on any refresh rate
  (the PC has a 180 Hz monitor).
* **F8 hide-all-UI:** the loading text and the OS mouse cursor count as interface, so they hide
  too (the cursor comes back whenever a menu is open). The "START / FINISH" lettering on the
  arch banner is painted world signage, part of the 3D scene, so it stays.
* **F1 overlay:** Jolt does not feed Godot's "active objects" monitor, so awake rigid
  bodies are counted directly. GPU particle counts are estimated from emitter settings.

## Testing (self-checks)

Run the scenarios through the safety launcher, one Godot process at a time.
Headless scenarios use no GPU:

```
powershell -ExecutionPolicy Bypass -File tools\run_monitored.ps1 -Name ai_race -TimeoutSec 600 -GodotArgs "--headless --fixed-fps 120 -- --autotest=ai_race"
```

| Scenario | What it checks |
|---|---|
| `track_stats` | 200 seeds: attempts, lengths 1.2-2.0 km, radius ≥ 18 m, determinism of seed 2024 |
| `drift_rules` | Drift window, score formula, combo steps and cap, 1 s banking, wall loss |
| `physics_lab` | Rest load, 0-100 km/h, top speed, braking, skidpad, step steer (no roll), handbrake drift, reverse |
| `drift_sweep` | Assist / damping / throttle / steering sweep used for tuning |
| `ai_race` / `full_race` | 3-lap race of AI only (player parked) / all 4 cars, results order |
| `drift` | One lap with a scripted handbrake drifter: drifts, score, smoke, skid marks |
| `wall` | 100 km/h @ 30 deg, 190 km/h @ 45 deg and 85 deg into the barrier: no tunnelling, sparks |
| `respawn`, `flip`, `ai_stuck` | `R` mid-corner, auto-respawn when upside down, AI stuck reset |
| `audio_check` | Engine synths produce sound, SFX streams generated |
| `screens`, `drift_shots`, `bench`, `results_shot` | GPU runs: screenshots, FPS benchmark, results screen |
| `ui_toggle` | F8 through the input system: no UI item draws while hidden; R / C / F3 / F1 / Esc still work; pause menu and main menu stay drawn; UI spawned while hidden and a race restart stay hidden; everything returns on the second F8 |
| `ui_toggle_shots` (GPU) | Frozen world: hidden frame == frame with every UI layer off, F12 file == no UI, frame after the second F8 == frame before hiding |

Logs from my runs are in `test_logs/`. Screenshots are in `drift-racer/Screenshots/`.

## Known limitations

* The scripted drift test driver is clumsy. It completes 4-6 handbrake drifts per lap
  with score building up, smoke and skid marks, but its line choice often brushes a
  barrier right after the slide, which correctly loses the drift chain (0-1 chains banked
  per lap). A human player should hold lines much better. Scoring rules and sustained
  slides are verified separately (`drift_rules`, `drift_sweep`).
* I cannot listen to the audio. I only verified that the engine buffers are non-silent
  (RMS ≈ 0.25) and that all SFX streams are generated. Mix balance may need taste
  tweaks.
* Gamepad bindings exist (optional requirement) but were not tested with a real device.
* Physics is not bit-deterministic between runs (multi-threaded Jolt), so AI lap times
  vary slightly. The track itself is fully deterministic per seed.
* In two extremely accelerated headless tuning runs (`drift_sweep`, thousands of physics
  steps per second on a busy CPU), Jolt printed one "job system exceeded the maximum
  number of jobs ... waiting" warning. It never appeared in race scenarios, real-time
  runs or GPU runs.
* No exported executable is included (run from the editor or the command line).

## Performance on this PC

RTX 5070 12 GB, Ryzen 7 5700X3D, 2560×1440 @ 180 Hz monitor. The GPU was shared with
other agents during all runs, and the VRAM figures are the whole GPU as reported by
nvidia-smi.

| Run (1920×1080 window, 4 cars, full track) | Avg FPS | Min FPS | Peak VRAM (GPU total) | Peak GPU temp |
|---|---|---|---|---|
| VSync on (180 Hz) | 179.4 | 159 | 2.77 GB | 55 °C |
| Uncapped (one 45 s benchmark) | 342.3 | 308 | 2.59 GB | 67 °C |
| All GPU test runs combined | – | – | 3.28 GB | 67 °C |

The game's own VRAM use is about 0.5-1 GB (GPU total before launch was 1.4-2.2 GB).
Draw calls are about 510-570 per frame. Track generation plus world build takes about
1.0-1.9 s.
