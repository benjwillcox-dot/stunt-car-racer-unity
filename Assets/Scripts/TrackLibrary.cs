using System.Collections.Generic;
using UnityEngine;

namespace StuntCar
{
    public struct TrackPoint
    {
        public Vector3 Position;   // metres; y is height above the ground
        public float Bank;         // degrees, positive raises the right-hand edge (left-hand bends)
        public float SpeedHint;    // m/s cap for the AI through this point (crests, landings)
        public float MinSpeed;     // m/s the AI must carry through this point (jump approaches)
        public bool Gap;           // no road between this point and the next: jump it
        public bool Drawbridge;    // the gap is spanned by a drawbridge that lifts and lowers

        public TrackPoint(float x, float y, float z, float bank = 0f, float speedHint = 999f, float minSpeed = 0f, bool gap = false, bool drawbridge = false)
        {
            Position = new Vector3(x, y, z);
            Bank = bank;
            SpeedHint = speedHint;
            MinSpeed = minSpeed;
            Gap = gap || drawbridge;
            Drawbridge = drawbridge;
        }
    }

    public class TrackDefinition
    {
        public string Name;
        public string Description;
        public int Division;       // league division; 0 for the long courses, which aren't in the league
        public TrackPoint[] Points;
        public float LapLength;    // metres, roughly (measured along the spline)
    }

    /// <summary>
    /// Track layouts, paired by division as in the original (Division 4 easiest, 1 hardest), plus
    /// the long courses. Every one is a road course laid out from its corners (see
    /// <see cref="Course"/>) and ridden like a roller coaster: a steady climb, often round banked
    /// bends, to a crest and a big drop, the bends banked into the turn (a few off camber), and
    /// some straights whose camber rocks from side to side. Higher divisions climb higher. Layouts
    /// are inspired by the original's names, not traced from it. The start line is point 0, on the
    /// low-z side, heading +x unless said otherwise.
    /// </summary>
    public static class TrackLibrary
    {
        const float H = 8f;              // standard deck height
        const float F = Course.Free;     // a corner the deck climbs or falls straight through

        static TrackDefinition Make(string name, int division, string desc, Course course)
        {
            var points = course.Points().ToArray();
            return new TrackDefinition { Name = name, Division = division, Description = desc, Points = points, LapLength = Measure(points) };
        }

        // Lap length along the same Catmull-Rom spline the track is built on.
        static float Measure(TrackPoint[] pts)
        {
            int n = pts.Length;
            float length = 0f;
            Vector3 prev = pts[0].Position;
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = pts[(i - 1 + n) % n].Position, p1 = pts[i].Position, p2 = pts[(i + 1) % n].Position, p3 = pts[(i + 2) % n].Position;
                for (int s = 1; s <= 16; s++)
                {
                    float t = s / 16f, t2 = t * t, t3 = t2 * t;
                    Vector3 c = 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                    length += Vector3.Distance(prev, c);
                    prev = c;
                }
            }
            return length;
        }

        public static readonly TrackDefinition[] All = BuildAll();

        public static TrackDefinition[] ForDivision(int division)
        {
            var list = new List<TrackDefinition>();
            foreach (var t in All) if (t.Division == division) list.Add(t);
            return list.ToArray();
        }

        static TrackDefinition[] BuildAll()
        {
            var list = new List<TrackDefinition>();

            // ---------------- Division 4

            // Little Ramp: a launch ramp on the start straight, then a gentle climb up and down the
            // spurs into the infield to 20 m, and the first big drop down the west side.
            list.Add(Make("Little Ramp", 4, "A small launch ramp, a steady climb round the infield and your first big drop.",
                new Course("Little Ramp")
                    .Corner(160, 0, 40, H, 14)
                    .Corner(160, 90, 35, H, 14)
                    .Corner(90, 90, 30, H, 12)              // right
                    .Corner(90, 190, 30, F, 14)
                    .Corner(-20, 190, 30, F, 14)
                    .Corner(-20, 95, 28, F, -6)             // right, off camber
                    .Corner(-100, 95, 30, F, 12)            // right
                    .Corner(-100, 190, 30, F, 14)
                    .Corner(-160, 190, 28, 20f, 16)
                        .Drop(34, 20f, H, 20, 50, 80)
                    .Corner(-160, 0, 40, H, 16)
                        .Start(60).Ramp(140, 2.2f).Roll(205, 60, 8)));

            // Hump Back: a chicane, then a steady climb round two banked sweepers to the high top
            // straight, the big hump along it and a drop off the end; an off-camber left, a quick
            // right, and a straight whose camber rocks both ways.
            list.Add(Make("Hump Back", 4, "Climb round the sweepers to the big hump. Too fast over the top and you'll land hard.",
                new Course("Hump Back")
                    .Corner(-80, 0, 40, H, 10)
                    .Corner(-40, 25, 40, H, -5)             // right, off camber
                    .Corner(10, 25, 40, H, 10)              // right
                    .Corner(50, 0, 40, H, 10)
                        .Start(25).Height(40, H)
                    .Corner(150, 0, 45, F, 16)
                    .Corner(200, 50, 45, F, 16)
                    .Corner(200, 150, 40, 18f, 18)
                        .Hump(50, 90, 6f, 28).Drop(150, 18f, H, 22, 50, 80)
                    .Corner(-60, 150, 40, H, -7)            // off camber
                    .Corner(-110, 100, 35, H, 14)           // right
                    .Corner(-170, 100, 35, H, 14)
                    .Corner(-220, 50, 40, H, 16)
                    .Corner(-180, 0, 40, H, 14)
                        .Roll(22, 60, 10)));

            // ---------------- Division 3

            // Stepping Stones: a long climb up and down the infield spurs, a rocking straight along
            // the top, a big drop down the west side and rows of kickers along the start straight.
            list.Add(Make("Stepping Stones", 3, "Climb the spurs, plunge off the top, then pace the kickers or crash into the next ramp.",
                new Course("Stepping Stones")
                    .Corner(200, 0, 45, H, 16)
                    .Corner(200, 120, 40, H, 14)
                    .Corner(120, 120, 35, F, 14)            // right
                    .Corner(120, 190, 30, F, 14)
                    .Corner(40, 190, 30, F, 14)
                    .Corner(40, 130, 28, F, -6)             // right, off camber
                    .Corner(-80, 130, 35, F, 14)            // right
                    .Corner(-80, 200, 32, 22f, 14)
                        .Roll(34, 44, 8)
                    .Corner(-200, 200, 40, 22f, 16)
                        .Drop(45, 22f, H, 24, 50, 80)
                    .Corner(-200, 0, 45, H, 16)
                        .Start(70).Kickers(105, 2).Kickers(215, 3)));

            // Big Ramp: a chicane, then a steady climb with its camber rocking, round the big banked
            // left-hander to 26 m, a steep drop off the top and a run of tight bends through the
            // corner of the infield.
            list.Add(Make("Big Ramp", 3, "A rocking climb round a banked bend to 26 m, a steep drop, then the tight stuff.",
                new Course("Big Ramp")
                    .Corner(-60, 0, 40, H, 10)
                    .Corner(-20, 25, 40, H, -5)             // right, off camber
                    .Corner(30, 25, 40, H, 10)              // right
                    .Corner(70, 0, 40, H, 10)
                        .Height(15, H).Roll(15, 48, 9)
                    .Corner(190, 0, 45, F, 20)
                    .Corner(190, 110, 40, 26f, 20)
                        .Drop(45, 26f, H, 28, 45, 80)
                    .Corner(-40, 110, 35, H, -7)            // right, off camber
                    .Corner(-40, 180, 32, 12f, 16)
                    .Corner(-120, 180, 32, 12f, 16)
                    .Corner(-120, 120, 25, 10f, 18)         // right
                    .Corner(-190, 120, 35, 10f, 16)
                    .Corner(-190, 0, 45, H, 16)
                        .Start(70)));

            // ---------------- Division 2

            // Ski Jump: a chicane, a long climb round two banked lefts to the high deck at 30 m, a
            // plunge off it with a gap to fly at the bottom, then esses home.
            list.Add(Make("Ski Jump", 2, "Climb to the high deck, plunge 22 m off it and fly the gap at the bottom.",
                new Course("Ski Jump")
                    .Corner(-80, 0, 40, H, 10)
                    .Corner(-40, 25, 40, H, -5)             // right, off camber
                    .Corner(10, 25, 40, H, 10)              // right
                    .Corner(50, 0, 40, H, 10)
                        .Height(15, H)
                    .Corner(170, 0, 45, F, 16)
                    .Corner(230, 60, 40, F, 18)
                    .Corner(230, 160, 40, 30f, 20)
                        .Drop(60, 30f, 9f, 30, 50, 90).Jump(150, 26, 3.5f, 32)
                    .Corner(-120, 160, 40, H, 14)
                        .Roll(18, 36, 8)
                    .Corner(-170, 110, 35, H, 14)           // right
                    .Corner(-240, 110, 30, 10f, 18)
                    .Corner(-200, 0, 45, H, 16)
                        .Start(50)));

            // Draw Bridge: a hairpin spur off the start, a steady climb to the high back straight,
            // the drawbridge across it at 24 m, lifting and lowering, and a drop off the end; then an
            // off-camber right and a rocking straight down the west side.
            list.Add(Make("Draw Bridge", 2, "The drawbridge goes up and down, 24 m up. How high it is when you get there decides the jump.",
                new Course("Draw Bridge")
                    .Corner(-60, 0, 30, H, 14)
                    .Corner(-60, 60, 24, H, 18)             // right
                    .Corner(-10, 60, 24, H, -6)             // right, off camber
                    .Corner(-10, 0, 30, H, 14)
                        .Height(35, H)
                    .Corner(190, 0, 45, F, 18)
                    .Corner(190, 110, 40, 24f, 18)
                        .Drawbridge(55).Drop(135, 24f, H, 26, 45, 80)
                    .Corner(-70, 110, 40, H, -7)            // right, off camber
                    .Corner(-120, 150, 40, H, 14)
                    .Corner(-190, 150, 40, H, 16)
                        .Roll(44, 52, 9)
                    .Corner(-190, 0, 45, H, 16)
                        .Start(70)));

            // ---------------- Division 1

            // High Jump: a chicane, a long climb up the start straight and round the banked spur to
            // 32 m, a steep kicker over a long gap at the top, then a big drop off the end.
            list.Add(Make("High Jump", 1, "Climb to 32 m and leap the gap at the top. Too slow and you drop; too fast and you crack.",
                new Course("High Jump")
                    .Corner(-110, 0, 40, H, 12)
                    .Corner(-70, 25, 40, H, -6)             // right, off camber
                    .Corner(-20, 25, 40, H, 12)             // right
                    .Corner(20, 0, 40, H, 12)
                        .Height(15, H).Roll(30, 100, 10)
                    .Corner(210, 0, 40, F, 16)
                    .Corner(210, 80, 35, F, 16)
                    .Corner(140, 80, 30, F, 14)             // right
                    .Corner(140, 150, 30, 32f, 16)
                        .Jump(35, 28, 5.5f, 33).Drop(162, 32f, H, 30, 50, 90)
                    .Corner(-160, 150, 40, H, 16)
                    .Corner(-210, 100, 40, H, 16)
                    .Corner(-210, 0, 45, H, 18)
                        .Start(50)));

            // Roller Coaster: figure of eight. A steady climb off the line to 22 m over the
            // crossing and a plunge into the right-hand loop, banked hard with a twist off camber at
            // the far end; a rocking straight back under the crossing, a climb into the high
            // left-hand loop and down round it to the line. Starts on the diagonal.
            list.Add(Make("Roller Coaster", 1, "Figure of eight. Climb over the crossing, plunge into the loop, then back underneath.",
                new Course("Roller Coaster")
                    .Corner(200, 130, 40, H, 22)            // right-hand loop
                    .Corner(240, 60, 40, H, -6)
                    .Corner(190, 0, 40, H, 22)
                        .Roll(35, 90, 12).Height(240, H)
                    .Corner(-200, 130, 40, 18f, 22)         // left-hand loop
                    .Corner(-240, 60, 40, 14f, 24)
                    .Corner(-190, 0, 40, H, 22)
                        .Start(50).Climb(80, 110, H, 22f).Drop(215, 22f, H, 28, 45, 80)));

            // ---------------- Long courses (single race and practice; not in the league)

            // Camel Back: a chicane and a launch ramp, a long climb up the right-hand spur and
            // round to the top straight at 28 m, four humps that grow along it, a big drop off the
            // end, then esses and a spur down the far side.
            list.Add(Make("Camel Back", 0, "A long climb to a run of humps that keep growing, then a big drop. Don't take the last one flat out.",
                new Course("Camel Back")
                    .Corner(-48, 0, 45, H, 10)
                    .Corner(0, 42, 40, H, -5)               // right, off camber
                    .Corner(72, 42, 45, H, 10)              // right
                    .Corner(120, 0, 45, H, 10)
                        .Ramp(45, 2.6f).Height(110, H)
                    .Corner(312, 0, 40, F, 16)
                    .Corner(312, 80, 33, F, 14)             // right
                    .Corner(382, 80, 33, F, 16)
                    .Corner(382, 170, 45, F, 18)
                    .Corner(264, 252, 55, 28f, 14)
                        .Hump(25, 75, 3.5f, 34).Hump(115, 85, 5f, 32).Hump(215, 80, 6.5f, 28).Drop(305, 28f, H, 28, 50, 90)
                    .Corner(-168, 252, 45, H, 16)
                        .Roll(22, 46, 10)
                    .Corner(-230, 190, 40, H, -7)           // right, off camber
                    .Corner(-300, 190, 40, 10f, 16)
                    .Corner(-300, 100, 30, 10f, 16)         // right
                    .Corner(-360, 100, 28, 9f, 16)
                    .Corner(-360, 0, 45, H, 18)
                        .Roll(60, 70, 12).Start(150)));

            // Big Dipper: a giant figure of eight. A long steady climb off the line to 36 m over
            // the crossing, the biggest drop of all and an airtime hump; the right-hand loop climbs
            // banked hard with a twist off camber, the back straight rocks then drops under the
            // crossing, and a climb takes you round the high left-hand loop and back down.
            list.Add(Make("Big Dipper", 0, "A giant figure of eight. Climb to 36 m over the crossing, then the biggest drop of all.",
                new Course("Big Dipper")
                    .Corner(320, 208, 45, 10f, 22)          // right-hand loop
                    .Corner(384, 96, 45, 16f, -8)
                    .Corner(304, 0, 45, 20f, 22)
                        .Roll(60, 120, 12, 2).Drop(190, 20f, H, 28, 45, 80).Climb(400, 160, H, 20f)
                    .Corner(-320, 208, 45, 22f, 22)         // left-hand loop
                    .Corner(-384, 96, 45, 18f, 24)
                    .Corner(-304, 0, 45, H, 22)
                        .Start(70).Climb(110, 220, H, 36f).Drop(345, 36f, H, 32, 55, 100).Hump(460, 100, 9f, 26)));

            // Leap Frog: a steep climb up the right-hand spur, a drop off the top, a gap at the
            // foot, a rocking straight and another gap, then a jump and a row of kickers home.
            list.Add(Make("Leap Frog", 0, "Climb the spur, drop off it, then three gaps with nothing between and a row of kickers.",
                new Course("Leap Frog")
                    .Corner(300, 0, 35, H, 16)
                    .Corner(300, 70, 33, F, 14)             // right
                    .Corner(366, 70, 30, F, 16)
                    .Corner(366, 160, 40, 24f, 18)
                        .Drop(40, 24f, H, 28, 45, 80)
                    .Corner(200, 190, 45, H, 10)
                        .Jump(60, 28, 5.5f, 33)
                    .Corner(-60, 190, 40, H, -7)            // right, off camber
                    .Corner(-120, 240, 40, H, 14)
                        .Roll(20, 80, 12)
                    .Corner(-280, 240, 45, 12f, 18)
                        .Jump(55, 31, 6.5f, 34)
                    .Corner(-300, 0, 50, H, 18)
                        .Start(110).Jump(230, 26, 5.5f, 33).Kickers(380, 4)));

            // Swing Bridge: kickers off the start, a steep climb up the right-hand spur to the high
            // back straight at 26 m, rocking along the top, the drawbridge, a drop off the end,
            // then esses, a rocking straight and an off-camber sweep home.
            list.Add(Make("Swing Bridge", 0, "Kickers, a steep climb, then a drawbridge 26 m up and the drop off the end.",
                new Course("Swing Bridge")
                    .Corner(280, 0, 35, H, 16)
                    .Corner(280, 70, 33, F, 14)             // right
                    .Corner(346, 70, 30, F, 16)
                    .Corner(346, 150, 40, 26f, 18)
                        .Roll(40, 100, 12, 2)
                    .Corner(150, 200, 45, 26f, 10)
                        .Drawbridge(40).Drop(120, 26f, H, 30, 50, 90)
                    .Corner(-150, 200, 40, H, -7)           // right, off camber
                    .Corner(-200, 250, 40, 10f, 16)
                    .Corner(-290, 250, 45, 12f, 18)
                        .Roll(40, 70, 12)
                    .Corner(-330, 120, 40, 10f, -6)         // off camber
                    .Corner(-280, 0, 50, H, 18)
                        .Start(120).Kickers(260, 5)));

            return list.ToArray();
        }
    }
}
