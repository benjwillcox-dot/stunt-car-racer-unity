# Stunt Car Racer – Unity remake (prototype)

A fan remake of Geoff Crammond's 1989 *Stunt Car Racer* in **Unity 6.3 (URP)**, built
entirely in the cloud. No local Unity install or GPU is needed: every push to `main` builds
a WebGL version with GitHub Actions ([GameCI](https://game.ci)) and publishes it to GitHub Pages.

## What's in v0.1

- Raised circuits on scaffolding, built from spline control points (`Assets/Scripts/TrackLibrary.cs`)
  - Little Ramp, Hump Back (Division 4), Stepping Stones, Big Ramp (Division 3)
- Raycast-suspension car with a tyre grip model, rear-wheel drive, limited **boost**
- **Chassis damage** from hard landings and crashes – at 100% you're wrecked
- **Crane recovery** when you fall off or end up on your roof (the time it takes is the penalty)
- 3-lap head-to-head race against an AI rival, or practice mode
- Original scoring: 2 points for the win, 1 for the fastest lap
- Chase camera and over-the-bonnet cockpit view
- Synthesised V8 engine sound
- URP lighting: soft shadows, procedural sky, fog, bloom, ACES tonemapping

## Controls

| Key | Action |
| --- | --- |
| Arrows / WASD | Steer, accelerate, brake/reverse |
| Space / Left Shift | Boost |
| C | Toggle chase / cockpit camera |
| R | Call the crane |
| Esc | Back to menu |

## How the build works

There is no hand-made scene. `Assets/Editor/BuildScript.cs` creates everything at build time:
the URP pipeline asset, materials, post-processing profile, the scene and the player
settings. Then it builds WebGL. The game itself (track, cars, scenery) is generated at runtime.

You can also open the project in the Unity Editor (6000.3.27f1) and run
**Stunt Car → Generate Project Assets**, then press Play on `Assets/Scenes/Main.unity`.

## One-off setup

1. **Unity licence** (free Personal). You need a `.ulf` licence file once:
   install Unity Hub on any computer (no GPU needed), sign in, then go to
   *Preferences → Licenses → Add → Get a free personal licence*. The file is saved at
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
2. In this repo, open **Settings → Secrets and variables → Actions** and add:
   - `UNITY_LICENSE` – the whole contents of the `.ulf` file
   - `UNITY_EMAIL` – your Unity account email
   - `UNITY_PASSWORD` – your Unity account password
3. Open **Settings → Pages** and set **Source** to **GitHub Actions**.
   (Pages needs a public repo on a free GitHub plan. If the repo is private, every
   build is still saved as a downloadable zip under the run's *Artifacts* section.)
4. Push to `main`, or go to **Actions → Build and deploy WebGL → Run workflow**.
   The first build takes about 20–30 minutes. Later builds are faster because the `Library` folder is cached.

## Roadmap

1. Tune the handling against the original's feel
2. Remaining tracks: High Jump, Roller Coaster, Ski Jump, Draw Bridge (needs gaps in the deck)
3. Full league: 4 divisions × 3 drivers, promotion and relegation, damage carried between races
4. Real car and scenery models, sound effects, gamepad support via the Input System
5. Two-player split screen

*Stunt Car Racer* is © its respective rights holders. This is a non-commercial fan project.
All code and assets here are original.
