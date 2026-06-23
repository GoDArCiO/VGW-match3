using System.Collections.Generic;
using UnityEngine;

namespace Proto.Game
{
    /// <summary>The procedural gem/token silhouettes. White, anti-aliased, tinted per use via
    /// <c>SpriteRenderer.color</c> — distinct shapes keep a colour-only board readable.</summary>
    public enum SpriteShape
    {
        Circle,
        RoundedSquare,
        Diamond,
        Triangle,
        Hexagon,
        Star
    }

    /// <summary>
    /// Generates clean, anti-aliased WHITE shape sprites in code (no art assets — the headless pillar), one
    /// per shape, cached. The caller tints with <c>SpriteRenderer.color</c>, so a single sprite serves every
    /// colour of a shape. Reusable across the board and the meta view.
    /// </summary>
    public sealed class SpriteFactory
    {
        private readonly Dictionary<SpriteShape, Sprite> _cache = new Dictionary<SpriteShape, Sprite>();
        private readonly int _px;

        public SpriteFactory(int pixels = 128) { _px = Mathf.Max(16, pixels); }

        public Sprite Get(SpriteShape shape)
        {
            if (_cache.TryGetValue(shape, out var sprite)) return sprite;
            sprite = Build(shape);
            _cache[shape] = sprite;
            return sprite;
        }

        private Sprite Build(SpriteShape shape)
        {
            var tex = new Texture2D(_px, _px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[_px * _px];
            Vector2[] poly = PolygonFor(shape); // null for circle / rounded square (SDF cases)

            for (int y = 0; y < _px; y++)
            {
                for (int x = 0; x < _px; x++)
                {
                    // 2x2 supersample -> smooth edges.
                    float coverage = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float nx = ((x + (sx + 0.5f) * 0.5f) / _px) * 2f - 1f; // [-1,1]
                            float ny = ((y + (sy + 0.5f) * 0.5f) / _px) * 2f - 1f;
                            if (Inside(shape, poly, nx, ny)) coverage += 0.25f;
                        }
                    }
                    byte a = (byte)Mathf.RoundToInt(coverage * 255f);
                    pixels[y * _px + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, _px, _px), new Vector2(0.5f, 0.5f), _px);
        }

        private static bool Inside(SpriteShape shape, Vector2[] poly, float x, float y)
        {
            switch (shape)
            {
                case SpriteShape.Circle:
                    return x * x + y * y <= 0.92f * 0.92f;
                case SpriteShape.RoundedSquare:
                    return RoundedBox(x, y, 0.82f, 0.22f);
                default:
                    return PointInPolygon(poly, x, y);
            }
        }

        // Signed-distance rounded box: half-extent `h`, corner radius `r`.
        private static bool RoundedBox(float x, float y, float h, float r)
        {
            float qx = Mathf.Abs(x) - (h - r);
            float qy = Mathf.Abs(y) - (h - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - r <= 0f;
        }

        private static Vector2[] PolygonFor(SpriteShape shape)
        {
            switch (shape)
            {
                case SpriteShape.Diamond:
                    return new[] { new Vector2(0, 0.95f), new Vector2(0.85f, 0), new Vector2(0, -0.95f), new Vector2(-0.85f, 0) };
                case SpriteShape.Triangle:
                    return new[] { new Vector2(0, 0.9f), new Vector2(0.86f, -0.66f), new Vector2(-0.86f, -0.66f) };
                case SpriteShape.Hexagon:
                    return RegularPolygon(6, 0.92f, 90f);
                case SpriteShape.Star:
                    return StarPolygon(5, 0.96f, 0.46f, 90f);
                default:
                    return null;
            }
        }

        private static Vector2[] RegularPolygon(int sides, float radius, float startDeg)
        {
            var v = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (startDeg + i * 360f / sides) * Mathf.Deg2Rad;
                v[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            }
            return v;
        }

        private static Vector2[] StarPolygon(int points, float outer, float inner, float startDeg)
        {
            var v = new Vector2[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                float r = (i % 2 == 0) ? outer : inner;
                float a = (startDeg + i * 180f / points) * Mathf.Deg2Rad;
                v[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return v;
        }

        private static bool PointInPolygon(Vector2[] poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].y > y) != (poly[j].y > y)) &&
                    (x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                    inside = !inside;
            }
            return inside;
        }
    }
}
