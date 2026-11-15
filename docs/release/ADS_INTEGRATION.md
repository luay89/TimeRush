# Rewarded ads

## What ads do in the game
One thing only: on the Results screen, the player can watch a rewarded ad to **continue the run once**. Continuing with coins doesn't depend on ads, and neither does progression. Stages, stars, ranks and skins don't use ads at all.

`ResultsController` talks to ads only through `IRewardedAdService`: `IsReady` and `Show(onReward, onClosed, onError)`. A continue is granted only from `onReward`, and EditMode tests cover callback-ordering safety (`RewardedContinueSafetyTests`).

## Current state
| Build | Provider | Behaviour |
|---|---|---|
| Editor / development build | `MockRewardedAdService` | Simulates a reward after 0.5 s (configurable), for testing only |
| Release build | none | Reports "No rewarded-ad provider is configured". **No free continue** is ever granted. |

Prepared, but not yet active:
- **`AdMobRewardedAdService`:** a real Google Mobile Ads provider that asks for consent through Google's UMP SDK first. It reports a reward only from the SDK's "user earned reward" callback. It's compiled only when the SDK is installed (`TIMERUSH_ADMOB`), so **it hasn't been compiled yet**.
- **`AdsSettings`:**
  - Editor and dev builds use Google's public **sample** rewarded unit, which only serves test ads.
  - Release builds use `ProductionRewardedAdUnitIdAndroid`, which is **empty**. Until it's filled in, release builds stay honest: "no ad available".
- **`ResultsController`** prefers a real provider over the mock, which Boot always carries. This is covered by `RewardedAdProviderSelectionTests`.

## Steps to turn on real ads
They need your AdMob account and decisions, so they can't be done for you.
1. **Create the AdMob app and ad unit.** Create an AdMob app for `com.luay.timerush.nebula` and one **rewarded** ad unit. You get an app ID (`ca-app-pub-…~…`) and an ad unit ID (`ca-app-pub-…/…`).
2. **Install the Google Mobile Ads Unity plugin.**
   - Through OpenUPM, as package `com.google.ads.mobile`: `TIMERUSH_ADMOB` is defined automatically by `TimeRush.Runtime.asmdef`.
   - Or as a `.unitypackage`: then also add `TIMERUSH_ADMOB` under Player Settings → Android → Scripting Define Symbols.
   - Let the External Dependency Manager resolve the Android libraries.
3. **Set the app ID (required).** Put it under **Assets → Google Mobile Ads → Settings → Android App ID**. Without it, the app crashes on launch.
4. **Set the ad unit.** Put the rewarded ad unit ID in `AdsSettings.ProductionRewardedAdUnitIdAndroid`.
5. **Set up consent.** In AdMob → Privacy & messaging, create a GDPR consent message (and a US-states message if needed). `AdMobRewardedAdService` shows it through UMP before it requests any ads.
6. **Update Play Console:**
   - Contains ads: yes.
   - Data safety: the SDK collects the advertising ID, device data and diagnostics. Follow Google's published guidance for the Mobile Ads SDK.
   - Privacy policy: must mention ads.
   - The plugin adds the `INTERNET` and `com.google.android.gms.permission.AD_ID` permissions.
7. **Rebuild and check.**
   - Run EditMode and PlayMode tests and a dev build: you should see sample ads, and a continue only after an ad is completed.
   - Then make a release build, and confirm on a device that a real ad loads and that closing early grants nothing.

If you'd rather use Unity LevelPlay or another network, implement `IRewardedAdService` in a new class the same way. Nothing else needs to change.
