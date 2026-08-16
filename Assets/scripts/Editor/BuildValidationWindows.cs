using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hegemonia.EditorTools
{
    /// <summary>
    /// Entry point deterministico para builds locais e CI. O batchmode passa a
    /// falhar explicitamente se nao houver cenas ou se o BuildPipeline falhar.
    /// </summary>
    public static class BuildValidationWindows
    {
        [MenuItem("Hegemonia/Build/Validate Windows 64")]
        public static void BuildWindows64()
        {
            string outputFromEnvironment = Environment.GetEnvironmentVariable("HEGEMONIA_BUILD_OUTPUT");
            string outputPath = string.IsNullOrWhiteSpace(outputFromEnvironment)
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Windows", "Hegemonia-Mundial.exe"))
                : Path.GetFullPath(outputFromEnvironment);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("BuildValidation: nenhuma cena habilitada em EditorBuildSettings.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    "BuildValidation: build Windows falhou. Resultado=" + report.summary.result
                    + " erros=" + report.summary.totalErrors
                    + " avisos=" + report.summary.totalWarnings
                    + " tamanho=" + report.summary.totalSize);
            }

            Debug.Log(
                "BuildValidation: SUCCESS | target=Windows64 | scenes=" + scenes.Length
                + " | output=" + outputPath
                + " | size=" + report.summary.totalSize);
        }
    }
}
