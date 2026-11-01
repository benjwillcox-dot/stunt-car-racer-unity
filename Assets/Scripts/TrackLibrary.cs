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
    /// <see cref="Course"/>), with bends both ways, some banked into the turn and some off camber.
    /// Layouts are inspired by the original's names, not traced from it. The start line is point 0,
    /// on the low-z side, heading +x unless said otherwise.
    /// </summary>
    public static class TrackLibrary
    {
        const float H = 8f;       // standard deck height

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

            // Little Ramp: a launch ramp on the start straight, then up and down two spurs into
            // the infield, with a right-hander at the foot of each and one off-camber.
            list.Add(Make("Little Ramp", 4, "A gentle road course with a small launch ramp. Learn the car here.",
                new Course("Little Ramp")
                    .Corner(160, 0, 40, H, 9)
                    .Corner(160, 90, 35, H, 9)
                    .Corner(90, 90, 30, H + 1f, 8)          // right
                    .Corner(90, 160, 30, H + 2f, 8)
                    .Corner(-20, 160, 30, H + 2f, 8)
                    .Corner(-20, 95, 28, H + 1f, -4)        // right, off camber
                    .Corner(-100, 95, 30, H + 1f, 8)        // right
                    .Corner(-100, 160, 30, H, 9)
                    .Corner(-160, 160, 28, H, 9)
                    .Corner(-160, 0, 40, H, 9)
                        .Start(60).Ramp(150, 2.2f)));

            // Hump Back: a chicane off the line, one big hump along the top, then an off-camber
            // left and a quick right.
            list.Add(Make("Hump Back", 4, "A chicane, one big hump, then an off-camber bend. Too fast over the top and you'll land hard.",
                new Course("Hump Back")
                    .Corner(-80, 0, 40, H, 8)
                    .Corner(-40, 25, 40, H, -5)             // right, off camber
                    .Corner(10, 25, 40, H, 8)               // right
                    .Corner(50, 0, 40, H, 8)
                    .Corner(150, 0, 45, H, 9)
                    .Corner(200, 50, 45, H, 9)
                    .Corner(200, 150, 40, H, 9)
                        .Hump(60, 110, 6.5f, 30)
                    .Corner(-60, 150, 40, H, -5)            // off camber
                    .Corner(-110, 100, 35, H, 9)            // right
                    .Corner(-170, 100, 35, H, 9)
                    .Corner(-220, 50, 40, H, 9)
                    .Corner(-180, 0, 40, H, 9)
                        .Start(55)));

            // ---------------- Division 3

            // Stepping Stones: rows of kickers on two straights, and two spurs into the infield.
            list.Add(Make("Stepping Stones", 3, "Rows of kickers. Pace them and land in the flat, or crash into the next ramp.",
                new Course("Stepping Stones")
                    .Corner(200, 0, 45, H, 9)
                    .Corner(200, 120, 40, H, 9)
                    .Corner(120, 120, 35, H, 8)             // right
                    .Corner(120, 190, 30, H + 2f, 8)
                    .Corner(40, 190, 30, H + 3f, 8)
                    .Corner(40, 130, 28, H + 3f, -5)        // right, off camber
                    .Corner(-80, 130, 35, H + 3f, 8)        // right
                    .Corner(-80, 200, 32, H + 2f, 9)
                    .Corner(-200, 200, 40, H, 9)
                        .Kickers(45, 2)
                    .Corner(-200, 0, 45, H, 9)
                        .Start(80).Kickers(205, 3)));

            // Big Ramp: a chicane, a tall climb and a cliff of a drop along the top, then a run
            // of tight bends through the corner of the infield.
            list.Add(Make("Big Ramp", 3, "Climb eleven metres, then a steep drop. Brace for the landing, then the tight stuff.",
                new Course("Big Ramp")
                    .Corner(-60, 0, 40, H, 8)
                    .Corner(-20, 25, 40, H, -5)             // right, off camber
                    .Corner(30, 25, 40, H, 8)               // right
                    .Corner(70, 0, 40, H, 8)
                    .Corner(190, 0, 45, H, 10)
                    .Corner(190, 110, 40, H, 10)
                        .Hill(45, 11f, 65, 10, 70, 28)
                    .Corner(-40, 110, 35, H, -5)            // right, off camber
                    .Corner(-40, 180, 32, H + 4f, 9)
                    .Corner(-120, 180, 32, H + 4f, 9)
                    .Corner(-120, 120, 25, H + 3f, 8)       // right
                    .Corner(-190, 120, 35, H + 2f, 10)
                    .Corner(-190, 0, 45, H, 10)
                        .Start(70)));

            // ---------------- Division 2

            // Ski Jump: a chicane, a steep climb to the high deck, a plunge off it and a gap to fly
            // at the bottom, then esses home.
            list.Add(Make("Ski Jump", 2, "Climb to the high deck, plunge off it and fly the gap at the bottom.",
                new Course("Ski Jump")
                    .Corner(-80, 0, 40, H, 8)
                    .Corner(-40, 25, 40, H, -5)             // right, off camber
                    .Corner(10, 25, 40, H, 8)               // right
                    .Corner(50, 0, 40, H, 8)
                    .Corner(170, 0, 45, H, 9)
                    .Corner(230, 60, 40, H + 5f, 10)
                    .Corner(230, 160, 40, H + 14f, 10)
                        .Height(70, H + 14f).Height(140, H + 1f).Jump(160, 26, 3.5f, 32)
                    .Corner(-120, 160, 40, H, 9)
                    .Corner(-170, 110, 35, H, 9)            // right
                    .Corner(-240, 110, 30, H + 2f, 10)
                    .Corner(-200, 0, 45, H, 9)
                        .Start(50)));

            // Draw Bridge: a hairpin spur off the start, then the drawbridge across the high
            // straight, lifting and lowering, and an off-camber right after it.
            list.Add(Make("Draw Bridge", 2, "The drawbridge goes up and down. How high it is when you get there decides the jump.",
                new Course("Draw Bridge")
                    .Corner(-60, 0, 30, H, 8)
                    .Corner(-60, 60, 24, H, 7)              // right
                    .Corner(-10, 60, 24, H, -4)             // right, off camber
                    .Corner(-10, 0, 30, H, 8)
                    .Corner(190, 0, 45, H + 2.5f, 9)
                    .Corner(190, 110, 40, H + 5f, 9)
                        .Drawbridge(110)
                    .Corner(-70, 110, 40, H + 5f, -5)       // right, off camber
                    .Corner(-120, 150, 40, H + 5f, 9)
                    .Corner(-190, 150, 40, H + 2f, 10)
                    .Corner(-190, 0, 45, H, 9)
                        .Start(70)));

            // ---------------- Division 1

            // High Jump: a chicane, a spur, then a steep kicker over a long gap along the top.
            list.Add(Make("High Jump", 1, "A steep kicker over a long gap. Too slow and you drop; too fast and you crack.",
                new Course("High Jump")
                    .Corner(-110, 0, 40, H, 8)
                    .Corner(-70, 25, 40, H, -5)             // right, off camber
                    .Corner(-20, 25, 40, H, 8)              // right
                    .Corner(20, 0, 40, H, 8)
                    .Corner(210, 0, 40, H, 9)
                    .Corner(210, 80, 35, H, 9)
                    .Corner(140, 80, 30, H + 1f, 8)         // right
                    .Corner(140, 150, 30, H + 3f, 9)
                        .Jump(60, 28, 5.5f, 33)
                    .Corner(-160, 150, 40, H + 3f, 10)
                    .Corner(-210, 100, 40, H + 2f, 9)
                    .Corner(-210, 0, 45, H, 10)
                        .Start(50)));

            // Roller Coaster: figure of eight, over the crossing on a hump and back under it in a
            // dip; the right-hand loop has an off-camber bend at the far end. Starts on the diagonal.
            list.Add(Make("Roller Coaster", 1, "Figure of eight. Over the top, round, then underneath your own track.",
                new Course("Roller Coaster")
                    .Corner(200, 130, 40, H + 6f, 10)       // right-hand loop
                    .Corner(240, 60, 40, H + 6f, -4)
                    .Corner(190, 0, 40, H + 2f, 10)
                        .Hump(135, 130, -2f, 999f)           // dip under the crossing, 200 m along
                    .Corner(-200, 130, 40, H + 2f, 10)      // left-hand loop
                    .Corner(-240, 60, 40, H + 2f, 12)
                    .Corner(-190, 0, 40, H + 2f, 10)
                        .Start(60).Hump(115, 170, 7f, 34)));   // over the crossing

            // ---------------- Long courses (single race and practice; not in the league)

            // Camel Back: a chicane and a launch ramp, a right-hand spur, four humps that grow along
            // the top straight, then esses and a spur down the far side.
            list.Add(Make("Camel Back", 0, "A long run of humps that keep growing. Don't take the last one flat out.",
                new Course("Camel Back")
                    .Corner(-48, 0, 45, H, 8)
                    .Corner(0, 42, 40, H, -5)               // right, off camber
                    .Corner(72, 42, 45, H, 9)               // right
                    .Corner(120, 0, 45, H, 9)
                        .Ramp(45, 2.6f)
                    .Corner(312, 0, 40, H, 10)
                    .Corner(312, 80, 33, H, 9)              // right
                    .Corner(382, 80, 33, H + 2f, 9)
                    .Corner(382, 170, 45, H + 3f, 10)
                    .Corner(264, 252, 55, H + 3f, 9)
                        .Hump(30, 80, 3.5f, 34).Hump(125, 90, 5f, 32).Hump(230, 75, 4f, 32).Hump(320, 90, 6f, 28)
                    .Corner(-168, 252, 45, H + 3f, 9)
                    .Corner(-230, 190, 40, H + 3f, -5)      // right, off camber
                    .Corner(-300, 190, 40, H + 4f, 9)
                    .Corner(-300, 100, 30, H + 3f, 8)       // right
                    .Corner(-360, 100, 28, H + 2f, 9)
                    .Corner(-360, 0, 45, H, 10)
                        .Start(150)));

            // Big Dipper: a giant figure of eight with steep roller-coaster hills, climbing to
            // 16 m over the crossing; the right-hand loop runs high with an off-camber bend.
            list.Add(Make("Big Dipper", 0, "A giant figure of eight with steep hills. Climb high over the crossing, then dive under it.",
                new Course("Big Dipper")
                    .Corner(320, 208, 45, H + 16f, 12)      // right-hand loop, up high
                    .Corner(384, 96, 45, H + 16f, -5)
                    .Corner(304, 0, 45, H + 4f, 11)
                        .Hump(90, 100, 10f, 26).Hump(255, 130, -4f, 999f).Hump(440, 100, 11f, 26)
                    .Corner(-320, 208, 45, H + 4f, 12)      // left-hand loop
                    .Corner(-384, 96, 45, H + 12f, 12)
                    .Corner(-304, 0, 45, H + 4f, -4)
                        .Start(70).Hump(130, 100, 12f, 26).Hump(250, 150, 9f, 30)));

            // Leap Frog: a jump straight after the start and a row of kickers, a right-hand spur,
            // then two more gaps.
            list.Add(Make("Leap Frog", 0, "Three gaps with nothing between, and a row of kickers. Keep your speed up, but not on the kickers.",
                new Course("Leap Frog")
                    .Corner(300, 0, 35, H, 10)
                    .Corner(300, 70, 33, H, 9)              // right
                    .Corner(366, 70, 30, H + 2f, 9)
                    .Corner(366, 160, 40, H + 3f, 10)
                    .Corner(200, 190, 45, H + 3f, 9)
                        .Jump(60, 28, 5.5f, 33)
                    .Corner(-60, 190, 40, H + 3f, -5)       // right, off camber
                    .Corner(-120, 240, 40, H + 3f, 9)
                    .Corner(-280, 240, 45, H + 3f, 10)
                        .Jump(55, 31, 6.5f, 34)
                    .Corner(-300, 0, 50, H, 10)
                        .Start(110).Jump(230, 26, 5.5f, 33).Kickers(380, 4)));

            // Swing Bridge: kickers off the start, a right-hand spur, the drawbridge on the high
            // back straight, esses after it and an off-camber sweep home.
            list.Add(Make("Swing Bridge", 0, "Kickers, then a drawbridge that lifts and lowers. How high it is when you get there decides the jump.",
                new Course("Swing Bridge")
                    .Corner(280, 0, 35, H + 2f, 10)
                    .Corner(280, 70, 33, H + 2f, 9)         // right
                    .Corner(346, 70, 30, H + 3f, 9)
                    .Corner(346, 150, 40, H + 6f, 10)
                    .Corner(150, 200, 45, H + 6f, 9)
                        .Drawbridge(110)
                    .Corner(-150, 200, 40, H + 6f, -5)      // right, off camber
                    .Corner(-200, 250, 40, H + 4f, 9)
                    .Corner(-290, 250, 45, H + 2f, 10)
                    .Corner(-330, 120, 40, H, -4)           // off camber
                    .Corner(-280, 0, 50, H, 10)
                        .Start(120).Kickers(260, 5)));

            return list.ToArray();
        }
    }
}
