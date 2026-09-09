using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Crease.EditorAutomation
{
    /// <summary>
    /// Deterministic command-line entry points for desktop player builds.
    /// The shell orchestrator passes -buildOutput so paths remain explicit.
    /// </summary>
    public static class WeeklyBuild
    {
        private const string BuildOutputArgument = "-buildOutput";

        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Windows/Crease.exe");
        }

        public static void BuildMacOS()
        {
            Build(BuildTarget.StandaloneOSX, "Mac/Crease.app");
        }

        private static void Build(BuildTarget target, string defaultRelativeOutput)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are configured in EditorBuildSettings.");
            }

            var outputPath = GetCommandLineValue(BuildOutputArgument);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", "..", "Builds", defaultRelativeOutput));
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException($"Invalid build output path: {outputPath}");
            }

            Directory.CreateDirectory(outputDirectory);

            Debug.Log($"Building {target} to {outputPath} with {scenes.Length} enabled scenes.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            });

            var summary = report.summary;
            Debug.Log(
                $"Build result: {summary.result}; output={summary.outputPath}; " +
                $"size={summary.totalSize}; duration={summary.totalTime}; " +
                $"warnings={summary.totalWarnings}; errors={summary.totalErrors}");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{target} build failed with {summary.totalErrors} errors. See the Unity build log.");
            }
        }

        private static string GetCommandLineValue(string argumentName)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(arguments[index + 1]);
                }
            }

            return null;
        }
    }
}
