using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PixelZombieSurvivor.Editor
{
    /// <summary>
    /// Builds a standalone Windows player so the game can be launched without the editor
    /// (and pinned to a desktop shortcut). Available from Tools > Pixel Zombie Survivor
    /// and from the command line:
    /// Unity.exe -batchmode -quit -projectPath &lt;project&gt; -executeMethod
    /// PixelZombieSurvivor.Editor.PlayerBuilder.BuildWindows64 -buildPath &lt;folder&gt;
    /// </summary>
    public static class PlayerBuilder
    {
        public const string DefaultBuildFolder = "Builds/Windows";
        public const string ExecutableName = "PixelZombieSurvivor.exe";
        public const string GameVersion = "1.2";

        [MenuItem("Tools/Pixel Zombie Survivor/Build Windows Player", priority = 30)]
        public static void BuildWindows64FromMenu()
        {
            string folder = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), DefaultBuildFolder));
            string result = BuildWindows64(folder);
            if (string.IsNullOrEmpty(result))
            {
                return;
            }

            // Keep the desktop / Start Menu shortcuts pointing at the fresh build.
            ShortcutCreator.CreateShortcuts(out string message);
            Debug.Log("[Pixel Zombie Survivor] " + message);
            EditorUtility.RevealInFinder(result);
        }

        public static void BuildWindows64()
        {
            string folder = ReadBuildPathArgument();
            string path = BuildWindows64(folder);
            if (string.IsNullOrEmpty(path))
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Builds the player and returns the produced executable path, or an empty string on failure.
        /// </summary>
        public static string BuildWindows64(string outputFolder)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[Pixel Zombie Survivor] Build aborted: no enabled scenes in Build Settings.");
                return string.Empty;
            }

            Directory.CreateDirectory(outputFolder);
            string executable = Path.Combine(outputFolder, ExecutableName);

            PlayerSettings.bundleVersion = GameVersion;
            PlayerSettings.runInBackground = true;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    $"[Pixel Zombie Survivor] Build failed with {summary.result}: " +
                    $"{summary.totalErrors} error(s), {summary.totalWarnings} warning(s).");
                return string.Empty;
            }

            Debug.Log(
                $"[Pixel Zombie Survivor] Build succeeded: {executable} " +
                $"({summary.totalSize / (1024f * 1024f):0.0} MB, {summary.totalTime.TotalSeconds:0}s).");
            return executable;
        }

        private static string ReadBuildPathArgument()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-buildPath", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(args[i + 1]);
                }
            }

            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), DefaultBuildFolder));
        }
    }
}
