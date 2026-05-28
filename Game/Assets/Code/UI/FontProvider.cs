using UnityEngine;
using UnityEngine.UI;

namespace Mimic.UI
{
    // Single source of truth for the UI font.
    // Unity's built-in `LegacyRuntime.ttf` (a.k.a. Arial) does NOT include Cyrillic
    // glyphs when the WebGL player runs in a browser — text rendered with it shows
    // as missing-glyph boxes. Roboto-Regular ships with full Latin + Cyrillic,
    // and we load it from Resources so it works in every player.
    public static class FontProvider
    {
        private const string FontResourcePath = "Fonts/Roboto-Regular";
        private static Font cached;
        private static bool tried;

        public static Font Default
        {
            get
            {
                if (cached != null) return cached;
                if (tried) return Fallback();

                tried = true;
                cached = Resources.Load<Font>(FontResourcePath);
                if (cached == null)
                {
                    Debug.LogWarning($"[FontProvider] Could not load '{FontResourcePath}' — falling back to LegacyRuntime (no Cyrillic in WebGL).");
                    cached = Fallback();
                }
                return cached;
            }
        }

        private static Font Fallback() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Walks the scene once and re-points any Text whose font is the legacy fallback
        // to FontProvider.Default. Cheap to run at scene start.
        public static int ApplyToAllScene()
        {
            int swapped = 0;
            var def = Default;
            if (def == null) return 0;
            #if UNITY_2023_1_OR_NEWER
            var texts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            #else
            var texts = Resources.FindObjectsOfTypeAll<Text>();
            #endif
            foreach (var t in texts)
            {
                if (t == null) continue;
                if (t.font == def) continue;
                // Only swap default fallback fonts; leave intentional custom fonts alone.
                if (t.font == null
                    || t.font.name == "LegacyRuntime"
                    || t.font.name == "Arial"
                    || t.font.name == "ArialMT")
                {
                    t.font = def;
                    swapped++;
                }
            }
            return swapped;
        }
    }
}
