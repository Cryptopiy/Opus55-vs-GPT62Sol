using UnityEngine;

namespace WaveSurvival
{
    /// <summary>Procedural PBR-ish surface textures (albedo + normal) for the arena, props and weapons.</summary>
    public static class SurfaceTextures
    {
        public struct Pair
        {
            public Texture2D Albedo;
            public Texture2D Normal;
        }

        static float Groove(float distPx, float width) => Mathf.Clamp01(1f - distPx / width);

        /// <summary>S-01: cast concrete wall panel (one 4 x 3 m panel per tile) with panel grooves and formwork tie holes.</summary>
        public static Pair ConcreteWall(int w = 512, int h = 384)
        {
            var p = new TexturePainter(w, h);
            Color baseCol = new Color(0.47f, 0.465f, 0.445f);
            p.ShadeWithHeight((float u, float v, int x, int y, out float height) =>
            {
                float big = Noise.Fbm(u, v, 3, 4, 11);
                float grain = Noise.Fbm(u, v, 48, 3, 23);
                float streak = Noise.Gradient2(u, v, 22, 2, 37);
                float pits = Noise.Cellular(u, v, 70, 41);
                float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                float groove = Groove(edge, 3.2f);
                float bevel = Mathf.Clamp01(1f - (edge - 3f) / 4f) * (1f - groove);

                // Formwork tie holes: 4 x 2 grid inside each panel.
                float hole = 0f, rim = 0f;
                for (int iy = 0; iy < 2; iy++)
                for (int ix = 0; ix < 4; ix++)
                {
                    float cx = (ix + 0.5f) / 4f * w, cy = (iy == 0 ? 0.22f : 0.78f) * h;
                    hole = Mathf.Max(hole, TexturePainter.Circle(x, y, cx, cy, 5.2f, 1.2f));
                    rim = Mathf.Max(rim, TexturePainter.Ring(x, y, cx, cy, 7.5f, 3f, 1.5f));
                }

                float stain = Mathf.SmoothStep(0f, 1f, (streak - 0.45f) * 3f) * Mathf.Pow(v, 1.5f);
                float shade = (0.74f + big * 0.42f) * (0.9f + grain * 0.2f) * (1f - stain * 0.28f);
                shade *= 1f - groove * 0.55f;
                shade *= 1f - hole * 0.7f;
                shade *= 1f + rim * 0.06f;
                if (pits < 0.09f) shade *= 0.72f;
                height = grain * 0.35f + big * 0.2f - groove * 1.4f - bevel * 0.35f - hole * 1.1f + rim * 0.15f - (pits < 0.09f ? 0.35f : 0f);
                return baseCol * shade;
            });
            return new Pair { Albedo = p.ToTexture("ConcreteWall"), Normal = p.NormalMap("ConcreteWall_N", 2.2f) };
        }

        /// <summary>Concrete floor slab (4 x 4 m per tile) with expansion joints, oil stains and aggregate speckles.</summary>
        public static Pair ConcreteFloor(int size = 512)
        {
            var p = new TexturePainter(size, size);
            Color baseCol = new Color(0.36f, 0.355f, 0.345f);
            Color oil = new Color(0.12f, 0.105f, 0.09f);
            p.ShadeWithHeight((float u, float v, int x, int y, out float height) =>
            {
                float big = Noise.Fbm(u, v, 2, 4, 101);
                float grain = Noise.Fbm(u, v, 64, 2, 113);
                float stainN = Noise.Fbm(u, v, 3, 4, 127);
                float scuff = Noise.Gradient2(u, v, 3, 40, 131);
                float speck = Noise.Hash(x, y, 139);
                float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                float joint = Groove(edge, 2.6f);
                float stain = Mathf.SmoothStep(0f, 1f, (stainN - 0.58f) * 6f);
                float shade = (0.78f + big * 0.38f) * (0.92f + grain * 0.16f);
                if (speck > 0.985f) shade *= 1.25f; else if (speck < 0.012f) shade *= 0.7f;
                shade *= 1f - Mathf.SmoothStep(0f, 1f, (scuff - 0.62f) * 8f) * 0.12f;
                Color c = Color.Lerp(baseCol * shade, oil, stain * 0.55f);
                c *= 1f - joint * 0.6f;
                height = grain * 0.25f - joint * 1.3f + (speck > 0.985f ? 0.2f : 0f);
                return c;
            });
            return new Pair { Albedo = p.ToTexture("ConcreteFloor"), Normal = p.NormalMap("ConcreteFloor_N", 2f) };
        }

        /// <summary>Painted steel plate with seams, rivets, scratches and rust (ceiling, beams, gate frames, lamps).</summary>
        public static Pair MetalPlate(Color paint, int seed, int size = 512)
        {
            var p = new TexturePainter(size, size);
            Color rust = new Color(0.32f, 0.16f, 0.07f);
            Color bare = new Color(0.55f, 0.56f, 0.58f);
            p.ShadeWithHeight((float u, float v, int x, int y, out float height) =>
            {
                float big = Noise.Fbm(u, v, 3, 4, seed);
                float rustN = Noise.Fbm(u, v, 5, 5, seed + 17);
                float scratch = Noise.Gradient2(u, v, 90, 6, seed + 29);
                float half = size * 0.5f;
                float sx = Mathf.Min(Mathf.Abs(x - half), Mathf.Min(x, size - 1 - x));
                float sy = Mathf.Min(Mathf.Abs(y - half), Mathf.Min(y, size - 1 - y));
                float seam = Groove(Mathf.Min(sx, sy), 2.2f);
                // Rivets every 32 px along the seams.
                float rivet = 0f;
                float rx = Mathf.Repeat(x, 32f) - 16f, ry = Mathf.Repeat(y, 32f) - 16f;
                if (sx < 12f) rivet = Mathf.Max(rivet, TexturePainter.Circle(sx, ry, 8f, 0f, 3.2f, 1.2f));
                if (sy < 12f) rivet = Mathf.Max(rivet, TexturePainter.Circle(rx, sy, 0f, 8f, 3.2f, 1.2f));
                float rustAmt = Mathf.SmoothStep(0f, 1f, (rustN - 0.6f) * 5f);
                float scr = Mathf.SmoothStep(0f, 1f, (scratch - 0.8f) * 10f);
                Color c = paint * (0.75f + big * 0.45f);
                c = Color.Lerp(c, bare, scr * 0.6f);
                c = Color.Lerp(c, rust, rustAmt * 0.75f);
                c *= 1f - seam * 0.6f;
                c *= 1f + rivet * 0.25f;
                height = big * 0.2f - seam * 1.2f + rivet * 1.1f - scr * 0.15f + rustAmt * 0.25f;
                return c;
            });
            return new Pair { Albedo = p.ToTexture("Metal_" + seed), Normal = p.NormalMap("Metal_" + seed + "_N", 2.4f) };
        }

        /// <summary>Shipping crate face (one face = one tile): raised frame, X braces, corner bolts and a hazard label.</summary>
        public static Pair CrateFace(int size = 512)
        {
            var p = new TexturePainter(size, size);
            Color paint = new Color(0.6f, 0.27f, 0.08f);
            Color label = new Color(0.82f, 0.66f, 0.1f);
            float frame = size * 0.085f;
            p.ShadeWithHeight((float u, float v, int x, int y, out float height) =>
            {
                float big = Noise.Fbm(u, v, 3, 4, 211);
                float wear = Noise.Fbm(u, v, 12, 3, 223);
                float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                float frameMask = Mathf.Clamp01((frame - edge) / 1.5f + 0.5f);
                float d1 = Mathf.Abs(x - y) / 1.41421f, d2 = Mathf.Abs(x + y - size) / 1.41421f;
                float brace = (1f - frameMask) * Mathf.Clamp01((size * 0.035f - Mathf.Min(d1, d2)) / 1.5f + 0.5f);
                float bolt = 0f;
                for (int i = 0; i < 4; i++)
                {
                    float cx = (i % 2 == 0) ? frame * 0.5f : size - frame * 0.5f;
                    float cy = (i < 2) ? frame * 0.5f : size - frame * 0.5f;
                    bolt = Mathf.Max(bolt, TexturePainter.Circle(x, y, cx, cy, 6f, 1.5f));
                }
                float lbl = TexturePainter.Rect(x, y, size * 0.3f, size * 0.62f, size * 0.7f, size * 0.76f, 1.5f) * (1f - brace);
                float stripe = Mathf.Repeat((x + y) / 22f, 1f) < 0.5f ? 1f : 0f;
                Color c = paint * (0.78f + big * 0.4f);
                if (lbl > 0f) c = Color.Lerp(c, stripe > 0.5f ? label : new Color(0.05f, 0.05f, 0.05f), lbl);
                float edgeWear = Mathf.Clamp01(1f - edge / 6f) * wear;
                c = Color.Lerp(c, new Color(0.45f, 0.45f, 0.46f), edgeWear * 0.7f);
                c *= 1f + frameMask * 0.08f + brace * 0.06f;
                height = frameMask * 1f + brace * 0.8f + bolt * 1.4f + big * 0.15f;
                return c;
            });
            return new Pair { Albedo = p.ToTexture("Crate"), Normal = p.NormalMap("Crate_N", 3f) };
        }

        /// <summary>Worn diagonal yellow/black hazard stripes (tiles horizontally and vertically).</summary>
        public static Pair Hazard(int w = 256, int h = 64)
        {
            var p = new TexturePainter(w, h);
            Color yellow = new Color(0.86f, 0.62f, 0.05f);
            Color black = new Color(0.04f, 0.04f, 0.045f);
            p.ShadeWithHeight((float u, float v, int x, int y, out float height) =>
            {
                float s = Mathf.Repeat((x + y) / 64f, 1f);
                // Anti-aliased stripe: yellow where s is in [0, 0.5); coverage = pixel distance to the stripe edge.
                float yellowCover = Mathf.Clamp01((0.25f - Mathf.Abs(s - 0.25f)) * 64f / 1.4f + 0.5f);
                float dirt = Noise.Fbm(u, v, 8, 3, 307);
                Color c = Color.Lerp(black, yellow, yellowCover);
                c *= 0.7f + dirt * 0.45f;
                height = dirt * 0.3f;
                return c;
            });
            return new Pair { Albedo = p.ToTexture("Hazard"), Normal = p.NormalMap("Hazard_N", 1f) };
        }

        /// <summary>Steel diamond tread plate for the ramps.</summary>
        public static Pair TreadPlate(int size = 256)
        {
            var p = new TexturePainter(size, size);
            Color steel = new Color(0.42f, 0.43f, 0.45f);
            p.ShadeWithHeight((float u, float v, int x, int y, out float height) =>
            {
                const float cell = 32f;
                float cx = Mathf.Repeat(x, cell) - cell * 0.5f;
                float cy = Mathf.Repeat(y, cell) - cell * 0.5f;
                bool flip = ((int)(x / cell) + (int)(y / cell)) % 2 == 0;
                // Elongated rotated "diamond" lug.
                float ax = flip ? (cx + cy) * 0.7071f : (cx - cy) * 0.7071f;
                float ay = flip ? (cy - cx) * 0.7071f : (cy + cx) * 0.7071f;
                float lug = Mathf.Clamp01(1f - Mathf.Sqrt((ax / 11f) * (ax / 11f) + (ay / 2.6f) * (ay / 2.6f)));
                float grime = Noise.Fbm(u, v, 4, 4, 401);
                Color c = steel * (0.7f + grime * 0.5f) * (1f + lug * 0.25f);
                height = Mathf.SmoothStep(0f, 1f, lug * 2.5f) * 1.2f + grime * 0.1f;
                return c;
            });
            return new Pair { Albedo = p.ToTexture("Tread"), Normal = p.NormalMap("Tread_N", 3.2f) };
        }

        /// <summary>Walnut-like wood grain for the shotgun stock and pump.</summary>
        public static Texture2D Wood(int size = 256)
        {
            var p = new TexturePainter(size, size);
            Color dark = new Color(0.17f, 0.075f, 0.03f), light = new Color(0.42f, 0.2f, 0.08f);
            p.Shade((u, v, x, y) =>
            {
                float warp = Noise.Fbm(u, v, 3, 3, 503) * 4f;
                float grain = Mathf.Sin((v * 18f + warp) * Mathf.PI * 2f) * 0.5f + 0.5f;
                float fine = Noise.Gradient2(u, v, 4, 64, 509);
                return Color.Lerp(dark, light, grain * 0.6f + fine * 0.4f);
            });
            return p.ToTexture("Wood");
        }

        /// <summary>Fine neutral grime/scratch texture multiplied onto weapons, enemies and props.</summary>
        public static Pair Grime(int size = 256)
        {
            var p = new TexturePainter(size, size);
            p.ShadeWithHeight((float u, float v, int x, int y, out float height) =>
            {
                float n = Noise.Fbm(u, v, 6, 4, 601);
                float s = Noise.Gradient2(u, v, 48, 4, 607);
                float scr = Mathf.SmoothStep(0f, 1f, (s - 0.78f) * 9f);
                float g = 0.8f + n * 0.3f + scr * 0.15f;
                height = n * 0.4f - scr * 0.3f;
                return new Color(g, g, g, 1f);
            });
            return new Pair { Albedo = p.ToTexture("Grime"), Normal = p.NormalMap("Grime_N", 1.6f) };
        }
    }
}
