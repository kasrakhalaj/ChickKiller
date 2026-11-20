using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChickenShooter.EditorTools
{
    /// <summary>Windows x86_64 build helpers (release build for submission, development build for smoke tests).</summary>
    public static class BuildTools
    {
        [MenuItem("Tools/Chick Killer/Build Windows (Release)")]
        public static void BuildRelease() => Build("Builds/Windows/ChickKiller.exe", BuildOptions.None);

        [MenuItem("Tools/Chick Killer/Build Windows (Development)")]
        public static void BuildDevelopment() => Build("Builds/Dev/ChickKiller.exe", BuildOptions.Development);

        private static void Build(string path, BuildOptions options)
        {
            var opts = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = options
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[BuildTools] {report.summary.result} -> {path} ({report.summary.totalErrors} errors, {report.summary.totalTime})");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
