using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StuntCar
{
    /// <summary>
    /// The Blender grandstand kit (Assets/Models/Grandstands, built by Art/Grandstands): three
    /// styles of stand, each a 12 m bay that repeats along a side of the stadium plus an end piece,
    /// mirrored for the other end. A side is laid out end, stretched bays, end, and merged a few
    /// modules at a time into one mesh, so each chunk is lit by the floodlights nearest it. Every
    /// row of seats picks its own strip of the crowd texture and every board its own advert.
    /// </summary>
    public static class Grandstands
    {
        public enum Style
        {
            Main,      // tiers either side of glazed suites, under a fabric canopy on cantilevered trusses
            Scaffold,  // temporary stand on a scaffold frame, with peaked tent roofs
            Terrace,   // classic concrete terrace under a roof on slim columns, with a clock tower
        }

        public const float BayWidth = 12f;
        const float EndWidth = 6f;      // how much of the stand's front an end piece takes up
        const int ChunkModules = 5;     // modules merged into each mesh
        const int Parts = 3;            // submeshes: palette structure, crowd, adverts
        static readonly string[] PartNames = { "_Palette", "_Crowd", "_Ads" };

        // One mesh of a module, read once from the model in the module's own space: x along the
        // stand (left to right seen from the track), y up, z away from the track.
        class Piece
        {
            public Vector3[] v, n;
            public Vector2[] uv;
            public int[] t;
        }

        class Module
        {
            public readonly Piece[] parts = new Piece[Parts];
            public Piece collider;
            public float depth;    // from the front edge to the back of the stand
        }

        static GameObject loadedFrom;
        static readonly Dictionary<string, Module> modules = new Dictionary<string, Module>();

        public static bool Available(GameBootstrap a) =>
            a.grandstandModel && a.GrandstandRuntime && Get(a, Style.Main, false) != null;

        /// <summary>How far a style's stands reach back from their front edge.</summary>
        public static float Depth(GameBootstrap a, Style style) => Get(a, style, false)?.depth ?? 25f;

        /// <summary>
        /// One side of the stadium: stands with their front edge on the line from 'from' to 'to'
        /// (at ground level), climbing away in 'away'. Scaffold stands come in blocks of about
        /// 80 m; the others run the whole side unless it's over 360 m.
        /// </summary>
        public static void BuildSide(Transform parent, string name, GameBootstrap a, Style style,
                                     Vector3 from, Vector3 to, Vector3 away, System.Random rng)
        {
            Module bay = Get(a, style, false), end = Get(a, style, true);
            if (bay == null || end == null) return;
            var rot = Quaternion.LookRotation(away);
            // The modules run left to right as seen from the track.
            if (Vector3.Dot(to - from, rot * Vector3.right) < 0f) (from, to) = (to, from);
            Vector3 along = (to - from).normalized;
            float total = Vector3.Distance(from, to);
            // Big stands get a wide gap, so their end towers don't stand back to back.
            bool scaffold = style == Style.Scaffold;
            float blockLength = scaffold ? 80f : 360f, gap = scaffold ? 8f : 20f;
            int blocks = Mathf.Max(1, Mathf.CeilToInt(total / blockLength));
            float length = (total - gap * (blocks - 1)) / blocks;

            // Every module as (piece, where its origin sits along the side, x scale); a negative
            // scale mirrors the end piece for the left-hand end.
            var placed = new List<(Module m, float at, float sx)>();
            for (int b = 0; b < blocks; b++)
            {
                float start = b * (length + gap);
                int bays = Mathf.Max(1, Mathf.RoundToInt((length - 2f * EndWidth) / BayWidth));
                float stretch = Mathf.Max(0.4f, (length - 2f * EndWidth) / (bays * BayWidth));
                placed.Add((end, start + EndWidth, -1f));
                for (int i = 0; i < bays; i++) placed.Add((bay, start + EndWidth + i * BayWidth * stretch, stretch));
                placed.Add((end, start + EndWidth + bays * BayWidth * stretch, 1f));
            }

            var crowdRows = new Dictionary<int, (int strip, float offset)>();
            var boards = new Dictionary<int, int>();
            for (int c = 0; c * ChunkModules < placed.Count; c++)
            {
                var chunk = new Chunk();
                for (int i = c * ChunkModules; i < Mathf.Min(placed.Count, (c + 1) * ChunkModules); i++)
                {
                    var (m, at, sx) = placed[i];
                    var xf = Matrix4x4.TRS(from + along * at, rot, Vector3.one) * Matrix4x4.Scale(new Vector3(sx, 1f, 1f));
                    bool mirror = sx < 0f;
                    crowdRows.Clear();
                    boards.Clear();
                    chunk.Add(m.parts[0], 0, xf, mirror, uv => uv);
                    chunk.Add(m.parts[1], 1, xf, mirror, uv => CrowdUV(uv, crowdRows, rng));
                    chunk.Add(m.parts[2], 2, xf, mirror, uv => AdvertUV(uv, mirror, boards));
                    chunk.AddCollider(m.collider, xf, mirror);
                }
                chunk.Build(parent, c == 0 ? name : name + " " + (c + 1),
                    new[] { a.GrandstandRuntime, a.CrowdRuntime, a.AdvertsRuntime });
            }
        }

        // The model's crowd UVs number each row in V's whole part; give each row of this module a
        // strip of the crowd texture and a shuffle along it.
        static Vector2 CrowdUV(Vector2 uv, Dictionary<int, (int strip, float offset)> rows, System.Random rng)
        {
            int row = Mathf.FloorToInt(uv.y);
            if (!rows.TryGetValue(row, out var r))
                rows[row] = r = (rng.Next(ProceduralTextures.CrowdRows), (float)rng.NextDouble());
            return new Vector2(uv.x + r.offset, (r.strip + uv.y - row) / ProceduralTextures.CrowdRows);
        }

        // The model's advert UVs number each board in U's whole part; give each board of this
        // module the next sponsor, reading the right way round on a mirrored end.
        static Vector2 AdvertUV(Vector2 uv, bool mirror, Dictionary<int, int> boards)
        {
            int board = Mathf.FloorToInt(uv.x);
            if (!boards.TryGetValue(board, out int slot)) boards[board] = slot = Scenery.NextAd();
            float u = uv.x - board;
            Rect r = ProceduralTextures.AdUV(slot);
            return new Vector2(r.xMin + (mirror ? 1f - u : u) * r.width, r.yMin + uv.y * r.height);
        }

        // Several modules' meshes merged into one renderer and one collider.
        class Chunk
        {
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int>[] tris = { new List<int>(), new List<int>(), new List<int>() };
            readonly List<Vector3> colV = new List<Vector3>();
            readonly List<int> colT = new List<int>();

            public void Add(Piece p, int sub, Matrix4x4 xf, bool mirror, System.Func<Vector2, Vector2> mapUV)
            {
                if (p == null) return;
                Matrix4x4 normals = xf.inverse.transpose;
                int b = v.Count;
                for (int i = 0; i < p.v.Length; i++)
                {
                    v.Add(xf.MultiplyPoint3x4(p.v[i]));
                    n.Add(normals.MultiplyVector(p.n[i]).normalized);
                    uv.Add(mapUV(p.uv[i]));
                }
                AddTriangles(tris[sub], p.t, b, mirror);
            }

            public void AddCollider(Piece p, Matrix4x4 xf, bool mirror)
            {
                if (p == null) return;
                int b = colV.Count;
                foreach (var pt in p.v) colV.Add(xf.MultiplyPoint3x4(pt));
                AddTriangles(colT, p.t, b, mirror);
            }

            // Mirroring turns faces inside out, so their corners go the other way round.
            static void AddTriangles(List<int> to, int[] t, int b, bool mirror)
            {
                for (int i = 0; i < t.Length; i += 3)
                {
                    to.Add(b + t[i]);
                    to.Add(b + t[mirror ? i + 2 : i + 1]);
                    to.Add(b + t[mirror ? i + 1 : i + 2]);
                }
            }

            public void Build(Transform parent, string name, Material[] materials)
            {
                var mesh = new Mesh { name = name };
                if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetUVs(0, uv);
                mesh.subMeshCount = Parts;
                for (int i = 0; i < Parts; i++) mesh.SetTriangles(tris[i], i);
                mesh.RecalculateBounds();

                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials;
                // Solid, so a car that flies into the crowd lands on the seats and gets craned out.
                var col = new Mesh { name = name + " collider" };
                if (colV.Count > 65000) col.indexFormat = IndexFormat.UInt32;
                col.SetVertices(colV);
                col.SetTriangles(colT, 0);
                go.AddComponent<MeshCollider>().sharedMesh = col;
            }
        }

        // ------------------------------------------------------------ reading the model

        static Module Get(GameBootstrap a, Style style, bool end)
        {
            if (loadedFrom != a.grandstandModel)
            {
                modules.Clear();
                loadedFrom = a.grandstandModel;
            }
            string name = style + (end ? "_End" : "_Bay");
            if (!modules.TryGetValue(name, out var m))
                modules[name] = m = a.grandstandModel ? Load(a.grandstandModel.transform, name) : null;
            return m;
        }

        static Module Load(Transform model, string name)
        {
            Transform root = model.Find(name);
            if (!root) return null;
            var m = new Module { collider = Read(root, name + "_Collider") };
            for (int i = 0; i < Parts; i++) m.parts[i] = Read(root, name + PartNames[i]);
            if (m.parts[0] == null || m.collider == null) return null;

            // Modelled with the stand's front on z = 0, its back towards +z and the bay running
            // along +x; put right a model that imported turned round or mirrored.
            var bounds = new Bounds(m.collider.v[0], Vector3.zero);
            foreach (var p in m.collider.v) bounds.Encapsulate(p);
            if (bounds.center.z < 0f) Reorient(m, new Vector3(-1f, 1f, -1f));
            if (bounds.center.x * (bounds.center.z < 0f ? -1f : 1f) < 0f) Reorient(m, new Vector3(-1f, 1f, 1f));
            float depth = 0f;
            foreach (var p in m.collider.v) depth = Mathf.Max(depth, p.z);
            m.depth = depth;
            return m;
        }

        static void Reorient(Module m, Vector3 scale)
        {
            bool flip = scale.x * scale.y * scale.z < 0f;
            foreach (var p in new List<Piece>(m.parts) { m.collider })
            {
                if (p == null) continue;
                for (int i = 0; i < p.v.Length; i++)
                {
                    p.v[i] = Vector3.Scale(p.v[i], scale);
                    if (p.n != null) p.n[i] = Vector3.Scale(p.n[i], scale);
                }
                if (flip)
                    for (int i = 0; i < p.t.Length; i += 3) (p.t[i + 1], p.t[i + 2]) = (p.t[i + 2], p.t[i + 1]);
            }
        }

        static Piece Read(Transform root, string name)
        {
            Transform t = root.Find(name);
            var mf = t ? t.GetComponent<MeshFilter>() : null;
            if (!mf || !mf.sharedMesh) return null;
            Mesh mesh = mf.sharedMesh;
            if (!mesh.isReadable)
            {
                Debug.LogWarning($"Grandstands: {mesh.name} isn't readable; reimport Assets/Models/Grandstands.");
                return null;
            }
            Matrix4x4 xf = root.worldToLocalMatrix * t.localToWorldMatrix;
            var p = new Piece { v = mesh.vertices, n = mesh.normals, uv = mesh.uv, t = mesh.triangles };
            if (p.uv.Length != p.v.Length) p.uv = new Vector2[p.v.Length];
            if (p.n.Length != p.v.Length)
            {
                var copy = Object.Instantiate(mesh);
                copy.RecalculateNormals();
                p.n = copy.normals;
                Object.Destroy(copy);
            }
            for (int i = 0; i < p.v.Length; i++)
            {
                p.v[i] = xf.MultiplyPoint3x4(p.v[i]);
                p.n[i] = xf.MultiplyVector(p.n[i]).normalized;
            }
            return p;
        }
    }
}
