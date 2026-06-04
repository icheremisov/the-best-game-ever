using UnityEngine;

namespace Mimic.Game
{
    public static class MusicPlayer
    {
        public const string MainThemeId = "dungeon_main_theme";
        public const string MainThemeResourcePath = "Audio/sound/" + MainThemeId;
        public const float MainThemeVolume = 0.5f;

        private static AudioSource source;

        public static void PlayMainTheme()
        {
            EnsureSource();
            if (source.isPlaying && source.clip != null && source.clip.name == MainThemeId) return;

            var clip = Resources.Load<AudioClip>(MainThemeResourcePath);
            if (clip == null) return;

            source.clip = clip;
            source.loop = true;
            source.volume = MainThemeVolume;
            source.Play();
        }

        private static void EnsureSource()
        {
            if (source != null) return;

            var go = new GameObject("MusicPlayer");
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.volume = MainThemeVolume;
        }
    }
}
