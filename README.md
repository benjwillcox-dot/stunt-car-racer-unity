# Stunt Car Racer – Unity remake (prototype)

A fan remake of Geoff Crammond's 1989 *Stunt Car Racer* in **Unity 6.3 (URP)**, built
entirely in the cloud. No local Unity install or GPU is needed: every push to `main` builds
a WebGL version with GitHub Actions ([GameCI](https://game.ci)) and publishes it to GitHub Pages.

## What's in it

- **12 road courses**, laid out from their corners (`TrackCourse.cs`) with bends both ways,
  chicanes and hairpin spurs, some banked into the turn and some off camber. The 8 league tracks
  (0.95–1.2 km), paired by division: Little Ramp and Hump Back (Div 4), Stepping Stones and Big
  Ramp (Div 3), Ski Jump and Draw Bridge (Div 2), High Jump and Roller Coaster (Div 1). Plus 4
  long courses (1.6–1.8 km, single race and practice): Camel Back, Big Dipper (a figure of eight
  with steep 25° hills), Leap Frog and Swing Bridge.
- Gaps to jump, rows of **kickers** to pace (up to about 30 m/s you land in the flat; faster and
  you hit the next ramp as it rises), and a moving **drawbridge** on Draw Bridge and Swing
  Bridge: down you drive over, part way up it's a ramp, high up it throws you a long way. Its
  lamps show red while it's moving or up.
- **Modelled stunt car** (Blender, ~15k triangles): supercharged V8, zoomie side pipes, roll cage
  and driver, in a yellow livery for you and a white/teal one for the rival
- Raycast independent suspension with front and rear **anti-roll bars**; tyres whose sideways grip
  follows the slip angle (peaking at 7° then easing off as they slide) and grows less than in
  proportion to load; rear-wheel drive, so too much throttle in a bend steps the tail out; and
  limited **boost**
- **Chassis damage** from hard landings and crashes; at 100% you're wrecked
- **Crane recovery** when you fall off or end up on your roof: a lifting platform carries the car
  back to the road (the time it takes is the penalty)
- A stadium built round each track: tall grandstands packed with fans close to the deck, with
  roofs, sponsors' boards (all made-up brands), a start/finish gantry and floodlight masts
- An infield with the pit paddock (garages, transporters, tents), a fan zone with food stalls and
  a big screen showing a live TV feed of your car, tyre walls, hay bales under the jumps, marshal
  huts, mown stripes and the game's name painted on the grass
- **Night races** lit by the four corner floodlights, each casting its own shadow, with backlit
  boards and headlights. Pick Day or Night for single races and practice; every league season
  ends with a night race.
- **League**: 4 divisions of 3 drivers, 4-race seasons, 2 points a win and 1 for the fastest lap,
  promotion and relegation; damage carries between races in a season. Saved in the browser.
- Single race v CPU and practice modes
- Sloped red-and-white **kerbs** you can ride, with ribs, grit and tyre rubber on them and a
  painted edge line inside them
- Keyboard, **gamepad** (Input System) and joystick-style steering wheels. Pads rumble on the
  kerbs, clunk when you hit the other car and buzz gently while the tyres slide; in the WebGL
  build that goes through the browser's Gamepad API (Chrome and Edge).
- Tyre smoke, scrape sparks and landing dust
- Chase camera and over-the-bonnet cockpit view
- Synthesised V8 engine sound
- URP lighting: soft shadows, procedural sky, fog, bloom, ACES tonemapping

## Browser preview

`preview/index.html` is a Three.js port of the same game (same track points, physics numbers,
AI and league rules) that runs in any browser without a Unity build. Use it to try handling
changes quickly; tuning values carry straight back to `Assets/Scripts`. Its track list is
generated from `TrackLibrary.cs`'s output, so the layouts match; its drawbridges stay down, and
it has none of the stadium, night or infield dressing.

## Controls

| Keyboard | Gamepad | Action |
| --- | --- | --- |
| Arrows / WASD | Left stick, triggers | Steer, accelerate, brake/reverse |
| Space / Left Shift | A | Boost |
| C | Y | Toggle chase / cockpit camera |
| N | X | Day / night (menu, single race and practice) |
| R | B | Call the crane |
| Esc | Start | Menu (retire, in a league race) |

## How the build works

There is no hand-made scene. `Assets/Editor/BuildScript.cs` creates everything at build time:
the URP pipeline asset, materials, post-processing profile, the scene and the player
settings. Then it builds WebGL. The game itself (track, cars, scenery) is generated at runtime.

You can also open the project in the Unity Editor (6000.3.27f1) and run
**Stunt Car → Generate Project Assets**, then press Play on `Assets/Scenes/Main.unity`.
Run it again whenever `BuildScript` changes, since it also sets the URP options; the night
floodlights' shadows, for example, need its additional-light settings.

### The car model

`Assets/Models/StuntCar/StuntCar.fbx` is one model shared by both cars. Every face's UVs point
at a flat-colour cell of a 256×256 palette, so a livery is just three textures in
`Textures/` (albedo, metallic R + smoothness A, emission). `CarModelImport` sets their import
options, `BuildScript` makes the `CarYellow` / `CarWhite` materials, and `CarFactory` reads
the wheel positions and radii straight off the model's `Wheel_*` objects. To add a livery,
copy and repaint the three PNGs and add a material for them. If the model is missing, cars fall back to
the primitive stand-in. The Blender source and its build scripts are in `Art/StuntCar`;
running its `rebuild_all.py` re-exports straight into `Assets/Models/StuntCar`.

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
2. A moving drawbridge in the browser preview
3. Steering-wheel pedal mapping and a controls screen
4. Real scenery models, recorded sound effects (the car model is in)
5. Two-player split screen

*Stunt Car Racer* is © its respective rights holders. This is a non-commercial fan project.
All code and assets here are original.
