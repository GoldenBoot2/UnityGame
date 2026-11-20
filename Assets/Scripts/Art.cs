using UnityEngine;

namespace GoldenBoot.BlockDodge
{
    // All graphics are drawn in code (no image files needed in the repo).
    public static class Art
    {
        public static readonly Color Background = new Color32(0x12, 0x18, 0x2B, 0xFF);
        public static readonly Color Card = new Color32(0x1F, 0x29, 0x45, 0xFF);
        public static readonly Color CardPressed = new Color32(0x2A, 0x36, 0x5A, 0xFF);
        public static readonly Color Player = new Color32(0x4D, 0xD0, 0xE1, 0xFF);
        public static readonly Color Block = new Color32(0xFF, 0x70, 0x6B, 0xFF);
        public static readonly Color Text = new Color32(0xE8, 0xEC, 0xF5, 0xFF);
        public static readonly Color SubText = new Color32(0xA9, 0xB3, 0xCC, 0xFF);
        public static readonly Color Accent = new Color32(0x4D, 0xD0, 0xE1, 0xFF);
        public static readonly Color AccentPressed = new Color32(0x80, 0xDE, 0xEA, 0xFF);
        public static readonly Color OnAccent = new Color32(0x00, 0x33, 0x3A, 0xFF);
        public static readonly Color Divider = new Color32(0x33, 0x3F, 0x63, 0xFF);

        // Signed distance to a rounded box (negative inside), in the same units as the inputs.
        static float RoundedBox(Vector2 p, Vector2 center, Vector2 half, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - (half - new Vector2(r, r));
            Vector2 qp = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return qp.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static Color Over(Color dst, Color src, float a)
        {
            a *= src.a;
            float outA = a + dst.a * (1f - a);
            if (outA <= 0f) return new Color(0, 0, 0, 0);
            Color c = (src * a + dst * dst.a * (1f - a)) / outA;
            c.a = outA;
            return c;
        }

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            return t;
        }

        // Anti-aliased rounded rectangle filling the texture.
        public static Texture2D RoundedRect(int w, int h, float radius, Color color)
        {
            var t = NewTex(w, h);
            var px = new Color[w * h];
            Vector2 half = new Vector2(w * 0.5f, h * 0.5f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = RoundedBox(new Vector2(x + 0.5f, y + 0.5f), half, half, radius);
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * w + x] = new Color(color.r, color.g, color.b, color.a * a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // The app icon: falling blocks above the player dot.
        public static Texture2D Icon(int size, bool withBackground)
        {
            var t = NewTex(size, size);
            var px = new Color[size * size];
            float s = size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    Color c = withBackground ? Background : new Color(0, 0, 0, 0);
                    float d1 = RoundedBox(p, new Vector2(0.36f, 0.68f) * s, new Vector2(0.16f, 0.065f) * s, 0.03f * s);
                    float d2 = RoundedBox(p, new Vector2(0.64f, 0.48f) * s, new Vector2(0.16f, 0.065f) * s, 0.03f * s);
                    float d3 = (p - new Vector2(0.5f, 0.27f) * s).magnitude - 0.105f * s;
                    c = Over(c, Block, Mathf.Clamp01(0.5f - d1));
                    c = Over(c, Block, Mathf.Clamp01(0.5f - d2));
                    c = Over(c, Player, Mathf.Clamp01(0.5f - d3));
                    px[y * size + x] = c;
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }
}
