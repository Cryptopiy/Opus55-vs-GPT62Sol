# Wave Survival — first-person arena shooter (Unity 6000.6.0f1, URP)

Wave-based survival FPS built entirely from code: a 50 × 50 m industrial arena, three weapons with procedural
primitive viewmodels, three enemy types with finite-state-machine AI (flanking runners, cover-using shooters,
charging armoured tanks), pickups, a three-phase boss ("The Warden") and an endless mode.
**No external assets**: every mesh is a Unity primitive or a procedural mesh, every texture is generated at runtime
(`Procedural/`), every sound is synthesized in code (`Audio/`). Packages used: URP, AI Navigation (NavMeshSurface),
Input System, uGUI (all official, already in the project).

## How to open and run
1. Open the folder `Wave Survival` (next to this file) with **Unity 6000.6.0f1**.
2. The main scene `Assets/Scenes/Main.unity` contains a single `GameBootstrap` object. It is opened automatically
   and is also set as the Play Mode start scene, so pressing **Play** from any scene starts the game.
   Everything (arena, lights, NavMesh, player, UI, audio) is built by code at startup (~1 s).
3. Optional player build: menu **Wave Survival → Build Windows Player** (output `Wave Survival/Build/WaveSurvival.exe`).
   The menu **Wave Survival → Setup Project** regenerates the scene and the shader-keeper materials (already done).

## Controls
| Action | Key |
|---|---|
| Move / look | W A S D / mouse |
| Sprint (stamina) | Left Shift (hold) |
| Crouch | C (hold) |
| Jump | Space |
| Fire / aim down sights | Left mouse / Right mouse (hold) |
| Reload | R |
| Weapons | 1 pistol, 2 assault rifle, 3 shotgun, mouse wheel cycles |
| Pause menu (Resume / Restart / Quit) | Esc |
| Debug overlay (FPS, frame time, draw calls, bodies, enemies, particles...) | F1 |
| AI debug (state labels, destination lines, NavMesh, cover points) | F2 |
| **Cheat:** jump straight to wave 5 (boss) | F3 |
| **Cheat:** toggle invulnerability (god mode) | F4 |
| Screenshot (PNG to `<project>/Screenshots/`) | F12 |
| **Hide / show all UI** for clean footage (HUD, crosshair, hit markers, damage arcs, health bars, banners, notifications, F1 overlay, F2 debug view; pause/death/victory screens stay usable; state survives Restart, F12 then shoots without UI) | F8 |

## Architecture (Assets/Scripts)
| Folder / file | Responsibility |
|---|---|
| `Config/GameConfig.cs` | **The single config file**: every number of the spec (tagged S-xx) and all tuning values. |
| `Core/GameBootstrap.cs` | Scene entry point; builds all systems in order, sets VSync and the layer collision matrix. |
| `Core/Game.cs`, `GameManager.cs` | Service locator; game state (playing / paused / dead / victory), restart (scene reload), quit, cursor. |
| `Core/ComponentPool.cs`, `MathUtil.cs`, `StringCache.cs`, `GameStats.cs` | Hard-capped pools, springs/cone sampling, cached number strings, run stats + rank. |
| `Input/InputRouter.cs` | Input System polling into an `InputState`; swappable source (keyboard/mouse or test bot). |
| `Procedural/*` | Tileable noise, texture painter (albedo + normal maps), surface/effect/UI textures, metric-UV meshes, URP material factory, asset cache. |
| `Environment/*` | Arena builder (walls, floor, ceiling, beams, platform, ramps, 12 cover objects, 6 gates, 8 lamps), flicker lamps, gate pulse, lighting + post FX volume, runtime `NavMeshSurface` bake. |
| `Player/*` | `PlayerRig` (CharacterController rig + camera), `PlayerMotor` (walk/sprint/stamina/crouch/jump/knockback/footsteps), `PlayerCamera` (look, ADS FOV, recoil, kick, flinch, bob, shake), `PlayerHealth`. |
| `Weapons/*` | Weapon data + rifle recoil pattern, `WeaponController` (fire/reload/switch/ADS/bloom), `Hitscan` resolver, primitive viewmodels, procedural viewmodel animator. |
| `VFX/*` | Shared particle emitters (impacts per surface, death/spawn/slam bursts), ring-buffer decals, pooled tracers, pooled physics shell casings, boss telegraph ring. |
| `Audio/*` | DSP toolkit, synthesized weapon/creature/world sounds (generated in parallel at startup), pooled 2D/3D `AudioManager`. |
| `Enemies/*` | `EnemyBase` (damage pipeline, death, stuck fix), primitive bodies + hitboxes, procedural animator, hit flash/fade visuals, FSM (`AI/`), Runner / Shooter (+`CoverFinder`) / Tank / Warden boss, projectile pool, enemy manager (pools, 25-alive cap). |
| `Waves/WaveManager.cs` | Wave composition, group spawning through gates, breaks, boss wave, endless mode. |
| `Pickups/PickupManager.cs` | Health/ammo drops (pooled), bob/spin, lifetime + blink, walk-over collection. |
| `UI/*` | HUD, dynamic crosshair + hit marker, damage direction arcs, boss bar, floating enemy health bars, menus, F1 overlay; `UiVisibility.cs` = the single F8 hide-UI manager (UI roots register with it once). |
| `DebugTools/*` | F2 AI debug view, dev keys (F1–F4, F12), screenshot helper, `AutoTest` command-line test bot. |
| `Editor/*` | Project setup (scene, shader keepers), player build, batch-mode play-mode test runner, Play-mode start scene. |

Enemy states are real classes (`EnemyStateBase` → `IdleState`, `RunnerChaseState`, `RunnerAttackState`,
`ShooterTakeCoverState`, `ShooterPeekState`, `ShooterAttackState`, `ShooterChaseState`, `TankChaseState`,
`TankChargeState`, `StunnedState`, `BossIntroState` (Idle), `BossChaseState`, `BossAttackState`, `BossSlamState` (Charge),
`DeadState`) over the shared enum `Idle, Chase, Attack, TakeCover, Peek, Charge, Stunned, Dead`.

## How the main systems work
* **Arena (S-01..S-04)**: walls/floor/platform use procedural meshes with metric UVs so the generated concrete
  (panel grooves + formwork tie holes + stains, with a derived normal map) keeps a constant 4 × 3 m panel size.
  Ramps are 4 m long for a 1.5 m rise (20.6°). `NavMeshSurface` (Humanoid agent: radius 0.5, height 1.8) is baked at
  startup from physics colliders inside a volume below the ceiling; a second surface for a "Boss" agent type
  (radius 1.1) keeps the ×2.2 Warden out of walls and cover.
* **Player (S-05..S-07)**: CharacterController with acceleration, stamina rules (drain only while actually
  sprinting, regen after 1 s; after exhaustion Shift must be re-pressed), crouch with head-room check,
  jump velocity `sqrt(2·g·1.0)`. Damage → red vignette pulse, spring camera flinch, HUD arcs aimed at the attacker.
* **Weapons (S-08..S-13)**: hitscan rays through the screen centre (`Physics.SyncTransforms` first so moving
  enemies are hit exactly where they are drawn). Rifle: deterministic 10-shot climb then horizontal wobble,
  bloom 0.8° → 4° (+0.4°/shot) recovering in 0.5 s. Shotgun: 8 pellets uniform in a 6° cone, linear falloff to
  30 % at 20 m, pump 0.9 s, shell-by-shell reload interruptible by firing. The viewmodel is rendered by the main
  camera but scaled ×0.1 toward the eye, so it never clips through walls and gets the same lighting/post FX.
* **Enemies (S-14..S-18)**: see *Decisions*. Hitboxes (head sphere, torso box, limb capsules, tank weak point)
  live on unscaled joints of a kinematic compound body; the hit flash, charge glow and death fade use
  MaterialPropertyBlocks (cleared afterwards so SRP batching resumes).
* **Waves (S-19..S-24)**: queue per wave, groups of 2–4 every 1.5 s through a gate ≥ 20 m away and preferably
  outside the player's view (frustum + line-of-sight test), gate pulses 1 s before; break of 8 s with
  "WAVE N INCOMING" countdown and +25 rifle / +6 shells.
* **Performance (S-28)**: pools for projectiles, tracers, decals, shells, pickups, health bars, damage indicators,
  enemies and audio voices; particles are emitted into 4 shared pre-allocated systems (`ParticleSystem.Emit`);
  no LINQ/coroutines/per-frame strings in gameplay code; NonAlloc queries and cached `NavMeshPath`.

## Decisions (ambiguities resolved)
* **Spread values are cone half-angles** (max deviation from the aim line), including the shotgun's "6° cone".
* **Pistol ADS spread is 0.2°** as S-08 states explicitly; S-11's "×0.5" is applied to the rifle (0.8 → 0.4°, bloom
  scaled too) and not to the shotgun.
* **Shotgun reserve maximum = 36** (the spec gives no separate maximum, so the starting reserve is the cap).
* **Drops roll independently**: health 30 % (tanks 100 %) and ammo 40 % may both drop. A health pack is not picked
  up at full health, an ammo crate not when both reserves are full (they stay until they expire).
* **Tank armour**: angle between the tank's forward and the direction the shot came from. ≤ 70° → ×0.4;
  rear arc (≥ 135°) **and** the glowing weak-point collider hit → ×2.0; everything else ×1.0. Headshot ×2 and stun ×1.5
  multiply on top. There is no charge cooldown beyond the 1.5 s stun (the spec defines none).
* **Tank charge** follows a straight line clipped by `NavMesh.Raycast` and is executed with `NavMeshAgent.Move`,
  so it can never leave the NavMesh; the charge ends on hit, after 2 s, at the end of the line or when blocked.
* **Shooter**: "not visible from the player's eyes" is tested with a line cast from the player's eye to the
  candidate at 1.4 m (standing head height). Candidates = points hugging the 12 cover objects + random NavMesh
  samples, always ≥ 12 (16). First arrival → short settle → peek; after each peek it returns, waits 1.5–3 s and runs a
  fresh search ("re-evaluate"). Retreat (< 6 m) uses the same search with far-from-player scoring.
  If no hidden point exists it fights from where it stands (`Attack`) and keeps searching.
* **Runner flanking**: with at least one other runner within 5 m, each runner targets a point 5.5 m from the player
  rotated by its flank slot (−70°, +70°, 0°, ±38°, assigned round-robin), re-evaluated as it moves, so a pack fans
  out around the player; plus distance-weighted separation steering. Within 6.5 m they go straight in.
* **Boss**: phase 2 *adds* the jump-slam to phase 1 behaviour; phase 3 replaces bursts by the 5-way volley
  (10 damage, 35 m/s, 10° apart) and runner summons by shooter summons, slams continue. The floor ring appears
  exactly 1 s before the landing. Summons count toward the 25-alive cap. Boss head ×1.5 replaces the ×2 headshot.
  On boss death the remaining summons collapse (no score) and the victory screen follows after 2.5 s.
* **Stuck rule (S-18)**: displacement < 0.5 m within a 3 s window while the agent has a path → path recomputed and
  agent snapped to the nearest NavMesh point; a second consecutive strike teleports it 2.5 m along its route.
  Independently, every 0.5 s an enemy off the NavMesh or outside the walls is warped back.
* **First wave** starts after a 5 s countdown (the 8 s break applies between waves).
* **Accuracy** counts trigger pulls (a shotgun blast that hits counts once); **headshots** = shots that hit a head.
* **Rank thresholds** (all conditions of a row): S = accuracy ≥ 45 %, damage taken ≤ 250, time ≤ 11 min;
  A = ≥ 33 %, ≤ 500, ≤ 15 min; B = ≥ 20 %, ≤ 900; otherwise C.
* **Score**: runner 100, shooter 150, tank 400, Warden 5000, +50 headshot kill, +250 × N per cleared wave.
* **Enclosed hall**: the arena has a ceiling at 6 m (with beams the lamps hang from), so the only light sources are
  the 8 lamps (2 flickering), the gate lights and emissive surfaces; ambient is a dim trilight.

## Test tooling (not part of gameplay)
* `Tools/fastcompile.py` — compiles all scripts with Unity's bundled Roslyn against the project references in seconds.
* `Tools/run_guarded.ps1` — launches Unity/the player with the GPU/RAM launch gate, 15 s monitoring, hard stops,
  timeouts and PID-only termination (MASTER_PROMPT section 5). `Tools/run_player_test.ps1` wraps a scenario run.
* `AutoTest` (only with the command line `-autotest <scenario> [-testDuration s]`): a bot plays through the real
  input path and logs `[TEST]` lines. Scenarios: `smoke`, `wave1` (runner spread/flank metrics), `wave2` (cover
  hidden/in-band checks, peeks, bursts, miss ratio, retreat), `wave3` (tank armour multipliers front/side/rear,
  telegraph→charge timing), `boss` (phase thresholds, summons, slams, victory), `wave4` (stuck check with F2 on),
  `perf` (25 enemies, FPS/frame time/GC), `gallery` (screenshots), `death` (death screen), `uitoggle` (F8 hide-UI check,
  `DebugTools/UiToggleCheck.cs`), `weapons` (sprint/stamina,
  crouch, jump, ADS FOV, all three weapons, NavMesh path onto the platform).
  Player: `powershell -File Tools/run_player_test.ps1 -Scenario wave2`; editor: run Unity with
  `-executeMethod WaveSurvival.EditorTools.BuildTools.PlayModeTest -autotest smoke -testDuration 70` (GUI editor).

## Known limitations
* Enemy deaths are an animated topple (allowed by S-17), not a physics ragdoll; the fade swaps to transparent twins
  of the enemy materials.
* Enemies are blocky primitive humanoids and the first-person "hands" are simple boxes.
* In dense crowds around the player, runners that are blocked by other runners trigger the S-18 fix (path
  recompute, then a 1.5 m hop) — it keeps them moving, but the hop can be visible.
* Shooters treat the 12–18 m band as a strong preference, not a hard rule (hidden-from-player is the hard rule):
  3 of 4 chosen covers in the wave-2 test were inside the band. With no hidden point at all they fight in the open
  (`Attack`) and keep searching.
* F2 labels overlap when many enemies are bunched together.
* GC: the Unity Profiler was not attached (verification ran on a release player). Measured instead: 1 garbage
  collection and ~13 KB/s managed allocation (including the test harness's own logging) during 30 s with 25 enemies,
  worst frame 33 ms (single frame), p99 5.56 ms.
* `BuildTools.PlayModeTest` works with the editor GUI; in `-batchmode -nographics` the editor does not tick Play Mode.
* Endless mode was code-reviewed and its button wired, but not played through end-to-end in the automated runs.
* Some `.cs` files have CRLF and others LF line endings (cosmetic).

## Performance on this PC
RTX 5070 12 GB, Ryzen 7 5700X3D, 64 GB RAM, shared with other agents; all runs windowed 1920×1080, VSync on
(the monitor refreshes at 180 Hz, so 180 FPS is the cap).

| Measurement | Value |
|---|---|
| 25 enemies alive (wave 6 composition), 30 s window | **178.8 FPS average**, 5.59 ms average frame, p99 5.56 ms, worst frame 33 ms |
| Normal waves 1–5 (status lines every 5 s) | 180 FPS (cap), short dips to 135–145 FPS on big spawn bursts |
| Peak VRAM (system-wide, nvidia-smi, includes other processes ≈ 1.4–2.3 GB before launch) | 2.6 GB during player runs, 3.3 GB with the editor GUI in Play Mode |
| Peak GPU temperature | 67 °C (boss run) |
| Free RAM during runs | never below 38 GB |
| Player build | ~108 MB, build time 30–140 s (shader variants cached after the first build) |

Rendering budget notes: 8 shadowed point lights × 6 faces at 512 px fit the 4096 additional-light atlas exactly
(no URP rescaling), unused URP features (light layers, cookies, mixed lighting, terrain holes, reflection-probe
blending) are switched off in `PC_RPAsset`, GPU Resident Drawer is off (no benefit for ~1 k dynamic renderers).
