using UnityEngine;

namespace StuntCar
{
    /// <summary>Ground, grandstands, floodlights and distant hills around the stadium.</summary>
    public static class Scenery
    {
        public static void Build(GameBootstrap a)
        {
            var root = new GameObject("Scenery").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(400f, 1f, 400f); // 4 km square
            ground.GetComponent<MeshRenderer>().sharedMaterial = a.grass;

            // Grandstands on all four sides of the arena.
            var stands = new MeshBuilder(2);
            AddStand(stands, new Vector3(0, 0, -70), 0f, 240f);
            AddStand(stands, new Vector3(0, 0, 190), 180f, 240f);
            AddStand(stands, new Vector3(-215, 0, 60), 90f, 160f);
            AddStand(stands, new Vector3(215, 0, 60), -90f, 160f);
            var standGo = new GameObject("Grandstands");
            standGo.transform.SetParent(root, false);
            standGo.AddComponent<MeshFilter>().sharedMesh = stands.ToMesh("Grandstands");
            standGo.AddComponent<MeshRenderer>().sharedMaterials = new[] { a.concrete, a.standSeats };

            // Floodlight towers at the corners.
            var towers = new MeshBuilder(2);
            foreach (var p in new[] { new Vector3(-200, 0, -60), new Vector3(200, 0, -60), new Vector3(-200, 0, 180), new Vector3(200, 0, 180) })
            {
                towers.AddBeam(0, p, p + Vector3.up * 38f, 1.0f);
                Vector3 toCentre = (new Vector3(0, 0, 60) - p).normalized;
                var lamp = Matrix4x4.TRS(p + Vector3.up * 39.5f, Quaternion.LookRotation(toCentre) * Quaternion.Euler(30f, 0, 0), new Vector3(9f, 4f, 0.6f));
                towers.AddBox(1, lamp);
            }
            var towerGo = new GameObject("Floodlights");
            towerGo.transform.SetParent(root, false);
            towerGo.AddComponent<MeshFilter>().sharedMesh = towers.ToMesh("Floodlights");
            towerGo.AddComponent<MeshRenderer>().sharedMaterials = new[] { a.scaffold, a.floodlight };

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

        // A stepped stand facing the arena; 'facing' rotates it about Y around its centre.
        static void AddStand(MeshBuilder mb, Vector3 centre, float facing, float length)
        {
            var rot = Quaternion.Euler(0, facing, 0);
            const int tiers = 8;
            for (int t = 0; t < tiers; t++)
            {
                float h = 1.2f + t * 1.1f;
                Vector3 local = new Vector3(0, h * 0.5f, -t * 1.6f);
                var m = Matrix4x4.TRS(centre + rot * local, rot, new Vector3(length, h, 1.6f));
                mb.AddBox(t == 0 ? 0 : 1, m);
            }
            // Back wall and roof.
            mb.AddBox(0, Matrix4x4.TRS(centre + rot * new Vector3(0, 6.5f, -tiers * 1.6f), rot, new Vector3(length, 13f, 0.6f)));
            mb.AddBox(0, Matrix4x4.TRS(centre + rot * new Vector3(0, 14f, -tiers * 0.8f), rot * Quaternion.Euler(-8f, 0, 0), new Vector3(length + 4f, 0.4f, tiers * 1.6f + 3f)));
        }
    }
}
