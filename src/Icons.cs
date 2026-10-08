using System.IO;
using System.Reflection;
using UnityEngine;

namespace Cloakcraft
{
    /// Icons are embedded PNGs (assets/icons). A PNG with the same name in an `icons` folder next to
    /// Cloakcraft.dll overrides the embedded one, so painted art can be dropped in without a rebuild.
    public static class Icons
    {
        static readonly System.Collections.Generic.Dictionary<string, Sprite?> cache = new System.Collections.Generic.Dictionary<string, Sprite?>();

        public static Sprite? Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            var bytes = Load(name);
            Sprite? sprite = null;
            if (bytes != null)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Cloakcraft_" + name };
                if (ImageConversion.LoadImage(tex, bytes))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    sprite.name = tex.name;
                }
                else Plugin.Log.LogWarning($"Icon {name}.png failed to decode");
            }
            cache[name] = sprite;
            return sprite;
        }

        static byte[]? Load(string name)
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var file = Path.Combine(dir, "icons", name + ".png");
            if (File.Exists(file)) return File.ReadAllBytes(file);
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Cloakcraft.icons." + name + ".png");
            if (s == null) return null;
            using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray();
        }
    }
}
