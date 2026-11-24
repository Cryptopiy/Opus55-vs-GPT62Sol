# Drift Racer · Timberline Circuit

A Windows arcade racer with a seeded alpine circuit, rear-wheel-drive touring cars, three rivals and three laps. Drift for points, bank them by straightening out, and finish all three laps for a place in the final standings.

## Open and run

1. Open `drift-racer/project.godot` in the supplied Godot **4.7.2 stable** editor.
2. Press **F5**. The main scene opens on the grid with the race menu ready.
3. Keep seed **2024** and **Normal** for the default circuit, or enter another whole-number seed and choose a difficulty. Select **Start Race**.

The existing project uses Jolt, Forward+ and Direct3D 12 on Windows. The window starts at 1280×720 with VSync enabled. The UI scales with the window.

## Controls

| Key | Action |
|---|---|
| W | Throttle |
| S | Brake, then reverse near a stop |
| A / D | Steer |
| Space | Lock the rear wheels with the handbrake |
| R | Reset to the last checkpoint |
| C | Switch between chase and hood cameras |
| F1 | Toggle FPS, frame time, draw calls, active bodies and skid count |
| F3 | Toggle automatic counter-steer assist, enabled by default |
| F8 | Toggle gameplay UI for recording, including the F1 overlay. The UI starts visible. |
| F12 | Save a timestamped PNG under `drift-racer/Screenshots/` |
| Esc | Pause or resume |

On a Windows export, screenshots go in the `Screenshots` folder beside the executable.

F8 carries across respawns, race restarts and track changes. The main, pause and results screens remain usable, and F12 captures the current view.

## Architecture

| File | Responsibility |
|---|---|
| `project.godot`, `main.tscn`, `game/main.gd` | Engine settings, entry scene, state changes, camera routing and screenshot capture |
| `game/config.gd` | Track, car, drift, AI and race constants |
| `game/track_data.gd`, `game/track.gd` | Seeded spline and geometry validation, road and barrier meshes, collisions and checkpoint areas |
| `game/scenery.gd`, `game/geometry.gd`, `shaders/` | Procedural landscape, props, material shaders and shape helpers |
| `game/car.gd`, `game/player_input.gd` | Jolt body, four suspension and tyre rays, gearbox, braking and keyboard input |
| `game/ai_driver.gd`, `game/race_session.gd` | Racing line and pace, traffic, checkpoint order, grid, timers, rankings and recovery |
| `game/drift_score.gd`, `game/skids.gd`, `game/car_effects.gd` | Score chains, 600-strip skid pool, rear tyre smoke and barrier sparks |
| `game/car_visual.gd`, `game/chase_camera.gd` | Touring car bodywork, wheels, lights and cameras |
| `game/audio.gd` | Synthesized engine, wind, tyre and contact sounds and countdown beeps |
| `game/interface.gd`, `game/ui_style.gd`, `game/race_hud.gd` | Menus, results, timing, minimap and instruments |
| `tests/` | Headless acceptance and performance drivers, plus the PID-scoped Windows test runner |

## Implementation decisions

- Catmull–Rom candidates use 2 m arc-length sampling and a shorter closing segment. The generator advances to the next derived seed when the road fails the clearance, curvature or length checks. It locates the grid and grandstand on the straightest sampled stretch.
- The circuit alternates pale concrete and modeled tyre stacks. The visible barriers are one metre high. A continuous collision mesh extends above them to contain airborne cars during impacts.
- AI uses the same suspension, tyre forces and transmission as the player. It follows an inside-biased line, uses a backward braking pass to anticipate corners and applies a small seeded lane offset. Easy, Normal and Hard scale the calculated pace to 85%, 95% and 100% before rubber-banding.
- The drift angle multiplier increases from 0.45 at 15° to 1.0 at 80°. A chain banks after a full second below the drift threshold. A wall contact erases the unbanked score. The skid buffer reuses its oldest slot when full, so heavy drifting can replace a mark before its 20-second fade completes.
- Results appear once all four cars have completed three laps. After the player finishes, the car follows the circuit while the rest of the field finishes. **New seed** increments the seed and returns to the menu, where the number is editable.

## Verification and limits

The keyboard acceptance driver uses real Godot key events for a complete handbrake lap. The remaining laps use the shared controller to verify results. Separate tests cover a parked player, straight-line acceleration and braking, rollover, checkpoint recovery and the zero-log-error soak. The supplied acceptance checklist records the evidence for each requirement.

The Windows editor project uses keyboard controls. Gamepad support was optional and omitted.

All three AI opponents finished without resets or wall impacts with the player parked on seeds 2024 and −632. Their individual lap times stayed within 8.64% and 8.19%, respectively. A separate four-car run on seed −632 ended with two recovery resets and a 28.49% spread.

On the default circuit, the additional Easy and Hard runs produced the expected lower and higher speeds. All three opponents finished on both settings. One opponent recovered in each run, bringing the lap-time spreads to 30.89% on Easy and 23.99% on Hard.

The acceptance and performance tests can be repeated from the folder containing this README.

```powershell
& .\drift-racer\tests\run.ps1 -Name acceptance -EngineArgs @('--headless','--fixed-fps','120','--quit-after','54000','--script','res://tests/acceptance.gd')
& .\drift-racer\tests\run.ps1 -Name performance -Graphics -Limit 119 -EngineArgs @('--resolution','1920x1080','--windowed','--quit-after','20900','--script','res://tests/graphics.gd','--','102')
```

The runner writes the engine output and GPU samples under `.test-logs/`. [Keyboard acceptance output](.test-logs/final-headless.log), [parked-player output](.test-logs/release-ai-parked.log), [five-minute simulation](.test-logs/release-soak.log).

The F12 captures show the menu, an active drift and the barrier sparks. [Main menu](drift-racer/Screenshots/timberline_2026-11-23T22-13-01_583.png), [drift](drift-racer/Screenshots/timberline_2026-11-23T22-13-16_364.png), [barrier contact](drift-racer/Screenshots/timberline_2026-11-23T22-13-17_342.png).

## Performance on this PC

Measurements use the RTX 5070, Ryzen 7 5700X3D and 64 GB shared Windows machine. The tests run windowed at 1920×1080 with VSync on, Forward+ and all four cars driving the full-detail seed 2024 circuit. GPU telemetry samples the entire shared machine approximately every 15 seconds.

| Rendered run | Measured time | Average FPS | Lowest one-second average |
|---|---:|---:|---:|
| 1 | 102 s | 179.94 | 176.00 |
| 2 | 102 s | 176.12 | 126.49 |
| 3 | 102 s | 180.01 | 179.82 |
| **Combined** | **306 s** | **178.69** | **126.49** |

Peak sampled VRAM was **3,895 MiB**, peak GPU temperature was **53 °C**, and free RAM stayed at or above **31.19 GiB** during the performance runs. The three rendered runs and the separate continuous five-minute headless simulation recorded zero console errors or warnings. [Run 1](.test-logs/perf-1.log), [run 2](.test-logs/perf-2.log), [run 3](.test-logs/perf-3.log).

The highest sampled temperature across the benchmark and visual checks was **57 °C** on the shared GPU. The final rendered effects check exited cleanly through the game's Quit handler. [Final visual and shutdown console](.test-logs/final-visual-exit.console.log).
