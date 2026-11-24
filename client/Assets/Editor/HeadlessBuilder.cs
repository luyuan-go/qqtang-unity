using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Qqtang.UnityClient.Editor
{
    public static class HeadlessBuilder
    {
        private const string MainScene = "Assets/Scenes/Main.unity";

        public static void BuildWindows64()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string outputDir = Path.Combine(repoRoot, "local-runtime", "build");
            Directory.CreateDirectory(outputDir);
            string exePath = Path.Combine(outputDir, "QqtangDaoistClient.exe");

            PlayerSettings.companyName = "LinghuStudio";
            PlayerSettings.productName = "QqtangDaoistClient";
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[HeadlessBuilder] Result={summary.result} Errors={summary.totalErrors} Warnings={summary.totalWarnings} Output={exePath} Size={summary.totalSize}");
            if (summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }
    }
}
