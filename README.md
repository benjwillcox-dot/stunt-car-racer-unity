# Stunt Car Racer – Unity remake (prototype)

A fan remake of Geoff Crammond's 1989 *Stunt Car Racer* in **Unity 6.3 (URP)**, built
entirely in the cloud. No local Unity install or GPU is needed: every push to `main` builds
a WebGL version with GitHub Actions ([GameCI](https://game.ci)) and publishes it to GitHub Pages.

## What's in it

- **All 8 tracks**, paired by division: Little Ramp and Hump Back (Div 4), Stepping Stones and
  Big Ramp (Div 3), Ski Jump and Draw Bridge (Div 2), High Jump and Roller Coaster (Div 1).
  Draw Bridge, Ski Jump and High Jump have gaps you have to jump; Roller Coaster is a figure
  of eight that crosses over itself.
- Raycast-suspension car with a tyre grip model, rear-wheel drive and limited **boost**
- **Chassis damage** from hard landings and crashes; at 100% you're wrecked
- **Crane recovery** when you fall off or end up on your roof (the time it takes is the penalty)
- **League**: 4 divisions of 3 drivers, 4-race seasons, 2 points a win and 1 for the fastest lap,
  promotion and relegation; damage carries between races in a season. Saved in the browser.
- Single race v CPU and practice modes
- Keyboard, **gamepad** (Input System) and joystick-style steering wheels
- Tyre smoke, scrape sparks and landing dust
- Chase camera and over-the-bonnet cockpit view
- Synthesised V8 engine sound
- URP lighting: soft shadows, procedural sky, fog, bloom, ACES tonemapping

## Browser preview

`preview/index.html` is a Three.js port of the same game (same track points, physics numbers,
AI and league rules) that runs in any browser without a Unity build. Use it to try handling
changes quickly; tuning values carry straight back to `Assets/Scripts`.

## Controls

| Keyboard | Gamepad | Action |
| --- | --- | --- |
| Arrows / WASD | Left stick, triggers | Steer, accelerate, brake/reverse |
| Space / Left Shift | A | Boost |
| C | Y | Toggle chase / cockpit camera |
| R | B | Call the crane |
| Esc | Start | Menu (retire, in a league race) |

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
2. Moving drawbridge on Draw Bridge
3. Steering-wheel pedal mapping and a controls screen
4. Real car and scenery models, recorded sound effects
5. Two-player split screen

*Stunt Car Racer* is © its respective rights holders. This is a non-commercial fan project.
All code and assets here are original.
