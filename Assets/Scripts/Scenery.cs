using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Ground and distant hills around the arena, plus the stadium (grandstands full of people,
    /// roofs, sponsors' boards, the start gantry and floodlight masts), which is built round each
    /// track as it loads along with its infield (see <see cref="Infield"/>).
    /// </summary>
    public static class Scenery
    {
        const float Podium = 3f;      // concourse wall the seating starts above
        const float Rise = 0.7f;      // height of each row of seats
        const float Tread = 0.85f;    // depth of each row
        const float CrowdTile = 12f;  // metres of stand per repeat of the crowd texture (24 people)

        // Stadium submeshes
        const int SubConcrete = 0, SubCrowd = 1, SubSteel = 2, SubLamp = 3, SubTrim = 4, SubAds = 5;

        public static void Build(GameBootstrap a)
        {
            var root = new GameObject("Scenery").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(400f, 1f, 400f); // 4 km square
            ground.GetComponent<MeshRenderer>().sharedMaterial = a.LawnRuntime;

            // Rolling hills on the horizon.
            var rng = new System.Random(7);
            for (int i = 0; i < 26; i++)
            {
                float ang = i / 26f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
                float dist = 650f + (float)rng.NextDouble() * 500f;
                var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hill.name = "Hill";
                Object.Destroy(hill.GetComponent<Collider>());
                hill.transform.SetParent(root, false);
                hill.transform.position = new Vector3(Mathf.Cos(ang) * dist, -20f, 60f + Mathf.Sin(ang) * dist);
                float w = 180f + (float)rng.NextDouble() * 260f;
                hill.transform.localScale = new Vector3(w, 60f + (float)rng.NextDouble() * 90f, w * 0.8f);
                hill.GetComponent<MeshRenderer>().sharedMaterial = a.grass;
            }
        }

        /// <summary>
        /// Grandstands close in round the track, tall enough that the crowd looks down on the deck:
        /// the main stand along the start straight, smaller ones on the other three sides, and a
        /// floodlight mast at each corner. The stands come from the Blender kit (see
        /// <see cref="Grandstands"/>), or are built from boxes without it. Built as a child of the
        /// track, so it goes with it.
        /// </summary>
        public static void BuildStadium(Track track, GameBootstrap a)
        {
            // The deck's footprint.
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < track.Count; i++)
            {
                Vector3 c = track.Center(i);
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z);
            }
            float hw = track.HalfWidth;
            minX -= hw; maxX += hw; minZ -= hw; maxZ += hw;

            const float mainGap = 10f, sideGap = 12f;   // from the deck edge to the front of the stands
            const int mainTiers = 30, sideTiers = 24;
            var rng = new System.Random(11);
            var go = new GameObject("Stadium");
            go.transform.SetParent(track.transform, false);
            var lights = go.AddComponent<StadiumLights>();
            var materials = new[] { a.concrete, a.CrowdRuntime, a.scaffold, a.FloodlightRuntime, a.standSeats, a.AdvertsRuntime };

            // The start line is on the low-z side of every track, so the main stand goes there.
            // Each side is drawn in pieces, so each is lit by the floodlights nearest it.
            Vector3 mainFrom = new Vector3(minX, 0, minZ - mainGap), mainTo = new Vector3(maxX, 0, minZ - mainGap);
            Vector3 farFrom = new Vector3(minX, 0, maxZ + sideGap), farTo = new Vector3(maxX, 0, maxZ + sideGap);
            Vector3 leftFrom = new Vector3(minX - sideGap, 0, minZ + 6f), leftTo = new Vector3(minX - sideGap, 0, maxZ - 6f);
            Vector3 rightFrom = new Vector3(maxX + sideGap, 0, minZ + 6f), rightTo = new Vector3(maxX + sideGap, 0, maxZ - 6f);
            float mainDepth, farDepth;
            if (Grandstands.Available(a))
            {
                var (far, ends) = StandStyles(track.Definition != null ? track.Definition.Name : track.name);
                Grandstands.BuildSide(go.transform, "Main stand", a, Grandstands.Style.Main, mainFrom, mainTo, Vector3.back, rng);
                Grandstands.BuildSide(go.transform, "Far stand", a, far, farFrom, farTo, Vector3.forward, rng);
                Grandstands.BuildSide(go.transform, "Left stand", a, ends, leftFrom, leftTo, Vector3.left, rng);
                Grandstands.BuildSide(go.transform, "Right stand", a, ends, rightFrom, rightTo, Vector3.right, rng);
                mainDepth = Grandstands.Depth(a, Grandstands.Style.Main);
                farDepth = Grandstands.Depth(a, far);
            }
            else
            {
                var side = new MeshBuilder(6);
                AddStands(side, mainFrom, mainTo, Vector3.back, mainTiers, rng);
                AddPart(go.transform, "Main stand", side, materials);
                side = new MeshBuilder(6);
                AddStands(side, farFrom, farTo, Vector3.forward, sideTiers, rng);
                AddPart(go.transform, "Far stand", side, materials);
                side = new MeshBuilder(6);
                AddStands(side, leftFrom, leftTo, Vector3.left, sideTiers, rng);
                AddPart(go.transform, "Left stand", side, materials);
                side = new MeshBuilder(6);
                AddStands(side, rightFrom, rightTo, Vector3.right, sideTiers, rng);
                AddPart(go.transform, "Right stand", side, materials);
                mainDepth = mainTiers * Tread;
                farDepth = sideTiers * Tread;
            }

            // Floodlight masts in the corners between the stands, lamps aimed at the middle, and
            // more behind the long stands on the bigger arenas, aimed straight across.
            var mb = new MeshBuilder(6);
            var centre = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            foreach (var corner in new[] { new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), new Vector3(minX, 0, maxZ), new Vector3(maxX, 0, maxZ) })
            {
                Vector3 outward = new Vector3(Mathf.Sign(corner.x - centre.x), 0f, Mathf.Sign(corner.z - centre.z));
                AddFloodlight(mb, lights, corner + outward * (sideGap + 6f), centre, true);
            }
            int between = Mathf.Max(1, Mathf.RoundToInt((maxX - minX) / 220f) - 1);
            for (int k = 1; k <= between; k++)
            {
                float x = Mathf.Lerp(minX, maxX, k / (float)(between + 1));
                float behindMain = minZ - mainGap - mainDepth - 8f, behindFar = maxZ + sideGap + farDepth + 8f;
                AddFloodlight(mb, lights, new Vector3(x, 0f, behindMain), new Vector3(x, 0f, Mathf.Lerp(minZ, maxZ, 0.4f)), false);
                AddFloodlight(mb, lights, new Vector3(x, 0f, behindFar), new Vector3(x, 0f, Mathf.Lerp(minZ, maxZ, 0.6f)), false);
            }

            AddStartGantry(mb, track);
            AddPart(go.transform, "Floodlights and gantry", mb, materials);

            Infield.Build(track, a, go.transform, minX, maxX, minZ, maxZ);
        }

        // The main grandstand always lines the start straight; the other sides get one of three
        // mixes, picked by the track's name so each arena keeps its own look.
        static (Grandstands.Style far, Grandstands.Style ends) StandStyles(string trackName)
        {
            int h = 0;
            foreach (char ch in trackName ?? "") h = h * 31 + ch;
            switch ((h % 3 + 3) % 3)
            {
                case 0: return (Grandstands.Style.Terrace, Grandstands.Style.Scaffold);
                case 1: return (Grandstands.Style.Main, Grandstands.Style.Scaffold);
                default: return (Grandstands.Style.Scaffold, Grandstands.Style.Terrace);
            }
        }

        // A stadium mesh with its renderer and collider: solid, so a car that flies off into the
        // crowd lands on the seats and gets craned out.
        static void AddPart(Transform parent, string name, MeshBuilder mb, Material[] materials)
        {
            var mesh = mb.ToMesh(name);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // A floodlight mast: a tapering pole, and a steel headframe of 4 x 5 lamps tilted down at the
        // track with a catwalk under it. Its spotlight and glow come on at night (StadiumLights);
        // only the corner masts' lights cast shadows, to keep night races quick to draw.
        static void AddFloodlight(MeshBuilder mb, StadiumLights lights, Vector3 foot, Vector3 aimAt, bool shadows)
        {
            const float height = 46f;
            Vector3 toCentre = new Vector3(aimAt.x - foot.x, 0f, aimAt.z - foot.z).normalized;
            mb.AddBeam(SubSteel, foot, foot + Vector3.up * 24f, 1.7f);
            mb.AddBeam(SubSteel, foot + Vector3.up * 23f, foot + Vector3.up * (height - 1f), 1.1f);
            mb.AddBox(SubConcrete, Matrix4x4.TRS(foot + Vector3.up * 0.4f, Quaternion.LookRotation(toCentre), new Vector3(3.2f, 0.8f, 3.2f)));

            Vector3 headCentre = foot + Vector3.up * height + toCentre * 1.2f;
            var head = Quaternion.LookRotation(toCentre) * Quaternion.Euler(20f, 0, 0);
            Vector3 H(float x, float y, float z) => headCentre + head * new Vector3(x, y, z);
            const float fw = 6.4f, fh = 3.9f;
            mb.AddBeam(SubSteel, foot + Vector3.up * (height - 2f), H(0, 0, -0.6f), 0.6f);
            // Frame round the lamps, with bars between the rows.
            foreach (float y in new[] { -fh, -fh * 0.5f, 0f, fh * 0.5f, fh }) mb.AddBeam(SubSteel, H(-fw, y, -0.45f), H(fw, y, -0.45f), 0.22f);
            mb.AddBeam(SubSteel, H(-fw, -fh, -0.45f), H(-fw, fh, -0.45f), 0.22f);
            mb.AddBeam(SubSteel, H(fw, -fh, -0.45f), H(fw, fh, -0.45f), 0.22f);
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    float x = (c - 2f) * 2.45f, y = (r - 1.5f) * 1.95f;
                    mb.AddBox(SubSteel, Matrix4x4.TRS(H(x, y, -0.55f), head, new Vector3(2.1f, 1.6f, 0.7f)));
                    mb.AddBox(SubLamp, Matrix4x4.TRS(H(x, y, -0.16f), head, new Vector3(1.85f, 1.35f, 0.08f)));
                }
            }
            // Catwalk under the lamps, with a rail.
            mb.AddBox(SubSteel, Matrix4x4.TRS(H(0, -fh - 0.6f, -1.2f), head, new Vector3(fw * 2f + 0.6f, 0.12f, 1.5f)));
            mb.AddBeam(SubSteel, H(-fw, -fh + 0.4f, -0.5f), H(fw, -fh + 0.4f, -0.5f), 0.08f);

            lights.AddMast(H(0, 0, 0.4f), aimAt, shadows);
        }

        // A sponsor's board: a quad seen from in front, with 'right' and 'up' spanning it as the
        // viewer sees it, showing advert <paramref name="slot"/>.
        public static void AdPanel(MeshBuilder mb, int sub, Vector3 bottomLeft, Vector3 right, Vector3 up, int slot)
        {
            Rect uv = ProceduralTextures.AdUV(slot);
            mb.AddQuad(sub, bottomLeft, bottomLeft + up, bottomLeft + up + right, bottomLeft + right,
                new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin));
        }

        static int nextAd;

        // The sponsors in turn, now and then the game's own board.
        public static int NextAd()
        {
            nextAd++;
            return nextAd % 7 == 0 ? ProceduralTextures.AdGame
                : ProceduralTextures.AdFirstSponsor + nextAd % (ProceduralTextures.AdSlots - ProceduralTextures.AdFirstSponsor);
        }

        // A row of boards along a stretch of wall, each about 4:1, facing 'out'.
        static void AdRow(MeshBuilder mb, Vector3 start, Vector3 end, float bottom, float height, Vector3 up)
        {
            float length = Vector3.Distance(start, end);
            int count = Mathf.Max(1, Mathf.RoundToInt(length / (height * 4f)));
            Vector3 step = (end - start) / count;
            for (int i = 0; i < count; i++)
                AdPanel(mb, SubAds, start + step * i + up * bottom, step, up * height, NextAd());
        }

        // The start/finish gantry over the line: posts off the deck's sides and a beam of boards.
        static void AddStartGantry(MeshBuilder mb, Track track)
        {
            Vector3 fwd = track.Forward(0), up = track.Up(0), right = track.Right(0);
            Vector3 origin = track.Center(0) + fwd * 0.9f;
            float half = track.HalfWidth + 0.8f, y0 = 5.2f, y1 = 6.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 post = origin + right * (side * half);
                mb.AddBeam(SubSteel, post - up * 0.6f, post + up * (y1 + 0.3f), 0.45f);
            }
            var rot = Quaternion.LookRotation(fwd, up);
            mb.AddBox(SubSteel, Matrix4x4.TRS(origin + up * ((y0 + y1) * 0.5f), rot, new Vector3(half * 2f + 0.45f, y1 - y0, 0.4f)));
            // Boards on both faces: two to oncoming cars, two on the back.
            float boardW = half;
            Vector3 frontBL = origin - fwd * 0.24f - right * half + up * y0, backBL = origin + fwd * 0.24f + right * half + up * y0;
            AdPanel(mb, SubAds, frontBL, right * boardW, up * (y1 - y0), ProceduralTextures.AdGame);
            AdPanel(mb, SubAds, frontBL + right * boardW, right * boardW, up * (y1 - y0), ProceduralTextures.AdStartFinish);
            AdPanel(mb, SubAds, backBL, -right * boardW, up * (y1 - y0), ProceduralTextures.AdStartFinish);
            AdPanel(mb, SubAds, backBL - right * boardW, -right * boardW, up * (y1 - y0), NextAd());
        }

        // Stand sections along a line at ground level (the front edge), climbing away in 'away',
        // with a gap between sections every 70 m or so.
        static void AddStands(MeshBuilder mb, Vector3 from, Vector3 to, Vector3 away, int tiers, System.Random rng)
        {
            const float gap = 6f;
            float total = Vector3.Distance(from, to);
            int count = Mathf.Max(1, Mathf.CeilToInt(total / 70f));
            float length = (total - gap * (count - 1)) / count;
            Vector3 along = (to - from).normalized;
            for (int s = 0; s < count; s++)
                AddStand(mb, from + along * (s * (length + gap) + length * 0.5f), away, length, tiers, rng);
        }

        // One stand section, its front edge centred on 'front'. Every row's riser and tread is
        // covered in crowd; each row picks its own strip of the texture and offset along it.
        static void AddStand(MeshBuilder mb, Vector3 front, Vector3 away, float length, int tiers, System.Random rng)
        {
            var rot = Quaternion.LookRotation(away);
            Vector3 P(float x, float y, float z) => front + rot * new Vector3(x, y, z);
            float hl = length * 0.5f, depth = tiers * Tread, height = Podium + tiers * Rise;
            float u0 = -hl / CrowdTile, u1 = hl / CrowdTile;
            const float strip = 1f / ProceduralTextures.CrowdRows;

            // Concourse wall with a row of sponsors' boards along its top, and its ends.
            const float boards = 1.5f;
            mb.AddQuad(SubConcrete, P(-hl, 0, 0), P(-hl, Podium - boards, 0), P(hl, Podium - boards, 0), P(hl, 0, 0));
            AdRow(mb, P(-hl, 0, 0), P(hl, 0, 0), Podium - boards, boards, Vector3.up);
            mb.AddQuad(SubConcrete, P(hl, 0, 0), P(hl, Podium, 0), P(hl, Podium, depth), P(hl, 0, depth));
            mb.AddQuad(SubConcrete, P(-hl, 0, depth), P(-hl, Podium, depth), P(-hl, Podium, 0), P(-hl, 0, 0));

            for (int t = 0; t < tiers; t++)
            {
                float y0 = Podium + t * Rise, y1 = y0 + Rise, z0 = t * Tread;
                float off = (float)rng.NextDouble();
                float vr = rng.Next(ProceduralTextures.CrowdRows) * strip, vt = rng.Next(ProceduralTextures.CrowdRows) * strip;
                // Riser, facing the track.
                mb.AddQuad(SubCrowd, P(-hl, y0, z0), P(-hl, y1, z0), P(hl, y1, z0), P(hl, y0, z0),
                    new Vector2(u0 + off, vr), new Vector2(u0 + off, vr + strip), new Vector2(u1 + off, vr + strip), new Vector2(u1 + off, vr));
                // Tread, facing up: what you see of the crowd from above.
                mb.AddQuad(SubCrowd, P(-hl, y1, z0), P(-hl, y1, z0 + Tread), P(hl, y1, z0 + Tread), P(hl, y1, z0),
                    new Vector2(u0 - off, vt + strip), new Vector2(u0 - off, vt), new Vector2(u1 - off, vt), new Vector2(u1 - off, vt + strip));
                // Stepped ends.
                mb.AddQuad(SubConcrete, P(hl, y0, z0), P(hl, y1, z0), P(hl, y1, depth), P(hl, y0, depth));
                mb.AddQuad(SubConcrete, P(-hl, y0, depth), P(-hl, y1, depth), P(-hl, y1, z0), P(-hl, y0, z0));
            }

            // Back wall, standing a little proud of the top row as a parapet.
            mb.AddBox(SubConcrete, Matrix4x4.TRS(P(0, (height + 1.2f) * 0.5f, depth + 0.2f), rot, new Vector3(length, height + 1.2f, 0.4f)));

            // Cantilevered roof over the back of the stand, held up by masts behind it with stays
            // out to its front edge, and a coloured fascia along that edge.
            float roofY = height + 6.5f, roofFront = depth * 0.45f, roofBack = depth + 1.5f;
            float roofDepth = roofBack - roofFront, tilt = 4f;
            var roofRot = rot * Quaternion.Euler(tilt, 0, 0);
            float frontLift = Mathf.Sin(tilt * Mathf.Deg2Rad) * roofDepth * 0.5f;
            mb.AddBox(SubSteel, Matrix4x4.TRS(P(0, roofY, (roofFront + roofBack) * 0.5f), roofRot, new Vector3(length + 2f, 0.5f, roofDepth)));
            mb.AddBox(SubTrim, Matrix4x4.TRS(P(0, roofY + frontLift - 0.2f, roofFront), roofRot, new Vector3(length + 2.2f, 1.4f, 0.3f)));
            AdRow(mb, P(-hl - 1f, 0, roofFront - 0.3f), P(hl + 1f, 0, roofFront - 0.3f), roofY + frontLift - 0.8f, 1.2f, Vector3.up);
            int masts = Mathf.Max(2, Mathf.RoundToInt(length / 18f) + 1);
            for (int m = 0; m < masts; m++)
            {
                float x = Mathf.Lerp(-hl + 1f, hl - 1f, m / (float)(masts - 1));
                Vector3 foot = P(x, 0, depth + 0.9f), mastTop = P(x, roofY + 6f, depth + 0.9f);
                mb.AddBeam(SubSteel, foot, mastTop, 0.7f);
                mb.AddBeam(SubSteel, mastTop, P(x, roofY + frontLift + 0.3f, roofFront + 1f), 0.16f);
            }
        }
    }
}
