using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Qqtang.UnityClient
{
    /// <summary>
    /// Three-tier runtime texture/sprite loader (ZERO images stored in Git):
    ///   1. StreamingAssets/ArtCache/daoist/<key>.png   (Daoist-chibi replacement art)
    ///   2. StreamingAssets/ArtCache/original/<key>.png (Decoded original QQTang fallback)
    ///   3. Procedural runtime Texture2D fallback       (Always playable on a fresh clone)
    /// </summary>
    public sealed class ArtAssetResolver
    {
        private readonly string _cacheRoot;
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, string> _resolvedSources = new Dictionary<string, string>();

        public ArtAssetResolver(string cacheRoot = null)
        {
            _cacheRoot = string.IsNullOrEmpty(cacheRoot)
                ? Path.Combine(Application.streamingAssetsPath, "ArtCache")
                : cacheRoot;
        }

        public string GetResolvedSource(string key) =>
            _resolvedSources.TryGetValue(key, out var src) ? src : "procedural";

        public Texture2D GetTexture(string key, Color fallbackColor, bool circleMask = false)
        {
            if (_textures.TryGetValue(key, out var cached)) return cached;

            string daoistPath = Path.Combine(_cacheRoot, "daoist", key.Replace('/', Path.DirectorySeparatorChar) + ".png");
            if (TryLoadPng(daoistPath, out var tex))
            {
                _textures[key] = tex;
                _resolvedSources[key] = "daoist";
                return tex;
            }

            string origPath = Path.Combine(_cacheRoot, "original", key.Replace('/', Path.DirectorySeparatorChar) + ".png");
            if (TryLoadPng(origPath, out tex))
            {
                _textures[key] = tex;
                _resolvedSources[key] = "original";
                return tex;
            }

            tex = CreateProceduralTexture(fallbackColor, circleMask);
            _textures[key] = tex;
            _resolvedSources[key] = "procedural";
            return tex;
        }

        public Sprite GetSprite(string key, Color fallbackColor, bool circleMask = false)
        {
            if (_sprites.TryGetValue(key, out var cached)) return cached;
            Texture2D tex = GetTexture(key, fallbackColor, circleMask);
            Sprite sp = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.15f),
                Mathf.Max(tex.width, tex.height));
            _sprites[key] = sp;
            return sp;
        }

        public Sprite GetCharacterSprite(byte roleId, string dirSuffix, Color teamTint)
        {
            int slot = Math.Abs((roleId - 1) % 4);
            string key = $"characters/role_{slot}_{dirSuffix}";
            return GetSprite(key, teamTint, circleMask: true);
        }

        public Texture2D GetMapElementTexture(int elementId, Color fallbackColor)
        {
            if (elementId <= 0) return GetTexture($"procedural/cell_{fallbackColor}", fallbackColor);
            return GetTexture($"maps/elem_{elementId}", fallbackColor);
        }

        private static bool TryLoadPng(string path, out Texture2D texture)
        {
            texture = null;
            if (!File.Exists(path)) return false;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(tex);
                    return false;
                }
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                texture = tex;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Texture2D CreateProceduralTexture(Color baseColor, bool circleMask)
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (circleMask)
                    {
                        float dx = (x - half) / half;
                        float dy = (y - half) / half;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist > 0.95f)
                        {
                            pixels[y * size + x] = Color.clear;
                        }
                        else
                        {
                            float shade = Mathf.Lerp(1.15f, 0.72f, dist);
                            pixels[y * size + x] = new Color(
                                Mathf.Clamp01(baseColor.r * shade),
                                Mathf.Clamp01(baseColor.g * shade),
                                Mathf.Clamp01(baseColor.b * shade),
                                baseColor.a);
                        }
                    }
                    else
                    {
                        bool border = x <= 1 || y <= 1 || x >= size - 2 || y >= size - 2;
                        pixels[y * size + x] = border ? baseColor * 0.65f : baseColor;
                        pixels[y * size + x].a = baseColor.a;
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return tex;
        }
    }
}
