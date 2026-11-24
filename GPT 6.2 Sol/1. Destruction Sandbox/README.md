# Destruction Range

A first-person destruction sandbox for Unity **6000.6.0f1**, using URP and the built-in PhysX backend. The tower loads immediately with a cannonball selected.

## Run

To play this revision, open the existing `Destruction Sandbox` project with Unity 6000.6.0f1, open `Assets/Scenes/Destruction Range.unity`, and press Play. The same scene is registered in **File → Build Profiles**. The executable in `Verification/Windows` is from the original release.

The mouse locks to the game during play. Press Esc to return control to the pointer. In Play Mode, F12 writes a timestamped PNG to `Destruction Sandbox/Screenshots`. In a standalone distribution, the screenshot folder is next to the executable. The local verification runner directs its screenshots to the project folder.

## Controls

| Input | Action |
| --- | --- |
| WASD, mouse | Move, look |
| Shift, Space | Sprint, jump |
| Left mouse button | Fire. Holding the button repeats when the weapon is ready. |
| 1, 2, 3 / mouse wheel | Select cannonball, explosive shell, or gravity orb |
| F5, F6, F7 | Rebuild as tower, arch wall, or pyramid |
| R | Reset the current structure, return to spawn, and refill ammunition |
| X | Clear shards, spent cannonballs, loose particles, and scorch marks |
| T | Toggle quarter-speed motion |
| Esc | Pause or resume |
| F1 | Toggle performance and pool counters |
| F8 | Hide or show all gameplay UI for recording, including diagnostics and popups. The pause menu stays available. |
| F12 | Capture a PNG screenshot |

The pause menu also contains mouse sensitivity, master volume, restart, and quit controls.

Gameplay UI starts visible and keeps its F8 setting through restarts, respawns, and structure changes. F12 captures the current view.

## Implementation

| File | Responsibility |
| --- | --- |
| `SandboxConfig.cs` | Gameplay dimensions, limits, timers, densities, impulse thresholds, and weapon parameters |
| `Sandbox.cs`, `PlayerRig.cs` | Scene construction, range boundaries, input, camera, CharacterController, recoil, and clock |
| `StructureLayout.cs` | Deterministic courses, bricks, arch geometry, and pyramid cells |
| `PieceField.cs`, `PhysicsPiece.cs` | Material bodies, collision impulses, queued fracture, bounded pools, shard age and shrink |
| `ProjectileField.cs`, `ProjectileBody.cs` | Ballistics, cooldown, ammo, shell fuse, and gravity field lifecycle |
| `ScoreRun.cs` | Unique block destruction, score, rolling combo, and session time |
| `RangeAppearance.cs`, `Resources/*.shader` | Procedural surface maps, sky, Fresnel glass, refraction, and URP post-processing |
| `VfxField.cs`, `SynthAudio.cs`, `RangePresentation.cs` | Particle bursts, blast pulse and decal pools, explosion lights, and synthesized voice playback |
| `RangeHud.cs`, `RangeInterface.cs` | Canvas-scaled HUD, score popups, diagnostics, and pause controls |
| `SandboxProbe.cs`, `AcceptanceRun.cs`, `DetailRun.cs`, `Editor/SandboxEditor.cs` | Runtime verification scenarios and headless Unity entry points |

## Design decisions and limits

- The tower rotates every odd course by 90° and offsets it by 0.16 m in both horizontal axes. The offset staggers joints between courses made of square blocks. Each layout begins with dynamic bodies asleep, ready to respond to contact.
- The wall uses the specified 1.0 × 0.5 × 0.5 m brick interpretation. The opening removes part of the 24-by-12 brick grid, leaving 254 bodies including 13 trapezoidal arch stones. Two diagonal glass blocks from the penultimate pyramid layer and the top block form the three-block cap.
- A block records its destruction once, at fracture or after crossing 3 m of displacement. The rolling combo uses unpaused real time and expires after two seconds without a destruction. Changing layouts or resetting restarts the score and session clock. Slow motion and the selected weapon carry across rebuilds.
- The radial shell and orb pulses use the material thresholds for their direct impulse. The shard fragments carry the parent angular velocity and the linear velocity at their position within the parent. Arch fragments use equal-mass boxes fitted to the wedge's bounds.
- Projectiles use sweep CCD. Blocks and shards use speculative contacts. Shards collide with the arena, intact pieces, and projectiles. Shard pairs pass through one another, which keeps the contact load bounded during a full collapse.
- The range uses generated textures, primitives, and one procedural arch mesh. All audio waveforms are synthesized on startup. The glass shader uses view-dependent transparency and brightened edges, while the orb shader samples the opaque scene for animated refraction.
- The arena lights and URP profile are assembled procedurally. The supplied PC renderer asset provides SSAO. Master volume and look sensitivity are session settings.
- F1 displays a dash for draw calls when Unity's recorder has no draw counter.
- The acceptance runner activates only with `-sandbox-mode`. It feeds the player's input handlers with Unity Input System devices. The safety runner in `Tools/Run-Sandbox.ps1` gates windowed launches, records telemetry, and enforces owned-process timeouts.

## Performance on this PC

The RTX 5070 and Ryzen 7 5700X3D ran the Windows player at **1920 × 1080**, windowed with VSync enabled. Five cannonball shots destroyed all 384 tower blocks and reached the 1,500-shard cap.

| Measurement | Result |
| --- | --- |
| Cannon test average | 168.3 FPS |
| Average with at least 1,000 active shards | 167.6 FPS across 1,521 frames |
| Longest frame in the loaded collapse | 33.03 ms |
| Longest frame in the shell test | 39.40 ms |
| Before and after 20 resets | 180.0 FPS both times, with stable object counts |
| Peak shared VRAM | 3,706 MiB |
| Peak GPU temperature | 52 °C |
| Lowest free system RAM | 31.2 GiB |

The final acceptance run and two endurance runs covered **308.38 seconds** of windowed testing with zero gameplay warnings or errors. The VRAM, temperature, and free RAM figures are desktop-wide samples from the shared PC.

The F8 regression ran in a fixed 1280 × 720 Unity Game view with VSync enabled. The loaded collapse averaged 158.6 FPS, the longest shell-test frame was 49.67 ms, and shared GPU readings peaked at 2,392 MiB and 46 °C during that run.

## Repeat verification

From the folder containing this README, run the commands one at a time. The runner writes logs and GPU samples into `Verification`.

```powershell
.\Tools\Run-Sandbox.ps1 -Action Build -LogName windows-build -Seconds 900
.\Tools\Run-Sandbox.ps1 -Action Player -Graphics -Mode acceptance -LogName windows-acceptance
.\Tools\Run-Sandbox.ps1 -Action Player -Mode detail -LogName windows-detail
.\Tools\Run-Sandbox.ps1 -Action Player -Mode stable-arch -LogName stable-arch
.\Tools\Run-Sandbox.ps1 -Action Player -Mode stable-pyramid -LogName stable-pyramid
```

The full acceptance sequence includes an untouched 60-second tower check, five cannon shots, a shell blast, the wall orb, the slow-motion pyramid shot, and 20 consecutive resets. The `endurance` mode repeats the three layouts and weapons for about 110 seconds per launch.

The updated F8 checks also run directly from the Unity project with `.\Tools\Run-Sandbox.ps1 -Action Play -Mode acceptance -LogName f8-acceptance`.
