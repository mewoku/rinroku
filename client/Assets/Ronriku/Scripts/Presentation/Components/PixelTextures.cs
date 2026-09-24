using System.Collections.Generic;
using UnityEngine;

namespace Ronriku.Presentation.Components
{
    /// <summary>
    /// Small point-filtered textures generated at runtime: ordered-dither (Bayer 4×4) radial glows and
    /// gradients give the "pixel ambient" look without image assets. Results are cached by parameters.
    /// </summary>
    public static class PixelTextures
    {
        private static readonly float[] Bayer =
        {
            0f / 16, 8f / 16, 2f / 16, 10f / 16,
            12f / 16, 4f / 16, 14f / 16, 6f / 16,
            3f / 16, 11f / 16, 1f / 16, 9f / 16,
            15f / 16, 7f / 16, 13f / 16, 5f / 16
        };

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        private static float Threshold(int x, int y) => Bayer[(x & 3) + 4 * (y & 3)];

        /// <summary>Soft round glow: alpha falls off with distance, quantised to <paramref name="steps"/> levels and dithered.</summary>
        public static Texture2D RadialGlow(Color color, int size = 48, int steps = 4)
        {
            string key = $"glow:{ColorUtility.ToHtmlStringRGBA(color)}:{size}:{steps}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = New(size, size);
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float intensity = Mathf.Clamp01(1f - d);
                intensity = intensity * intensity * (3f - 2f * intensity);
                float scaled = intensity * steps;
                int level = Mathf.FloorToInt(scaled);
                if (scaled - level > Threshold(x, y)) level++;
                float alpha = Mathf.Clamp01(level / (float)steps) * color.a;
                pixels[x + y * size] = new Color(color.r, color.g, color.b, alpha);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Cache[key] = tex;
        }

        /// <summary>Vertical two-colour gradient (top → bottom) with ordered dithering between 8 bands.</summary>
        public static Texture2D VerticalGradient(Color top, Color bottom, int width = 8, int height = 64)
        {
            string key = $"vgrad:{ColorUtility.ToHtmlStringRGBA(top)}:{ColorUtility.ToHtmlStringRGBA(bottom)}:{width}:{height}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = New(width, height);
            var pixels = new Color32[width * height];
            const int bands = 8;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // Texture row 0 is the bottom.
                float t = 1f - y / (float)(height - 1);
                float scaled = t * bands;
                int band = Mathf.FloorToInt(scaled);
                if (scaled - band > Threshold(x, y)) band++;
                pixels[x + y * width] = Color.Lerp(top, bottom, Mathf.Clamp01(band / (float)bands));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Cache[key] = tex;
        }

        /// <summary>Diagonal two-colour gradient used for glowing button fills.</summary>
        public static Texture2D DiagonalGradient(Color a, Color b, int width = 48, int height = 16)
        {
            string key = $"dgrad:{ColorUtility.ToHtmlStringRGBA(a)}:{ColorUtility.ToHtmlStringRGBA(b)}:{width}:{height}";
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = New(width, height);
            var pixels = new Color32[width * height];
            const int bands = 6;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float t = (x / (float)(width - 1)) * 0.8f + (1f - y / (float)(height - 1)) * 0.2f;
                float scaled = t * bands;
                int band = Mathf.FloorToInt(scaled);
                if (scaled - band > Threshold(x, y)) band++;
                pixels[x + y * width] = Color.Lerp(a, b, Mathf.Clamp01(band / (float)bands));
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Cache[key] = tex;
        }

        private static Texture2D New(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
    }
}
