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
        // game-ci passes -androidExportType androidAppBundle for a Play Store bundle (.aab);
        // anything else builds an installable APK.
        bool appBundle = string.Equals(GetArgument("-androidExportType"), "androidAppBundle", StringComparison.OrdinalIgnoreCase);
        string extension = appBundle ? ".aab" : ".apk";

        string output = GetArgument("-customBuildPath");
        if (string.IsNullOrEmpty(output))
        {
            output = Path.ChangeExtension(DefaultOutput, extension);
        }

        if (!output.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            output = Path.Combine(output, "TimeRushNebula" + extension);
        }

        ApplyCiVersion();

        string result;
        try
        {
            using (ApplyCiSigning())
            {
                result = Build(output, appBundle);
            }
        }
        catch (InvalidOperationException signingError)
        {
            Debug.LogError(signingError.Message);
            result = null;
        }

        if (result == null)
        {
            EditorApplication.Exit(1);
        }
    }

    private static string Build(string output, bool appBundle = false)
    {
        ApplySettings();

        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        string fullPath = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        EditorUserBuildSettings.buildAppBundle = appBundle;

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

        Debug.Log($"TimeRushBuild: {(appBundle ? "AAB" : "APK")} ready at {fullPath} ({report.summary.totalSize / (1024 * 1024)} MB).");
        return fullPath;
    }

    /// <summary>
    /// Signs with the upload keystore when game-ci provides one (-androidKeystoreName plus the
    /// passwords and alias decoded from repository secrets); otherwise Unity's debug key is used.
    /// Passwords are only applied in memory, never logged, and the previous signing settings are
    /// restored after the build so nothing is persisted into ProjectSettings.
    /// </summary>
    private static IDisposable ApplyCiSigning()
    {
        string keystore = GetArgument("-androidKeystoreName");
        string keystorePass = GetArgument("-androidKeystorePass");
        string alias = GetArgument("-androidKeyaliasName");
        string aliasPass = GetArgument("-androidKeyaliasPass");

        var previous = new SigningSnapshot();
        bool anyProvided = !string.IsNullOrEmpty(keystore) || !string.IsNullOrEmpty(keystorePass)
            || !string.IsNullOrEmpty(alias) || !string.IsNullOrEmpty(aliasPass);

        if (!anyProvided)
        {
            // Only the flag changes here; the keystore fields are left untouched (re-assigning an
            // empty keystore name makes Unity rewrite it as "{inproject}: " in ProjectSettings).
            PlayerSettings.Android.useCustomKeystore = false;
            Debug.Log("TimeRushBuild: signing with the Android debug key (no upload keystore provided).");
            return previous;
        }

        previous.RestoreKeystoreFields = true;

        string keystorePath = string.IsNullOrEmpty(keystore) ? null : Path.GetFullPath(keystore);
        if (keystorePath == null || !File.Exists(keystorePath) || string.IsNullOrEmpty(keystorePass)
            || string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(aliasPass))
        {
            previous.RestoreKeystoreFields = false;
            previous.Dispose();
            throw new InvalidOperationException(
                "TimeRushBuild: incomplete signing configuration (need keystore file, keystore password, key alias and key password).");
        }

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystorePath;
        PlayerSettings.Android.keystorePass = keystorePass;
        PlayerSettings.Android.keyaliasName = alias;
        PlayerSettings.Android.keyaliasPass = aliasPass;
        Debug.Log("TimeRushBuild: signing with the provided upload keystore.");
        return previous;
    }

    private sealed class SigningSnapshot : IDisposable
    {
        private readonly bool useCustomKeystore = PlayerSettings.Android.useCustomKeystore;
        private readonly string keystoreName = PlayerSettings.Android.keystoreName;
        private readonly string keystorePass = PlayerSettings.Android.keystorePass;
        private readonly string keyaliasName = PlayerSettings.Android.keyaliasName;
        private readonly string keyaliasPass = PlayerSettings.Android.keyaliasPass;

        public bool RestoreKeystoreFields { get; set; }

        public void Dispose()
        {
            PlayerSettings.Android.useCustomKeystore = useCustomKeystore;
            if (!RestoreKeystoreFields)
            {
                return;
            }

            PlayerSettings.Android.keystoreName = keystoreName;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = keyaliasName;
            PlayerSettings.Android.keyaliasPass = keyaliasPass;
        }
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

    /// <summary>
    /// game-ci passes -buildVersion and -androidVersionCode to the build method; apply them so every
    /// CI APK carries its own version and can be installed over an older one. Local builds that pass
    /// neither keep the project's own Player Settings.
    /// </summary>
    private static void ApplyCiVersion()
    {
        string version = GetArgument("-buildVersion");
        if (!string.IsNullOrEmpty(version) && !version.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            PlayerSettings.bundleVersion = version;
        }

        string versionCode = GetArgument("-androidVersionCode");
        if (int.TryParse(versionCode, out int code) && code > 0)
        {
            PlayerSettings.Android.bundleVersionCode = code;
        }

        Debug.Log($"TimeRushBuild: version {PlayerSettings.bundleVersion} (code {PlayerSettings.Android.bundleVersionCode}).");
    }

    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                // A flag passed without a value is followed directly by the next flag.
                string value = args[i + 1];
                return value.StartsWith("-", StringComparison.Ordinal) ? null : value;
            }
        }

        return null;
    }
}
