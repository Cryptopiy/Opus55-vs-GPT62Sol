# Bastion 06

Bastion 06 is a first-person wave survival game set in an enclosed industrial containment hall. Clear four waves, defeat the Warden in wave 5, then continue into Endless mode from the victory screen.

## Run

1. Open the existing `Wave Survival` project in **Unity 6000.6.0f1**.
2. Open `Assets/Scenes/SampleScene.unity` and press **Play**. Use the Game view for keyboard and mouse input.
3. For a Windows player, build the enabled scene with the existing PC URP quality settings. Keep VSync enabled.

## Controls

| Input | Action |
| --- | --- |
| WASD / mouse | Move / look |
| Shift | Sprint |
| C (hold) | Crouch |
| Space | Jump |
| LMB / RMB | Fire / aim down sights |
| R | Reload |
| 1, 2, 3 / mouse wheel | Pistol, rifle, shotgun / cycle |
| Esc | Pause or resume |
| F1 | Toggle the performance overlay |
| F2 | Toggle AI states, destinations, cover points and the NavMesh |
| F3 | Jump to wave 5 |
| Shift + F3 | Toggle player invulnerability |
| F8 | Toggle gameplay UI and debug overlays for clean footage. UI starts visible, and the setting survives restarts. Pause and results remain available. |
| F12 | Save a timestamped PNG in `Wave Survival/Screenshots/` |

## Implementation

| Source files under `Wave Survival/Assets/Wave/` | Responsibility |
| --- | --- |
| `GameConfig.cs` | Arena and gameplay constants, pool limits, weapon tables and rank thresholds |
| `CoreGame.cs`, `WaveDirector.cs`, `MatchStats.cs` | Scene initialization, match state, wave queues, gate selection and statistics |
| `Arena.cs`, `ArenaArt.cs`, `Geometry.cs` | Primitive geometry, runtime NavMesh, procedural surfaces, lamps and post-processing |
| `PlayerMotor.cs`, `Weapons.cs` | CharacterController, look and aim, stamina, firing, ammunition and weapon cadence |
| `Enemy.cs`, `EnemyBrains.cs`, `EnemyModel.cs` | Agent movement, recovery, state machines, armor, hit zones, animation and primitive characters |
| `Projectiles.cs`, `Pickups.cs` | Bounded projectile and pickup pools |
| `Viewmodel.cs`, `Effects.cs`, `Presentation.cs`, `SynthAudio.cs` | Weapon models, animation, combat effects, pooled audio sources and synthesized clips |
| `Hud.cs`, `HudWidgets.cs`, `HudMenus.cs`, `WorldHud.cs`, `CoreHud.cs`, `DrawCounters.cs` | Screen and world HUD, menus, AI and performance overlays, screenshots |
| `Acceptance*.cs` | Command-line verification scenarios for the running game |

## Design decisions

- The containment hall uses teal route lights and amber warning lights. The player starts with the pistol selected and all three weapons available.
- For pistol ADS, the specified 0.2° overrides the general half-spread rule. The shotgun uses a 6° included cone, and each pellet counts separately toward accuracy and headshots.
- Shooter cover is scored from reachable positions in the 12–18 m band with occlusion at crouched eye and body height. A shooter without a valid cover point moves back into its firing band and uses a direct peek when it has a clear shot.
- The Warden keeps the slam in phase III. Phase III replaces rifle bursts with the spread volley and runner summons with shooter summons. Endless begins with an eight-second break and clears the Warden's surviving summons.
- The HUD counts active and queued enemies as hostiles. Slot pressure can delay a group until two slots are open. A boss spawn or the final queued enemy can use a single slot.
- Rank requires all three thresholds for the grade. S requires at least 65% accuracy, at most 100 damage and at most 7:00. A uses 50%, 225 and 11:00. B uses 35%, 400 and 16:00. Other results receive C.
- The URP build retains post-processing variants for the Volume created by the gameplay scene.

## Known limitations

- Enemy collapses use posed animation, and the AI overlay labels agents within the viewport. Shooter cover depends on the player's position and nearby occluding geometry.
- Play verification used scripted inputs and invulnerability during the population benchmark. The performance numbers describe this workload on the shared PC.

## Verification

The headless runs cover the four regular waves, the Warden, armor, drops and synthesized audio. The Windows runs cover input, screenshots, 1080p and 1440p HUD layout, and the sustained 25-enemy workload. The per-requirement results are in [CHECKLIST.md](CHECKLIST.md).

See the [25-enemy encounter](<Wave Survival/Screenshots/Bastion_2026-11-23_22-16-25-572_Wave07.png>), [1440p Warden capture](<Wave Survival/Screenshots/Bastion_2026-11-23_22-16-04-097_Warden.png>) and [victory screen](<Wave Survival/Screenshots/Bastion_2026-11-23_22-16-05-894_Victory.png>).

## Performance on this PC

The Windows Development Build ran at 1920 × 1080 on the RTX 5070 and Ryzen 7 5700X3D, in a window with VSync enabled. Both wave 7 benchmarks held 25 active enemies after spawn warm-up.

| Measurement | First benchmark | Repeat |
| --- | ---: | ---: |
| Mean frame rate at the enemy cap | 179.96 FPS | 179.99 FPS |
| Sampled frames at the enemy cap | 15,998 | 16,287 |
| Longest sampled frame | 30.54 ms | 11.13 ms |
| Peak sampled GC allocation or collection marker | 1.223 ms | 1.210 ms |
| Peak managed allocations in one sampled frame | 105 bytes | 105 bytes |
| Peak draw calls | 301 | 442 |

The final windowed runs peaked at **2,586 MiB** of total sampled VRAM and **59 °C**, with at least **44.2 GiB** of free system RAM. The three longer 1080p runs totaled **313 seconds** of scripted gameplay and stress testing with zero runtime errors or warnings. [First benchmark](.verification/checked-performance.log), [repeat benchmark](.verification/checked-repeat.log), [controls and progression](.verification/checked-controls.log), [GPU telemetry](.verification/checked-performance-gpu.csv).
