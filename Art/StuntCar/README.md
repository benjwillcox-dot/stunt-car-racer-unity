# Stunt Car — Blender model for the Unity remake

A modern take on the classic stunt racer: an open-wheel hot rod with a front-mounted
supercharged V8, chrome zoomie headers merging into side pipes with turned-out tips, a full roll cage, a helmeted driver, chrome push bars front and rear, a carbon
splitter, a rear wing and LED lights. It comes in two colour schemes:

| Scheme | Look |
|---|---|
| **Yellow** | racing yellow body, gloss-black twin stripes, black cage and rims with polished lips, gold visor |
| **White** | pearl white body, teal stripes, teal blower, white rims with teal lips, carbon accents |

![both cars](renders/both_cars.png)

## Files

This folder is the Blender source. It sits outside `Assets`, so Unity ignores it. The game uses
the exports in `Assets/Models/StuntCar`.

| Path | What it is |
|---|---|
| `StuntCar.blend` | both cars side by side, plus a preview camera and lights (never exported) |
| `blender_scripts/` | the procedural build; `rebuild_all.py` regenerates the car and re-exports it |
| `renders/` | preview renders |
| `Assets/Models/StuntCar/StuntCar.fbx` | the exported model, shared by both liveries |
| `Assets/Models/StuntCar/Textures/StuntCar_<Scheme>_Albedo.png` | colour palette (sRGB) |
| `Assets/Models/StuntCar/Textures/StuntCar_<Scheme>_MetallicSmoothness.png` | R = metallic, A = smoothness (linear) |
| `Assets/Models/StuntCar/Textures/StuntCar_<Scheme>_Emission.png` | headlights, tail-light bar, dash screen |

## Model specs

- **Units / axes in Unity:** 1 unit = 1 m, Y up, car faces **+Z**. Everything imports with zero rotation and scale 1.
- **Size:** about 2.05 m wide, 1.42 m tall and 4.25 m long. Wheelbase 2.60 m.
- **Root pivot:** on the ground between the axles, 1.35 m behind the front axle.
- **Triangles:** 14,880 per car.

| Object | Tris | Pivot / notes |
|---|---|---|
| `Body` | 4,454 | shell, cage, front and rear push bars, suspension, side pipes, wing, seat |
| `Engine` | 2,868 | engine centre, so you can add an idle shake |
| `Driver` | 896 | separate, so you can hide or swap it |
| `SteeringWheel` | 374 | hub centre, tilted 25° |
| `Wheel_FL` / `Wheel_FR` | 1,436 each | hub centre, radius **0.34 m**, spins on local X |
| `Wheel_RL` / `Wheel_RR` | 1,604 each | hub centre, radius **0.42 m**, spins on local X |
| `Caliper_FL/FR/RL/RR` | 52 each | same pivot as their wheel |

The right-hand wheels are mirrored rather than rotated, so the tread faces the same way on both sides.

## In the game

Nothing needs setting up by hand:

- `Assets/Editor/CarModelImport.cs` applies the import options. The textures get point filtering, no mipmaps and no compression, and the metallic map is linear. The FBX imports no materials of its own.
- **Stunt Car → Generate Project Assets** (`BuildScript`) makes the `CarYellow` material for the player and `CarWhite` for the rival.
- `CarFactory` builds each car from the model:
  - wheel positions and radii come from the `Wheel_*` objects
  - the calipers and steering wheel follow the steering
  - the engine rocks at idle
  - the boost flames sit at the side-pipe tips

Every mesh uses a single material, so a livery is just the three textures.

## Editing the colours

Each face's UVs point at one 32×32 px cell of a 256×256 palette (8×8 grid). To make a new
scheme, copy the three PNGs and repaint the cells. Cell *n* is at column `n % 8` and row
`n // 8`, counting from the top-left.

| # | Role | Used on | Yellow | White |
|---|---|---|---|---|
| 0 | PaintPrimary | body, wing flap | `#FFB300` | `#F2F3F0` |
| 1 | PaintSecondary | stripes, scoop, wing endplates, helmet stripe | `#0D0D0F` | `#00A39A` |
| 2 | Carbon | underside, skirts, splitter, diffuser, wing, steering wheel | `#1C1E22` | `#1A1C1F` |
| 3 | Interior | cockpit tub, firewall | `#232427` | `#232427` |
| 4 | Chrome | headers, side pipes, push bar, pulleys, scoop lip | `#E6E8EB` | `#A7ACB2` (titanium) |
| 5 | EngineBlock | block, heads, fins | `#2B2D31` | `#1F2023` |
| 6 | EngineAccent | blower, valve covers | `#C9CCD1` | `#00A39A` |
| 7 | Metal | suspension, axles | `#44474D` | `#3B3E44` |
| 8 | Cage | roll cage | `#121214` | `#2A2D32` |
| 9 | Rim | spokes, barrel | `#111113` | `#EDEDE9` |
| 10 | RimLip | polished dish | `#DADDE1` | `#00A39A` |
| 11 | Tire | rubber, blower belt | `#171717` | `#171717` |
| 12 | Accent | tyre sidewall stripe, calipers, springs, distributor cap | `#FFB300` | `#00C2B5` |
| 13 | BrakeDisc | discs | `#8C9096` | `#8C9096` |
| 14 | Grille | intakes, exhaust tips | `#0A0A0B` | `#0A0A0B` |
| 15 | LightFront | LED headlights (emissive) | `#F5F8FF` | `#F5F8FF` |
| 16 | LightRear | LED tail bar (emissive) | `#B00000` | `#B00000` |
| 17 | Screen | dash display (emissive) | amber | teal |
| 18 | Suit | driver suit | `#141416` | `#009188` |
| 19 | Gloves | gloves | `#FFB300` | `#F2F3F0` |
| 20 | Helmet | helmet | `#FFB300` | `#F2F3F0` |
| 21 | Visor | iridium visor | `#B8862B` (gold) | `#1F8FA8` (blue) |
| 22 | Seat | seat, headrest pad | `#1E1F22` | `#1E1F22` |
| 23 | DecalBase | number roundel | `#F2F2F2` | `#00A39A` |
| 24 | DecalText | number "7" | `#0D0D0F` | `#F2F3F0` |

## Rebuilding in Blender

Open `StuntCar.blend`, load `blender_scripts/rebuild_all.py` in the Text Editor and press **Run Script**.
This regenerates both cars from `build_car.py` and the palettes from `schemes.py`, where the
`SCHEMES` table holds the hex, metallic and smoothness values. It then writes the palette textures
and `StuntCar.fbx` straight into `Assets/Models/StuntCar`. Both liveries share one mesh, so only
the yellow car is exported. A new livery also needs a material in `BuildScript`, like `CarSkin("CarWhite", "White")`.
In Blender the objects carry `Y_` / `W_` prefixes so the two cars can sit in the same scene. The
export strips the prefixes, so in Unity the objects are simply named `Body`, `Wheel_FL` and so on.

If you hand-edit the meshes, don't run `rebuild_all.py` afterwards, because it overwrites them.
To export, use `schemes.export_fbx(root, prefix, path)`, or File › Export › FBX to
`Assets/Models/StuntCar/StuntCar.fbx` with these settings: Selected Objects, Empty + Mesh,
Apply Scalings = FBX All, Forward −Z, Up Y, Apply Unit, Apply Transform, and Path Mode = Copy
with embedding on.
