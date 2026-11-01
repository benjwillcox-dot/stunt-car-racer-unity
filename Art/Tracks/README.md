# Tracks — roller-coaster road courses, previewed in Blender

The tracks themselves are defined in code: `Assets/Scripts/TrackLibrary.cs` lays each one out
from its corners with the `Course` builder (`TrackCourse.cs`). That way the AI, kerbs, jumps,
drawbridges and crane all keep working from the same centreline. This folder holds 3D views
of them built in Blender, and the exporter that checks them and feeds both Blender and the
browser preview.

Every track is ridden like a roller coaster:
- **A steady climb**, often round banked bends, to a crest. The deck climbs in a straight line
  between the heights it's given, through any `Course.Free` corners, rounded off where the
  grade changes.
- **A big drop.** `Course.Drop` gives a crest rounded to 45–55 m, a plunge at up to 24–31°
  and a long pull-out. The AI eases over the crest so it stays on the road. Take it flat out
  and you fly the drop: no damage on Little Ramp, 12 on Big Ramp, 24 on Ski Jump, 36 on Roller
  Coaster, 69 on High Jump, and you're wrecked on Big Dipper.
- **Banked bends.** On camber at 10–24° and off camber at 5–8°. The banking eases in on the
  approach and out after the bend, over a run-off that grows with the angle.
- **Rocking straights.** `Course.Roll` makes the camber lean one way, then the other, along
  a straight.
- **The old features.** Gaps to jump, kicker rows, the launch ramps, humps, the drawbridges
  and both figure-of-eight crossings.

![every track's biggest drop](renders/all_drop.jpg)
![riding over the top of each drop](renders/all_ride.jpg)
![every track side on](renders/all_side.jpg)

The sheets run left to right, top to bottom, in the order of the table below.

| Track | Div | Lap | Heights | The ride | AI lap |
|---|---|---|---|---|---|
| Little Ramp | 4 | 1.07 km | 8–20 m | launch ramp, gentle climb up and down the infield spurs, 12 m drop at 20°, rocking start straight | 45 s |
| Hump Back | 4 | 1.00 km | 8–24 m | chicane, climb round two banked sweepers, 6 m hump on the top straight, 10 m drop | 41 s |
| Stepping Stones | 3 | 1.17 km | 8–22 m | climb up the spurs, rocking top straight, 14 m drop at 23°, five kickers to pace | 49 s |
| Big Ramp | 3 | 1.01 km | 8–26 m | rocking climb round a 20° banked left, 18 m drop at 27°, then the tight stuff | 43 s |
| Ski Jump | 2 | 1.12 km | 8–30 m | climb round two banked lefts, 21 m plunge at 28°, a gap at the bottom | 44 s |
| Draw Bridge | 2 | 1.04 km | 8–24 m | climb to the high back straight, the drawbridge 24 m up, 16 m drop at 25° | 44 s |
| High Jump | 1 | 1.05 km | 8–38 m | rocking climb up the start straight and round the spur to 32 m, a gap at the top, 24 m drop at 30° | 43 s |
| Roller Coaster | 1 | 1.09 km | 8–22 m | figure of eight: climb over the crossing (13.5 m clear), 14 m plunge, loops banked at 22–24° with an off-camber twist | 38 s |
| Camel Back | long | 1.81 km | 8–35 m | launch ramp, long climb to 28 m, growing humps along the top, 20 m drop at 27° | 70 s |
| Big Dipper | long | 1.78 km | 8–36 m | figure of eight: 28 m climb to 36 m over the crossing (25 m clear), 28 m drop at 31°, an airtime hump, a second drop on the rocking back straight | 59 s |
| Leap Frog | long | 1.64 km | 8–24 m | steep climb up the spur, 16 m drop at 26°, three gaps, a rocking straight, four kickers | 61 s |
| Swing Bridge | long | 1.64 km | 8–26 m | five kickers, steep climb, rocking top straight, the drawbridge 26 m up, 18 m drop at 27° | 63 s |

The AI laps are flying laps by the browser preview's AI at its top skill (0.95). It also gets
round every track at 0.85. At both speeds it needs no crane recoveries, and the most damage it
takes is 2.3, on Camel Back's launch ramp.

## Files

| Path | What it is |
|---|---|
| `exporter/` | builds TrackLibrary's tracks outside Unity, checks them and writes `tracks.json` and the preview's track list |
| `tracks.json` | every track sampled every 2 m as `Track.cs` samples it: position, banked right vector, bank, gap, the AI's planned speed |
| `Tracks.blend` | each track built in 3D (collection per track) with its deck, kerbs and scaffolding |
| `blender_scripts/build_tracks.py` | builds the tracks from `tracks.json`, renders the views and makes the contact sheets |
| `renders/` | three views of every track, and the contact sheets above |

## After changing a track

1. From the repo root, run the exporter:

   ```bash
   dotnet run --project Art/Tracks/exporter
   ```

   It needs the .NET 10 SDK and Unity's `UnityEngine.CoreModule.dll`. If Unity isn't installed
   where `TrackExport.csproj` expects it, pass your install's Editor/Data folder, for example
   `-p:UnityData="C:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Data"`.

   For each track it reports:
   - lap length and the range of heights;
   - the steepest climb and drop;
   - crests the car would fly over at the speed it gets there;
   - the hardest dips, in g;
   - camber rolls quicker than 1.5° a metre;
   - overlaps, and the clearance at crossings.

   It writes `tracks.json` and replaces the track list in `preview/index.html`.
2. Open `Tracks.blend` and run this in Blender's Python console:

   ```python
   import sys; sys.path.append(bpy.path.abspath("//blender_scripts"))
   import build_tracks as T
   T.build_all(bpy.path.abspath("//tracks.json"))
   T.render_all(bpy.path.abspath("//renders"))
   T.contact_sheets(bpy.path.abspath("//renders"), [c.name for c in bpy.data.collections["Tracks"].children])
   ```

   Building and rendering all twelve takes a few minutes. `render_all` takes a list of track
   names to do just some of them.

Blender's axes follow the FBX export's mapping from Unity's (x, y, z) to (−x, −z, y), so the
tracks turn the same way as in the game.
