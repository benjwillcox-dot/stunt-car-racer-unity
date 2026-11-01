using System.Collections.Generic;
using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Lays a road course out from its corners, for <see cref="TrackLibrary"/>.
    ///
    /// Corners are listed in driving order round a closed loop. Each is rounded into an arc of its
    /// radius and banked by its bank angle: positive leans into the bend (on camber), negative leans
    /// out of it (off camber). The deck is at the corner's height at the middle of the bend and
    /// climbs or falls steadily, by distance, to the next one, through any Height() set on the
    /// straight between. Features go on the straight after the most recent corner, at "at" metres
    /// from that corner: humps, hills, ramps, rows of kickers, gaps to jump and drawbridges. So
    /// does the start line.
    /// </summary>
    public class Course
    {
        const float FillerSpacing = 26f;  // points along plain straights
        const float ArcStep = 14f;        // degrees of bend per point
        const float MinSpacing = 3f;      // points closer than this are dropped (feature points win)

        struct CornerSpec { public Vector2 at; public float radius, height, bank; }

        struct Mark
        {
            public float s, dh, hint, min, height;
            public bool gap, drawbridge, level, start, keep;   // keep: a Height() mark, fixing the deck at 'height'
        }

        class Straight
        {
            public readonly List<Mark> marks = new List<Mark>();
            public readonly List<Vector2> spans = new List<Vector2>();   // (from, to) in metres from the corner
        }

        readonly string name;
        readonly List<CornerSpec> corners = new List<CornerSpec>();
        readonly List<Straight> straights = new List<Straight>();

        public Course(string name) { this.name = name; }

        Straight Current => straights[straights.Count - 1];

        /// <summary>A corner at (x, z), rounded to <paramref name="radius"/>, at deck height <paramref name="height"/>.</summary>
        public Course Corner(float x, float z, float radius, float height, float bank = 9f)
        {
            corners.Add(new CornerSpec { at = new Vector2(x, z), radius = radius, height = height, bank = bank });
            straights.Add(new Straight());
            return this;
        }

        /// <summary>
        /// The deck height <paramref name="at"/> metres along the straight. Between these (and the
        /// corners) it climbs or falls steadily, so two of them make a plunge or a steep climb.
        /// </summary>
        public Course Height(float at, float height)
        {
            Current.marks.Add(new Mark { s = at, hint = 999f, keep = true, height = height });
            return this;
        }

        /// <summary>The start line, on the straight after the last corner.</summary>
        public Course Start(float at)
        {
            Current.marks.Add(new Mark { s = at, hint = 999f, start = true });
            return this;
        }

        /// <summary>A rounded hump rising <paramref name="height"/> over <paramref name="length"/> metres.</summary>
        public Course Hump(float at, float length, float height, float hint = 32f)
        {
            float[] f = { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1f }, h = { 0f, 0.65f, 1f, 1f, 0.65f, 0f };
            for (int i = 0; i < f.Length; i++)
                Add(at + f[i] * length, h[i] * height, i == 0 || i == 5 ? 999f : (i == 1 || i == 4 ? hint + 4f : hint));
            Current.spans.Add(new Vector2(at, at + length));
            return this;
        }

        /// <summary>
        /// A climb over <paramref name="up"/> metres to <paramref name="height"/>, a level top of
        /// <paramref name="top"/> metres, and a drop over <paramref name="down"/> metres back to the road.
        /// </summary>
        public Course Hill(float at, float height, float up, float top, float down, float hint = 30f)
        {
            // S-curves both ways, so the drop is a slope to land on rather than a cliff.
            float[] f = { 0f, 0.25f, 0.5f, 0.75f, 1f }, h = { 0f, 0.18f, 0.5f, 0.82f, 1f };
            for (int i = 0; i < f.Length; i++) Add(at + f[i] * up, h[i] * height, i == 0 ? 999f : hint + 4f);
            for (int i = 1; i < f.Length; i++) Add(at + up + top + f[i] * down, h[f.Length - 1 - i] * height, i == f.Length - 1 ? 999f : hint);
            Current.spans.Add(new Vector2(at, at + up + top + down));
            return this;
        }

        /// <summary>A launch ramp: up to <paramref name="lip"/> metres and a quick drop back to the road.</summary>
        public Course Ramp(float at, float lip, float hint = 34f)
        {
            Add(at, 0f, 999f); Add(at + 22f, lip * 0.85f, hint); Add(at + 36f, lip, hint); Add(at + 54f, 0f, 999f);
            Current.spans.Add(new Vector2(at, at + 54f));
            return this;
        }

        /// <summary>
        /// A row of small kickers with flat landings between. Up to about 30 m/s you land in the
        /// flat each time; much faster and you come down on the next one as it rises.
        /// </summary>
        public Course Kickers(float at, int count, float lip = 1.3f, float spacing = 44f, float hint = 28f)
        {
            for (int k = 0; k < count; k++)
            {
                float s0 = at + k * spacing;
                Add(s0, 0f, hint); Add(s0 + 7f, lip * 0.4f, hint); Add(s0 + 14f, lip, hint); Add(s0 + 19f, lip * 0.2f, hint); Add(s0 + 24f, 0f, hint);
            }
            Current.spans.Add(new Vector2(at, at + count * spacing));
            return this;
        }

        /// <summary>A kicker up to <paramref name="lip"/> metres, then <paramref name="gap"/> metres of nothing, then a landing ramp down.</summary>
        public Course Jump(float at, float gap, float lip = 5.5f, float minSpeed = 33f)
        {
            // The AI carries at least minSpeed up the kicker, but not much more, or it overshoots the landing.
            Add(at, 0f, minSpeed + 2f, minSpeed - 3f);
            Add(at + 22f, lip * 0.55f, minSpeed + 2f, minSpeed);
            Current.marks.Add(new Mark { s = at + 36f, dh = lip, hint = 999f, min = minSpeed, gap = true });
            Add(at + 36f + gap, lip - 1f, 40f);
            Add(at + 64f + gap, lip * 0.36f, 999f);
            Add(at + 94f + gap, 0f, 999f);
            Current.spans.Add(new Vector2(at, at + 94f + gap));
            return this;
        }

        /// <summary>A drawbridge of <paramref name="span"/> metres. The straight must be level here, with room either side.</summary>
        public Course Drawbridge(float at, float span = 26f)
        {
            var c = Current;
            c.marks.Add(new Mark { s = at, hint = 999f, level = true });
            c.marks.Add(new Mark { s = at + 26f, hint = 999f, level = true, drawbridge = true });
            c.marks.Add(new Mark { s = at + 26f + span, hint = 40f, level = true });
            c.marks.Add(new Mark { s = at + 40f + span, hint = 999f, level = true });
            c.spans.Add(new Vector2(at, at + 40f + span));
            return this;
        }

        void Add(float s, float dh, float hint, float min = 0f) =>
            Current.marks.Add(new Mark { s = s, dh = dh, hint = hint, min = min });

        static Vector2 Flat(TrackPoint p) => new Vector2(p.Position.x, p.Position.z);

        static Vector2 Rotate(Vector2 v, float a) => new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));

        /// <summary>The track points, starting from the start line.</summary>
        public List<TrackPoint> Points()
        {
            int n = corners.Count;
            var arcStart = new Vector2[n]; var arcEnd = new Vector2[n]; var centre = new Vector2[n];
            var turn = new float[n]; var tangent = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = corners[(i - 1 + n) % n].at, cur = corners[i].at, next = corners[(i + 1) % n].at;
                Vector2 dIn = (cur - prev).normalized, dOut = (next - cur).normalized;
                turn[i] = Mathf.Atan2(dIn.x * dOut.y - dIn.y * dOut.x, Vector2.Dot(dIn, dOut));   // + is a left-hander
                tangent[i] = corners[i].radius * Mathf.Tan(Mathf.Abs(turn[i]) * 0.5f);
                arcStart[i] = cur - dIn * tangent[i];
                arcEnd[i] = cur + dOut * tangent[i];
                centre[i] = arcStart[i] + new Vector2(-dIn.y, dIn.x) * (corners[i].radius * Mathf.Sign(turn[i]));
            }

            // First pass: the points round the loop. Their y is left as the offset from the deck
            // height; the deck height itself is fixed at each bend's apex and at Height() marks.
            var points = new List<TrackPoint>();
            var fixedHeight = new List<float>();   // NaN where the deck height isn't set
            var levelGroup = new List<int>();      // which level feature a point is part of, or -1
            int startIndex = 0, levels = 0;
            for (int i = 0; i < n; i++)
            {
                var c = corners[i];
                int j = (i + 1) % n;

                // Round the bend, banked; half the bank where it meets the straights.
                float sweep = Mathf.Abs(turn[i]) * Mathf.Rad2Deg;
                int steps = Mathf.Max(2, Mathf.CeilToInt(sweep / ArcStep));
                float bank = c.bank * Mathf.Sign(turn[i]);
                // Where the straight into this bend is too short to matter, its end is our start.
                bool shortIn = Vector2.Distance(arcEnd[(i - 1 + n) % n], arcStart[i]) < MinSpacing;
                for (int k = shortIn && i > 0 ? 1 : 0; k <= steps; k++)
                {
                    Vector2 p = centre[i] + Rotate(arcStart[i] - centre[i], turn[i] * k / steps);
                    float b = k == 0 || k == steps ? bank * 0.5f : bank;
                    points.Add(new TrackPoint(p.x, 0f, p.y, b));
                    fixedHeight.Add(k == steps / 2 ? c.height : float.NaN);
                    levelGroup.Add(-1);
                }

                // The straight to the next corner.
                Vector2 from = arcEnd[i], to = arcStart[j];
                float length = Vector2.Distance(from, to);
                if (Vector2.Dot(to - from, corners[j].at - c.at) < 0f)
                    throw new System.InvalidOperationException($"{name}: corners {i} and {j} are too close for their radii");
                Vector2 dir = (to - from).normalized;

                // Feature marks (measured from the corner) plus filler points outside their spans.
                var marks = new List<Mark>();
                foreach (var m in straights[i].marks)
                {
                    var mm = m;
                    mm.s -= tangent[i];
                    if (mm.s < 1f || mm.s > length - 1f)
                        throw new System.InvalidOperationException($"{name}: a feature after corner {i} doesn't fit on its straight ({m.s:0} m, straight runs {tangent[i]:0}-{tangent[i] + length:0} m)");
                    marks.Add(mm);
                }
                int fillers = Mathf.FloorToInt(length / FillerSpacing);
                for (int f = 1; f <= fillers; f++)
                {
                    float s = f * length / (fillers + 1);
                    bool inSpan = false;
                    foreach (var span in straights[i].spans)
                        if (s > span.x - tangent[i] - 8f && s < span.y - tangent[i] + 8f) { inSpan = true; break; }
                    if (!inSpan) marks.Add(new Mark { s = s, hint = 999f });
                }
                marks.Sort((a, b) => a.s.CompareTo(b.s));

                bool inLevel = false;
                float lastS = 0f;
                for (int m = 0; m < marks.Count; m++)
                {
                    var mk = marks[m];
                    bool keep = mk.keep || mk.start || mk.gap || mk.drawbridge || mk.level || mk.dh != 0f || mk.hint < 999f || mk.min > 0f;
                    if (!keep && (mk.s - lastS < MinSpacing || length - mk.s < MinSpacing)) continue;
                    if (mk.level && !inLevel) { levels++; inLevel = true; }
                    else if (!mk.level) inLevel = false;
                    Vector2 p = from + dir * mk.s;
                    if (mk.start) startIndex = points.Count;
                    points.Add(new TrackPoint(p.x, mk.dh, p.y, 0f, mk.hint, mk.min, mk.gap, mk.drawbridge));
                    fixedHeight.Add(mk.keep ? mk.height : float.NaN);
                    levelGroup.Add(mk.level ? levels : -1);
                    lastS = mk.s;
                }
            }

            // The loop closes on the first bend's start; drop a last point that would sit on it.
            int last = points.Count - 1;
            if (last > 0 && Vector2.Distance(Flat(points[last]), Flat(points[0])) < MinSpacing)
            {
                points.RemoveAt(last); fixedHeight.RemoveAt(last); levelGroup.RemoveAt(last);
                if (startIndex >= points.Count) startIndex = 0;
            }

            // Second pass: the deck height runs steadily between the fixed heights, by distance
            // round the loop, so a climb is spread over the bends as well as the straights. A level
            // feature holds the height it starts at.
            int count = points.Count;
            var dist = new float[count];
            for (int i = 1; i < count; i++) dist[i] = dist[i - 1] + Vector2.Distance(Flat(points[i - 1]), Flat(points[i]));
            float lap = dist[count - 1] + Vector2.Distance(Flat(points[count - 1]), Flat(points[0]));
            var knots = new List<int>();
            for (int i = 0; i < count; i++) if (!float.IsNaN(fixedHeight[i])) knots.Add(i);
            var deck = new float[count];
            for (int i = 0; i < count; i++)
            {
                int after = knots.FindIndex(k => k >= i);
                int b = after < 0 ? knots[0] : knots[after];
                int a = after < 0 ? knots[knots.Count - 1] : (knots[after] == i ? i : knots[(after - 1 + knots.Count) % knots.Count]);
                float da = dist[a] > dist[i] ? dist[a] - lap : dist[a];
                float db = dist[b] < dist[i] ? dist[b] + lap : dist[b];
                deck[i] = a == b ? fixedHeight[a] : Mathf.Lerp(fixedHeight[a], fixedHeight[b], (dist[i] - da) / Mathf.Max(db - da, 0.01f));
            }
            var groupHeight = new Dictionary<int, float>();
            var result = new List<TrackPoint>(count);
            for (int i = 0; i < count; i++)
            {
                var p = points[i];
                float y = deck[i];
                if (levelGroup[i] >= 0)
                {
                    if (!groupHeight.TryGetValue(levelGroup[i], out float held)) groupHeight[levelGroup[i]] = held = y;
                    y = held;
                }
                p.Position.y = y + p.Position.y;
                result.Add(p);
            }

            // Start line first.
            var ordered = new List<TrackPoint>(count);
            for (int i = 0; i < count; i++) ordered.Add(result[(startIndex + i) % count]);
            return ordered;
        }
    }
}
