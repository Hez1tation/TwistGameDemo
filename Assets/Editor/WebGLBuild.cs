using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebGLBuild
{
    [MenuItem("Twist/Build WebGL")]
    public static void BuildFromMenu()
    {
        Build("Builds/WebGL");
    }

    // Batch mode: -executeMethod WebGLBuild.BuildFromCommandLine -buildPath <dir>
    public static void BuildFromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-buildPath");
        Build(i >= 0 && i + 1 < args.Length ? args[i + 1] : "Builds/WebGL");
    }

    static void Build(string path)
    {
        PlayerSettings.productName = "TwistGameDemo";
        // GitHub Pages does not send Content-Encoding headers for .br/.gz, so keep files uncompressed.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var report = BuildPipeline.BuildPlayer(scenes, path, BuildTarget.WebGL, BuildOptions.None);
        Debug.Log("WEBGL BUILD RESULT: " + report.summary.result + " -> " + path);
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
