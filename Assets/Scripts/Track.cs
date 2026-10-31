using System.Collections.Generic;
using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// A built circuit. Samples a closed Catmull-Rom spline through the definition's control
    /// points every ~2 m, then builds the road deck, kerbs, start line and scaffolding from
    /// those samples. Also answers "where am I on the track" queries for laps, AI and recovery.
    /// </summary>
    public class Track : MonoBehaviour
    {
        public const float SampleSpacing = 2f;
        public const float Width = 8f;
        public const float Thickness = 0.6f;
        const float KerbWidth = 0.7f;
        const float KerbHeight = 0.09f;   // at the outer edge; the inner edge is flush with the asphalt
        const float KerbBlock = 1.5f;     // rough length of each red or white block
        const float EdgeLine = 0.12f;     // white line painted along the inside of each kerb
        const float AiGrip = 1.15f;     // lateral g the AI plans corners with
        const float AiBraking = 7f;     // m/s^2 the AI plans braking with

        // Kerb cross-section from the inner edge out, as fractions of KerbWidth and KerbHeight.
        // It flattens off towards the top so the facets catch the light like a rounded kerb.
        static readonly Vector2[] KerbProfile =
        {
            new Vector2(0f, 0f), new Vector2(0.4f, 0.5f), new Vector2(0.8f, 0.89f), new Vector2(1f, 1f),
        };

        // Road submeshes
        const int SubAsphalt = 0, SubKerbRed = 1, SubKerbWhite = 2, SubSides = 3, SubStartDark = 4;
        // Kerb submeshes
        const int KerbRed = 0, KerbWhite = 1;

        public TrackDefinition Definition { get; private set; }
        public int Count => centers.Length;
        public float Length { get; private set; }
        public float HalfWidth => Width * 0.5f;

        Vector3[] centers, forwards, rights, ups;
        float[] distances, aiSpeeds, aiMinSpeeds;
        bool[] gaps; // no road from this sample to the next

        public int Wrap(int i)
        {
            int n = centers.Length;
            i %= n;
            return i < 0 ? i + n : i;
        }

        public Vector3 Center(int i) => centers[Wrap(i)];
        public Vector3 Forward(int i) => forwards[Wrap(i)];
        public Vector3 Right(int i) => rights[Wrap(i)];
        public Vector3 Up(int i) => ups[Wrap(i)];
        public float Distance(int i) => distances[Wrap(i)];
        public float AiSpeed(int i) => aiSpeeds[Wrap(i)];
        public float AiMinSpeed(int i) => aiMinSpeeds[Wrap(i)];
        public bool IsGap(int i) => gaps[Wrap(i)];

        /// <summary>A sample safely before any gap near <paramref name="i"/>, for dropping a car back on.</summary>
        public int SafeIndex(int i)
        {
            for (int guard = 0; guard < centers.Length; guard++)
            {
                bool nearGap = false;
                for (int k = -2; k <= 4; k++) if (IsGap(i + k)) { nearGap = true; break; }
                if (!nearGap) return Wrap(i);
                i--;
            }
            return Wrap(i);
        }
        public Quaternion Rotation(int i) => Quaternion.LookRotation(Forward(i), Up(i));

        /// <summary>Index roughly <paramref name="metres"/> further along the track.</summary>
        public int IndexAhead(int i, float metres) => Wrap(i + Mathf.Max(1, Mathf.RoundToInt(metres / SampleSpacing)));

        public int FindNearest(Vector3 p)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < centers.Length; i++)
            {
                float d = (centers[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>Nearest sample searched only around a previous index, so crossings and humps don't confuse it.</summary>
        public int FindNearest(Vector3 p, int hint, int window)
        {
            int best = Wrap(hint);
            float bestD = float.MaxValue;
            for (int o = -window; o <= window; o++)
            {
                int i = Wrap(hint + o);
                float d = (centers[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public void Build(TrackDefinition def, GameBootstrap assets)
        {
            Definition = def;
            SampleSpline(def);
            ComputeAiSpeeds();
            BuildRoad(assets);
            BuildScaffolding(assets);
            Scenery.BuildStadium(this, assets);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        void SampleSpline(TrackDefinition def)
        {
            var pts = def.Points;
            int n = pts.Length;
            var cs = new List<Vector3>();
            var banks = new List<float>();
            var hints = new List<float>();
            var mins = new List<float>();
            var gapList = new List<bool>();

            for (int i = 0; i < n; i++)
            {
                TrackPoint p0 = pts[(i - 1 + n) % n], p1 = pts[i], p2 = pts[(i + 1) % n], p3 = pts[(i + 2) % n];
                float segLen = Vector3.Distance(p1.Position, p2.Position);
                int steps = Mathf.Max(2, Mathf.CeilToInt(segLen / SampleSpacing));
                for (int s = 0; s < steps; s++)
                {
                    float t = s / (float)steps;
                    float st = Mathf.SmoothStep(0f, 1f, t);
                    cs.Add(CatmullRom(p0.Position, p1.Position, p2.Position, p3.Position, t));
                    banks.Add(Mathf.Lerp(p1.Bank, p2.Bank, st));
                    hints.Add(Mathf.Min(p1.SpeedHint, p2.SpeedHint));
                    mins.Add(p1.MinSpeed);
                    gapList.Add(p1.Gap);
                }
            }

            int count = cs.Count;
            centers = cs.ToArray();
            forwards = new Vector3[count];
            rights = new Vector3[count];
            ups = new Vector3[count];
            distances = new float[count];
            aiSpeeds = hints.ToArray();
            aiMinSpeeds = mins.ToArray();
            gaps = gapList.ToArray();

            for (int i = 0; i < count; i++)
            {
                Vector3 fwd = (centers[(i + 1) % count] - centers[(i - 1 + count) % count]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                Vector3 up = Vector3.Cross(fwd, right);
                up = Quaternion.AngleAxis(-banks[i], fwd) * up;
                forwards[i] = fwd;
                ups[i] = up;
                rights[i] = Vector3.Cross(up, fwd).normalized;
                if (i > 0) distances[i] = distances[i - 1] + Vector3.Distance(centers[i - 1], centers[i]);
            }
            Length = distances[count - 1] + Vector3.Distance(centers[count - 1], centers[0]);
        }

        void ComputeAiSpeeds()
        {
            int n = centers.Length;
            const int k = 4; // measure curvature over +/- 8 m
            for (int i = 0; i < n; i++)
            {
                Vector3 a = Center(i - k), b = Center(i), c = Center(i + k);
                // Horizontal radius from the circumcircle of three points.
                Vector2 A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), C = new Vector2(c.x, c.z);
                float ab = (A - B).magnitude, bc = (B - C).magnitude, ca = (C - A).magnitude;
                float cross = Mathf.Abs((B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x));
                float radius = cross < 0.0001f ? 10000f : (ab * bc * ca) / (2f * cross);
                float vCorner = Mathf.Sqrt(AiGrip * 9.81f * radius);
                aiSpeeds[i] = Mathf.Min(aiSpeeds[i], vCorner, 80f);
            }
            // Brake in time: walk backwards (twice round to cover the wrap).
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = n - 1; i >= 0; i--)
                {
                    int next = (i + 1) % n;
                    float ds = Vector3.Distance(centers[i], centers[next]);
                    float vMax = Mathf.Sqrt(aiSpeeds[next] * aiSpeeds[next] + 2f * AiBraking * ds);
                    if (aiSpeeds[i] > vMax) aiSpeeds[i] = vMax;
                }
            }
        }

        Vector3 Pt(int i, float lateral, float vertical) => Center(i) + Right(i) * lateral + Up(i) * vertical;

        // A point part of the way (t = 0..1) from sample i to the next.
        Vector3 Pt(int i, float t, float lateral, float vertical) => Vector3.Lerp(Pt(i, lateral, vertical), Pt(i + 1, lateral, vertical), t);

        // The top of the deck across its width, left edge to right: flat asphalt, with the kerbs
        // sloping up to the edges. x is the lateral offset and y the height.
        static Vector2[] DeckTop()
        {
            float inner = Width * 0.5f - KerbWidth;
            var top = new List<Vector2>();
            for (int k = KerbProfile.Length - 1; k >= 0; k--)
                top.Add(new Vector2(-inner - KerbProfile[k].x * KerbWidth, KerbProfile[k].y * KerbHeight));
            foreach (var p in KerbProfile)
                top.Add(new Vector2(inner + p.x * KerbWidth, p.y * KerbHeight));
            return top.ToArray();
        }

        void BuildRoad(GameBootstrap assets)
        {
            int n = centers.Length;
            float hw = HalfWidth;
            float inner = hw - KerbWidth;
            float line = inner - EdgeLine;
            float lineU = EdgeLine / (2f * inner);
            var top = DeckTop();
            var mb = new MeshBuilder(5);
            var kerbs = new MeshBuilder(2);
            // Blocks are cut by distance round the lap, and there's an even number of them, so they
            // are all the same length and red and white alternate all the way round.
            float block = Length / (2 * Mathf.Max(1, Mathf.RoundToInt(Length / (2f * KerbBlock))));

            for (int i = 0; i < n; i++)
            {
                int j = i + 1;

                if (gaps[i])
                {
                    // End faces where the road stops and starts again, following the kerbs' slope.
                    for (int k = 0; k + 1 < top.Length; k++)
                    {
                        Vector2 a = top[k], b = top[k + 1];
                        if (!IsGap(i - 1))
                            mb.AddQuad(SubSides, Pt(i, a.x, -Thickness), Pt(i, b.x, -Thickness), Pt(i, b.x, b.y), Pt(i, a.x, a.y));
                        if (!IsGap(j))
                            mb.AddQuad(SubSides, Pt(j, a.x, -Thickness), Pt(j, a.x, a.y), Pt(j, b.x, b.y), Pt(j, b.x, -Thickness));
                    }
                    continue;
                }

                float d0 = distances[i], d1 = j < n ? distances[j] : Length;
                float v0 = d0 / 8f, v1 = d1 / 8f;

                // Asphalt between the edge lines (u across the road, v along it).
                mb.AddQuad(SubAsphalt, Pt(i, -line, 0), Pt(j, -line, 0), Pt(j, line, 0), Pt(i, line, 0),
                    new Vector2(lineU, v0), new Vector2(lineU, v1), new Vector2(1 - lineU, v1), new Vector2(1 - lineU, v0));
                mb.AddQuad(SubKerbWhite, Pt(i, -inner, 0), Pt(j, -inner, 0), Pt(j, -line, 0), Pt(i, -line, 0));
                mb.AddQuad(SubKerbWhite, Pt(i, line, 0), Pt(j, line, 0), Pt(j, inner, 0), Pt(i, inner, 0));

                // Kerbs, split wherever a block ends.
                for (float t0 = 0f; t0 < 1f;)
                {
                    int blk = Mathf.FloorToInt(Mathf.Lerp(d0, d1, t0) / block + 0.001f);
                    float t1 = Mathf.Min(1f, ((blk + 1) * block - d0) / (d1 - d0));
                    AddKerbs(kerbs, i, t0, t1, (blk & 1) == 0 ? KerbRed : KerbWhite);
                    t0 = t1;
                }

                // Side walls and underside.
                mb.AddQuad(SubSides, Pt(i, hw, KerbHeight), Pt(j, hw, KerbHeight), Pt(j, hw, -Thickness), Pt(i, hw, -Thickness));
                mb.AddQuad(SubSides, Pt(i, -hw, -Thickness), Pt(j, -hw, -Thickness), Pt(j, -hw, KerbHeight), Pt(i, -hw, KerbHeight));
                mb.AddQuad(SubSides, Pt(i, hw, -Thickness), Pt(j, hw, -Thickness), Pt(j, -hw, -Thickness), Pt(i, -hw, -Thickness));
            }

            // The kerbs get their own collider so wheels can tell when they're riding them.
            var kerbMesh = kerbs.ToMesh("Kerbs");
            var kerbGo = new GameObject("Kerbs");
            kerbGo.transform.SetParent(transform, false);
            kerbGo.AddComponent<MeshFilter>().sharedMesh = kerbMesh;
            kerbGo.AddComponent<MeshRenderer>().sharedMaterials = new[] { assets.kerbRed, assets.kerbWhite };
            var kerbCollider = kerbGo.AddComponent<MeshCollider>();
            kerbCollider.sharedMesh = kerbMesh;
            kerbCollider.sharedMaterial = assets.RoadPhysics;
            kerbGo.AddComponent<KerbSurface>();

            var collisionMesh = mb.ToMesh("RoadCollision");

            // Chequered start line, drawn just above the deck and kept out of the collider.
            const int cols = 8;
            float lift = 0.015f;
            for (int row = 0; row < 2; row++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float l0 = Mathf.Lerp(-inner, inner, c / (float)cols);
                    float l1 = Mathf.Lerp(-inner, inner, (c + 1) / (float)cols);
                    Vector3 f0 = Forward(0) * (row * 0.9f), f1 = Forward(0) * ((row + 1) * 0.9f);
                    int sub = ((row + c) & 1) == 0 ? SubStartDark : SubKerbWhite;
                    mb.AddQuad(sub, Pt(0, l0, lift) + f0, Pt(0, l0, lift) + f1, Pt(0, l1, lift) + f1, Pt(0, l1, lift) + f0);
                }
            }

            var road = new GameObject("Road");
            road.transform.SetParent(transform, false);
            road.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Road");
            var mr = road.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { assets.AsphaltRuntime, assets.kerbRed, assets.kerbWhite, assets.concrete, assets.darkMetal };
            var mc = road.AddComponent<MeshCollider>();
            mc.sharedMesh = collisionMesh;
            mc.sharedMaterial = assets.RoadPhysics;
        }

        // Both kerbs over part (t0..t1) of the stretch from sample i to the next, one quad per facet.
        void AddKerbs(MeshBuilder mb, int i, float t0, float t1, int sub)
        {
            float inner = HalfWidth - KerbWidth;
            for (int k = 0; k + 1 < KerbProfile.Length; k++)
            {
                float la = inner + KerbProfile[k].x * KerbWidth, ha = KerbProfile[k].y * KerbHeight;
                float lb = inner + KerbProfile[k + 1].x * KerbWidth, hb = KerbProfile[k + 1].y * KerbHeight;
                mb.AddQuad(sub, Pt(i, t0, la, ha), Pt(i, t1, la, ha), Pt(i, t1, lb, hb), Pt(i, t0, lb, hb));
                mb.AddQuad(sub, Pt(i, t0, -lb, hb), Pt(i, t1, -lb, hb), Pt(i, t1, -la, ha), Pt(i, t0, -la, ha));
            }
        }

        void BuildScaffolding(GameBootstrap assets)
        {
            int n = centers.Length;
            var mb = new MeshBuilder(1);
            float inset = HalfWidth - 0.5f;
            bool havePrev = false;
            Vector3 prevBotL = default, prevBotR = default;

            for (int i = 0; i < n; i += 3)
            {
                Vector3 topL = Pt(i, -inset, -Thickness);
                Vector3 topR = Pt(i, inset, -Thickness);
                if (topL.y < 1f || topR.y < 1f || gaps[i] || IsGap(i - 1) || OverLowerDeck(i)) { havePrev = false; continue; }

                Vector3 botL = new Vector3(topL.x, 0f, topL.z);
                Vector3 botR = new Vector3(topR.x, 0f, topR.z);

                mb.AddBeam(0, botL, topL, 0.32f);
                mb.AddBeam(0, botR, topR, 0.32f);
                mb.AddBeam(0, topL, topR, 0.28f);

                float top = Mathf.Min(topL.y, topR.y) - 0.6f;
                for (float h = 2.5f; h < top; h += 3f)
                    mb.AddBeam(0, new Vector3(topL.x, h, topL.z), new Vector3(topR.x, h, topR.z), 0.14f);

                // Footings.
                mb.AddBox(0, Matrix4x4.TRS(botL + Vector3.up * 0.15f, Quaternion.identity, new Vector3(0.9f, 0.3f, 0.9f)));
                mb.AddBox(0, Matrix4x4.TRS(botR + Vector3.up * 0.15f, Quaternion.identity, new Vector3(0.9f, 0.3f, 0.9f)));

                if (havePrev)
                {
                    mb.AddBeam(0, prevBotL, topL, 0.12f);
                    mb.AddBeam(0, prevBotR, topR, 0.12f);
                }
                prevBotL = botL;
                prevBotR = botR;
                havePrev = true;
            }

            var go = new GameObject("Scaffolding");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Scaffolding");
            go.AddComponent<MeshRenderer>().sharedMaterial = assets.scaffold;
        }

        // True where this part of the deck passes over another part of the track (the
        // Roller Coaster crossover), so its scaffolding would stand on the road below.
        bool OverLowerDeck(int i)
        {
            int n = centers.Length;
            Vector3 c = centers[i];
            float clear = HalfWidth + 2.5f;
            for (int k = 0; k < n; k++)
            {
                int along = Mathf.Abs(k - i);
                if (Mathf.Min(along, n - along) < 25) continue;
                Vector3 o = centers[k];
                if (o.y > c.y - 3f) continue;
                float dx = o.x - c.x, dz = o.z - c.z;
                if (dx * dx + dz * dz < clear * clear) return true;
            }
            return false;
        }
    }

    /// <summary>Marks the kerbs' collider, so a wheel can tell when it's riding a kerb.</summary>
    public class KerbSurface : MonoBehaviour { }
}
