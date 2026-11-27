using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SlotGame.EditorTools
{
    /// <summary>Builds the shippable players from the command line or the Slot Game menu.</summary>
    public static class BuildScript
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Slot Game/2. Build Windows Player", priority = 20)]
        public static void BuildWindows()
        {
            PlayerSettings.companyName = "Slot Machine Assignment";
            PlayerSettings.productName = "Slot Machine";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1024;
            PlayerSettings.defaultScreenHeight = 768;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            Build(BuildTarget.StandaloneWindows64, "Build/Windows/SlotMachine.exe");
        }

        [MenuItem("Slot Game/3. Build WebGL", priority = 21)]
        public static void BuildWebGL()
        {
            // Uncompressed on purpose: Unity's gzip/brotli builds need the web server to
            // set Content-Encoding, which plain static hosts (python -m http.server,
            // GitHub Pages) do not do. An uncompressed build runs anywhere.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;

            Build(BuildTarget.WebGL, "Build/WebGL");
        }

        private static void Build(BuildTarget target, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Build");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] {target} build succeeded: {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime.TotalSeconds:F1}s -> {outputPath}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }

                return;
            }

            Debug.LogError($"[BuildScript] {target} build failed: {summary.result} ({summary.totalErrors} errors)");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
