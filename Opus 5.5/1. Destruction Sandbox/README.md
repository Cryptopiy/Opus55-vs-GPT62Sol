# Destruction Sandbox (Unity 6000.6.0f1 · URP 17.6 · PhysX)

First-person destruction sandbox: three structures built from hundreds of physics blocks, three kinds of
ammunition (cannonball, explosive shell, gravity orb), fracture into shards with dust, sparks and splinters,
synthesized audio, score with combo. Everything is built from code at start-up: no imported models,
textures, sounds or fonts (the only font is Unity's built-in `LegacyRuntime.ttf`).

## How to open and run

1. Unity Hub → **Add project from disk** → select the folder `Destruction Sandbox` (next to this file).
   Open it with **Unity 6000.6.0f1**.
2. The main scene is `Assets/Scenes/DestructionSandbox.unity`. It contains one object, `GameBootstrap`; the
   world, player, UI and pools are created when Play starts. The editor script `ProjectSetup` also sets the
   **Play-mode start scene** to it, so pressing **Play** from any open scene starts the game.
3. Press **Play** and click into the Game view to capture the mouse (Esc frees it and opens the pause menu).
4. `Builds/Windows/DestructionSandbox.exe` is the build I tested before the F8 UI toggle was added, so it does not
   have F8. Rebuild to get the current version (windowed 1920×1080, VSync on). To rebuild from the command line:
   ```
   Unity.exe -batchmode -nographics -quit -projectPath "<path>\Destruction Sandbox" -executeMethod DestructionSandbox.EditorTools.ProjectSetup.BatchBuildPlayer
   ```
   The menu item **Destruction Sandbox → Rebuild Main Scene** recreates the main scene if it is ever deleted.

## Controls

| Input | Action |
|---|---|
| W A S D | Move (6 m/s) |
| Shift | Sprint (10 m/s) |
| Space | Jump (1.2 m) |
| Mouse | Look (sensitivity slider in the pause menu) |
| Left mouse button | Fire (hold = fire again as soon as the cooldown allows) |
| 1 / 2 / 3, mouse wheel | Cannonball / Explosive shell / Gravity orb |
| F5 / F6 / F7 | Build Tower / Wall with arch / Pyramid (old structure and its shards are removed) |
| R | Reset the current structure and refill shell ammo |
| X | Clear all shards and spent cannonballs (the structure stays as it is) |
| T | Toggle slow motion (×0.25) |
| F1 | Debug overlay (FPS, frame time, draw calls/SetPass, physics bodies, shards, particles, voices, pools) |
| F8 | Hide / show all UI for clean footage: HUD, crosshair, score pop-ups and the F1 overlay. The pause menu (Esc) still works while hidden |
| F12 | PNG screenshot to `<project>/Screenshots/Screenshot_<timestamp>.png` |
| Esc | Pause menu: Resume, Restart Structure, Mouse Sensitivity, Master Volume, Quit |

## Architecture (`Assets/DestructionSandbox`)

| File | Responsibility |
|---|---|
| `Scripts/Config/GameConfig.cs` | **The one config file**: every number from the spec (marked D-xx) and every tuning value |
| `Scripts/Core/GameBootstrap.cs` | Only scene object; builds everything at start, spawns the Tower |
| `Scripts/Core/Game.cs` | Static access to the running systems (reset on play-mode start) |
| `Scripts/Core/RuntimeSettings.cs` | VSync, 1/60 s fixed step, solver 12/4, sleep threshold, contact offset, layers |
| `Scripts/Core/GameInput.cs` | Keyboard/mouse polling (Input System package; the legacy Input Manager is disabled) |
| `Scripts/Core/GameController.cs` | Maps keys to actions (structures, reset, clear, slow motion, weapons, pause, F1, F12) |
| `Scripts/Core/ScoreSystem.cs` | Points per material, combo ×1…×5 on a rolling 2 s window, decay after 2 s |
| `Scripts/Core/SlowMotion.cs` | timeScale 0.25, fixed step ×0.25, audio pitch ×0.5 |
| `Scripts/Core/ScreenshotCapture.cs` | F12 PNG with timestamp |
| `Scripts/Core/PerfProbe.cs`, `PhysicsStepTimer.cs`, `FrameProbe.cs` | Allocation-free timing of physics step / fracture work (overlay + self-test) |
| `Scripts/World/WorldBuilder.cs` | 80×80 m ground, concrete border, invisible walls, warm sun, gradient sky, fog, post Volume |
| `Scripts/World/SkyAmbient.cs` | Projects the sky gradient into the ambient SH probe ("ambient light from sky") |
| `Scripts/Rendering/MaterialLibrary.cs` | All render materials (from the project's own shaders) and physics materials |
| `Scripts/Rendering/MeshLibrary.cs` | Procedural meshes: unit cube, sphere, cylinder, planes, convex prisms |
| `Scripts/Structures/BasicLayouts.cs` | Tower (384) and Pyramid (385) layouts |
| `Scripts/Structures/WallLayout.cs` + `PolygonUtil.cs` | Running-bond wall, 17-stone arch, cut stones, glass windows (268 pieces) |
| `Scripts/Structures/StructureData.cs` | Material constants, piece definitions, 2×2×2 split, per-piece colour hash |
| `Scripts/Structures/StructureManager.cs` | Block pool, build/rebuild, destroyed counting (fracture or >3 m), speed clamp |
| `Scripts/Physics/Block.cs`, `Shard.cs` | Pooled bodies; collision callbacks feed the fracture system |
| `Scripts/Physics/ShardManager.cs` | 1500-shard pool, oldest-first recycling, settle 12 s → shrink 1.5 s, safety checks |
| `Scripts/Physics/FractureSystem.cs` | Break rules, 8-shard split, inherited velocities, projectile pass-through, spawn budget |
| `Scripts/Physics/RadialImpulse.cs` | Explosion/pulse impulse (AddExplosionForce semantics) limited to the 80 m/s clamp |
| `Scripts/Weapons/WeaponController.cs` | Selection, cooldowns, shell ammo, parallax-corrected aiming, firing |
| `Scripts/Weapons/Projectile.cs`, `ProjectileManager.cs` | Pooled cannonballs/shells/orbs (ContinuousDynamic), trails, lifetimes |
| `Scripts/Weapons/CannonView.cs` | Procedural cannon view model, recoil 0.15 m / 0.2 s, muzzle light |
| `Scripts/Weapons/ExplosionSystem.cs` | Shell explosion: blast fracture of exposed pieces, radial impulse, VFX, sound, shake |
| `Scripts/Weapons/GravityOrbSystem.cs` | Stationary orbs: 4 s pull (60·m, clamped), 600 N·s pulse, visuals, hum |
| `Scripts/Player/PlayerController.cs`, `CameraShake.cs` | CharacterController FPS movement; distance-scaled trauma shake |
| `Scripts/VFX/ParticleFactory.cs`, `EffectsManager.cs` | Emit-driven world-space particle systems (dust, sparks, splinters, glass, fire, smoke, orb) |
| `Scripts/VFX/TransientEffects.cs` | Pools of flash lights, fresnel shockwaves, ground scorch decals |
| `Scripts/Audio/AudioSynth.cs` | All 11 sounds synthesized at start (sines, filtered noise, envelopes) |
| `Scripts/Audio/AudioManager.cs` | 16 voices, quietest-voice stealing, per-step impact budget, slow-mo pitch, master volume |
| `Scripts/UI/*.cs` | uGUI built from code: HUD, pause menu, F1 overlay, 20 pooled score pop-ups |
| `Scripts/UI/UiVisibility.cs` | F8 hide-all-UI switch: the one place that turns interface drawing on/off |
| `Scripts/Testing/AutoTest*.cs` | Self-test (only with `-autotest <scenario>` on the command line) |
| `Editor/ProjectSetup.cs` | Creates the main scene, Play-mode start scene, batch build / batch play entry points |
| `Shaders/DS_*.shader`, `DS_Common.hlsl` | Custom URP HLSL: blocks (stone speckle/wood grain/concrete/metal), glass (fresnel), grid ground, gradient sky, particles, shockwave, orb distortion, scorch |

## Decisions I made (and why)

**Fracture rule (D-10).** A block breaks when `Collision.impulse.magnitude` exceeds its threshold (glass 6,
wood 40, stone 180 N·s) **and** the contact is a real impact. Contacts inside a resting stack report the static
support impulse every step (a single glass block resting on another already reports m·g·dt ≈ 204 N·s), which is
above every threshold, so the raw rule would make the structures shatter under their own weight. "Real impact" means
the contact closed at ≥ 2.5 m/s, or it is a crushing impact: ≥ 0.6 m/s with an impulse ≥ 20× the threshold
(a column dropping onto rubble). Resting contacts close at about 0 m/s, so they never count.

**Explosion.** Every body within 6 m gets 1500·(1 − d/6) N·s with upward modifier 0.5 (AddExplosionForce
semantics, see `RadialImpulse`). Exposed pieces (line of sight to the centre) whose received blast impulse
exceeds their D-10 threshold shatter immediately (at most 32 per blast) and their shards are blasted too.
Without this the blast only slid whole floors a few centimetres: blocks pushed against neighbours keep their
existing contacts and never produce a new collision.

**Shards (D-11/D-12).**
- **Shape and mass:** the 8 shards are the exact 2×2×2 cells of the parent, each with its exact mass share. Every
  shard is drawn and collides at 85% of its cell; the missing volume is the dust cloud.
- **Support loss:** without the inset, eight cells fill the parent's volume exactly, so a tower whose base block
  shattered kept standing on the rubble.
- **Velocity:** shards inherit the parent's rigid-body velocity at their own centre (v + ω×r), plus an outward
  burst of 1.5–4 m/s.
- **D-12:** the "smaller than 0.25 m in every dimension" rule is applied literally to the exact cell size, so
  0.25 m is not "smaller". A tower block therefore splits into 0.5 → 0.25 → 0.125 m pieces.

**Cannonball pass-through.** A piece that fails can only resist with its threshold impulse, so the ball keeps the rest of its
momentum and ignores the debris of the piece it just broke (Physics.IgnoreCollision on those shards).

**Projectiles fly straight.** No gravity until their first impact, so they land under the crosshair. Cannonballs
get gravity after the first impact and roll. The spec gives no gravity for projectiles, and an 18 m/s orb would
otherwise drop 12 m before reaching the structure.

**Gravity orb "clamped".** Two clamps:
- the 60·m pull ramps to zero inside a 1.2 m core;
- the pull stops once a body already falls inward at 14 m/s.

Inside 2.2 m debris is also damped (4/s) so it gathers into a clump instead of oscillating through the centre.

**Robustness (D-27).**
- 80 m/s is set as `maxLinearVelocity` and also enforced after every step: PhysX only clamps before its solver.
- Blast impulses are limited to 80 m/s at the source.
- Bodies faster than 30 m/s temporarily use the cheap speculative CCD. The ContinuousDynamic mode is used only on projectiles.
- Light shards squeezed into the ground under a heavy pile, because of the PhysX mass ratio, are removed as soon as
  their centre is 0.3 m below the surface.

**Performance (D-26).**
- **Debris solver:** collapsing blocks and all shards use solver 4/1. Standing blocks keep the spec's 12/4.
- **Sleep:** shards sleep earlier (threshold 0.05).
- **Catch-up cap:** at most 2 physics steps per frame (`maximumDeltaTime` = 1/30 s × time scale). Overload slows
  the simulation slightly instead of stalling frames.
- **Fracture budget:** at most 64 shard spawns per step. Block breaks always wait their turn; queued sub-fractures
  of shards older than 0.15 s are dropped (the shard simply stays whole).
- **Broadphase:** automatic box pruning, which suits many moving bodies.

**Stability (D-08).**
- **Solver:** TGS, 12/4 iterations, improved patch friction.
- **Contacts:** contact offset 0.02, max depenetration 3 m/s.
- **Sleep:** sleep threshold 0.01.
- **Placement:** blocks are placed exactly face to face.

All blocks are asleep within 3 s and stay asleep.

**Structures.**
- **Tower:** blocks are 1×0.5×1 m, i.e. square, so "odd floors rotated 90°" only shows in the grain/pattern
  direction.
- **Pyramid:**
  - the "glass cap (3 blocks)" is the top three layers (3×3 + 2×2 + 1×1 = 14 glass cubes). That is the only
    symmetric reading that keeps the 385 total.
  - the "wood/stone" alternation starts with wood at the bottom.
- **Wall:**
  - built from 1.0×0.5×0.5 m bricks.
  - the 6 m arch springs at 1.5 m. Its outer outline has vertices exactly on brick course lines, so every brick
    next to the arch is a convex "rectangle ∩ half-plane" piece (no gaps, no overlaps).
  - 17 wedge stones (radial joints through every outline vertex, long spans split; 13 are exact trapezoids, 4 are
    slightly irregular wedge quadrilaterals) and 4 glass windows on rows 8–9.
  - it has 268 pieces instead of ~288 because the 6 m opening removes bricks.

**Score.**
- **Combo:** level = max(level, 1 + destroyed-in-last-2 s / 10), capped at ×5, and back to ×1 after 2 s without
  destruction.
- **Totals:** the score is a session total (R does not reset it); "Destroyed N / Total" is per structure.

**UI visibility (F8).**
- **One switch:** every canvas is created by `UIFactory.CreateCanvas`, which registers it with `UiVisibility`. F8
  only enables or disables those Canvas components; it never touches GameObjects, time, input or game state.
  F8 works at any moment, also with the pause menu open, and every other key keeps working while the UI is hidden.
- **What hides:** the HUD (crosshair, weapon/ammo/cooldown, score, combo, destroyed %, shards, timer, slow-motion
  tag), the score pop-ups and the F1 overlay. Pop-ups spawned while hidden, and the overlay toggled with F1 while
  hidden, stay hidden until F8 is pressed again. The state survives R and F5–F7.
- **What stays:** the pause menu is registered as always visible, so the game can never get stuck. The first-person
  cannon is a 3D model in the world, not interface, so it stays visible like the other 3D objects and effects.
- **F12:** the screenshot is the rendered frame, so it follows the switch. With the UI hidden, even an open pause menu
  is left out of the captured frame; it disappears for that one frame only.

**Time base.** Player movement, mouse look and weapon cooldowns use unscaled time, so the player keeps full control in
slow motion. Projectiles, physics, the shell fuse, the orb timer, combo and shard settle timers use game time.

**Rendering.**
- **Shaders:** custom URP shaders with procedural patterns in object-space metres. Per-block ±6% brightness and
  pattern seed come from `MeshRenderer.SetShaderUserValue`, which keeps all blocks of a type on one
  SRP-Batcher-friendly material.
- **SSAO:** it is the URP renderer feature (intensity 0.9). The SSAO Volume override of this URP version is compiled
  out (`MODERN_SSAO`). Bloom 0.6, ACES and vignette 0.2 are in the runtime Volume.
- **Glass:** it casts no shadow.

**Audio.**
- **Voices:** 16 voices; when all are busy the quietest one (volume × distance attenuation × remaining decay) is
  stolen, or the new sound is dropped if it would be the quietest.
- **Impacts:** only the 3 loudest per physics step are played.
- **Volume and pitch:** volume grows with impulse; pitch goes from 1.2 (light hit) to 0.78 (heavy hit).

**Scene / start-up.** Everything is created by `GameBootstrap`. Pools are filled at start:
- 385 blocks
- 1500 shards
- 21 projectiles
- 20 pop-ups
- 16 voices
- lights, shockwaves and decals

After that nothing is instantiated or destroyed: the GameObject count stays at 2082 through every test.

## Self-test (how the checklist was verified)

`AutoTest` plays the acceptance script through the same action methods as the keyboard and writes a report to
`<project>/TestResults`. Scenarios:

| Scenario | What it covers |
|---|---|
| `stability` | D-08 for all three structures (3 s settle check, 63 s watch) |
| `acceptance` | The full spec section 8 script |
| `collapse` | D-26 FPS during a full tower collapse |
| `soak` | Scripted play with all weapons, slow motion, resets and clears; then idle shrink and GameObject count |
| `showcase` | Screenshots |
| `ui` | F8 switch via real Input System key events (U-01..U-06); also runs at the end of `acceptance` |

**Running modes:**
- **Headless:** the editor in batch mode (`-nographics`). Every frame advances exactly 1/60 s of game time.
- **GPU:** the Windows build, windowed 1920×1080 with VSync. Each run goes through
  `Tools/run_with_gpu_guard.ps1`, which enforces the launch gate, 15 s monitoring, the hard-stop limits and a
  per-PID timeout.

Reports are in `Destruction Sandbox/TestResults` (headless editor) and `Builds/Windows/TestResults` (GPU build,
made before F8 was added). The single uncapped benchmark run is kept as its player log
(`uncapped_benchmark_player.log`). Screenshots from the GPU runs are in `Builds/Windows/Screenshots`, and every GPU/RAM
reading taken during the runs is in `Tools/gpu_monitor.log`. F8 was checked without a new build: headless, plus one
rendered `ui` run in the editor (`TestResults/AutoTest_ui_gpu_*.txt`, screenshots in `Destruction Sandbox/Screenshots`).

## Known limitations

- **Collapse spikes:** while a full 1500-shard pile is chaotic, one PhysX step can take 20–35 ms. At the single
  moment a pile is created or hit by another blast, an isolated frame of 100–250 ms was measured. The average stays
  far above 60 FPS (VSync at 180 Hz: 179 FPS average, 99th percentile 5.6 ms). The explosion test of section 8 stayed
  below 40 ms in every run.
- **Debris thins out:** with the 1500-shard budget, a full tower collapse recycles about 10–15 thousand of the oldest
  shards, so older debris disappears while new debris forms. After 12 s at rest, shards shrink away as specified.
- **Fragile structures:** the D-10 thresholds are tiny compared with the block masses (stone block 1.2 t vs 180 N·s),
  so almost every fall breaks a block. Five cannonballs at the base bring the tower down almost completely
  (86–100%), and one shell destroys about 80% of it.
- **Draw calls in builds:** the "Draw calls" and "batches" counters exist only in the editor and development
  builds. A release build shows "n/a" for them, but SetPass calls are shown.
- **Score pop-ups:** they overlap when dozens of blocks break in the same instant (the pool is limited to 20 by the
  spec).
- **Audio:** it is verified through code paths and voice counters; I could not listen to it.
- **Console check:** the "5 minutes without console messages" check was split into four soak runs of about 1.3
  minutes each, because every test process is limited to 120 s on this PC. Every other headless and GPU run also
  logged zero errors and zero warnings.
- **Editor speed:** in the editor the frame rate is lower than in the build (Hierarchy and inspector overhead with
  about 2,000 pooled objects).

## Performance on this PC (RTX 5070 12 GB, Ryzen 7 5700X3D, 64 GB)

The PC was shared with other agents during the measurements; the GPU was at 0–67% load from other processes at
launch. Measured with the Windows build, windowed 1920×1080:

| Measurement | Result |
|---|---|
| Tower standing, VSync on (180 Hz monitor) | 180 FPS, 5.56 ms frame, physics 0.3 ms |
| Full tower collapse (384 blocks + 1500 shards), VSync on, final build | avg **179 FPS** (5.59 ms), 99th percentile 5.6 ms, 3 of 3576 frames > 20 ms, worst single frame 56 ms (106 ms in an earlier run of the same physics code) |
| Same collapse, uncapped benchmark (one 33 s run) | avg **527 FPS** (1.90 ms), 99th percentile 7.9 ms, worst single frame 243 ms |
| Explosive shell into the tower (section 8, step 3) | worst frame 22 ms in the 3 s after the blast (final build; 22–37 ms over all runs) |
| 20 resets (D-28) | 5.56 → 5.56 ms average frame, GameObjects 2082 → 2082, managed heap 4.7 → 4.7 MB |
| Peak VRAM | 3.28 GB system-wide (2.2–3.0 GB were already used by other processes); the game itself adds about 0.26 GB (1.53 → 1.79 GB on an idle GPU) |
| Peak GPU temperature | 59 °C (while other processes also loaded the GPU); 43–48 °C on an idle GPU (final runs: 43–44 °C) |
| Headless physics cost (editor, no rendering) | 3–10 ms per step at the peak of a collapse, 0.3 ms when everything sleeps |
