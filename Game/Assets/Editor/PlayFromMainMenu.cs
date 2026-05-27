#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mimic.EditorTools
{
    // Wires up two conveniences:
    // 1. Toggle Editor Preferences flag: "playmode start scene = MainMenu".
    //    When ON, pressing the regular Play button always boots from MainMenu.unity,
    //    no matter which scene is currently open.
    // 2. Menu item "Mimic / Play From Main Menu" (Cmd/Ctrl+Shift+P) that flips
    //    the flag on, enters play mode, and reverts after exit.
    [InitializeOnLoad]
    public static class PlayFromMainMenu
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string PrefKey = "Mimic.PlayFromMainMenu.Always";
        private const string MenuAlways = "Mimic/Always Play From Main Menu";
        private const string MenuOnce = "Mimic/Play From Main Menu %#p"; // Cmd/Ctrl+Shift+P

        static PlayFromMainMenu()
        {
            EditorApplication.delayCall += SyncStartScene;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        // Menu item: persistent toggle. When checked, Play always starts from MainMenu.
        [MenuItem(MenuAlways)]
        private static void ToggleAlways()
        {
            bool value = !EditorPrefs.GetBool(PrefKey, false);
            EditorPrefs.SetBool(PrefKey, value);
            SyncStartScene();
        }

        [MenuItem(MenuAlways, true)]
        private static bool ToggleAlwaysValidate()
        {
            Menu.SetChecked(MenuAlways, EditorPrefs.GetBool(PrefKey, false));
            return true;
        }

        // Menu item: one-shot Play From Main Menu. Sets the playmode start scene for this run only.
        [MenuItem(MenuOnce)]
        private static void PlayOnce()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.playModeStartScene =
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
                EditorApplication.isPlaying = true;
            }
            else
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static void SyncStartScene()
        {
            bool always = EditorPrefs.GetBool(PrefKey, false);
            if (always)
                EditorSceneManager.playModeStartScene =
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
            else if (!EditorApplication.isPlaying)
                EditorSceneManager.playModeStartScene = null;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // On exit play mode, restore the regular behavior unless "Always" is on.
            if (change == PlayModeStateChange.EnteredEditMode && !EditorPrefs.GetBool(PrefKey, false))
                EditorSceneManager.playModeStartScene = null;
        }
    }
}
#endif
