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

        public TrackPoint(float x, float y, float z, float bank = 0f, float speedHint = 999f, float minSpeed = 0f, bool gap = false)
        {
            Position = new Vector3(x, y, z);
            Bank = bank;
            SpeedHint = speedHint;
            MinSpeed = minSpeed;
            Gap = gap;
        }
    }

    public class TrackDefinition
    {
        public string Name;
        public string Description;
        public int Division;
        public TrackPoint[] Points;
    }

    /// <summary>
    /// Track layouts, paired by division as in the original (Division 4 easiest, 1 hardest).
    /// Layouts are inspired by the original's names, not traced from it. Start line is point 0.
    /// </summary>
    public static class TrackLibrary
    {
        const float H = 8f;       // standard deck height
        const float Bank = 9f;    // standard bend banking

        static TrackPoint P(float x, float y, float z, float bank = 0f, float hint = 999f) => new TrackPoint(x, y, z, bank, hint);
        static TrackPoint Jump(float x, float y, float z, float minSpeed) => new TrackPoint(x, y, z, 0f, 999f, minSpeed);
        static TrackPoint Takeoff(float x, float y, float z, float minSpeed) => new TrackPoint(x, y, z, 0f, 999f, minSpeed, true);

        // Bend from angle a0 to a1 (degrees) around (cx, cz), height easing from y0 to y1.
        static IEnumerable<TrackPoint> Bend(float cx, float cz, float r, float a0, float a1, int steps, float y0 = H, float y1 = float.NaN, float bank = Bank)
        {
            if (float.IsNaN(y1)) y1 = y0;
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                float a = Mathf.Lerp(a0, a1, t) * Mathf.Deg2Rad;
                yield return P(cx + Mathf.Cos(a) * r, Mathf.Lerp(y0, y1, t), cz + Mathf.Sin(a) * r, bank);
            }
        }

        static TrackDefinition Make(string name, int division, string desc, IEnumerable<TrackPoint> pts)
        {
            return new TrackDefinition { Name = name, Division = division, Description = desc, Points = new List<TrackPoint>(pts).ToArray() };
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

            // Little Ramp: simple oval with a gentle ramp on the back straight.
            {
                var p = new List<TrackPoint> { P(0, H, 0), P(40, H, 0), P(80, H, 0) };
                p.AddRange(Bend(80, 50, 50, -90, 90, 5));
                p.Add(P(80, H, 100)); p.Add(P(40, H, 100));
                p.Add(P(18, H + 2.2f, 100, 0, 40)); p.Add(P(4, H + 2.6f, 100, 0, 40));
                p.Add(P(-14, H, 100)); p.Add(P(-50, H, 100)); p.Add(P(-80, H, 100));
                p.AddRange(Bend(-80, 50, 50, 90, 270, 5));
                p.Add(P(-80, H, 0)); p.Add(P(-40, H, 0));
                list.Add(Make("Little Ramp", 4, "A gentle oval with a small launch ramp. Learn the car here.", p));
            }

            // Hump Back: long oval with a big hump on the back straight.
            {
                var p = new List<TrackPoint> { P(0, H, 0), P(50, H, 0), P(100, H, 0) };
                p.AddRange(Bend(100, 55, 55, -90, 90, 5));
                p.Add(P(100, H, 110)); p.Add(P(55, H, 110));
                p.Add(P(28, H + 4.5f, 110, 0, 34)); p.Add(P(10, H + 6.5f, 110, 0, 30)); p.Add(P(-8, H + 6.5f, 110, 0, 30));
                p.Add(P(-26, H + 4.5f, 110, 0, 34)); p.Add(P(-55, H, 110)); p.Add(P(-100, H, 110));
                p.AddRange(Bend(-100, 55, 55, 90, 270, 5));
                p.Add(P(-100, H, 0)); p.Add(P(-50, H, 0));
                list.Add(Make("Hump Back", 4, "One big hump. Too fast over the top and you'll land hard.", p));
            }

            // ---------------- Division 3

            // Stepping Stones: a run of short humps you skip across at speed.
            {
                var p = new List<TrackPoint> { P(0, H, 0), P(50, H, 0), P(90, H, 0) };
                p.AddRange(Bend(90, 50, 50, -90, 90, 5));
                p.Add(P(90, H, 100)); p.Add(P(60, H, 100));
                float x = 48f;
                for (int i = 0; i < 4; i++)
                {
                    p.Add(P(x, H + 2.4f, 100, 0, 36));
                    p.Add(P(x - 9f, H, 100, 0, 36));
                    x -= 18f;
                }
                p.Add(P(-40, H, 100)); p.Add(P(-90, H, 100));
                p.AddRange(Bend(-90, 50, 50, 90, 270, 5));
                p.Add(P(-90, H, 0)); p.Add(P(-45, H, 0));
                list.Add(Make("Stepping Stones", 3, "Four humps in a row. Keep it straight and skip across the tops.", p));
            }

            // Big Ramp: a tall climb and a drop, with tighter bends.
            {
                var p = new List<TrackPoint> { P(0, H, 0), P(45, H, 0), P(90, H, 0) };
                p.AddRange(Bend(90, 40, 40, -90, 90, 5, H, H, 12f));
                p.Add(P(90, H, 80)); p.Add(P(60, H + 1f, 80));
                p.Add(P(35, H + 6f, 80, 0, 38)); p.Add(P(15, H + 10f, 80, 0, 36)); p.Add(P(2, H + 10.5f, 80, 0, 34));
                p.Add(P(-12, H + 5f, 80)); p.Add(P(-35, H + 1f, 80)); p.Add(P(-70, H, 80));
                p.AddRange(Bend(-70, 40, 40, 90, 270, 5, H, H, 12f));
                p.Add(P(-70, H, 0)); p.Add(P(-35, H, 0));
                list.Add(Make("Big Ramp", 3, "Climb ten metres, then a steep drop. Brace for the landing.", p));
            }

            // ---------------- Division 2

            // Ski Jump: climb to a high deck, plunge down and jump a gap at the bottom.
            {
                var p = new List<TrackPoint> { P(0, H, 0), P(50, H + 1.5f, 0), P(100, H + 4f, 0) };
                p.AddRange(Bend(100, 55, 55, -90, 90, 5, H + 4f, H + 12f));
                p.Add(P(100, H + 12f, 110)); p.Add(P(72, H + 12f, 110));
                p.Add(Jump(48, H + 6f, 110, 30)); p.Add(Jump(28, H + 1.5f, 110, 32));
                p.Add(Takeoff(14, H + 3f, 110, 32));                 // lip of the jump
                p.Add(P(-12, H + 0.5f, 110, 0, 40)); p.Add(P(-40, H, 110)); p.Add(P(-100, H, 110));
                p.AddRange(Bend(-100, 55, 55, 90, 270, 5));
                p.Add(P(-100, H, 0)); p.Add(P(-50, H, 0));
                list.Add(Make("Ski Jump", 2, "Plunge off the high deck and fly the gap at the bottom.", p));
            }

            // Draw Bridge: the bridge is up – hit the raised halves and jump between them.
            {
                var p = new List<TrackPoint> { P(0, H, 0), P(50, H, 0), P(100, H, 0) };
                p.AddRange(Bend(100, 55, 55, -90, 90, 5));
                p.Add(P(100, H, 110)); p.Add(P(62, H, 110));
                p.Add(Jump(38, H + 2.5f, 110, 33));
                p.Add(Takeoff(22, H + 3.6f, 110, 33));
                p.Add(P(2, H + 2.4f, 110, 0, 40)); p.Add(P(-16, H + 1.0f, 110)); p.Add(P(-45, H, 110)); p.Add(P(-100, H, 110));
                p.AddRange(Bend(-100, 55, 55, 90, 270, 5));
                p.Add(P(-100, H, 0)); p.Add(P(-50, H, 0));
                list.Add(Make("Draw Bridge", 2, "The bridge is up. Carry enough speed to clear the gap.", p));
            }

            // ---------------- Division 1

            // High Jump: a steep kicker, a long gap and a long landing.
            {
                var p = new List<TrackPoint> { P(0, H, 0), P(50, H, 0), P(100, H, 0) };
                p.AddRange(Bend(100, 60, 60, -90, 90, 5));
                p.Add(P(100, H, 120)); p.Add(Jump(70, H, 120, 30));
                p.Add(Jump(48, H + 3f, 120, 33));
                p.Add(Takeoff(34, H + 5.5f, 120, 33));
                p.Add(P(6, H + 4.5f, 120, 0, 40)); p.Add(P(-22, H + 2f, 120)); p.Add(P(-52, H, 120)); p.Add(P(-100, H, 120));
                p.AddRange(Bend(-100, 60, 60, 90, 270, 5));
                p.Add(P(-100, H, 0)); p.Add(P(-50, H, 0));
                list.Add(Make("High Jump", 1, "A steep kicker over a long gap. Too slow and you drop; too fast and you crack.", p));
            }

            // Roller Coaster: figure of eight with a crossover, climbs and dips.
            {
                const float R = -9f; // right-hand bends bank the other way
                var p = new List<TrackPoint>
                {
                    P(-60, H + 2f, 15),
                    P(-20, H + 7f, 38, 0, 40),
                    P(0, H + 10f, 50, 0, 36),        // over the crossing
                    P(25, H + 9f, 65),
                    P(60, H + 8f, 85, R * 0.5f),
                    P(100, H + 7f, 97, R),
                    P(132, H + 5f, 72, R),
                    P(132, H + 3f, 30, R),
                    P(100, H + 1f, 4, R),
                    P(60, H, 15, R * 0.5f),
                    P(28, H + 2.5f, 33, 0, 34),      // dip and hump on the lower diagonal
                    P(0, H, 50),                      // under the crossing
                    P(-28, H + 2.5f, 67, 0, 34),
                    P(-60, H, 85, Bank * 0.5f),
                    P(-100, H + 2f, 97, Bank),
                    P(-132, H + 4f, 72, Bank),
                    P(-132, H + 4f, 30, Bank),
                    P(-100, H + 3f, 4, Bank),
                };
                list.Add(Make("Roller Coaster", 1, "Figure of eight. Over the top, round, then underneath your own track.", p));
            }

            return list.ToArray();
        }
    }
}
