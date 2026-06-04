using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Mimic.UI
{
    // Single source of truth for the UI font (legacy uGUI Text + TextMeshPro).
    // Monomakh ships with full Cyrillic, so it renders correctly in every player
    // (incl. WebGL, where the built-in LegacyRuntime/Arial lacks Cyrillic glyphs).
    public static class FontProvider
    {
        private const string FontResourcePath = "Fonts/Monomakh-Regular";
        private const string TmpFontResourcePath = "Fonts/Monomakh SDF";

        private static Font cached;
        private static bool tried;
        private static TMP_FontAsset cachedTmp;
        private static bool triedTmp;

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
                    Debug.LogWarning($"[FontProvider] Could not load '{FontResourcePath}' — falling back to LegacyRuntime.");
                    cached = Fallback();
                }
                return cached;
            }
        }

        public static TMP_FontAsset DefaultTmp
        {
            get
            {
                if (cachedTmp != null) return cachedTmp;
                if (triedTmp) return null;
                triedTmp = true;
                cachedTmp = Resources.Load<TMP_FontAsset>(TmpFontResourcePath);
                if (cachedTmp == null)
                    Debug.LogWarning($"[FontProvider] Could not load TMP font '{TmpFontResourcePath}'.");
                return cachedTmp;
            }
        }

        private static Font Fallback() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Walks the scene once and re-points EVERY uGUI Text to FontProvider.Default.
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
                if (t == null || t.font == def) continue;
                t.font = def;
                swapped++;
            }
            return swapped;
        }

        // Same for TextMeshPro — re-points EVERY TMP_Text to the Monomakh SDF asset.
        public static int ApplyTmpToAllScene()
        {
            int swapped = 0;
            var def = DefaultTmp;
            if (def == null) return 0;
            #if UNITY_2023_1_OR_NEWER
            var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            #else
            var texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
            #endif
            foreach (var t in texts)
            {
                if (t == null || t.font == def) continue;
                t.font = def;
                swapped++;
            }
            return swapped;
        }
    }
}
