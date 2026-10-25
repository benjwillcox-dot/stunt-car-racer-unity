using System.Collections.Generic;
using UnityEngine;

namespace StuntCar
{
    public struct TrackPoint
    {
        public Vector3 Position;   // metres; y is height above the ground
        public float Bank;         // degrees, positive raises the right-hand edge (left-hand bends)
        public float SpeedHint;    // m/s cap for the AI through this point (crests, jumps)

        public TrackPoint(float x, float y, float z, float bank = 0f, float speedHint = 999f)
        {
            Position = new Vector3(x, y, z);
            Bank = bank;
            SpeedHint = speedHint;
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
    /// Track layouts. All circuits run anticlockwise seen from above and start at point 0.
    /// Layouts are inspired by the original's names, not traced from it.
    /// </summary>
    public static class TrackLibrary
    {
        const float H = 8f;       // standard deck height
        const float Bank = 9f;    // standard bend banking

        static TrackPoint P(float x, float y, float z, float bank = 0f, float hint = 999f) => new TrackPoint(x, y, z, bank, hint);

        // Semicircle bend from angle a0 to a1 (degrees) around (cx, cz).
        static IEnumerable<TrackPoint> Bend(float cx, float cz, float r, float a0, float a1, int steps, float y = H, float bank = Bank)
        {
            for (int i = 1; i < steps; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
                yield return P(cx + Mathf.Cos(a) * r, y, cz + Mathf.Sin(a) * r, bank);
            }
        }

        static TrackDefinition Make(string name, int division, string desc, IEnumerable<TrackPoint> pts)
        {
            return new TrackDefinition { Name = name, Division = division, Description = desc, Points = new List<TrackPoint>(pts).ToArray() };
        }

        public static readonly TrackDefinition[] All = BuildAll();

        static TrackDefinition[] BuildAll()
        {
            var list = new List<TrackDefinition>();

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

            // Big Ramp: a tall climb and a drop, with a tighter bend at the far end.
            {
                var p = new List<TrackPoint> { P(0, H, 0), P(45, H, 0), P(90, H, 0) };
                p.AddRange(Bend(90, 40, 40, -90, 90, 5, H, 12f));
                p.Add(P(90, H, 80)); p.Add(P(60, H + 1f, 80));
                p.Add(P(35, H + 6f, 80, 0, 38)); p.Add(P(15, H + 10f, 80, 0, 36)); p.Add(P(2, H + 10.5f, 80, 0, 34));
                p.Add(P(-12, H + 5f, 80, 0, 999)); p.Add(P(-35, H + 1f, 80)); p.Add(P(-70, H, 80));
                p.AddRange(Bend(-70, 40, 40, 90, 270, 5, H, 12f));
                p.Add(P(-70, H, 0)); p.Add(P(-35, H, 0));
                list.Add(Make("Big Ramp", 3, "Climb ten metres, then a steep drop. Brace for the landing.", p));
            }

            return list.ToArray();
        }
    }
}
