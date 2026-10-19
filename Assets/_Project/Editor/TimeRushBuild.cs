using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-click Android build of "Time Rush Nebula":
///  - menu: Time Rush > Build Android APK  (writes Builds/TimeRushNebula.apk and opens the folder)
///  - CI:   -executeMethod TimeRushBuild.BuildAndroidCI  (game-ci passes -customBuildPath)
/// Applies the app name, package id, icon and a 64-bit IL2CPP setup before building, so the APK
/// installs on any modern Android phone next to (not over) any other Time Rush build.
/// </summary>
public static class TimeRushBuild
{
    private const string ProductName = "Time Rush Nebula";
    private const string PackageId = "com.luay.timerush.nebula";
    private const string IconPath = "Assets/_Project/Art/AppIcon.png";
    private const string DefaultOutput = "Builds/TimeRushNebula.apk";

    [MenuItem("Time Rush/Build Android APK")]
    public static void BuildAndroidFromMenu()
    {
        string output = Build(DefaultOutput);
        if (output != null)
        {
            EditorUtility.RevealInFinder(output);
        }
    }

    public static void BuildAndroidCI()
    {
        string output = GetArgument("-customBuildPath");
        if (string.IsNullOrEmpty(output))
        {
            output = DefaultOutput;
        }

        if (!output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
        {
            output = Path.Combine(output, "TimeRushNebula.apk");
        }

        if (Build(output) == null)
        {
            EditorApplication.Exit(1);
        }
    }

    private static string Build(string output)
    {
        ApplySettings();

        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        string fullPath = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        EditorUserBuildSettings.buildAppBundle = false;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = fullPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"TimeRushBuild: Android build failed ({report.summary.result}, {report.summary.totalErrors} errors).");
            return null;
        }

        Debug.Log($"TimeRushBuild: APK ready at {fullPath} ({report.summary.totalSize / (1024 * 1024)} MB).");
        return fullPath;
    }

    private static void ApplySettings()
    {
        PlayerSettings.productName = ProductName;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageId);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon)
        {
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
        }
    }

    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
