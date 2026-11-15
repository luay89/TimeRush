/// <summary>
/// Rewarded-ad configuration for the real ad provider (see AdMobRewardedAdService and
/// docs/release/ADS_INTEGRATION.md).
///
/// Editor and development builds always use Google's public sample rewarded unit, which only ever
/// serves test ads. Release builds use the production unit below; while it is empty, release builds
/// report "no rewarded ad available" and the Continue-by-ad option stays unavailable. Nothing here
/// grants a reward on its own.
/// </summary>
public static class AdsSettings
{
    /// <summary>Google's documented sample rewarded ad unit for Android (test ads only).</summary>
    public const string GoogleSampleRewardedAdUnitIdAndroid = "ca-app-pub-3940256099942544/5224354917";

    /// <summary>
    /// The production rewarded ad unit id from your AdMob account (format ca-app-pub-XXXX/YYYY).
    /// Leave empty until the AdMob app and ad unit exist and the store listing declares ads.
    /// </summary>
    public const string ProductionRewardedAdUnitIdAndroid = "";

    public static string RewardedAdUnitId
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return GoogleSampleRewardedAdUnitIdAndroid;
#else
            return ProductionRewardedAdUnitIdAndroid;
#endif
        }
    }

    public static bool HasRewardedAdUnit => !string.IsNullOrEmpty(RewardedAdUnitId);
}
