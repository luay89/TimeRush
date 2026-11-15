#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Fills every Android icon slot from Assets/_Project/Art/Icons: adaptive (background + foreground
/// layers, Android 8+), round (Android 7.1) and legacy. Re-run after changing the artwork.
/// Batch usage: Unity -batchmode -quit -projectPath . -executeMethod AndroidIconSetup.Apply
/// </summary>
public static class AndroidIconSetup
{
    private const string Folder = "Assets/_Project/Art/Icons/";
    private const string LegacyIcon = "Assets/_Project/Art/AppIcon.png";

    [MenuItem("TimeRush/Android/Apply App Icons")]
    public static void Apply()
    {
        var background = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "AdaptiveBackground.png");
        var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "AdaptiveForeground.png");
        var round = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "RoundIcon.png");
        var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(LegacyIcon);
        if (!background || !foreground || !round || !legacy)
        {
            Debug.LogError("AndroidIconSetup: icon textures are missing under " + Folder);
            return;
        }

        Fill(AndroidPlatformIconKind.Adaptive, background, foreground);
        Fill(AndroidPlatformIconKind.Round, round);
        Fill(AndroidPlatformIconKind.Legacy, legacy);
        AssetDatabase.SaveAssets();
        Debug.Log("AndroidIconSetup: adaptive, round and legacy Android icons assigned.");
    }

    private static void Fill(PlatformIconKind kind, params Texture2D[] layers)
    {
        PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
        foreach (PlatformIcon icon in icons)
        {
            icon.SetTextures(layers);
        }

        PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
    }
}
#endif
