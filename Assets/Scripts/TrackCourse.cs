using System.Collections.Generic;
using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Lays a road course out from its corners, for <see cref="TrackLibrary"/>.
    ///
    /// Corners are listed in driving order round a closed loop. Each is rounded into an arc of its
    /// radius and banked by its bank angle: positive leans into the bend (on camber), negative leans
    /// out of it (off camber). The banking eases in on the approach, holds through the bend and
    /// eases out after it, over a run-off that grows with the angle.
    ///
    /// The deck height is fixed at each bend's apex (unless the corner is <see cref="Free"/>) and at
    /// Height() marks. Between them it climbs or falls steadily, by distance round the loop, through
    /// any free corners, and it rounds off over up to 40 m wherever the grade changes, so climbs,
    /// crests and dips feel like a roller coaster's.
    ///
    /// Features go on the straight after the most recent corner, at "at" metres from that corner:
    /// climbs, drops, camber rolls, humps, hills, ramps, rows of kickers, gaps to jump and
    /// drawbridges. So does the start line.
    /// </summary>
    public class Course
    {
        /// <summary>A corner height that isn't fixed: the deck carries on through the bend.</summary>
        public const float Free = float.NaN;

        const float FillerSpacing = 15f;  // points along plain straights
        const float ArcStep = 14f;        // degrees of bend per point
        const float MinSpacing = 3f;      // points closer than this are dropped (feature points win)
        const float MaxRounding = 20f;    // either side of a change of grade
        const float Gravity = 9.81f;

        struct CornerSpec { public Vector2 at; public float radius, height, bank; }

        struct Mark
        {
            public float s, dh, hint, min, height, roll;
            public bool gap, drawbridge, level, start;
            public bool keep;     // fixes the deck at 'height'
            public bool pinned;   // never dropped for being close to another point
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

        /// <summary>
        /// A corner at (x, z), rounded to <paramref name="radius"/>, at deck height
        /// <paramref name="height"/> (or <see cref="Free"/>), banked <paramref name="bank"/> degrees.
        /// </summary>
        public Course Corner(float x, float z, float radius, float height, float bank = 12f)
        {
            corners.Add(new CornerSpec { at = new Vector2(x, z), radius = radius, height = height, bank = bank });
            straights.Add(new Straight());
            return this;
        }

        /// <summary>The deck height <paramref name="at"/> metres along the straight.</summary>
        public Course Height(float at, float height, float hint = 999f)
        {
            Current.marks.Add(new Mark { s = at, hint = hint, keep = true, pinned = true, height = height });
            return this;
        }

        /// <summary>The start line, on the straight after the last corner.</summary>
        public Course Start(float at)
        {
            Current.marks.Add(new Mark { s = at, hint = 999f, start = true, pinned = true });
            return this;
        }

        /// <summary>
        /// A steady climb (or descent) from <paramref name="from"/> to <paramref name="to"/> metres
        /// high over <paramref name="length"/> metres. To carry a climb on round bends, make them
        /// <see cref="Free"/> and fix the height where it tops out instead.
        /// </summary>
        public Course Climb(float at, float length, float from, float to) => Height(at, from).Height(at + length, to);

        /// <summary>
        /// The big drop, like a roller coaster's first: over a crest rounded to
        /// <paramref name="crest"/> metres, down a plunge as steep as <paramref name="angle"/>
        /// degrees from <paramref name="top"/> to <paramref name="bottom"/> metres high, and out along
        /// a pull-out rounded to <paramref name="pullOut"/> metres. The AI eases over the crest so it
        /// stays on the road; take it faster and you fly the drop.
        /// </summary>
        public Course Drop(float at, float top, float bottom, float angle = 26f, float crest = 50f, float pullOut = 90f)
        {
            float fall = top - bottom;
            float a = angle * Mathf.Deg2Rad;
            // Not tall enough for the full angle: the two curves meet at a shallower one.
            if ((crest + pullOut) * (1f - Mathf.Cos(a)) > fall) a = Mathf.Acos(1f - fall / (crest + pullOut));
            float crestLen = crest * Mathf.Sin(a), outLen = pullOut * Mathf.Sin(a);
            float plunge = (fall - (crest + pullOut) * (1f - Mathf.Cos(a))) / Mathf.Tan(a);
            float length = crestLen + plunge + outLen;
            float hint = 0.9f * Mathf.Sqrt(Gravity * crest);   // keeps a fifth of the car's weight on the tyres
            int steps = Mathf.Max(4, Mathf.CeilToInt(length / 5f));
            for (int k = 0; k <= steps; k++)
            {
                float x = length * k / steps, y;
                if (x <= crestLen) y = top - (crest - Mathf.Sqrt(crest * crest - x * x));
                else if (x <= crestLen + plunge) y = top - crest * (1f - Mathf.Cos(a)) - (x - crestLen) * Mathf.Tan(a);
                else { float u = length - x; y = bottom + (pullOut - Mathf.Sqrt(pullOut * pullOut - u * u)); }
                Height(at + x, y, x <= crestLen ? hint : 999f);
            }
            Current.spans.Add(new Vector2(at, at + length));
            return this;
        }

        /// <summary>
        /// Camber that rocks from side to side along a straight: <paramref name="waves"/> times
        /// over <paramref name="length"/> metres, leaning up to <paramref name="angle"/> degrees each
        /// way, starting with the right-hand edge up.
        /// </summary>
        public Course Roll(float at, float length, float angle, int waves = 1)
        {
            int quarters = waves * 4;
            for (int q = 0; q <= quarters; q++)
            {
                float roll = q % 4 == 1 ? angle : q % 4 == 3 ? -angle : 0f;
                Current.marks.Add(new Mark { s = at + length * q / quarters, hint = 999f, roll = roll, pinned = true });
            }
            Current.spans.Add(new Vector2(at, at + length));
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

        // A point that shapes the road, so it stays however close it is to the next.
        static bool Needed(Mark m) => m.pinned || m.gap || m.drawbridge || m.level || m.dh != 0f || m.hint < 999f || m.min > 0f;

        static Mark Merge(Mark a, Mark b)
        {
            if (a.dh == 0f) a.dh = b.dh;
            if (a.roll == 0f) a.roll = b.roll;
            if (b.keep) { a.keep = true; a.height = b.height; }
            a.hint = Mathf.Min(a.hint, b.hint);
            a.min = Mathf.Max(a.min, b.min);
            a.gap |= b.gap; a.drawbridge |= b.drawbridge; a.level |= b.level; a.start |= b.start;
            a.pinned = true;
            return a;
        }

        static Vector2 Flat(TrackPoint p) => new Vector2(p.Position.x, p.Position.z);

        static Vector2 Rotate(Vector2 v, float a) => new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));

        // How far the banking takes to ease in or out: longer for steeper banks.
        static float RunOff(float bank) => Mathf.Clamp(Mathf.Abs(bank) * 1.5f, 10f, 30f);

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
            // height; the deck height itself is fixed at bends' apexes and at Height() marks.
            var points = new List<TrackPoint>();
            var fixedHeight = new List<float>();   // NaN where the deck height isn't set
            var levelGroup = new List<int>();      // which level feature a point is part of, or -1
            var roll = new List<float>();          // camber rolls along the straights
            var arcFirst = new int[n]; var arcLast = new int[n];
            int startIndex = 0, levels = 0;
            for (int i = 0; i < n; i++)
            {
                var c = corners[i];
                int j = (i + 1) % n;

                // Round the bend (its banking is worked out once the whole loop is laid out).
                float sweep = Mathf.Abs(turn[i]) * Mathf.Rad2Deg;
                int steps = Mathf.Max(2, Mathf.CeilToInt(sweep / ArcStep));
                // Where the straight into this bend is too short to matter, its end is our start.
                bool shortIn = Vector2.Distance(arcEnd[(i - 1 + n) % n], arcStart[i]) < MinSpacing;
                arcFirst[i] = points.Count;
                for (int k = shortIn && i > 0 ? 1 : 0; k <= steps; k++)
                {
                    Vector2 p = centre[i] + Rotate(arcStart[i] - centre[i], turn[i] * k / steps);
                    points.Add(new TrackPoint(p.x, 0f, p.y));
                    fixedHeight.Add(k == steps / 2 ? c.height : float.NaN);
                    levelGroup.Add(-1);
                    roll.Add(0f);
                }
                arcLast[i] = points.Count - 1;

                // The straight to the next corner.
                Vector2 from = arcEnd[i], to = arcStart[j];
                float length = Vector2.Distance(from, to);
                if (Vector2.Dot(to - from, corners[j].at - c.at) < 0f)
                    throw new System.InvalidOperationException($"{name}: corners {i} and {j} are too close for their radii");
                Vector2 dir = (to - from).normalized;

                // Feature marks (measured from the corner), plus filler points and points where the
                // banking eases in and out, outside the features' spans.
                var marks = new List<Mark>();
                foreach (var m in straights[i].marks)
                {
                    var mm = m;
                    mm.s -= tangent[i];
                    if (mm.s < 1f || mm.s > length - 1f)
                        throw new System.InvalidOperationException($"{name}: a feature after corner {i} doesn't fit on its straight ({m.s:0} m, straight runs {tangent[i]:0}-{tangent[i] + length:0} m)");
                    marks.Add(mm);
                }
                bool Clear(float s)
                {
                    if (s < 1f || s > length - 1f) return false;
                    foreach (var span in straights[i].spans)
                        if (s > span.x - tangent[i] - 8f && s < span.y - tangent[i] + 8f) return false;
                    return true;
                }
                int fillers = Mathf.FloorToInt(length / FillerSpacing);
                for (int f = 1; f <= fillers; f++)
                {
                    float s = f * length / (fillers + 1);
                    if (Clear(s)) marks.Add(new Mark { s = s, hint = 999f });
                }
                float outOf = RunOff(c.bank), into = RunOff(corners[j].bank);
                foreach (float s in new[] { outOf * 0.5f, outOf, length - into, length - into * 0.5f })
                    if (Clear(s) && s > 0f && s < length) marks.Add(new Mark { s = s, hint = 999f });
                marks.Sort((a, b) => a.s.CompareTo(b.s));
                // Points from two features that land on top of each other (a roll along a climb)
                // become one.
                var merged = new List<Mark>();
                foreach (var mk in marks)
                {
                    int prev = merged.Count - 1;
                    if (prev >= 0 && mk.s - merged[prev].s < 1.5f && Needed(mk) && Needed(merged[prev])) merged[prev] = Merge(merged[prev], mk);
                    else merged.Add(mk);
                }
                // Filler and easing points give way to the ones that shape the road.
                marks = merged.FindAll(mk => Needed(mk) || !merged.Exists(o => Needed(o) && Mathf.Abs(o.s - mk.s) < MinSpacing));

                bool inLevel = false;
                float lastS = 0f;
                for (int m = 0; m < marks.Count; m++)
                {
                    var mk = marks[m];
                    bool keep = Needed(mk);
                    if (!keep && (mk.s - lastS < MinSpacing || length - mk.s < MinSpacing)) continue;
                    if (mk.level && !inLevel) { levels++; inLevel = true; }
                    else if (!mk.level) inLevel = false;
                    Vector2 p = from + dir * mk.s;
                    if (mk.start) startIndex = points.Count;
                    points.Add(new TrackPoint(p.x, mk.dh, p.y, 0f, mk.hint, mk.min, mk.gap, mk.drawbridge));
                    fixedHeight.Add(mk.keep ? mk.height : float.NaN);
                    levelGroup.Add(mk.level ? levels : -1);
                    roll.Add(mk.roll);
                    lastS = mk.s;
                }
            }

            // The loop closes on the first bend's start; drop a last point that would sit on it.
            int last = points.Count - 1;
            if (last > 0 && Vector2.Distance(Flat(points[last]), Flat(points[0])) < MinSpacing)
            {
                points.RemoveAt(last); fixedHeight.RemoveAt(last); levelGroup.RemoveAt(last); roll.RemoveAt(last);
                if (startIndex >= points.Count) startIndex = 0;
                for (int i = 0; i < n; i++) arcLast[i] = Mathf.Min(arcLast[i], points.Count - 1);
            }

            // Second pass: distances round the loop, then the deck heights and the banking.
            int count = points.Count;
            var dist = new float[count];
            for (int i = 1; i < count; i++) dist[i] = dist[i - 1] + Vector2.Distance(Flat(points[i - 1]), Flat(points[i]));
            float lap = dist[count - 1] + Vector2.Distance(Flat(points[count - 1]), Flat(points[0]));
            var knots = new List<int>();
            for (int i = 0; i < count; i++) if (!float.IsNaN(fixedHeight[i])) knots.Add(i);
            if (knots.Count == 0) throw new System.InvalidOperationException($"{name}: nothing fixes the deck height");
            float[] deck = DeckHeights(dist, lap, knots, fixedHeight);
            float[] bank = Banking(dist, lap, arcFirst, arcLast, turn);

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
                p.Bank = bank[i] + roll[i];
                result.Add(p);
            }

            // Start line first.
            var ordered = new List<TrackPoint>(count);
            for (int i = 0; i < count; i++) ordered.Add(result[(startIndex + i) % count]);
            return ordered;
        }

        // The deck height at every point: straight lines between the fixed heights, by distance
        // round the loop, with each change of grade rounded into a curve tangent to both lines over
        // up to MaxRounding either side (never more than half the way to the next fixed height).
        static float[] DeckHeights(float[] dist, float lap, List<int> knots, List<float> fixedHeight)
        {
            int m = knots.Count, count = dist.Length;
            var deck = new float[count];
            if (m == 1)
            {
                for (int i = 0; i < count; i++) deck[i] = fixedHeight[knots[0]];
                return deck;
            }
            var s = new float[m]; var y = new float[m]; var h = new float[m]; var grade = new float[m]; var round = new float[m];
            for (int k = 0; k < m; k++) { s[k] = dist[knots[k]]; y[k] = fixedHeight[knots[k]]; }
            for (int k = 0; k < m; k++)
            {
                int k1 = (k + 1) % m;
                h[k] = Mathf.Max((k1 == 0 ? s[0] + lap : s[k1]) - s[k], 0.01f);
                grade[k] = (y[k1] - y[k]) / h[k];
            }
            for (int k = 0; k < m; k++) round[k] = Mathf.Min(MaxRounding, 0.5f * h[(k - 1 + m) % m], 0.5f * h[k]);

            // Near knot k (x metres past it, negative before), on the curve rounding its corner.
            float Rounded(int k, float x)
            {
                float r = round[k], gIn = grade[(k - 1 + m) % m];
                return y[k] + gIn * x + (grade[k] - gIn) * (x + r) * (x + r) / (4f * r);
            }

            int last = -1;   // the last knot at or before this point; before the first, the last one round the loop
            for (int i = 0; i < count; i++)
            {
                while (last + 1 < m && s[last + 1] <= dist[i]) last++;
                int k0 = last < 0 ? m - 1 : last, k1 = (k0 + 1) % m;
                float x = dist[i] - s[k0];
                if (x < 0f) x += lap;
                if (x < round[k0]) deck[i] = Rounded(k0, x);
                else if (h[k0] - x < round[k1]) deck[i] = Rounded(k1, x - h[k0]);
                else deck[i] = y[k0] + grade[k0] * x;
            }
            return deck;
        }

        // The corners' banking at every point: full through each bend, easing in and out over its
        // run-off. Banks that overlap add, but never past the steepest of them, so an S-bend rolls
        // straight from one lean to the other.
        float[] Banking(float[] dist, float lap, int[] arcFirst, int[] arcLast, float[] turn)
        {
            int count = dist.Length, n = corners.Count;
            var bank = new float[count];
            for (int i = 0; i < count; i++)
            {
                float pos = 0f, neg = 0f, posMax = 0f, negMin = 0f;
                for (int c = 0; c < n; c++)
                {
                    float b = corners[c].bank * Mathf.Sign(turn[c]);
                    if (b == 0f) continue;
                    float s0 = dist[arcFirst[c]], s1 = dist[arcLast[c]];
                    float half = (s1 - s0) * 0.5f, hold = Mathf.Min(0.3f * (s1 - s0), 8f), runOff = RunOff(b);
                    float delta = Mathf.Repeat(dist[i] - (s0 + s1) * 0.5f + lap * 0.5f, lap) - lap * 0.5f;
                    float a = Mathf.Abs(delta), w;
                    if (a <= half - hold) w = 1f;
                    else if (a >= half + runOff) continue;
                    else w = Mathf.SmoothStep(0f, 1f, 1f - (a - (half - hold)) / (runOff + hold));
                    if (b > 0f) { pos += b * w; posMax = Mathf.Max(posMax, b); }
                    else { neg += b * w; negMin = Mathf.Min(negMin, b); }
                }
                bank[i] = Mathf.Min(pos, posMax) + Mathf.Max(neg, negMin);
            }
            return bank;
        }
    }
}
