using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using StuntCar;

// Track exporter: builds TrackLibrary's tracks outside Unity, samples them every 2 m the way
// Track.SampleSpline does and plans the AI's speeds as Track.ComputeAiSpeeds does, then reports
// each track's heights, grades, crests the car would fly over, the hardest dips, quick camber rolls,
// overlaps and crossing clearances. It writes the samples to Art/Tracks/tracks.json for the
// Blender previews and the track points into preview/index.html for the browser preview.
//   dotnet run --project Art/Tracks/exporter      (from the repo root)
static class Program
{
    const float Spacing = 2f, HalfWidth = 4f;

    class Sampled
    {
        public TrackDefinition def;
        public List<Vector3> c = new List<Vector3>(), right = new List<Vector3>();
        public List<float> bank = new List<float>(), hint = new List<float>(), dist = new List<float>(), ai = new List<float>(), v = new List<float>();
        public List<bool> gap = new List<bool>(), bridge = new List<bool>();
    }

    static float BankAt(float b0, float b1, float b2, float b3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        float b = 0.5f * ((2f * b1) + (-b0 + b2) * t + (2f * b0 - 5f * b1 + 4f * b2 - b3) * t2 + (-b0 + 3f * b1 - 3f * b2 + b3) * t3);
        return Mathf.Clamp(b, Mathf.Min(b1, b2), Mathf.Max(b1, b2));
    }

    static Vector3 Cr(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static Sampled Sample(TrackDefinition def)
    {
        var s = new Sampled { def = def };
        var pts = def.Points; int n = pts.Length;
        for (int i = 0; i < n; i++)
        {
            TrackPoint p0 = pts[(i - 1 + n) % n], p1 = pts[i], p2 = pts[(i + 1) % n], p3 = pts[(i + 2) % n];
            float seg = Vector3.Distance(p1.Position, p2.Position);
            if (seg < 0.5f) Console.WriteLine($"  !! {def.Name}: points {i} and {i + 1} only {seg:0.00} m apart");
            int steps = Mathf.Max(2, Mathf.CeilToInt(seg / Spacing));
            for (int k = 0; k < steps; k++)
            {
                float t = k / (float)steps;
                s.c.Add(Cr(p0.Position, p1.Position, p2.Position, p3.Position, t));
                s.bank.Add(BankAt(p0.Bank, p1.Bank, p2.Bank, p3.Bank, t));
                s.hint.Add(Mathf.Min(p1.SpeedHint, p2.SpeedHint));
                s.gap.Add(p1.Gap && !p1.Drawbridge);
                s.bridge.Add(p1.Drawbridge);
            }
        }
        int N = s.c.Count;
        float d = 0f;
        for (int i = 0; i < N; i++)
        {
            Vector3 f = (s.c[(i + 1) % N] - s.c[(i - 1 + N) % N]).normalized;
            Vector3 r0 = Vector3.Cross(Vector3.up, f).normalized, u0 = Vector3.Cross(f, r0);
            float b = s.bank[i] * Mathf.Deg2Rad;
            s.right.Add(r0 * Mathf.Cos(b) + u0 * Mathf.Sin(b));   // positive bank raises the right edge
            if (i > 0) d += Vector3.Distance(s.c[i - 1], s.c[i]);
            s.dist.Add(d);
        }
        ComputeAi(s);
        Simulate(s);
        return s;
    }

    // Roughly how fast the AI really goes: flat out up to its plan, slowed by climbs, sped up by drops.
    static void Simulate(Sampled s)
    {
        int n = s.c.Count; float v = 15f;
        for (int i = 0; i < n; i++) s.v.Add(0f);
        for (int lap = 0; lap < 2; lap++)
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                float ds = Vector3.Distance(s.c[i], s.c[next]);
                float slope = (s.c[next].y - s.c[i].y) / Mathf.Max(ds, 0.01f);
                float a = s.gap[i] ? 0f : 8.2f * (1f - (v / 58f) * (v / 58f)) - 9.81f * slope;
                v = Mathf.Min(s.ai[next], Mathf.Sqrt(Mathf.Max(4f, v * v + 2f * a * ds)));
                s.v[next] = v;
            }
    }

    // As Track.ComputeAiSpeeds.
    static void ComputeAi(Sampled s)
    {
        int n = s.c.Count; const int k = 4;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = s.c[(i - k + n) % n], b = s.c[i], c = s.c[(i + k) % n];
            Vector2 A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), C = new Vector2(c.x, c.z);
            float ab = (A - B).magnitude, bc = (B - C).magnitude, ca = (C - A).magnitude;
            float turn = (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x);
            float cross = Mathf.Abs(turn);
            float radius = cross < 0.0001f ? 10000f : (ab * bc * ca) / (2f * cross);
            float camber = s.bank[i] * Mathf.Sign(turn) * Mathf.Deg2Rad;
            float grip = Mathf.Max(0.6f, 1f + Mathf.Tan(camber));
            s.ai.Add(Mathf.Min(s.hint[i], Mathf.Sqrt(grip * 9.81f * radius), 80f));
        }
        for (int pass = 0; pass < 2; pass++)
            for (int i = n - 1; i >= 0; i--)
            {
                int next = (i + 1) % n;
                float ds = Vector3.Distance(s.c[i], s.c[next]);
                float slope = (s.c[next].y - s.c[i].y) / Mathf.Max(ds, 0.01f);
                float braking = Mathf.Max(2f, 7f + 9.81f * slope);
                float vMax = Mathf.Sqrt(s.ai[next] * s.ai[next] + 2f * braking * ds);
                if (s.ai[i] > vMax) s.ai[i] = vMax;
            }
    }

    static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    static void Main(string[] args)
    {
        // The repo root: the first folder up from here with the game's scripts in it.
        string root = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "Assets", "Scripts", "TrackLibrary.cs"))) root = Path.GetDirectoryName(root);
        if (root == null) { Console.WriteLine("Run this from inside the repo."); return; }
        TrackDefinition[] all;
        try { all = TrackLibrary.All; }
        catch (Exception e) { Console.WriteLine("BUILD FAILED: " + (e.InnerException?.Message ?? e.Message)); return; }

        var json = new StringBuilder("[\n");
        var js = new StringBuilder();
        foreach (var def in all)
        {
            var s = Sample(def);
            int N = s.c.Count;
            float lap = s.dist[N - 1] + Vector3.Distance(s.c[N - 1], s.c[0]);
            float maxY = s.c.Max(p => p.y), minY = s.c.Min(p => p.y);
            // Grades over +/- 6 m, skipping gaps.
            float up = 0, down = 0; float upAt = 0, downAt = 0;
            for (int i = 0; i < N; i++)
            {
                bool nearGap = false;
                for (int k = -4; k <= 4; k++) if (s.gap[(i + k + N) % N]) nearGap = true;
                if (nearGap) continue;
                Vector3 a = s.c[(i - 3 + N) % N], b = s.c[(i + 3) % N];
                float h = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                float ang = Mathf.Atan2(b.y - a.y, h) * Mathf.Rad2Deg;
                if (ang > up) { up = ang; upAt = s.dist[i]; }
                if (-ang > down) { down = -ang; downAt = s.dist[i]; }
            }
            // Vertical curvature over +/- 10 m: tightest crests and dips away from gaps.
            var crests = new List<(float r, float at, float v)>(); var dips = new List<(float r, float at, float v)>();
            for (int i = 0; i < N; i++)
            {
                bool nearGap = false;
                for (int k = -8; k <= 8; k++) if (s.gap[(i + k + N) % N]) nearGap = true;
                if (nearGap) continue;
                Vector3 a = s.c[(i - 5 + N) % N], b = s.c[i], c = s.c[(i + 5) % N];
                float h1 = new Vector2(b.x - a.x, b.z - a.z).magnitude, h2 = new Vector2(c.x - b.x, c.z - b.z).magnitude;
                float s1 = (b.y - a.y) / h1, s2 = (c.y - b.y) / h2;
                float curv = (s2 - s1) / ((h1 + h2) * 0.5f) / Mathf.Pow(1f + Mathf.Pow((s1 + s2) * 0.5f, 2f), 1.5f);
                if (curv < -0.002f) crests.Add((-1f / curv, s.dist[i], s.v[i]));
                if (curv > 0.002f) dips.Add((1f / curv, s.dist[i], s.v[i]));
            }
            // Places where the camber rolls faster than 1.5 degrees a metre.
            var rolls = new List<(float rate, float at, float from, float to)>();
            for (int i = 0; i < N; i++)
            {
                float ds = Mathf.Max(0.1f, Vector3.Distance(s.c[i], s.c[(i + 1) % N]));
                float rate = Mathf.Abs(s.bank[(i + 1) % N] - s.bank[i]) / ds;
                if (rate > 1.5f && !rolls.Exists(r => Mathf.Abs(r.at - s.dist[i]) < 25f)) rolls.Add((rate, s.dist[i], s.bank[Math.Max(0, i - 8)], s.bank[(i + 8) % N]));
            }
            float maxBank = s.bank.Max(Mathf.Abs);
            float bankRate = 0f; float bankRateAt = 0f;
            for (int i = 0; i < N; i++)
            {
                float db = Mathf.Abs(s.bank[(i + 1) % N] - s.bank[i]) / Mathf.Max(0.1f, Vector3.Distance(s.c[i], s.c[(i + 1) % N]));
                if (db > bankRate) { bankRate = db; bankRateAt = s.dist[i]; }
            }
            // Overlaps: two parts of the track within a deck's width of each other, not far enough
            // apart in height to pass over and under.
            float minClear = float.MaxValue; string overlap = "";
            for (int i = 0; i < N; i += 2)
                for (int j = i + 40; j < N; j += 2)
                {
                    int along = Math.Min(j - i, N - (j - i));
                    if (along < 40) continue;
                    float dx = s.c[i].x - s.c[j].x, dz = s.c[i].z - s.c[j].z;
                    float hd = Mathf.Sqrt(dx * dx + dz * dz);
                    if (hd > 2f * HalfWidth + 3f) continue;
                    float dy = Mathf.Abs(s.c[i].y - s.c[j].y);
                    if (hd < 2f * HalfWidth) minClear = Mathf.Min(minClear, dy);
                    if (dy < 7f && overlap.Length == 0) overlap = $"OVERLAP at {s.dist[i]:0} m and {s.dist[j]:0} m (apart {hd:0.0} m, dy {dy:0.0} m)";
                }
            // Rough lap time on the AI's planned speeds.
            float time = 0f;
            for (int i = 0; i < N; i++) time += Vector3.Distance(s.c[i], s.c[(i + 1) % N]) / Mathf.Max(5f, s.ai[i]);

            Console.WriteLine($"{def.Name,-16} lap {lap,5:0} m  height {minY:0.0}-{maxY:0.0} m  steepest up {up:0.0} deg @{upAt:0}, down {down:0.0} deg @{downAt:0}  bank max {maxBank:0} deg, fastest roll {bankRate:0.00} deg/m @{bankRateAt:0}  ~{time:0} s{(minClear < float.MaxValue ? $"  crossing clearance {minClear:0.0} m" : "")}");
            // Crests the car would leave the road over at the speed it gets there, and the hardest dips.
            foreach (var cr in crests.Where(x => x.v > Mathf.Sqrt(9.81f * x.r)).GroupBy(x => Mathf.Round(x.at / 30f)).Select(g => g.OrderBy(x => x.r).First()).Take(6))
                Console.WriteLine($"     airborne over crest r {cr.r,4:0} m @{cr.at,5:0} (about {cr.v:0} m/s, lift-off at {Mathf.Sqrt(9.81f * cr.r):0})");
            foreach (var dp in dips.OrderByDescending(x => x.v * x.v / x.r).Take(2))
                Console.WriteLine($"     dip   r {dp.r,5:0} m @{dp.at,5:0} (about {dp.v:0} m/s, {1f + dp.v * dp.v / dp.r / 9.81f:0.0} g)");
            if (overlap.Length > 0) Console.WriteLine("     !! " + overlap);
            foreach (var r in rolls) Console.WriteLine($"     roll {r.rate:0.0} deg/m @{r.at:0} ({r.from:0} to {r.to:0} deg)");

            json.Append($"{{\"name\": \"{def.Name}\", \"division\": {def.Division}, \"samples\": [");
            for (int i = 0; i < N; i++)
            {
                var c = s.c[i]; var r = s.right[i];
                json.Append($"{(i > 0 ? "," : "")}[{F(c.x)},{F(c.y)},{F(c.z)},{F(r.x)},{F(r.y)},{F(r.z)},{F(s.bank[i])},{(s.gap[i] ? 1 : 0)},{F(s.ai[i])}]");
            }
            json.Append("]},\n");

            js.Append($"T(\"{def.Name}\", {def.Division}, \"{def.Description}\", [\n");
            var pts = def.Points;
            for (int i = 0; i < pts.Length; i++)
            {
                var p = pts[i];
                if (i % 6 == 0) js.Append("  ");
                js.Append($"P({F(p.Position.x)}, {F(p.Position.y)}, {F(p.Position.z)}, {F(p.Bank)}, {F(p.SpeedHint)}, {F(p.MinSpeed)}{(p.Gap && !p.Drawbridge ? ", true" : "")}), ");
                if (i % 6 == 5 || i == pts.Length - 1) js.Append(i == pts.Length - 1 ? "]);\n" : "\n");
            }
        }
        json.Length -= 2; json.Append("\n]\n");
        File.WriteAllText(Path.Combine(root, "Art", "Tracks", "tracks.json"), json.ToString());

        // The preview's track list runs from the first T(...) to its "track runtime" section.
        string html = Path.Combine(root, "preview", "index.html");
        var lines = File.ReadAllLines(html).ToList();
        int first = lines.FindIndex(l => l.StartsWith("T(\""));
        int end = lines.FindIndex(l => l.StartsWith("// =") && l.Contains("track runtime"));
        if (first < 0 || end < first) { Console.WriteLine("Couldn't find the track list in preview/index.html"); return; }
        lines.RemoveRange(first, end - first);
        var block = js.ToString().TrimEnd('\n').Split('\n').ToList();
        block.Add("");
        lines.InsertRange(first, block);
        File.WriteAllText(html, string.Join("\n", lines) + "\n");
        Console.WriteLine("Wrote Art/Tracks/tracks.json and the track list in preview/index.html.");
    }
}
