# Living Island

Living Island is a third-person coastal landscape for Unity 6000.6.0f1 and URP 17.6. Open the existing **Living Island** project, then press **Play** in `Assets/Scenes/SampleScene.unity`. The scene builds itself and starts at noon in Clear weather on seed 777, facing the bay and lighthouse. Click the Game view to give it keyboard and mouse focus.

## Controls

| Input | Action |
| --- | --- |
| WASD | Walk at 3 m/s or swim at 2.5 m/s |
| Shift | Run at 6 m/s on land |
| Space | Jump |
| Mouse | Orbit the character. Tilt up while swimming to put the camera underwater. |
| 1 / 2 / 3 / 4 / 5 | Clear / Cloudy / Rain / Fog / Storm |
| 0 | Toggle automatic weather |
| T | Pause or resume the clock |
| Tab | Open settings. Drag the time slider to hold a specific hour. Enter an integer seed and choose Regenerate to rebuild the island. |
| P | Enter or leave photo mode. Photo mode freezes the explorer and hides the HUD. |
| WASD / Q / E in photo mode | Fly forward, sideways, down or up. Hold Shift to move faster. |
| Tab in photo mode | Adjust field of view, focus distance, aperture, exposure and time |
| F1 | Toggle FPS, frame time, draw count when available, physics and particle counts, and submitted grass instances |
| F8 | Toggle gameplay UI for recording. Starts visible, keeps the pause menu accessible, and remembers the choice across level restarts. |
| F12 | Save a timestamped PNG in `Living Island/Screenshots/`. Photo mode renders a 3840 × 2160 image. |
| Esc | Pause, then select Resume, Restart or Quit. Quit stops Play Mode in the editor. |

## Project structure

| File | Responsibility |
| --- | --- |
| `IslandConfig.cs` | World sizes, seed, wave parameters, counts, distances, rates and weather profiles |
| `IslandGame.cs` | Scene startup, settings, input routing, pause and seeded regeneration |
| `IslandTerrain.cs` | Single sampled heightfield, matching collision interpolation and landmark sites |
| `IslandOcean.cs`, `Water.shader` | Camera-centered grid, Gerstner waves, scene-depth shading, refraction and foam |
| `IslandSkyWeather.cs`, `Sky.shader` | Sun and moon, sky palette, clouds, stars, atmosphere and lightning |
| `IslandVegetation.cs`, `Grass.shader`, `Props.shader` | Seeded placement, instanced palms and grass, LOD, wind and player interaction |
| `IslandLandmarks.cs`, `Beacon.shader`, `IslandEffects.cs` | Campfire, lighthouse, boat, rain, ground and swim spray, and fireflies |
| `IslandPlayer.cs` | CharacterController, swimming and animations, orbit collision and free-fly camera |
| `IslandAudio.cs` | Runtime sound synthesis and mixing |
| `IslandPost.cs`, `IslandPhotography.cs`, `IslandHud.cs` | URP Volume, PNG capture and interface |
| `IslandGlbImporter.cs`, `IslandModels.cs`, `IslandModels.asset` | GLB import, URP material assignment and references for Play Mode |
| `IslandVerification.cs`, `IslandControlVerification.cs`, `IslandEditor.cs`, `Run-Island.ps1` | Headless checks, keyboard exercises, acceptance playback and guarded graphics runs |

C# files are in `Living Island/Assets/Island/` or its `Editor/` subfolder. Shaders and runtime references are under `Resources/`.

## Design decisions

- The starting view looks along the sheltered bay. A light-painted lighthouse sits on the bay’s eastern headland. The main steep scarp runs along the northern coast.
- High, Medium and Low share the same seeded vegetation. Each preset selects a stable subset of 80,000, 50,000 or 20,000 blades before distance and camera culling. The presets also change shadow range, render scale, water detail and palm LOD.
- The water samples the procedural sky for reflections. The fog uses height-dependent extinction and switches to blue-green underwater. The beach and seabed share one mesh, so there is no duplicated geometry at sea level.
- The day clock can stop independently of wind, waves and weather. The pause menu stops both the clock and simulation. Manual weather selection turns automatic changes off.
- The island, materials, vegetation textures, star map, effects and sounds were generated in this project. The Blender props were authored in the supplied scene through the live MCP connection.

## Blender exports

The source is `Living Island/Living IslandGPT6.2Sol.blend`. The `Living Island assets` collection contains the assembled models. `Model-Island.py` preserves the modeling and export procedure. It runs through Blender MCP after verifying the open file path. It authors meshes at their export origin at meter scale, welds coincident vertices, preserves loop UVs, and exports the selected models with normals, UVs and materials to `Assets/Models/` as GLB 2.0. The Blender file is saved at its original path. Unity’s importer combines material batches for instancing and keeps the lantern optic and windows as separate emissive surfaces.

| Export | Triangles |
| --- | --- |
| SM_Lighthouse | 2,118 |
| SM_Rowboat | 1,120 |
| SM_Rock_01 through SM_Rock_05 | 80 each |
| SM_Palm_01 / 02 / 03 | 748 / 792 / 836 at LOD0, 256 / 276 / 296 at LOD1 |
| SM_Driftwood | 150 for the seat and 220 for the campfire log assembly |

## Validation

Use `& .\Run-Island.ps1 -Mode Core` for the headless checks. `-Mode Controls` and `-Mode Acceptance` exercise the keyboard and acceptance route. `-Mode Day` and `-Mode Storm` select the 1440p benchmarks, while `-Mode Soak` runs a timed play session with moving camera and weather changes. Run one command at a time. The runner writes logs and hardware telemetry under `Living Island/Logs/`. `-Mode Build` creates the optional Windows validation player on demand.

The acceptance logs record a 30.02 m swim, 200 live fireflies, 5,121 streaks in Rain, a lightning flash and delayed thunder, 60.00 m of fog visibility at sea level, and seed 778 regeneration in 0.559 s.

See [CHECKLIST.md](CHECKLIST.md) for test results by requirement.

## Captures

- [Noon on the beach](<Living Island/Screenshots/Island-20261123-222318-768-Beach.png>)
- [Underwater caustics](<Living Island/Screenshots/Island-20261123-222338-088-Underwater.png>)
- [Campfire and lighthouse at night](<Living Island/Screenshots/Island-20261123-222342-548-Night.png>)
- [Lightning over the bay](<Living Island/Screenshots/Island-20261123-222404-760-Lightning.png>)
- [4K photo](<Living Island/Screenshots/Island-20261123-222415-299-4K-Photo.png>)
- [F1 diagnostics](<Living Island/Screenshots/Island-20261123-222822-240.png>)

## Known limitations

- The ocean reflects the procedural sky and clouds. Shore geometry appears through refraction.
- The grass counter reports submitted instances. The shader performs the final 80 m cutoff on individual blades.
- The importer is scoped to the static, indexed GLB format produced by the included Blender script.

## Performance on this PC

Unity 6000.6.0f1 Game view on the RTX 5070 and Ryzen 7 5700X3D. Both samples ran windowed at 2560 × 1440 on High, with VSync enabled on the 180 Hz display. Each sample covered 25 seconds after a 10-second warm-up.

| Scenario | Mean FPS | Longest frame | Frames over 50 ms |
| --- | ---: | ---: | ---: |
| Clear at noon | 179.97 | 14.05 ms | 0 |
| Storm at night | 180.01 | 7.53 ms | 0 |

The benchmarks peaked at 2,479 MiB of shared GPU memory and 48 °C. Across the full set of monitored development runs, including the 4K captures, the highest readings were 4,138 MiB of shared GPU memory and 58 °C. These readings include the other processes on the shared PC.

Benchmark logs are available for [Clear](<Living Island/Logs/Island-Day-2.log>) and [Storm](<Living Island/Logs/Island-Storm-2.log>).

Three windowed Play Mode stability runs totaled 5 minutes 3 seconds at 1600 × 900. They counted 53,144 camera renders and recorded zero game warnings and errors while exercising movement, camera orbit and weather transitions. Three sessions kept each process within the shared-PC test time limit. [Run 1](<Living Island/Logs/Island-Soak-3.log>), [Run 2](<Living Island/Logs/Island-Soak-4.log>), [Run 3](<Living Island/Logs/Island-Soak-5.log>).

