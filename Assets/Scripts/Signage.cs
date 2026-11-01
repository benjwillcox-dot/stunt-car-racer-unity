using UnityEngine;

namespace StuntCar
{
    /// <summary>
    /// The trackside textures: kerb detail, the mown lawn, the sponsors' adverts (drawn with a
    /// small pixel font) and an atlas for the infield props. Each has a *Pixels() function that
    /// only does arithmetic, so it can be checked outside Unity.
    /// </summary>
    public static partial class ProceduralTextures
    {
        // ------------------------------------------------------------ noise

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777216f;
            }
        }

        // Smooth value noise that tiles every px by py lattice cells.
        static float Noise(float x, float y, int px, int py, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int ax = ((x0 % px) + px) % px, bx = (ax + 1) % px, ay = ((y0 % py) + py) % py, by = (ay + 1) % py;
            float top = Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), fx);
            float bottom = Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), fx);
            return Mathf.Lerp(top, bottom, fy);
        }

        static Texture2D Make(string name, int w, int h, Color[] px, TextureWrapMode wrapU, TextureWrapMode wrapV)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapModeU = wrapU, wrapModeV = wrapV, anisoLevel = 8 };
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        // ------------------------------------------------------------ kerbs

        public const int KerbW = 128, KerbH = 256;

        /// <summary>
        /// Detail multiplied over the kerbs' red and white: painted concrete with grit, grooves across
        /// it with a lit edge (a serrated kerb), tyre rubber worn into the inner half and a few chips.
        /// U runs across the kerb from its inner edge, V along it, one repeat per red or white block.
        /// </summary>
        public static Texture2D Kerb() => Make("Kerb", KerbW, KerbH, KerbPixels(), TextureWrapMode.Clamp, TextureWrapMode.Repeat);

        public static Color[] KerbPixels()
        {
            var px = new Color[KerbW * KerbH];
            for (int y = 0; y < KerbH; y++)
            {
                for (int x = 0; x < KerbW; x++)
                {
                    float u = (x + 0.5f) / KerbW;
                    float g = 0.93f + (Hash(x, y, 3) - 0.5f) * 0.07f + (Noise(x / 8f, y / 8f, KerbW / 8, KerbH / 8, 5) - 0.5f) * 0.06f;
                    // Grooves every 32 px (8 to a block), on the raised part only.
                    int gy = y % 32;
                    if (u > 0.12f)
                    {
                        if (gy < 4) g *= 0.62f;
                        else if (gy < 6) g = Mathf.Min(1f, g * 1.08f);
                    }
                    // Rubber laid down by tyres riding the inner half.
                    float worn = Mathf.Clamp01((0.6f - u) / 0.5f) * Noise(x / 16f, y / 32f, KerbW / 16, KerbH / 32, 9);
                    g *= 1f - 0.45f * worn;
                    if (u > 0.95f) g *= 0.85f; // outer lip
                    px[y * KerbW + x] = new Color(g, g, g, 1f);
                }
            }
            var rng = new System.Random(17);
            for (int i = 0; i < 40; i++)
            {
                float cx = (float)rng.NextDouble() * KerbW, cy = (float)rng.NextDouble() * KerbH, r = 1.5f + (float)rng.NextDouble() * 2.5f;
                Blob(px, KerbW, cx - r, cy - r, cx + r, cy + r, r, new Color(0.55f, 0.55f, 0.53f));
            }
            return px;
        }

        // ------------------------------------------------------------ lawn

        public const int LawnW = 128, LawnH = 256;

        /// <summary>
        /// The arena's grass, mown in light and dark stripes: one of each per repeat, across V.
        /// It's a tint multiplied over the grass colour, like <see cref="Grass"/>.
        /// </summary>
        public static Texture2D Lawn() => Make("Lawn", LawnW, LawnH, LawnPixels(), TextureWrapMode.Repeat, TextureWrapMode.Repeat);

        public static Color[] LawnPixels()
        {
            var px = new Color[LawnW * LawnH];
            for (int y = 0; y < LawnH; y++)
            {
                for (int x = 0; x < LawnW; x++)
                {
                    // 1 across the light stripe (centred on v = 0.25), 0 across the dark one, with a
                    // few pixels' blend at each edge; distances wrap so the repeat is seamless.
                    float v = (y + 0.5f) / LawnH, d = Mathf.Abs(v - 0.25f);
                    float light = Mathf.Clamp01((0.25f - Mathf.Min(d, 1f - d)) * 40f + 0.5f);
                    // Kept under 1, as the texture can't store brighter.
                    float n = Noise(x / 8f, y / 8f, LawnW / 8, LawnH / 8, 21) * 0.14f + Hash(x, y, 23) * 0.1f;
                    float t = Mathf.Lerp(0.7f, 0.9f, light) + n - 0.12f;
                    px[y * LawnW + x] = new Color(t * 0.98f, t, t * 0.9f, 1f);
                }
            }
            return px;
        }

        // ------------------------------------------------------------ adverts

        public const int AdsW = 1024, AdsH = 1024, AdSlots = 16;

        /// <summary>Where advert <paramref name="i"/> sits in the adverts texture (2 across, 8 down).</summary>
        public static Rect AdUV(int i)
        {
            i = ((i % AdSlots) + AdSlots) % AdSlots;
            const float inset = 1f / AdsW;
            return new Rect((i % 2) * 0.5f + inset, (i / 2) / 8f + inset, 0.5f - 2f * inset, 1f / 8f - 2f * inset);
        }

        public const int AdGame = 0, AdStartFinish = 1, AdFirstSponsor = 2;

        enum AdStyle { Plain, Chequer, Wave, Chevrons, Stripes, Circle }

        // Made-up sponsors, as (big word, small word, background, lettering, accent, style).
        static readonly (string big, string small, Color bg, Color fg, Color accent, AdStyle style)[] Sponsors =
        {
            ("STUNT CAR", "RACER", new Color(1f, 0.72f, 0.05f), new Color(0.05f, 0.05f, 0.06f), new Color(0.05f, 0.05f, 0.06f), AdStyle.Chequer),
            ("START", "FINISH", new Color(0.05f, 0.05f, 0.06f), Color.white, Color.white, AdStyle.Chequer),
            ("NITRO", "COLA", new Color(0.8f, 0.07f, 0.06f), Color.white, Color.white, AdStyle.Wave),
            ("APEX", "TYRES", new Color(0.05f, 0.05f, 0.06f), new Color(1f, 0.8f, 0.1f), new Color(1f, 0.8f, 0.1f), AdStyle.Chevrons),
            ("TURBOX", "MOTOR OIL", new Color(0.05f, 0.4f, 0.2f), Color.white, new Color(1f, 0.85f, 0.2f), AdStyle.Stripes),
            ("VELOCITY", "FM 101.7", new Color(0.35f, 0.1f, 0.55f), Color.white, new Color(1f, 0.55f, 0.1f), AdStyle.Circle),
            ("REDLINE", "ENERGY DRINK", new Color(0.06f, 0.06f, 0.07f), new Color(0.95f, 0.12f, 0.1f), new Color(0.95f, 0.12f, 0.1f), AdStyle.Stripes),
            ("PISTON", "PIZZA", new Color(0.95f, 0.45f, 0.08f), Color.white, new Color(0.75f, 0.08f, 0.05f), AdStyle.Circle),
            ("KINETIC", "BATTERIES", new Color(0.1f, 0.3f, 0.8f), new Color(1f, 0.85f, 0.15f), new Color(1f, 0.85f, 0.15f), AdStyle.Chevrons),
            ("GRIDLOCK", "INSURANCE", new Color(0.06f, 0.1f, 0.3f), Color.white, new Color(0f, 0.62f, 0.58f), AdStyle.Plain),
            ("HYPERDRIVE", "GAMES", new Color(0f, 0.62f, 0.58f), new Color(0.04f, 0.04f, 0.05f), Color.white, AdStyle.Stripes),
            ("TORQUE", "TOOLS", new Color(0.22f, 0.23f, 0.25f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.55f, 0.1f), AdStyle.Chevrons),
            ("SPEEDWAY", "BURGERS", new Color(0.78f, 0.08f, 0.06f), new Color(1f, 0.85f, 0.15f), new Color(1f, 0.85f, 0.15f), AdStyle.Wave),
            ("SKYHOOK", "CRANE HIRE", new Color(0.95f, 0.95f, 0.93f), new Color(0.05f, 0.05f, 0.06f), new Color(1f, 0.72f, 0.05f), AdStyle.Chevrons),
            ("CHROME", "CAR WASH", new Color(0.55f, 0.78f, 0.95f), new Color(0.06f, 0.1f, 0.3f), Color.white, AdStyle.Wave),
            ("MAXGRIP", "RACE GLOVES", new Color(0.95f, 0.95f, 0.93f), new Color(0.8f, 0.07f, 0.06f), new Color(0.05f, 0.05f, 0.06f), AdStyle.Stripes),
        };

        /// <summary>Sixteen sponsor boards, 512 x 128 px each (a 4:1 board).</summary>
        public static Texture2D Adverts() => Make("Adverts", AdsW, AdsH, AdvertPixels(), TextureWrapMode.Clamp, TextureWrapMode.Clamp);

        public static Color[] AdvertPixels()
        {
            var px = new Color[AdsW * AdsH];
            for (int i = 0; i < AdSlots; i++) DrawAdvert(px, (i % 2) * 512, (i / 2) * 128, Sponsors[i % Sponsors.Length]);
            return px;
        }

        static void DrawAdvert(Color[] px, int x0, int y0, (string big, string small, Color bg, Color fg, Color accent, AdStyle style) ad)
        {
            const int W = 512, H = 128;
            for (int y = y0; y < y0 + H; y++)
                for (int x = x0; x < x0 + W; x++)
                    px[y * AdsW + x] = ad.bg;

            float left = x0 + 16, right = x0 + W - 16, bottom = 10f;
            switch (ad.style)
            {
                case AdStyle.Chequer:
                    for (int cy = 0; cy < 4; cy++)
                        for (int cx = 0; cx < 3; cx++)
                            if (((cx + cy) & 1) == 0)
                            {
                                Blob(px, AdsW, x0 + 8 + cx * 24, y0 + 16 + cy * 24, x0 + 32 + cx * 24, y0 + 40 + cy * 24, 0f, ad.accent);
                                Blob(px, AdsW, x0 + W - 80 + cx * 24, y0 + 16 + cy * 24, x0 + W - 56 + cx * 24, y0 + 40 + cy * 24, 0f, ad.accent);
                            }
                    left = x0 + 90; right = x0 + W - 90;
                    break;
                case AdStyle.Wave:
                    for (int x = 0; x < W; x += 2)
                    {
                        float wy = y0 + 9 + Mathf.Sin(x / 40f) * 4f;
                        Blob(px, AdsW, x0 + x, wy, x0 + x + 3, wy + 6, 1f, ad.accent);
                    }
                    bottom = 24f;   // keep the lettering above the wave
                    break;
                case AdStyle.Chevrons:
                    for (int c = 0; c < 3; c++)
                    {
                        float cx = x0 + 22 + c * 26;
                        for (int k = 0; k < 24; k++)
                        {
                            float yy = y0 + 64 + (k - 12) * 2.6f, xx = cx + (12 - Mathf.Abs(k - 12)) * 1.4f;
                            Blob(px, AdsW, xx, yy - 3, xx + 9, yy + 3, 2f, ad.accent);
                        }
                    }
                    left = x0 + 110;
                    break;
                case AdStyle.Stripes:
                    for (int s = 0; s < 3; s++)
                        for (int y = 0; y < H; y++)
                        {
                            float sx = x0 + W - 110 + s * 30 + y * 0.45f;
                            Blob(px, AdsW, sx, y0 + y, sx + 16, y0 + y + 1, 0f, ad.accent);
                        }
                    right = x0 + W - 116;
                    break;
                case AdStyle.Circle:
                    Blob(px, AdsW, x0 + 18, y0 + 18, x0 + 110, y0 + 110, 46f, ad.accent);
                    Blob(px, AdsW, x0 + 38, y0 + 38, x0 + 90, y0 + 90, 26f, ad.bg);
                    Blob(px, AdsW, x0 + 52, y0 + 52, x0 + 76, y0 + 76, 12f, ad.accent);
                    left = x0 + 124;
                    break;
                default:
                    Blob(px, AdsW, x0 + 6, y0 + 6, x0 + W - 6, y0 + 12, 0f, ad.accent);
                    Blob(px, AdsW, x0 + 6, y0 + H - 12, x0 + W - 6, y0 + H - 6, 0f, ad.accent);
                    break;
            }

            // Big word on top, small word under it, both centred in the space the style leaves.
            float room = right - left, tall = H - bottom - 10f;
            float ss = Mathf.Min(4f, Mathf.Floor(room / PixelFont.Width(ad.small, 1f)));
            float sb = Mathf.Min(Mathf.Floor((tall - 8f - 7f * ss) / 7f), Mathf.Floor(room / PixelFont.Width(ad.big, 1f)));
            float total = 7f * sb + 8f + 7f * ss;
            float top = y0 + bottom + (tall + total) * 0.5f;
            float mid = (left + right) * 0.5f;
            PixelFont.Draw(px, AdsW, ad.big, mid - PixelFont.Width(ad.big, sb) * 0.5f, top - 7f * sb, sb, ad.fg);
            PixelFont.Draw(px, AdsW, ad.small, mid - PixelFont.Width(ad.small, ss) * 0.5f, top - 7f * sb - 8f - 7f * ss, ss, ad.fg);
        }

        // ------------------------------------------------------------ props atlas

        public const int PropsSize = 512;

        // Flat colours for the infield props: cells of the 8 x 8 palette in the top-left quarter.
        public const int PalYellow = 0, PalTeal = 1, PalWhite = 2, PalBlack = 3, PalDarkGrey = 4, PalGrey = 5, PalLightGrey = 6,
            PalRed = 7, PalOrange = 8, PalGreen = 9, PalBlue = 10, PalNavy = 11, PalBrown = 12, PalTan = 13, PalTarmac = 14,
            PalConcrete = 15, PalGlass = 16, PalSteel = 17, PalSkin0 = 18, PalSkinCount = 5, PalShirt0 = 23, PalShirtCount = 10,
            PalHair0 = 33, PalHairCount = 4, PalJeans = 37, PalScreen = 38;

        static readonly Color[] Palette =
        {
            new Color(1f, 0.72f, 0.05f), new Color(0f, 0.62f, 0.58f), new Color(0.93f, 0.93f, 0.91f), new Color(0.05f, 0.05f, 0.06f),
            new Color(0.18f, 0.19f, 0.2f), new Color(0.45f, 0.46f, 0.48f), new Color(0.72f, 0.73f, 0.74f), new Color(0.8f, 0.08f, 0.06f),
            new Color(0.98f, 0.45f, 0.05f), new Color(0.12f, 0.45f, 0.18f), new Color(0.1f, 0.3f, 0.8f), new Color(0.06f, 0.1f, 0.3f),
            new Color(0.4f, 0.26f, 0.14f), new Color(0.82f, 0.72f, 0.52f), new Color(0.11f, 0.11f, 0.12f), new Color(0.6f, 0.6f, 0.57f),
            new Color(0.25f, 0.4f, 0.5f), new Color(0.55f, 0.58f, 0.62f),
            new Color(0.96f, 0.8f, 0.68f), new Color(0.88f, 0.67f, 0.5f), new Color(0.75f, 0.54f, 0.38f), new Color(0.55f, 0.37f, 0.25f), new Color(0.36f, 0.24f, 0.16f),
            new Color(1f, 0.72f, 0.05f), new Color(0f, 0.62f, 0.58f), new Color(0.92f, 0.92f, 0.9f), new Color(0.78f, 0.1f, 0.08f), new Color(0.08f, 0.12f, 0.32f),
            new Color(0.06f, 0.06f, 0.07f), new Color(0.12f, 0.45f, 0.18f), new Color(0.95f, 0.45f, 0.08f), new Color(0.35f, 0.6f, 0.88f), new Color(0.9f, 0.45f, 0.6f),
            new Color(0.08f, 0.06f, 0.05f), new Color(0.25f, 0.15f, 0.08f), new Color(0.5f, 0.33f, 0.15f), new Color(0.8f, 0.65f, 0.35f),
            new Color(0.16f, 0.22f, 0.36f), new Color(0.02f, 0.02f, 0.03f),
        };

        /// <summary>Middle of palette cell <paramref name="i"/>, for a flat-coloured face.</summary>
        public static Vector2 PaletteUV(int i)
        {
            int col = i % 8, row = i / 8;
            return new Vector2((col + 0.5f) / 16f, 0.5f + (row + 0.5f) / 16f);
        }

        // The patterned quarters and halves of the atlas.
        public static readonly Rect TyreWallUV = new Rect(0.5f, 0.75f, 0.5f, 0.25f);    // 4 stacks of 5 tyres
        public static readonly Rect HayUV = new Rect(0.5f, 0.5f, 0.5f, 0.25f);
        public static readonly Rect AwningRedUV = new Rect(0f, 0.25f, 0.5f, 0.25f);
        public static readonly Rect AwningTeamUV = new Rect(0.5f, 0.25f, 0.5f, 0.25f);
        public static readonly Rect ShutterUV = new Rect(0f, 0f, 0.5f, 0.25f);
        public static readonly Rect ChequerUV = new Rect(0.5f, 0f, 0.5f, 0.25f);

        /// <summary>The props atlas: palette, tyre wall, hay, awning stripes, roller shutter and chequers.</summary>
        public static Texture2D Props() => Make("Props", PropsSize, PropsSize, PropsPixels(), TextureWrapMode.Clamp, TextureWrapMode.Clamp);

        public static Color[] PropsPixels()
        {
            const int S = PropsSize, Q = S / 2;
            var px = new Color[S * S];
            void Fill(int x0, int y0, int w, int h, System.Func<int, int, Color> f)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        px[(y0 + y) * S + x0 + x] = f(x, y);
            }

            // Palette.
            Fill(0, Q, Q, Q, (x, y) => { int i = (y / 32) * 8 + x / 32; return i < Palette.Length ? Palette[i] : Color.magenta; });

            // Tyre wall: four stacks of five tyres seen side on, some with painted tops.
            Fill(Q, Q + Q / 2, Q, Q / 2, (x, y) =>
            {
                int stack = x / 64, tyre = y / 25;
                float sx = (x % 64) / 64f, ty = (y % 25) / 25f;
                if (sx < 0.04f || sx > 0.96f) return new Color(0.03f, 0.03f, 0.03f);
                float bulge = Mathf.Sin(ty * Mathf.PI);
                float g = 0.05f + 0.11f * bulge + (Hash(x, y, 31) - 0.5f) * 0.03f;
                if ((x % 8) == 0 && ty > 0.2f && ty < 0.8f) g *= 0.6f; // tread blocks
                bool painted = tyre == 4 && (stack & 1) == 0;
                return painted ? new Color(0.9f, 0.9f, 0.88f) * (0.6f + 0.4f * bulge) : new Color(g, g, g);
            });

            // Hay: straw with two twine bands.
            Fill(Q, Q, Q, Q / 2, (x, y) =>
            {
                float n = Noise(x / 3f, y / 14f, Q / 3 + 1, 10, 41);
                float straw = 0.75f + 0.35f * n + (Hash(x, y, 43) - 0.5f) * 0.2f;
                var c = new Color(0.86f, 0.72f, 0.38f) * straw;
                if (Mathf.Abs(x - Q * 0.3f) < 3f || Mathf.Abs(x - Q * 0.7f) < 3f) c *= 0.55f;
                c.a = 1f;
                return c;
            });

            // Awnings: eight vertical stripes, shaded towards the hem.
            Color AwningPixel(int x, int y, Color a, Color b)
            {
                var c = ((x / 32) & 1) == 0 ? a : b;
                float hem = y < 14 ? 0.78f : 1f;
                c *= hem * (0.92f + Hash(x, y, 51) * 0.08f);
                c.a = 1f;
                return c;
            }
            Fill(0, Q / 2, Q, Q / 2, (x, y) => AwningPixel(x, y, new Color(0.8f, 0.08f, 0.06f), new Color(0.95f, 0.95f, 0.93f)));
            Fill(Q, Q / 2, Q, Q / 2, (x, y) => AwningPixel(x, y, new Color(1f, 0.72f, 0.05f), new Color(0f, 0.62f, 0.58f)));

            // Roller shutter: grey slats, a darker bottom rail.
            Fill(0, 0, Q, Q / 2, (x, y) =>
            {
                float g = 0.55f + (y % 8 < 1 ? -0.2f : (y % 8) * 0.012f) + (Hash(x, y, 61) - 0.5f) * 0.03f;
                if (y < 6) g = 0.2f;
                return new Color(g, g, g * 1.03f);
            });

            // Chequers.
            Fill(Q, 0, Q, Q / 2, (x, y) => (((x / 32) + (y / 32)) & 1) == 0 ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.04f, 0.04f, 0.05f));

            for (int i = 0; i < px.Length; i++) px[i].a = 1f;
            return px;
        }

        // ------------------------------------------------------------ glow

        /// <summary>A soft round dot, for particles and the floodlights' glow.</summary>
        public static Texture2D SoftDot()
        {
            const int size = 64;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            return Make("SoftDot", size, size, px, TextureWrapMode.Clamp, TextureWrapMode.Clamp);
        }
    }

    /// <summary>
    /// A chunky 5 x 7 pixel font for adverts and the painted logo: capitals, digits and a little
    /// punctuation. Each glyph is seven rows of five bits, top row first.
    /// </summary>
    public static class PixelFont
    {
        static readonly System.Collections.Generic.Dictionary<char, byte[]> Glyphs = new System.Collections.Generic.Dictionary<char, byte[]>
        {
            ['A'] = new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 }, ['B'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E },
            ['C'] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E }, ['D'] = new byte[] { 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E },
            ['E'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F }, ['F'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 },
            ['G'] = new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F }, ['H'] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['I'] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E }, ['J'] = new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C },
            ['K'] = new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 }, ['L'] = new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F },
            ['M'] = new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 }, ['N'] = new byte[] { 0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11 },
            ['O'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E }, ['P'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 },
            ['Q'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D }, ['R'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 },
            ['S'] = new byte[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E }, ['T'] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
            ['U'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E }, ['V'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 },
            ['W'] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A }, ['X'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 },
            ['Y'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 }, ['Z'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F },
            ['0'] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E }, ['1'] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['2'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F }, ['3'] = new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E },
            ['4'] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 }, ['5'] = new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E },
            ['6'] = new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E }, ['7'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 },
            ['8'] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E }, ['9'] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C },
            ['-'] = new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 }, ['.'] = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C },
            ['!'] = new byte[] { 0x04, 0x04, 0x04, 0x04, 0x04, 0x00, 0x04 }, ['&'] = new byte[] { 0x0C, 0x12, 0x14, 0x08, 0x15, 0x12, 0x0D },
            ['/'] = new byte[] { 0x01, 0x02, 0x02, 0x04, 0x08, 0x08, 0x10 }, [':'] = new byte[] { 0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00 },
            ['\''] = new byte[] { 0x04, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00 },
        };

        /// <summary>Width of <paramref name="text"/> at <paramref name="scale"/> pixels per font pixel.</summary>
        public static float Width(string text, float scale) => text.Length == 0 ? 0f : (text.Length * 6 - 1) * scale;

        /// <summary>Calls <paramref name="cell"/> with the bottom-left corner (in font pixels, y up) of every lit pixel.</summary>
        public static void ForEachPixel(string text, System.Action<int, int> cell)
        {
            for (int c = 0; c < text.Length; c++)
            {
                if (!Glyphs.TryGetValue(char.ToUpperInvariant(text[c]), out var rows)) continue;
                for (int r = 0; r < 7; r++)
                    for (int b = 0; b < 5; b++)
                        if ((rows[r] & (0x10 >> b)) != 0) cell(c * 6 + b, 6 - r);
            }
        }

        /// <summary>Draws <paramref name="text"/> into a texture's pixels; (x, y) is the bottom-left corner.</summary>
        public static void Draw(Color[] px, int w, string text, float x, float y, float scale, Color c)
        {
            ForEachPixel(text, (cx, cy) =>
            {
                float x0 = x + cx * scale, y0 = y + cy * scale;
                ProceduralTextures.Blob(px, w, x0, y0, x0 + scale, y0 + scale, scale * 0.15f, c);
            });
        }
    }
}
