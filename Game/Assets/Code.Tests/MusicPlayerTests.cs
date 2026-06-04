using Mimic.Game;
using NUnit.Framework;
using UnityEngine;

namespace Mimic.Tests
{
    public class MusicPlayerTests
    {
        [Test]
        public void MainThemeSettings_AreStable()
        {
            Assert.AreEqual("dungeon_main_theme", MusicPlayer.MainThemeId);
            Assert.AreEqual("Audio/sound/dungeon_main_theme", MusicPlayer.MainThemeResourcePath);
            Assert.AreEqual(0.5f, MusicPlayer.MainThemeVolume);
        }

        [Test]
        public void MainThemeClip_LoadsFromResources()
        {
            var clip = Resources.Load<AudioClip>(MusicPlayer.MainThemeResourcePath);

            Assert.IsNotNull(clip);
            Assert.AreEqual(MusicPlayer.MainThemeId, clip.name);
        }
    }
}
