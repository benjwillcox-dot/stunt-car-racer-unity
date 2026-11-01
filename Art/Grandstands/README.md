# Grandstands — Blender kit for the Unity remake

Three styles of grandstand, inspired by stands at real circuits but not copies of any of them.
Each style is a 12 m **bay** that repeats along a side of the stadium, plus an **end** piece
that closes the run off. The game mirrors the end piece for the other end of the run.

| Style | Inspired by | Look |
|---|---|---|
| **Main** | the modern main grandstands at Austin and Silverstone | lower and upper tiers with two floors of glazed hospitality suites between them, under a white fabric canopy slung between big cantilevered trusses; a stair and lift tower at each end |
| **Scaffold** | temporary street-circuit and harbour-front stands | seats on a galvanised scaffold frame wrapped in banners, a row of peaked tent roofs with pennants, and a switchback stair at each end |
| **Terrace** | classic main stands at Monza and Interlagos | concrete terrace under a sloping roof on slim green columns, press boxes along the brick back wall, and a clock tower at each end |

![the kit laid out round a mock track](renders/aerial.jpg)
![main grandstand from the track](renders/main_trackside.jpg)
![scaffold stands](renders/scaffold_trackside.jpg)
![classic terrace and its clock tower](renders/terrace_trackside.jpg)

## Files

| Path | What it is |
|---|---|
| `Grandstands.blend` | the kit (collection `Grandstands`) and a preview stadium built from copies of it (collection `Preview`, never exported) |
| `blender_scripts/build_grandstands.py` | builds every module from code, writes the palette and exports the FBX |
| `blender_scripts/preview_grandstands.py` | lays the preview stadium out the way the game does and renders `renders/` |
| `preview_textures/` | the game's crowd and advert textures, rendered from its own generator code, for the previews only |
| `Assets/Models/Grandstands/Grandstands.fbx` | the exported kit |
| `Assets/Models/Grandstands/Textures/Grandstand_*.png` | the palette: albedo, metallic (R) + smoothness (A), emission |

## How a module is put together

Every module is an empty (`Main_Bay`, `Main_End`, `Scaffold_Bay`, `Scaffold_End`,
`Terrace_Bay`, `Terrace_End`) with four child meshes:

| Child | Material in the game | UVs |
|---|---|---|
| `<Module>_Palette` | `Grandstand` (palette) | every face points at one 32 px cell of the 256 px palette |
| `<Module>_Crowd` | the game's crowd texture | U = metres / 12. V's whole part numbers the row (risers and treads separately) and its fraction runs 0.02–0.98, so the game can give every row its own strip of the texture |
| `<Module>_Ads` | the game's adverts | U's whole part numbers the board; its fraction and V run 0–1 across it, and the game picks a sponsor for each board |
| `<Module>_Collider` | none (physics only) | a simple solid of the seating and roof |

In Unity's axes a module's x runs along the stand (left to right as seen from the track), y is
up and z points away from the track. The stand's front edge is on z = 0 and a bay runs from
x = 0 to 12. An end piece's seats stop at x = 5.4. Its end wall stands at x = 6, which is where
the game puts the end of the run, and its tower reaches a few metres past that.

| Module | Triangles | Depth | Height |
|---|---|---|---|
| `Main_Bay` | 1,508 | 27.5 m | 35.2 m (roof), top row 27.9 m |
| `Main_End` | 1,372 | | tower 31.6 m |
| `Scaffold_Bay` | 1,110 | 21.6 m | 26.6 m (pennants), top row 17.5 m |
| `Scaffold_End` | 1,274 | | |
| `Terrace_Bay` | 714 | 21.2 m | 21.7 m (roof), top row 15.2 m |
| `Terrace_End` | 738 | | clock tower 33.2 m |

The suite windows, press boxes, clock faces and the lamps under the roofs have emission in the
palette, and the game turns it up at night.

## Palette

Cell *n* is at column `n % 8` and row `n // 8`, counting from the top-left. The hex, metallic
and smoothness values are in the `PALETTE` table at the top of `build_grandstands.py`.

| # | Role | Used on |
|---|---|---|
| 0 | Concrete | walkways, coping, tunnel mouths' walls, end walls |
| 1 | ConcreteDark | front and back walls, stair tower |
| 2 | Step | aisle steps |
| 3 | Nosing | yellow step edges |
| 4 | SteelWhite | trusses, fins, suite slabs and fascia |
| 5 | SteelGrey | scaffold tubes, steel decking, masts |
| 6 | SteelDark | board backing, underside of the scaffold deck |
| 7 | SteelGreen | the terrace's columns, girders and fascia |
| 8 | Membrane | fabric canopy and tent tops |
| 9 | Canvas | tent undersides |
| 10 | RoofSheet | the terrace's corrugated roof |
| 11 | Glass | suites, press boxes, tower glazing (glows warm at night) |
| 12 | Mullion | window frames, clock hands |
| 13 | Railing | guard rails |
| 14 | Banner | scaffold wraps and hoardings |
| 15 | Accent | tent valances, the main stand's tower stripe |
| 16 | Accent2 | pennants |
| 17 | Brick | the terrace's back wall and clock tower |
| 18 | Opening | tunnel mouths, doors, windows seen from outside |
| 19 | Lamp | lights under the roofs (emissive) |
| 20 | ClockFace | clock faces (backlit at night) |

## In the game

- `Assets/Editor/GrandstandImport.cs` sets the import options: the same as the car's, except that
  the meshes stay readable because the game copies them.
- **Stunt Car → Generate Project Assets** (`BuildScript`) makes the `Grandstand` material and hands
  the model to `GameBootstrap`.
- `Grandstands.cs` lays out each side of the stadium (`Scenery.BuildStadium`):
  - each run is a mirrored end, bays stretched slightly to fit, then an end
  - scaffold stands come in blocks of about 80 m; the others are split only past 360 m
  - the runs are merged five modules at a time into one mesh, so each piece is lit by the floodlights nearest it
  - the main grandstand always lines the start straight; the other sides get one of three mixes, picked by the track's name
- Without the model, the stadium falls back to the stands built from boxes.

## Rebuilding

Open `Grandstands.blend`, load `blender_scripts/build_grandstands.py` in the Text Editor and press
**Run Script**. It rebuilds every module and writes the palette and `Grandstands.fbx` straight
into `Assets/Models/Grandstands`. To refresh the renders afterwards, run this in Blender's Python
console:

```python
import sys; sys.path.append(bpy.path.abspath("//blender_scripts"))
import preview_grandstands as P; P.setup(); P.render_all(bpy.path.abspath("//renders"))
```

The export settings match the car's: Selected Objects, Empty + Mesh, Apply Scalings = FBX All,
Forward −Z, Up Y, Apply Unit, Apply Transform. Textures are not embedded, because the game
doesn't use the FBX's materials.
