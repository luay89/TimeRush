// Compiled only when the Google Mobile Ads Unity plugin is installed: the TIMERUSH_ADMOB symbol is
// defined automatically by TimeRush.Runtime.asmdef when the "com.google.ads.mobile" package is
// present (or can be added by hand in Player Settings > Scripting Define Symbols when the plugin
// was imported as a .unitypackage). Without the plugin this file compiles to nothing and the game
// keeps its current behaviour: development builds use MockRewardedAdService, release builds
// report that no rewarded ad is available.
#if TIMERUSH_ADMOB
using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

/// <summary>
/// Real rewarded-ad provider backed by Google Mobile Ads, with Google's User Messaging Platform
/// consent flow. A reward is only reported from the SDK's own "user earned reward" callback; there
/// is no path that grants a reward without a completed ad.
/// </summary>
[DisallowMultipleComponent]
public sealed class AdMobRewardedAdService : MonoBehaviour, IRewardedAdService
{
    private RewardedAd loadedAd;
    private bool initialized;
    private bool loading;
    private bool showing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!AdsSettings.HasRewardedAdUnit)
        {
            Debug.Log("AdMobRewardedAdService: no rewarded ad unit configured; Continue-by-ad stays unavailable.");
            return;
        }

        if (FindObjectOfType<AdMobRewardedAdService>())
        {
            return;
        }

        var host = new GameObject("AdMobRewardedAdService");
        DontDestroyOnLoad(host);
        host.AddComponent<AdMobRewardedAdService>();
    }

    public bool IsReady => initialized && !showing && loadedAd != null && loadedAd.CanShowAd();

    private void Start()
    {
        // Deliver SDK callbacks on Unity's main thread so they can safely touch game objects.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        RequestConsentThenInitialize();
    }

    private void RequestConsentThenInitialize()
    {
        ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
        {
            if (updateError != null)
            {
                Debug.LogWarning($"AdMobRewardedAdService: consent information update failed: {updateError.Message}");
            }

            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (formError != null)
                {
                    Debug.LogWarning($"AdMobRewardedAdService: consent form failed: {formError.Message}");
                }

                if (ConsentInformation.CanRequestAds())
                {
                    InitializeAds();
                }
                else
                {
                    Debug.Log("AdMobRewardedAdService: consent does not allow ad requests; Continue-by-ad stays unavailable.");
                }
            });
        });
    }

    private void InitializeAds()
    {
        if (initialized)
        {
            return;
        }

        MobileAds.Initialize(_ =>
        {
            initialized = true;
            LoadAd();
        });
    }

    private void LoadAd()
    {
        if (loading || loadedAd != null || !initialized)
        {
            return;
        }

        loading = true;
        RewardedAd.Load(AdsSettings.RewardedAdUnitId, new AdRequest(), (ad, error) =>
        {
            loading = false;
            if (error != null || ad == null)
            {
                Debug.LogWarning($"AdMobRewardedAdService: rewarded ad failed to load: {(error != null ? error.GetMessage() : "no ad")}");
                return;
            }

            loadedAd = ad;
        });
    }

    public void Show(Action onReward, Action onClosed, Action<string> onError)
    {
        if (!IsReady)
        {
            onError?.Invoke("No rewarded ad is loaded.");
            onClosed?.Invoke();
            LoadAd();
            return;
        }

        RewardedAd ad = loadedAd;
        loadedAd = null;
        showing = true;

        ad.OnAdFullScreenContentClosed += () => FinishShow(ad, onClosed);
        ad.OnAdFullScreenContentFailed += adError =>
        {
            onError?.Invoke(adError != null ? adError.GetMessage() : "Rewarded ad failed to show.");
            FinishShow(ad, onClosed);
        };

        ad.Show(_ => onReward?.Invoke());
    }

    private void FinishShow(RewardedAd ad, Action onClosed)
    {
        showing = false;
        ad.Destroy();
        onClosed?.Invoke();
        LoadAd();
    }

    private void OnDestroy()
    {
        loadedAd?.Destroy();
        loadedAd = null;
    }
}
#endif
