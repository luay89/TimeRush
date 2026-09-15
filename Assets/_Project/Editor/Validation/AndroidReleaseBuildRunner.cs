#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public static class AndroidReleaseBuildRunner
{
    private const string MenuPath = "TimeRush/Validation/Build Android Release Artifacts";
    private const string ArtifactBaseName = "TimeRush-phasei";
    private static readonly string UnityAndroidPlayerRoot = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer");
    private static readonly string UnityNdkClangPath = Path.Combine(UnityAndroidPlayerRoot, "NDK", "toolchains", "llvm", "prebuilt", "linux-x86_64", "bin", "clang++");
    private static readonly string UnitySdkManagerPath = Path.Combine(UnityAndroidPlayerRoot, "SDK", "cmdline-tools", "latest", "bin", "sdkmanager");
    private static readonly string UnityJdkPath = Path.Combine(UnityAndroidPlayerRoot, "OpenJDK", "bin", "java");

    private readonly struct BuildFlagSnapshot
    {
        public readonly BuildTarget ActiveBuildTarget;
        public readonly BuildTargetGroup ActiveBuildTargetGroup;
        public readonly bool BuildAppBundle;
        public readonly bool Development;
        public readonly bool AllowDebugging;
        public readonly bool ConnectProfiler;
        public readonly bool DeepProfiling;
        public readonly bool WaitForManagedDebugger;

        public BuildFlagSnapshot(
            BuildTarget activeBuildTarget,
            BuildTargetGroup activeBuildTargetGroup,
            bool buildAppBundle,
            bool development,
            bool allowDebugging,
            bool connectProfiler,
            bool deepProfiling,
            bool waitForManagedDebugger)
        {
            ActiveBuildTarget = activeBuildTarget;
            ActiveBuildTargetGroup = activeBuildTargetGroup;
            BuildAppBundle = buildAppBundle;
            Development = development;
            AllowDebugging = allowDebugging;
            ConnectProfiler = connectProfiler;
            DeepProfiling = deepProfiling;
            WaitForManagedDebugger = waitForManagedDebugger;
        }
    }

    [MenuItem(MenuPath)]
    public static void RunFromMenu()
    {
        RunBuildBatch();
    }

    public static void RunPhaseIAndroidReleaseBuild()
    {
        RunBuildBatch();
    }

    private static void RunBuildBatch()
    {
        string outputRoot = ResolveOutputRoot();
        Directory.CreateDirectory(outputRoot);

        string reportPath = Path.Combine(outputRoot, "phasei-android-build-report.txt");
        var report = new StringBuilder(2048);
        var snapshot = CaptureBuildFlags();
        bool aabSucceeded = false;
        bool apkSucceeded = false;

        try
        {
            AppendConfigurationReport(report, outputRoot);

            if (!SwitchToAndroid(report))
            {
                WriteReport(reportPath, report);
                ExitBatchMode(1);
                return;
            }

            if (!ValidateBundledAndroidToolchain(report))
            {
                WriteReport(reportPath, report);
                ExitBatchMode(1);
                return;
            }

            ForceReleaseBuildFlags();

            aabSucceeded = BuildArtifact(outputRoot, true, report);
            apkSucceeded = BuildArtifact(outputRoot, false, report);
        }
        catch (Exception exception)
        {
            report.AppendLine("Unhandled Exception: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            RestoreBuildFlags(snapshot);
            WriteReport(reportPath, report);
        }

        ExitBatchMode(aabSucceeded || apkSucceeded ? 0 : 1);
    }

    private static BuildFlagSnapshot CaptureBuildFlags()
    {
        return new BuildFlagSnapshot(
            EditorUserBuildSettings.activeBuildTarget,
            EditorUserBuildSettings.selectedBuildTargetGroup,
            EditorUserBuildSettings.buildAppBundle,
            EditorUserBuildSettings.development,
            EditorUserBuildSettings.allowDebugging,
            EditorUserBuildSettings.connectProfiler,
            EditorUserBuildSettings.buildWithDeepProfilingSupport,
            EditorUserBuildSettings.waitForManagedDebugger);
    }

    private static void RestoreBuildFlags(BuildFlagSnapshot snapshot)
    {
        EditorUserBuildSettings.buildAppBundle = snapshot.BuildAppBundle;
        EditorUserBuildSettings.development = snapshot.Development;
        EditorUserBuildSettings.allowDebugging = snapshot.AllowDebugging;
        EditorUserBuildSettings.connectProfiler = snapshot.ConnectProfiler;
        EditorUserBuildSettings.buildWithDeepProfilingSupport = snapshot.DeepProfiling;
        EditorUserBuildSettings.waitForManagedDebugger = snapshot.WaitForManagedDebugger;

        if (snapshot.ActiveBuildTarget != BuildTarget.Android && snapshot.ActiveBuildTargetGroup != BuildTargetGroup.Unknown)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(snapshot.ActiveBuildTargetGroup, snapshot.ActiveBuildTarget);
        }
    }

    private static bool SwitchToAndroid(StringBuilder report)
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android &&
            EditorUserBuildSettings.selectedBuildTargetGroup == BuildTargetGroup.Android)
        {
            report.AppendLine("Active Build Target: Android");
            return true;
        }

        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        report.AppendLine("Active Build Target: " + (switched ? "Android" : "Switch FAILED"));

        if (!switched)
        {
            Debug.LogError("AndroidReleaseBuildRunner: Failed to switch active build target to Android.");
        }

        return switched;
    }

    private static void ForceReleaseBuildFlags()
    {
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
        EditorUserBuildSettings.waitForManagedDebugger = false;
    }

    private static bool BuildArtifact(string outputRoot, bool buildAppBundle, StringBuilder report)
    {
        EditorUserBuildSettings.buildAppBundle = buildAppBundle;

        string extension = buildAppBundle ? "aab" : "apk";
        string label = buildAppBundle ? "AAB" : "APK";
        string outputPath = Path.Combine(outputRoot, ArtifactBaseName + "." + extension);

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            targetGroup = BuildTargetGroup.Android,
            target = BuildTarget.Android,
            locationPathName = outputPath,
            options = BuildOptions.None,
        };

        BuildReport buildReport = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = buildReport.summary;
        bool exists = File.Exists(outputPath);
        long fileBytes = exists ? new FileInfo(outputPath).Length : 0L;
        bool succeeded = summary.result == BuildResult.Succeeded && exists && fileBytes > 0L;

        report.AppendLine(label + " Result: " + summary.result);
        report.AppendLine(label + " Output: " + outputPath);
        report.AppendLine(label + " Size Bytes: " + fileBytes);
        report.AppendLine(label + " Total Errors: " + summary.totalErrors);
        report.AppendLine(label + " Total Warnings: " + summary.totalWarnings);
        report.AppendLine(label + " Duration Seconds: " + summary.totalTime.TotalSeconds.ToString("0.00"));
        report.AppendLine();

        Debug.Log($"[PhaseI Build] {label} result={summary.result} path={outputPath} bytes={fileBytes} errors={summary.totalErrors} warnings={summary.totalWarnings}");
        return succeeded;
    }

    private static void AppendConfigurationReport(StringBuilder report, string outputRoot)
    {
        report.AppendLine("PHASE I ANDROID RELEASE BUILD");
        report.AppendLine("============================");
        report.AppendLine("TimestampUtc: " + DateTime.UtcNow.ToString("O"));
        report.AppendLine("UnityVersion: " + Application.unityVersion);
        report.AppendLine("OutputRoot: " + outputRoot);
        report.AppendLine("CompanyName: " + PlayerSettings.companyName);
        report.AppendLine("ProductName: " + PlayerSettings.productName);
        report.AppendLine("ApplicationIdentifierAndroid: " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
        report.AppendLine("BundleVersion: " + PlayerSettings.bundleVersion);
        report.AppendLine("AndroidBundleVersionCode: " + PlayerSettings.Android.bundleVersionCode);
        report.AppendLine("ScriptingBackendAndroid: " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android));
        report.AppendLine("ArchitectureAndroid: " + PlayerSettings.Android.targetArchitectures);
        report.AppendLine("MinSdkAndroid: " + PlayerSettings.Android.minSdkVersion);
        report.AppendLine("TargetSdkAndroid: " + PlayerSettings.Android.targetSdkVersion);
        report.AppendLine("ApiCompatibilityAndroid: " + PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Android));
        report.AppendLine("ManagedStrippingAndroid: " + PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.Android));
        report.AppendLine("StripEngineCode: " + PlayerSettings.stripEngineCode);
        report.AppendLine("ColorSpace: " + PlayerSettings.colorSpace);
        report.AppendLine("DefaultOrientation: " + PlayerSettings.defaultInterfaceOrientation);
        report.AppendLine("UseCustomKeystore: " + PlayerSettings.Android.useCustomKeystore);
        report.AppendLine("DevelopmentBuildForced: False");
        report.AppendLine("ScriptDebuggingForced: False");
        report.AppendLine("ProfilerForced: False");
        report.AppendLine("GraphicsApiModeAndroid: " + DescribeGraphicsApiMode());
        report.AppendLine("EnabledScenes: " + string.Join(", ", GetEnabledScenes()));
        report.AppendLine("UnityBundledClangPath: " + UnityNdkClangPath);
        report.AppendLine("UnityBundledSdkManagerPath: " + UnitySdkManagerPath);
        report.AppendLine("UnityBundledJavaPath: " + UnityJdkPath);
        report.AppendLine();
    }

    private static bool ValidateBundledAndroidToolchain(StringBuilder report)
    {
        bool clangReady = ProbeExecutable(UnityNdkClangPath, "--version", out string clangOutput);
        bool sdkReady = ProbeExecutable(UnitySdkManagerPath, "--version", out string sdkOutput);
        bool javaReady = ProbeExecutable(UnityJdkPath, "-version", out string javaOutput);

        report.AppendLine("Bundled Toolchain Preflight:");
        report.AppendLine("- clang++: " + (clangReady ? "READY" : "BLOCKED"));
        AppendIndented(report, clangOutput);
        report.AppendLine("- sdkmanager: " + (sdkReady ? "READY" : "BLOCKED"));
        AppendIndented(report, sdkOutput);
        report.AppendLine("- java: " + (javaReady ? "READY" : "BLOCKED"));
        AppendIndented(report, javaOutput);
        report.AppendLine();

        if (clangReady && sdkReady && javaReady)
        {
            return true;
        }

        Debug.LogError("AndroidReleaseBuildRunner: Unity-bundled Android toolchain preflight failed. See build report for details.");
        return false;
    }

    private static bool ProbeExecutable(string executablePath, string arguments, out string detail)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            detail = "missing: " + executablePath;
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    detail = "process failed to start";
                    return false;
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(10000);

                string combined = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
                if (string.IsNullOrWhiteSpace(combined))
                {
                    combined = stderr;
                }

                detail = (combined ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(detail))
                {
                    detail = "exitCode=" + process.ExitCode;
                }

                return process.ExitCode == 0;
            }
        }
        catch (Exception exception)
        {
            detail = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

    private static void AppendIndented(StringBuilder report, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            report.AppendLine("  " + lines[i].Trim());
        }
    }

    private static string DescribeGraphicsApiMode()
    {
        if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android))
        {
            return "Automatic";
        }

        GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        if (apis == null || apis.Length == 0)
        {
            return "ExplicitEmpty";
        }

        var builder = new StringBuilder();
        for (int i = 0; i < apis.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(apis[i]);
        }

        return builder.ToString();
    }

    private static string[] GetEnabledScenes()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        int count = 0;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].enabled)
            {
                count++;
            }
        }

        var result = new string[count];
        int index = 0;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (!scenes[i].enabled)
            {
                continue;
            }

            result[index++] = scenes[i].path;
        }

        return result;
    }

    private static string ResolveOutputRoot()
    {
        string overrideRoot = Environment.GetEnvironmentVariable("TIMERUSH_PHASEI_BUILD_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            return Path.GetFullPath(overrideRoot);
        }

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TimeRush_Builds", "PhaseI", timestamp));
    }

    private static void WriteReport(string reportPath, StringBuilder report)
    {
        File.WriteAllText(reportPath, report.ToString());
        Debug.Log("[PhaseI Build] Wrote report to " + reportPath);
    }

    private static void ExitBatchMode(int exitCode)
    {
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exitCode);
        }
    }
}
#endif