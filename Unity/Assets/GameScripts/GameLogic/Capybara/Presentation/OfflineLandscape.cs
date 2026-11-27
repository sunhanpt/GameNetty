using UnityEngine;

namespace CapybaraRebuild
{
    internal static class OfflineLandscape
    {
        private const int Width = 384;

        internal static Color TerrainColor(ChapterMap map)
        {
            if (ColorUtility.TryParseHtmlString(map.groundColor, out var tint)) return tint;
            return Color.HSVToRGB(Mathf.Repeat(map.id * .137f, 1), .24f, .55f);
        }

        internal static Texture2D Distant(int mapId, Color terrain)
        {
            const int height = 160;
            var pixels = new Color32[Width * height];
            var sky = Color.Lerp(new Color(.22f, .36f, .47f), terrain, .22f);
            var horizon = Color.Lerp(new Color(.80f, .82f, .76f), terrain, .18f);
            var ridge = Color.Lerp(sky, horizon, .42f);
            var foothill = Color.Lerp(sky, terrain, .55f);
            float phase = Mathf.Repeat(mapId * .193f, 1);
            for (int x = 0; x < Width; x++)
            {
                float u = x / (float)Width;
                float ridgeTop = .33f + .075f * Wave(u, phase, 2) + .046f * Wave(u, phase, 5);
                float foothillTop = .17f + .055f * Wave(u, phase + .32f, 3) + .023f * Wave(u, phase, 7);
                for (int y = 0; y < height; y++)
                {
                    float v = y / (float)(height - 1);
                    Color color = Color.Lerp(horizon, sky, v);
                    color = Color.Lerp(color, ridge, Mathf.Clamp01((ridgeTop - v) * height + .5f));
                    color = Color.Lerp(color, foothill, Mathf.Clamp01((foothillTop - v) * height + .5f));
                    pixels[y * Width + x] = color;
                }
            }
            return Upload(pixels, height, "Offline distant landscape");
        }

        internal static Texture2D Roadside(int mapId, Color terrain)
        {
            const int height = 80;
            var pixels = new Color32[Width * height];
            var shadow = Color.Lerp(terrain, new Color(.11f, .20f, .23f), .42f);
            var highlight = Color.Lerp(terrain, new Color(.79f, .82f, .68f), .40f);
            float phase = Mathf.Repeat(mapId * .217f, 1);
            for (int x = 0; x < Width; x++)
            {
                float u = x / (float)Width;
                float top = .37f + .15f * Wave(u, phase, 4) + .08f * Wave(u, phase, 9);
                float lip = .21f + .059f * Wave(u, phase, 6);
                for (int y = 0; y < height; y++)
                {
                    float v = y / (float)(height - 1);
                    Color color = Color.Lerp(shadow, highlight, Mathf.Clamp01(v * 1.8f));
                    color = Color.Lerp(color, shadow, Mathf.Clamp01((lip - v) * height) * .35f);
                    color.a = Mathf.Clamp01((top - v) * height + .5f);
                    pixels[y * Width + x] = color;
                }
            }
            return Upload(pixels, height, "Offline roadside landscape");
        }

        private static float Wave(float u, float phase, int frequency) =>
            Mathf.Sin((u * frequency + phase) * (2 * Mathf.PI));

        private static Texture2D Upload(Color32[] pixels, int height, string name)
        {
            var texture = new Texture2D(Width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
