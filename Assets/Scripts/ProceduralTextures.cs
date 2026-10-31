using UnityEngine;

namespace StuntCar
{
    /// <summary>Textures generated at start-up, so the project needs no image files yet.</summary>
    public static class ProceduralTextures
    {
        /// <summary>Asphalt with a dashed centre line. U runs across the road, V along it (one tile = 8 m).</summary>
        public static Texture2D Asphalt()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Asphalt", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            var px = new Color32[size * size];
            var rng = new System.Random(1234);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.06f + (float)rng.NextDouble() * 0.07f;
                    float g = 0.2f + n;
                    // Darker tyre lines where cars usually drive.
                    float u = x / (float)size;
                    float wear = Mathf.Exp(-Mathf.Pow((u - 0.3f) * 12f, 2)) + Mathf.Exp(-Mathf.Pow((u - 0.7f) * 12f, 2));
                    g -= wear * 0.035f;
                    bool dash = Mathf.Abs(u - 0.5f) < 0.012f && y < size / 2;
                    Color c = dash ? new Color(0.85f, 0.85f, 0.8f) : new Color(g, g, g * 1.04f);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        public const int CrowdRows = 8;

        /// <summary>
        /// Seated spectators for the grandstands: <see cref="CrowdRows"/> strips stacked up the
        /// texture, each a row of 24 people (U wraps; one repeat is 12 m of stand). Some seats are
        /// empty, some fans have their arms up or a scarf held high, and a few wave flags, many of
        /// them in the two cars' colours.
        /// </summary>
        public static Texture2D Crowd()
        {
            const int w = 1024, h = 512, seats = 24;
            const int rowH = h / CrowdRows;
            float pitch = w / (float)seats;
            var px = new Color[w * h];
            var rng = new System.Random(42);
            float R() => (float)rng.NextDouble();

            Color yellow = new Color(1f, 0.72f, 0.05f), teal = new Color(0f, 0.62f, 0.58f);
            Color[] shirts =
            {
                new Color(0.92f, 0.92f, 0.9f), new Color(0.78f, 0.1f, 0.08f), new Color(0.08f, 0.12f, 0.32f),
                new Color(0.06f, 0.06f, 0.07f), new Color(0.12f, 0.45f, 0.18f), new Color(0.95f, 0.45f, 0.08f),
                new Color(0.5f, 0.5f, 0.52f), new Color(0.35f, 0.6f, 0.88f), new Color(0.9f, 0.45f, 0.6f),
                new Color(0.45f, 0.08f, 0.12f),
            };
            Color[] skins =
            {
                new Color(0.96f, 0.8f, 0.68f), new Color(0.88f, 0.67f, 0.5f), new Color(0.75f, 0.54f, 0.38f),
                new Color(0.55f, 0.37f, 0.25f), new Color(0.36f, 0.24f, 0.16f),
            };
            Color[] hairs =
            {
                new Color(0.08f, 0.06f, 0.05f), new Color(0.25f, 0.15f, 0.08f), new Color(0.5f, 0.33f, 0.15f),
                new Color(0.8f, 0.65f, 0.35f), new Color(0.55f, 0.55f, 0.55f),
            };
            Color Shirt() { float r = R(); return r < 0.25f ? yellow : r < 0.4f ? teal : shirts[rng.Next(shirts.Length)]; }
            Color Team() => R() < 0.6f ? yellow : teal;

            Color shade = new Color(0.05f, 0.055f, 0.07f), seat = new Color(0.13f, 0.2f, 0.42f);
            for (int i = 0; i < px.Length; i++) px[i] = shade;

            var flags = new System.Collections.Generic.List<(float x, int y0, Color c)>();
            for (int r = 0; r < CrowdRows; r++)
            {
                int y0 = r * rowH;
                for (int s = 0; s < seats; s++)
                {
                    float cx = (s + 0.5f) * pitch;
                    Blob(px, w, cx - 15f, y0 + 3f, cx + 15f, y0 + 31f, 4f, seat);
                    if (R() < 0.12f) continue; // empty seat

                    float k = 0.9f + R() * 0.2f, x = cx + (R() - 0.5f) * 6f, top = y0 + 33f * k + (R() - 0.5f) * 4f;
                    Color shirt = Shirt(), skin = skins[rng.Next(skins.Length)];
                    float headY = top + 9f * k;
                    bool armsUp = R() < 0.18f;
                    if (armsUp)
                    {
                        foreach (float side in new[] { -1f, 1f })
                        {
                            float ax = x + side * 15f * k;
                            Blob(px, w, ax - 2.5f, y0 + 22f, ax + 2.5f, y0 + 57f * k, 2.5f, shirt);
                            Blob(px, w, ax - 3f, y0 + 54f * k, ax + 3f, y0 + 60f * k, 3f, skin);
                        }
                        if (R() < 0.35f) Blob(px, w, x - 18f * k, y0 + 51f * k, x + 18f * k, y0 + 56f * k, 1.5f, Team());
                    }
                    Blob(px, w, x - 14f * k, y0 + 1f, x + 14f * k, top, 7f * k, shirt);
                    Blob(px, w, x - 8.6f * k, headY - 6.6f * k, x + 8.6f * k, headY + 10.6f * k, 8.6f * k, hairs[rng.Next(hairs.Length)]);
                    Blob(px, w, x - 7.6f * k, headY - 8.6f * k, x + 7.6f * k, headY + 6.6f * k, 7.6f * k, skin);
                    if (R() < 0.2f)
                    {
                        Color cap = R() < 0.5f ? Team() : shirts[rng.Next(shirts.Length)];
                        Blob(px, w, x - 9f * k, headY + 3.5f * k, x + 9f * k, headY + 10f * k, 3f, cap);
                        Blob(px, w, x - 11f * k, headY + 2.5f * k, x + 11f * k, headY + 4.5f * k, 1f, cap);
                    }
                    if (R() < 0.05f) flags.Add((x, y0, Team()));
                }
            }
            // Flags last, so they fly over the neighbours.
            foreach (var (x, y0, c) in flags)
            {
                Blob(px, w, x + 10f, y0 + 28f, x + 12f, y0 + 63f, 0.5f, new Color(0.7f, 0.7f, 0.7f));
                Blob(px, w, x + 12f, y0 + 45f, x + 36f, y0 + 62f, 1f, c);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "Crowd", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, anisoLevel = 8,
            };
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        // Paints a rounded rectangle (corner radius r) with a soft 1 px edge; x wraps round the texture.
        static void Blob(Color[] px, int w, float x0, float y0, float x1, float y1, float r, Color c)
        {
            int h = px.Length / w;
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float hx = (x1 - x0) * 0.5f - r, hy = (y1 - y0) * 0.5f - r;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(y0) - 1); y <= Mathf.Min(h - 1, Mathf.CeilToInt(y1)); y++)
            {
                for (int x = Mathf.FloorToInt(x0) - 1; x <= Mathf.CeilToInt(x1); x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - cx) - hx, qy = Mathf.Abs(y + 0.5f - cy) - hy;
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (a <= 0f) continue;
                    int i = y * w + ((x % w) + w) % w;
                    px[i] = Color.Lerp(px[i], c, a);
                }
            }
        }

        public static Texture2D Grass()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Grass", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            var px = new Color32[size * size];
            var rng = new System.Random(99);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.5f + Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.3f + (float)rng.NextDouble() * 0.2f;
                    var c = Color.Lerp(new Color(0.75f, 0.85f, 0.6f), new Color(1.05f, 1.1f, 0.85f), n);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
