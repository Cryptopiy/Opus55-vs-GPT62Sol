# Claude Opus 5.5 vs GPT 6.2 Sol: 5 games built by AI

**English** | [Русский](README.ru.md)

Both models got the **same five game briefs** and built the games from scratch, on their own:

- **Claude Opus 5.5** in Claude Code, thinking level **Max**
- **GPT 6.2 Sol** in Codex, thinking level **Extra High**

This repository holds every finished game from the video, ready to play, plus the full source code. Opus 5.5 is in the `Opus 5.5` folder, GPT 6.2 Sol is in the `GPT 6.2 Sol` folder.

> The prompts and my step-by-step guide on how I make games with AI are not in this repo. They are in my Telegram channel (link in the video description).

---

## How to run a game (one file)

Each game folder contains one zip file with the finished Windows build.

1. Open the game folder, for example `Opus 5.5/3. Wave Survival/`.
2. Click the `.zip` file and download it (the **Download raw file** button).
3. Unzip it anywhere.
4. Double-click the one `.exe` file inside. The game starts.

That's all. Nothing to install, no engine needed.

> You do not have to download the whole repository (about 400 MB). Download only the games you want, one zip at a time. Cloning the repository or `Code → Download ZIP` also works if you want everything.

### Which file to start

| # | Game | Engine | Opus 5.5: zip → exe | GPT 6.2 Sol: zip → exe |
|---|------|--------|---------------------|------------------------|
| 1 | Destruction Sandbox | Unity | `DestructionSandbox.zip` → `DestructionSandbox.exe` | `DestructionRange.zip` → `DestructionRange.exe` |
| 2 | Drift Racer | Godot | `DriftRacer.zip` → `DriftRacer.exe` | `DriftRacer.zip` → `DriftRacer.exe` |
| 3 | Wave Survival (shooter) | Unity | `WaveSurvival.zip` → `WaveSurvival.exe` | `Bastion.zip` → `Bastion.exe` |
| 4 | Living Island | Unity + Blender | `LivingIsland.zip` → `LivingIsland.exe` | `IslandValidation.zip` → `IslandValidation.exe` |
| 5 | Physics Playground (puzzles) | Godot | `ContraptionLab.zip` → `ContraptionLab.exe` | `ContraptionLab.zip` → `ContraptionLab.exe` |

The two Godot games are a single `.exe`. The Unity games are an `.exe` that must stay next to the folders and `.dll` files from the same zip, so **unzip the whole archive** and start the game from there (do not drag the `.exe` out).

### Requirements

- Windows 10 or 11, 64-bit
- A modern graphics card (DirectX 12 recommended)

All games were built and tested on one PC (RTX 5070 12 GB, Ryzen 7 5700X3D, 64 GB RAM). I have not tested them on other hardware, so on weaker PCs the heavier games may run slowly.

### Windows warnings

The games are not code-signed, so Windows may show **"Windows protected your PC"** (SmartScreen) or an antivirus may complain. This is normal for unsigned programs. Click **More info → Run anyway**. If you want to check that the files are exactly the ones I published, compare them with the SHA-256 hashes in [`CHECKSUMS.txt`](CHECKSUMS.txt):

```powershell
Get-FileHash .\DriftRacer.zip -Algorithm SHA256
```

---

## Controls

Keys that work in every game:

| Key | Action |
|-----|--------|
| `Esc` | Pause menu |
| `F1` | Performance overlay (FPS, draw calls, physics bodies) |
| `F8` | Hide or show **all** game UI (handy for clean footage) |
| `F12` | Save a PNG screenshot |

### 1. Destruction Sandbox (both models)

| Input | Action |
|-------|--------|
| `W` `A` `S` `D`, mouse | Move and look |
| `Shift` / `Space` | Sprint / jump |
| Left mouse button | Fire (hold to keep firing) |
| `1` `2` `3` or mouse wheel | Cannonball / explosive shell / gravity orb |
| `F5` `F6` `F7` | Build the tower / the wall with an arch / the pyramid |
| `R` | Reset the structure and refill ammo |
| `X` | Clear shards and spent cannonballs |
| `T` | Slow motion |

### 2. Drift Racer (both models)

| Key | Action |
|-----|--------|
| `W` / `S` | Throttle / brake, then reverse |
| `A` `D` | Steer |
| `Space` | Handbrake |
| `R` | Respawn at the last checkpoint |
| `C` | Switch camera (chase / hood) |
| `F3` | Counter-steer assist on/off |

### 3. Wave Survival (both models)

| Input | Action |
|-------|--------|
| `W` `A` `S` `D`, mouse | Move and look |
| `Shift` / `C` / `Space` | Sprint / crouch / jump |
| Left / right mouse button | Fire / aim |
| `R` | Reload |
| `1` `2` `3` or mouse wheel | Pistol / rifle / shotgun |
| `F3` | Cheat: jump straight to wave 5 (the boss) |
| `F4` (Opus) or `Shift`+`F3` (GPT) | Cheat: invulnerability on/off |

The GPT build is a *development build*, so you will see a small "Development Build" label in the corner.

### 4. Living Island (both models)

| Input | Action |
|-------|--------|
| `W` `A` `S` `D`, mouse | Walk and orbit the camera (walk into the water to swim) |
| `Shift` / `Space` | Run / jump |
| `1` `2` `3` `4` `5` | Weather: clear / cloudy / rain / fog / storm |
| `0` | Automatic weather on/off |
| `T` | Pause the day cycle |
| `Tab` | Settings: time of day slider, island seed |
| `P` | Photo mode |

### 5. Physics Playground (both models)

Pick a level, place parts so the ball reaches the bowl, then press `Space`.

| Input | Action |
|-------|--------|
| `1`–`9` or click a tile | Pick a part |
| Left mouse button | Place a part / select a placed part (drag to move) |
| `Q` / `E` | Rotate by 15° |
| `Delete` | Remove the selected part |
| `Ctrl`+`Z` / `Ctrl`+`Y` | Undo / redo |
| `Space` | Start or pause the simulation |
| `R` | Reset to build mode |
| Middle mouse drag / wheel / `F` | Orbit / zoom / re-frame the board |
| `F9` | Load the reference solution, then press `Space` to watch it |

---

## Results from the video

| Game | Opus 5.5 (time / cost) | GPT 6.2 Sol (time / cost) |
|------|------------------------|---------------------------|
| 1. Destruction Sandbox | 2 h 04 min / $44.62 | 41 min / $2.55 |
| 2. Drift Racer | 1 h 51 min / $34.25 | 45 min / $3.37 |
| 3. Wave Survival | 1 h 55 min / $35.56 | 53 min / $3.23 |
| 4. Living Island | 1 h 49 min / $59.06 | 1 h 00 min / $5.22 |
| 5. Physics Playground | 2 h 41 min / $42.38 | 42 min / $2.97 |
| **Total** | **10 h 20 min / $215.87** | **4 h 01 min / $17.34** |

Opus costs were calculated from the Claude Code session logs at API prices. GPT time is active working time (without waiting for a usage limit) and its prices come from third-party sources. In my opinion Opus 5.5 won four games on visuals and GPT 6.2 Sol won the shooter, but judge for yourself, the games are right here.

---

## What is in each folder

```
Opus 5.5/  and  GPT 6.2 Sol/
└── 1. Destruction Sandbox/ … 5. Physics Playground/
    ├── <Game>.zip     the playable Windows build (start here)
    ├── Source/        the project source (open it in the engine)
    └── README.md      the AI agent's own technical notes
```

The `README.md` inside each game folder was written by the AI agent while it was building the game. Paths in it (`Builds/`, `tools/` …) refer to the original working folder and do not exist in this repository.

### Open the source (optional)

- **Unity games** (1, 3, 4): install Unity **6000.6.0f1**, then in Unity Hub choose *Add project from disk* and select the `Source` folder. The first import takes a few minutes.
- **Godot games** (2, 5): install Godot **4.7.2**, then import `Source/project.godot`.
- Living Island also contains the Blender source files and the Python scripts the models used to build the lighthouse, the boat, the palms and the rocks.

---

## Disclaimer

Everything here was written by AI models to compare them. The games may contain bugs. They are provided as they are, without any warranty. Play at your own risk.
