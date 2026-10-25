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
