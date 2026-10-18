using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShuYu
{
    /// <summary>命令行打包脚本：`-executeMethod ShuYu.BuildDemo.BuildWindows` 出 Windows 独立版 demo。</summary>
    public static class BuildDemo
    {
        public static void BuildWindows()
        {
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = "Build/demo/ShuYu.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] 成功：{summary.totalSize} bytes -> {summary.outputPath}");
            }
            else
            {
                Debug.LogError($"[Build] 失败：{summary.result}，错误数 {summary.totalErrors}");
                EditorApplication.Exit(1);
            }
        }
    }
}
