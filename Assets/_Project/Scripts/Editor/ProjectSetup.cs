using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ChickenShooter.EditorTools
{
    /// <summary>
    /// One-time project configuration: registers the 4 required scenes in Build Settings
    /// (MainMenu must be index 0) and sets basic player settings.
    /// Run from the menu (Tools/Chick Killer/Setup Project) or via -executeMethod.
    /// </summary>
    public static class ProjectSetup
    {
        private const string SceneFolder = "Assets/_Project/Scenes/";
        private static readonly string[] SceneNames = { "MainMenu", "Level1", "Level2", "Level3" };

        [MenuItem("Tools/Chick Killer/Setup Project")]
        public static void Run()
        {
            EditorBuildSettings.scenes = SceneNames
                .Select(n => new EditorBuildSettingsScene(SceneFolder + n + ".unity", true))
                .ToArray();

            PlayerSettings.productName = "Chick Killer";
            PlayerSettings.companyName = "KasraKhalaj";
            // Start in borderless full screen at the monitor's native resolution, so the whole HUD is always
            // visible (a 1920x1080 *window* on a 1080p monitor pushed the bottom HUD under the taskbar).
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.allowFullscreenSwitch = true;   // Alt+Enter toggles windowed / full screen
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Build scenes: " +
                      string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path)));
        }
    }
}
