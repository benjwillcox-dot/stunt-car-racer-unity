using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace StuntCar
{
    /// <summary>
    /// What fills the grass inside the stadium, laid out round each track so nothing sits under
    /// the deck: tyre walls on the outside of the bends, hay bales under the jumps and marshal huts
    /// along the way; the paddock (garages, transporters, tents) by the start straight; a fan zone
    /// with food stalls, flags, people and a big screen showing the race; and the game's name
    /// painted on the grass. The grass itself is mown in stripes (see GameBootstrap.LawnRuntime).
    /// </summary>
    public static class Infield
    {
        const int SubProps = 0, SubAds = 1;
        const string Logo = "STUNT CAR RACER";

        public static void Build(Track track, GameBootstrap a, Transform parent, float minX, float maxX, float minZ, float maxZ)
        {
            var map = new FreeSpace(track, minX, maxX, minZ, maxZ);
            var mb = new MeshBuilder(2);
            var rng = new System.Random(5);
            var middle = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);

            TrackFurniture(mb, track, map, middle);
            Paddock(mb, track, map, middle, rng);
            PaintedLogo(mb, map, middle);
            FanZone(mb, track, map, middle, rng, parent, a);

            var mesh = mb.ToMesh("Infield");
            var go = new GameObject("Infield");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { a.PropsRuntime, a.AdvertsRuntime };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // ------------------------------------------------------------ track furniture

        // Tyre walls outside the bends and either side of the jumps, hay bales on the ground under
        // each jump, and a marshal hut with a flag every 110 m or so on the inside of the track.
        static void TrackFurniture(MeshBuilder mb, Track t, FreeSpace map, Vector3 middle)
        {
            int n = t.Count;
            float hw = t.HalfWidth;
            for (int i = 0; i < n; i += 6)
            {
                if (t.IsGap(i) || t.IsGap(i - 1) || t.Center(i).y < 2f) continue;
                Vector3 fwd = Flat(t.Forward(i)), right = Flat(t.Right(i));
                float turn = Vector3.Cross(t.Forward(i - 4), t.Forward(i + 4)).y;
                bool nearJump = false;
                for (int k = -10; k <= 10 && !nearJump; k++) nearJump = t.IsGap(i + k);
                foreach (float side in new[] { -1f, 1f })
                {
                    // A right-hand bend (turn > 0) throws cars off to the left.
                    bool outside = Mathf.Abs(turn) > 0.04f && side == (turn > 0f ? -1f : 1f);
                    if (!outside && !nearJump) continue;
                    Vector3 p = Ground(t.Center(i) + right * (side * (hw + 1.6f)));
                    Box(mb, p + Vector3.up * 0.6f, Quaternion.LookRotation(fwd), new Vector3(0.7f, 1.2f, 4f), ProceduralTextures.TyreWallUV);
                }
            }

            // Hay bales under the jumps (not the drawbridges), two deep at every other row.
            for (int i = 0; i < n; i++)
            {
                if (!t.IsGap(i) || t.IsDrawbridge(i)) continue;
                Vector3 fwd = Flat(t.Forward(i)), right = Flat(t.Right(i));
                var rot = Quaternion.LookRotation(fwd);
                for (float lat = -hw + 0.7f; lat <= hw - 0.6f; lat += 1.3f)
                {
                    Vector3 p = Ground(t.Center(i) + right * lat);
                    Box(mb, p + Vector3.up * 0.4f, rot, new Vector3(1.2f, 0.8f, 0.9f), ProceduralTextures.HayUV);
                    if ((i & 1) == 0) Box(mb, p + Vector3.up * 1.2f, rot, new Vector3(1.2f, 0.8f, 0.9f), ProceduralTextures.HayUV);
                }
            }

            // Marshal huts on the inside of the track.
            for (int i = 10; i < n; i += 55)
            {
                Vector3 right = Flat(t.Right(i));
                float inward = Mathf.Sign(Vector3.Dot(middle - t.Center(i), right));
                Vector3 p = Ground(t.Center(i) + right * (inward * (hw + 7f)));
                if (map.Clearance(p) < 4f || !map.Fits(p, 1.5f, 1.5f, 1f)) continue;
                map.Take(p, 3f, 3f);
                var rot = Quaternion.LookRotation(-right * inward);
                Box(mb, p + Vector3.up * 1.2f, rot, new Vector3(2.2f, 2.4f, 2.2f), ProceduralTextures.PalOrange);
                Box(mb, p + Vector3.up * 1.75f, rot, new Vector3(2.24f, 0.6f, 2.24f), ProceduralTextures.PalGlass);
                Box(mb, p + Vector3.up * 2.5f, rot, new Vector3(2.7f, 0.2f, 2.7f), ProceduralTextures.PalWhite);
                Vector3 pole = p + rot * new Vector3(1.6f, 0f, 0.8f);
                mb.AddBeam(SubProps, pole, pole + Vector3.up * 4.6f, 0.08f, ProceduralTextures.PaletteUV(ProceduralTextures.PalLightGrey));
                Box(mb, pole + Vector3.up * 4.25f + rot * new Vector3(0.5f, 0f, 0f), rot, new Vector3(1f, 0.65f, 0.03f), ProceduralTextures.PalYellow);
            }
        }

        // ------------------------------------------------------------ paddock

        // Pit garages along the back of a tarmac apron facing the start straight, two team
        // transporters, pop-up tents and piles of tyres.
        static void Paddock(MeshBuilder mb, Track t, FreeSpace map, Vector3 middle, System.Random rng)
        {
            Vector3 right = Flat(t.Right(0));
            float inward = Mathf.Sign(Vector3.Dot(middle - t.Center(0), right));
            bool alongX = Mathf.Abs(t.Forward(0).x) >= Mathf.Abs(t.Forward(0).z);
            float hx = alongX ? 34f : 16f, hz = alongX ? 16f : 34f;
            if (!map.Find(Ground(t.Center(0) + right * (inward * 30f)), hx, hz, 4f, out Vector3 at)) return;
            map.Take(at, hx, hz);

            // Local frame: x along the apron, z towards the track.
            Vector3 toTrack = alongX ? new Vector3(0f, 0f, Mathf.Sign(t.Center(0).z - at.z)) : new Vector3(Mathf.Sign(t.Center(0).x - at.x), 0f, 0f);
            var rot = Quaternion.LookRotation(toTrack);
            float half = 34f, deep = 16f;
            Vector3 L(float x, float y, float z) => at + rot * new Vector3(x, y, z);

            Box(mb, L(0, 0.06f, 0), rot, new Vector3(half * 2f, 0.12f, deep * 2f), ProceduralTextures.PalTarmac);

            // Garages: six bays, roller shutters, a fascia in the team colours.
            const int bays = 6;
            const float bay = 6.5f, gDepth = 9f, gHeight = 4.5f;
            float gz = -deep + 0.5f + gDepth * 0.5f, front = gz + gDepth * 0.5f;
            Box(mb, L(0, gHeight * 0.5f, gz), rot, new Vector3(bays * bay, gHeight, gDepth), ProceduralTextures.PalConcrete);
            Box(mb, L(0, gHeight + 0.15f, gz + 0.6f), rot, new Vector3(bays * bay + 1f, 0.3f, gDepth + 1.4f), ProceduralTextures.PalWhite);
            for (int b = 0; b < bays; b++)
            {
                float x = (b - (bays - 1) * 0.5f) * bay;
                Box(mb, L(x, 1.85f, front + 0.05f), rot, new Vector3(5f, 3.6f, 0.1f), ProceduralTextures.ShutterUV);
                Box(mb, L(x, 4.1f, front + 0.08f), rot, new Vector3(bay, 0.7f, 0.12f), (b & 1) == 0 ? ProceduralTextures.PalYellow : ProceduralTextures.PalTeal);
            }

            // Transporters parked in front of the garages, sponsors down their sides.
            for (int k = 0; k < 2; k++)
            {
                float x = k == 0 ? -12f : 12f, z = front + 5f;
                int team = k == 0 ? ProceduralTextures.PalYellow : ProceduralTextures.PalTeal;
                Box(mb, L(x, 2.4f, z), rot, new Vector3(13.6f, 3.6f, 2.6f), team);
                Box(mb, L(x + (k == 0 ? -8.1f : 8.1f), 1.9f, z), rot, new Vector3(2.4f, 2.8f, 2.5f), ProceduralTextures.PalWhite);
                Box(mb, L(x + (k == 0 ? -9.32f : 9.32f), 2.5f, z), rot * Quaternion.Euler(0, 90, 0), new Vector3(2.1f, 1.0f, 0.04f), ProceduralTextures.PalGlass);
                for (int w = 0; w < 4; w++)
                    foreach (float s in new[] { -1f, 1f })
                        Box(mb, L(x - 5f + w * 3.2f + (k == 0 ? -1.5f : 1.5f), 0.5f, z + s * 1.15f), rot, new Vector3(1f, 1f, 0.4f), ProceduralTextures.PalBlack);
                Advert(mb, L(x, 2.6f, z + 1.33f), rot * Vector3.forward, 11f, 2.75f, Scenery.NextAd());
                Advert(mb, L(x, 2.6f, z - 1.33f), rot * Vector3.back, 11f, 2.75f, Scenery.NextAd());
            }

            // Pop-up tents and piles of tyres along the front of the apron.
            for (int k = 0; k < 3; k++)
            {
                Vector3 c = L((k - 1) * 14f, 0, deep - 5f);
                Tent(mb, c, rot, (k & 1) == 0 ? ProceduralTextures.PalYellow : ProceduralTextures.PalTeal);
                for (int s = 0; s < 4; s++)
                {
                    Vector3 p = c + rot * new Vector3(3.6f + (s % 2) * 0.75f, 0f, -1f + (s / 2) * 0.75f);
                    Box(mb, p + Vector3.up * 0.5f, rot, new Vector3(0.7f, 1f, 0.7f), new Rect(0.5f, 0.75f, 0.125f, 0.2f));
                }
            }
        }

        // A 4 m pop-up gazebo: four legs, a pitched roof and a valance.
        static void Tent(MeshBuilder mb, Vector3 c, Quaternion rot, int colour)
        {
            const float h = 2.6f, r = 2f, top = 0.3f, peak = 3.5f;
            Vector3 L(float x, float y, float z) => c + rot * new Vector3(x, y, z);
            var steel = ProceduralTextures.PaletteUV(ProceduralTextures.PalLightGrey);
            foreach (var (x, z) in new[] { (-r, -r), (r, -r), (r, r), (-r, r) }) mb.AddBeam(SubProps, L(x, 0, z), L(x, h, z), 0.07f, steel);
            Vector3[] eave = { L(-r, h, -r), L(r, h, -r), L(r, h, r), L(-r, h, r) };
            Vector3[] peakRing = { L(-top, peak, -top), L(top, peak, -top), L(top, peak, top), L(-top, peak, top) };
            var uv = new Rect(ProceduralTextures.PaletteUV(colour), Vector2.zero);
            for (int s = 0; s < 4; s++)
            {
                int e = (s + 1) % 4;
                Vector3 outward = ((eave[s] + eave[e]) * 0.5f - c); outward.y = 0f;
                Face(mb, SubProps, eave[s], peakRing[s], peakRing[e], eave[e], outward + Vector3.up, uv);
                Vector3 mid = (eave[s] + eave[e]) * 0.5f - Vector3.up * 0.17f;
                Box(mb, mid, Quaternion.LookRotation(outward.normalized), new Vector3(2f * r, 0.34f, 0.04f), colour);
            }
            Face(mb, SubProps, peakRing[0], peakRing[3], peakRing[2], peakRing[1], Vector3.up, uv);
        }

        // ------------------------------------------------------------ painted logo

        // The game's name painted on the grass near the middle, reading left to right from the
        // main stand, inside a yellow border.
        static void PaintedLogo(MeshBuilder mb, FreeSpace map, Vector3 middle)
        {
            for (float s = 1.2f; s >= 0.5f; s -= 0.1f)
            {
                float hx = PixelFont.Width(Logo, s) * 0.5f + 3f, hz = 3.5f * s + 2.5f;
                if (!map.Find(middle, hx, hz, 3f, out Vector3 at)) continue;
                map.Take(at, hx, hz);
                Vector3 origin = at - new Vector3(PixelFont.Width(Logo, s) * 0.5f, 0f, 3.5f * s);
                PixelFont.ForEachPixel(Logo, (x, y) =>
                    Box(mb, origin + new Vector3((x + 0.5f) * s, 0.08f, (y + 0.5f) * s), Quaternion.identity, new Vector3(s * 0.96f, 0.06f, s * 0.96f), ProceduralTextures.PalWhite));
                float bx = hx - 1.2f, bz = hz - 1.2f;
                Box(mb, at + new Vector3(0, 0.08f, -bz), Quaternion.identity, new Vector3(bx * 2f, 0.06f, 0.6f), ProceduralTextures.PalYellow);
                Box(mb, at + new Vector3(0, 0.08f, bz), Quaternion.identity, new Vector3(bx * 2f, 0.06f, 0.6f), ProceduralTextures.PalYellow);
                Box(mb, at + new Vector3(-bx, 0.08f, 0), Quaternion.identity, new Vector3(0.6f, 0.06f, bz * 2f), ProceduralTextures.PalYellow);
                Box(mb, at + new Vector3(bx, 0.08f, 0), Quaternion.identity, new Vector3(0.6f, 0.06f, bz * 2f), ProceduralTextures.PalYellow);
                return;
            }
        }

        // ------------------------------------------------------------ fan zone

        // Food stalls either side, flags round the edge, people about, and a big screen at the back
        // showing a TV feed of the race (BigScreen).
        static void FanZone(MeshBuilder mb, Track t, FreeSpace map, Vector3 middle, System.Random rng, Transform parent, GameBootstrap a)
        {
            const float hx = 22f, hz = 16f;
            if (!map.Find(middle + new Vector3(0f, 0f, 25f), hx, hz, 5f, out Vector3 at)) return;
            map.Take(at, hx, hz);
            // Face the nearest bit of track, so the screen can be seen from it.
            Vector3 toTrack = Flat(t.Center(t.FindNearest(at)) - at);
            var rot = Quaternion.LookRotation(toTrack);
            Vector3 L(float x, float y, float z) => at + rot * new Vector3(x, y, z);
            float R() => (float)rng.NextDouble();

            Box(mb, L(0, 0.05f, 0), rot, new Vector3(hx * 2f, 0.1f, hz * 2f), ProceduralTextures.PalTan);

            // Big screen on two legs, with a sponsor's board between them.
            const float sw = 14f, sh = 8f, sy = 4.2f;
            float sz = -hz + 2f;
            var dark = ProceduralTextures.PaletteUV(ProceduralTextures.PalBlack);
            mb.AddBeam(SubProps, L(-sw * 0.35f, 0, sz - 0.6f), L(-sw * 0.35f, sy + sh, sz - 0.6f), 0.5f, dark);
            mb.AddBeam(SubProps, L(sw * 0.35f, 0, sz - 0.6f), L(sw * 0.35f, sy + sh, sz - 0.6f), 0.5f, dark);
            Box(mb, L(0, sy + sh * 0.5f, sz - 0.25f), rot, new Vector3(sw + 0.8f, sh + 0.8f, 0.5f), ProceduralTextures.PalBlack);
            Advert(mb, L(0, sy * 0.5f + 0.4f, sz - 0.3f), rot * Vector3.forward, 8f, 2f, Scenery.NextAd());
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Big Screen";
            Object.Destroy(screen.GetComponent<Collider>());
            screen.transform.SetParent(parent, false);
            screen.transform.SetPositionAndRotation(L(0, sy + sh * 0.5f, sz + 0.02f), Quaternion.LookRotation(-toTrack));
            screen.transform.localScale = new Vector3(sw, sh, 1f);
            screen.AddComponent<BigScreen>().Init(middle, a);

            // Food stalls down both sides, each with a striped awning and a sign.
            int[] food = { 7, 12, 2, 14 };
            for (int k = 0; k < 4; k++)
            {
                float side = k < 2 ? -1f : 1f, z = (k % 2 == 0) ? -3f : 6f;
                Vector3 c = L(side * (hx - 3f), 0, z);
                var face = rot * Quaternion.Euler(0, side < 0 ? 90f : -90f, 0);   // facing into the zone
                Box(mb, c + Vector3.up * 1.2f, face, new Vector3(3.2f, 2.4f, 2.2f), k % 2 == 0 ? ProceduralTextures.PalWhite : ProceduralTextures.PalTeal);
                Box(mb, c + face * new Vector3(0, 0.5f, 1.35f), face, new Vector3(3.2f, 1f, 0.5f), ProceduralTextures.PalBrown);
                Vector3 a0 = c + face * new Vector3(-1.7f, 2.4f, 1.1f), a1 = c + face * new Vector3(1.7f, 2.4f, 1.1f);
                Vector3 b0 = c + face * new Vector3(-1.7f, 1.95f, 2.4f), b1 = c + face * new Vector3(1.7f, 1.95f, 2.4f);
                Face(mb, SubProps, a0, b0, b1, a1, face * Vector3.up, (k & 1) == 0 ? ProceduralTextures.AwningRedUV : ProceduralTextures.AwningTeamUV);
                Face(mb, SubProps, a0, a1, b1, b0, face * Vector3.down, ProceduralTextures.AwningRedUV);
                Advert(mb, c + face * new Vector3(0, 2.8f, 1.12f), face * Vector3.forward, 3.2f, 0.8f, food[k]);
            }

            // Flags round the edge, in the two teams' colours.
            for (int k = 0; k < 8; k++)
            {
                float x = (k % 4 - 1.5f) * (hx * 0.6f), z = k < 4 ? hz - 1f : -hz + 6f;
                Vector3 p = L(x, 0, z);
                mb.AddBeam(SubProps, p, p + Vector3.up * 6.5f, 0.1f, ProceduralTextures.PaletteUV(ProceduralTextures.PalLightGrey));
                Box(mb, p + Vector3.up * 5.8f + rot * new Vector3(0.8f, 0, 0), rot, new Vector3(1.6f, 1.1f, 0.03f), (k & 1) == 0 ? ProceduralTextures.PalYellow : ProceduralTextures.PalTeal);
            }

            // People: groups in front of the screen and round the stalls.
            for (int k = 0; k < 70; k++)
            {
                Vector3 p, look;
                if (k < 40)
                {
                    p = L((R() - 0.5f) * hx * 1.2f, 0, sz + 6f + R() * hz * 1.1f);
                    look = -toTrack;   // watching the screen
                }
                else
                {
                    int stall = k % 4;
                    float side = stall < 2 ? -1f : 1f, z = (stall % 2 == 0) ? -3f : 6f;
                    p = L(side * (hx - 7f - R() * 3f), 0, z + (R() - 0.5f) * 4f);
                    look = Flat(L(side * (hx - 3f), 0, z) - p);   // queueing at the stall
                }
                var facing = Quaternion.LookRotation(look) * Quaternion.Euler(0, (R() - 0.5f) * 60f, 0);
                float tall = 0.92f + R() * 0.16f;
                int shirt = ProceduralTextures.PalShirt0 + rng.Next(ProceduralTextures.PalShirtCount);
                int skin = ProceduralTextures.PalSkin0 + rng.Next(ProceduralTextures.PalSkinCount);
                int legs = R() < 0.6f ? ProceduralTextures.PalJeans : ProceduralTextures.PalHair0 + rng.Next(ProceduralTextures.PalHairCount);
                Box(mb, p + Vector3.up * 0.44f * tall, facing, new Vector3(0.36f, 0.88f, 0.22f) * tall, legs);
                Box(mb, p + Vector3.up * 1.18f * tall, facing, new Vector3(0.46f, 0.62f, 0.26f) * tall, shirt);
                Box(mb, p + Vector3.up * 1.62f * tall, facing, new Vector3(0.23f, 0.26f, 0.24f) * tall, skin);
            }
        }

        // ------------------------------------------------------------ helpers

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }

        static Vector3 Ground(Vector3 p) => new Vector3(p.x, 0f, p.z);

        static void Box(MeshBuilder mb, Vector3 centre, Quaternion rot, Vector3 size, int palette) =>
            mb.AddBox(SubProps, Matrix4x4.TRS(centre, rot, size), ProceduralTextures.PaletteUV(palette));

        static void Box(MeshBuilder mb, Vector3 centre, Quaternion rot, Vector3 size, Rect uv) =>
            mb.AddBox(SubProps, Matrix4x4.TRS(centre, rot, size), uv);

        // A quad that faces 'outward', whichever way round its corners are given.
        static void Face(MeshBuilder mb, int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Rect uv)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) (b, d) = (d, b);
            mb.AddQuad(sub, a, b, c, d,
                new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin));
        }

        // A sponsor's board facing horizontal direction n, centred on 'centre'.
        static void Advert(MeshBuilder mb, Vector3 centre, Vector3 n, float width, float height, int slot)
        {
            Vector3 right = Vector3.Cross(Vector3.up, -n).normalized * width, up = Vector3.up * height;
            Scenery.AdPanel(mb, SubAds, centre - right * 0.5f - up * 0.5f, right, up, slot);
        }

        /// <summary>
        /// A coarse grid over the arena floor recording how far each cell is from the deck, and
        /// which cells are already used, for finding room for things.
        /// </summary>
        class FreeSpace
        {
            const float Cell = 4f;
            readonly float x0, z0;
            readonly int nx, nz;
            readonly float[] clear;
            readonly bool[] taken;

            public FreeSpace(Track t, float minX, float maxX, float minZ, float maxZ)
            {
                x0 = minX; z0 = minZ;
                nx = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / Cell));
                nz = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / Cell));
                clear = new float[nx * nz];
                taken = new bool[nx * nz];
                for (int j = 0; j < nz; j++)
                {
                    for (int i = 0; i < nx; i++)
                    {
                        float cx = x0 + (i + 0.5f) * Cell, cz = z0 + (j + 0.5f) * Cell, best = float.MaxValue;
                        for (int s = 0; s < t.Count; s++)
                        {
                            Vector3 c = t.Center(s);
                            float dx = c.x - cx, dz = c.z - cz;
                            best = Mathf.Min(best, dx * dx + dz * dz);
                        }
                        clear[j * nx + i] = Mathf.Sqrt(best) - t.HalfWidth;
                    }
                }
            }

            public float Clearance(Vector3 p)
            {
                int i = Mathf.FloorToInt((p.x - x0) / Cell), j = Mathf.FloorToInt((p.z - z0) / Cell);
                return i < 0 || j < 0 || i >= nx || j >= nz ? -1f : clear[j * nx + i];
            }

            // Every cell an axis-aligned rectangle touches is inside the arena, this far from the deck and free.
            public bool Fits(Vector3 c, float hx, float hz, float clearance)
            {
                int i0 = Mathf.FloorToInt((c.x - hx - x0) / Cell), i1 = Mathf.FloorToInt((c.x + hx - x0) / Cell);
                int j0 = Mathf.FloorToInt((c.z - hz - z0) / Cell), j1 = Mathf.FloorToInt((c.z + hz - z0) / Cell);
                if (i0 < 0 || j0 < 0 || i1 >= nx || j1 >= nz) return false;
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                        if (taken[j * nx + i] || clear[j * nx + i] < clearance + Cell * 0.71f) return false;
                return true;
            }

            public void Take(Vector3 c, float hx, float hz)
            {
                int i0 = Mathf.Max(0, Mathf.FloorToInt((c.x - hx - x0) / Cell)), i1 = Mathf.Min(nx - 1, Mathf.FloorToInt((c.x + hx - x0) / Cell));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((c.z - hz - z0) / Cell)), j1 = Mathf.Min(nz - 1, Mathf.FloorToInt((c.z + hz - z0) / Cell));
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                        taken[j * nx + i] = true;
            }

            // The free spot for a rectangle nearest to 'near', trying cell centres outwards.
            public bool Find(Vector3 near, float hx, float hz, float clearance, out Vector3 at)
            {
                var order = new List<(float d, Vector3 p)>();
                for (int j = 0; j < nz; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        var p = new Vector3(x0 + (i + 0.5f) * Cell, 0f, z0 + (j + 0.5f) * Cell);
                        order.Add(((p - near).sqrMagnitude, p));
                    }
                order.Sort((a, b) => a.d.CompareTo(b.d));
                foreach (var (_, p) in order)
                    if (Fits(p, hx, hz, clearance)) { at = p; return true; }
                at = near;
                return false;
            }
        }
    }

    /// <summary>
    /// The fan zone's big screen: a low-resolution TV camera that follows your car from a
    /// broadcast angle (or circles the arena when nobody's racing), shown on the screen.
    /// </summary>
    public class BigScreen : MonoBehaviour
    {
        const int ScreenLayer = 4;   // "Water": the TV camera skips it, so the screen never films itself

        Camera tv;
        RenderTexture feed;
        Material mat;
        Vector3 arena, velocity;

        public void Init(Vector3 arenaCentre, GameBootstrap a)
        {
            arena = arenaCentre;
            gameObject.layer = ScreenLayer;
            feed = new RenderTexture(384, 216, 16) { name = "Big screen feed" };
            mat = new Material(a.floodlight) { name = "Big screen (runtime)" };
            mat.SetTexture("_BaseMap", feed);
            mat.SetTexture("_EmissionMap", feed);
            mat.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.15f));
            mat.SetColor("_EmissionColor", new Color(1.1f, 1.1f, 1.1f));
            var r = GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var go = new GameObject("TV Camera");
            go.transform.SetParent(transform.parent, false);
            tv = go.AddComponent<Camera>();
            tv.targetTexture = feed;
            tv.fieldOfView = 32f;
            tv.nearClipPlane = 0.3f;
            tv.farClipPlane = 1500f;
            tv.depth = -10f;
            tv.cullingMask = ~(1 << ScreenLayer);
            var data = tv.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            go.transform.position = arena + new Vector3(0f, 80f, -150f);
        }

        void LateUpdate()
        {
            if (!tv) return;
            // Thirty pictures a second is plenty for a stadium screen, and halves its cost.
            tv.enabled = (Time.frameCount & 1) == 0;
            var target = PlayerDriver.Current ? PlayerDriver.Current.transform : null;
            Vector3 want, look;
            if (target)
            {
                Vector3 fwd = target.forward; fwd.y = 0f;
                fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                want = target.position - fwd * 14f + side * 9f + Vector3.up * 6f;
                look = target.position + Vector3.up;
            }
            else
            {
                float a = Time.time * 0.1f;
                want = arena + new Vector3(Mathf.Cos(a) * 170f, 70f, Mathf.Sin(a) * 170f);
                look = arena;
            }
            tv.transform.position = Vector3.SmoothDamp(tv.transform.position, want, ref velocity, 0.35f);
            tv.transform.rotation = Quaternion.LookRotation(look - tv.transform.position);
        }

        void OnDestroy()
        {
            if (tv) Destroy(tv.gameObject);
            if (feed) feed.Release();
            if (mat) Destroy(mat);
        }
    }
}
