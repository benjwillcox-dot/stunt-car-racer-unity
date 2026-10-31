using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// Ground and distant hills around the arena, plus the stadium (grandstands full of people,
    /// roofs and floodlight masts), which is built round each track as it loads.
    /// </summary>
    public static class Scenery
    {
        const float Podium = 3f;      // concourse wall the seating starts above
        const float Rise = 0.7f;      // height of each row of seats
        const float Tread = 0.85f;    // depth of each row
        const float CrowdTile = 12f;  // metres of stand per repeat of the crowd texture (24 people)

        // Stadium submeshes
        const int SubConcrete = 0, SubCrowd = 1, SubSteel = 2, SubLamp = 3, SubTrim = 4;

        public static void Build(GameBootstrap a)
        {
            var root = new GameObject("Scenery").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(400f, 1f, 400f); // 4 km square
            ground.GetComponent<MeshRenderer>().sharedMaterial = a.grass;

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
        /// floodlight mast at each corner. Built as a child of the track, so it goes with it.
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
            var mb = new MeshBuilder(5);

            // The start line is on the low-z side of every track, so the main stand goes there.
            AddStands(mb, new Vector3(minX, 0, minZ - mainGap), new Vector3(maxX, 0, minZ - mainGap), Vector3.back, mainTiers, rng);
            AddStands(mb, new Vector3(minX, 0, maxZ + sideGap), new Vector3(maxX, 0, maxZ + sideGap), Vector3.forward, sideTiers, rng);
            AddStands(mb, new Vector3(minX - sideGap, 0, minZ + 6f), new Vector3(minX - sideGap, 0, maxZ - 6f), Vector3.left, sideTiers, rng);
            AddStands(mb, new Vector3(maxX + sideGap, 0, minZ + 6f), new Vector3(maxX + sideGap, 0, maxZ - 6f), Vector3.right, sideTiers, rng);

            // Floodlight masts in the corners between the stands, lamps aimed at the middle.
            var centre = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            foreach (var corner in new[] { new Vector3(minX, 0, minZ), new Vector3(maxX, 0, minZ), new Vector3(minX, 0, maxZ), new Vector3(maxX, 0, maxZ) })
            {
                Vector3 outward = new Vector3(Mathf.Sign(corner.x - centre.x), 0f, Mathf.Sign(corner.z - centre.z));
                Vector3 p = corner + outward * (sideGap + 6f);
                mb.AddBeam(SubSteel, p, p + Vector3.up * 44f, 1.2f);
                Vector3 toCentre = new Vector3(centre.x - p.x, 0f, centre.z - p.z).normalized;
                var head = Quaternion.LookRotation(toCentre) * Quaternion.Euler(25f, 0, 0);
                mb.AddBox(SubSteel, Matrix4x4.TRS(p + Vector3.up * 45.5f - toCentre * 0.5f, head, new Vector3(10.6f, 5.6f, 0.5f)));
                mb.AddBox(SubLamp, Matrix4x4.TRS(p + Vector3.up * 45.5f, head, new Vector3(10f, 5f, 0.6f)));
            }

            var mesh = mb.ToMesh("Stadium");
            var go = new GameObject("Stadium");
            go.transform.SetParent(track.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { a.concrete, a.CrowdRuntime, a.scaffold, a.floodlight, a.standSeats };
            // Solid, so a car that flies off into the crowd lands on the seats and gets craned out.
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
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

            // Concourse wall with a coloured band along its top, and its ends.
            mb.AddQuad(SubConcrete, P(-hl, 0, 0), P(-hl, Podium - 0.8f, 0), P(hl, Podium - 0.8f, 0), P(hl, 0, 0));
            mb.AddQuad(SubTrim, P(-hl, Podium - 0.8f, 0), P(-hl, Podium, 0), P(hl, Podium, 0), P(hl, Podium - 0.8f, 0));
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
