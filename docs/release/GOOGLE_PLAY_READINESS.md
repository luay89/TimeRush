# Google Play readiness

Everything below is taken from the project and the built APK (`v1.0-preview.1`). Anything that depends on your decisions or your Play Console account is marked **(you)**.

## Technical requirements
| Requirement | Status |
|---|---|
| Upload format: Android App Bundle (`.aab`) | Ready in CI, but needs the upload-key secrets (see `ANDROID_SIGNING.md`) |
| Signed with the upload key; Play App Signing | **(you)** create the upload keystore. Current builds are debug-signed. |
| Target API level (new apps and updates) | targetSdk **36** ✔ |
| 64-bit support | `arm64-v8a` ✔ (plus `armeabi-v7a`) |
| 16 KB memory-page support | all native libraries 16 KB-aligned (`LOAD align 0x4000`) ✔ |
| minSdk | 22 (Android 5.1) |
| Release build | non-development, not debuggable ✔ |
| Package name | `com.luay.timerush.nebula`. It's **permanent** once uploaded; confirm it before the first upload. **(you)** |

## Versioning plan
- **versionName:** `1.0.<CI run number>`, for example `1.0.42`. Raise the `1.0` prefix in `.github/workflows/android-apk.yml` (`APP_VERSION`) for bigger releases.
- **versionCode:** the CI run number. It always increases, and Play rejects a code it has already seen.
- **Upload only CI-built, upload-key-signed AABs.** Local builds use the project's code (currently 1) and are for testing only.
- **Git tags:** `v<versionName>-preview.N` for GitHub pre-releases, and `v<versionName>` for store releases.

## Permissions
| Permission | Source | Needed |
|---|---|---|
| `android.permission.VIBRATE` | `Handheld.Vibrate()` in `GameAudio` (vibration setting) | yes |

There's no `INTERNET` permission. Adding ads would add `INTERNET` and `com.google.android.gms.permission.AD_ID` (see `ADS_INTEGRATION.md`).

## Data safety (current build)
What's true of this build, from the code and settings:
- **No network:** there's no networking code and no `INTERNET` permission, so the app can't send data anywhere.
- **No Unity services:** Analytics, Crash Reporting, Ads and Performance Reporting are all disabled in `UnityConnectSettings`.
- **Local data only:** scores, coins, stage stars, settings and the tutorial flag, in Android's `SharedPreferences`.

So for **this build**, the honest Data safety answer is **"No data collected or shared"**.

**This changes as soon as an ads SDK is added.** The form must then be updated from the SDK's documented data practices. **(you)**

## Privacy policy (you)
- **Policy:** Play Console asks every app for a privacy policy URL; check the current requirement in Play Console. This build collects nothing, and the policy can say so. Once ads are enabled, it must describe the ad SDK's data use and consent.
- **Hosting:** host it on a public page you control. A GitHub Pages page in this repository would work.

## Content rating (IARC questionnaire) (you)
- **What's in the game:** space combat with lasers, explosions and destroyed ships, with no blood and no humans. That's typically answered as fantasy violence.
- **What it doesn't have:** chat, user-generated content, location or purchases with real money. The in-game coins are earned, not bought.
- **Ads:** answer "contains ads" only once real ads ship.

## Target audience (you)
- **Under-13s:** if they're included, Families policy rules apply. Ads must then come from Families-certified SDKs, and consent rules are stricter.
- **13+:** choosing it keeps things simpler.

## Store listing assets
| Asset | Requirement | Status |
|---|---|---|
| App name | ≤ 30 characters | "Time Rush Nebula" (16) ✔ |
| Short description | ≤ 80 characters | to write **(you)** |
| Full description | ≤ 4000 characters | to write **(you)** |
| App icon | 512×512, 32-bit PNG with alpha | source `Assets/_Project/Art/AppIcon.png` is 1024×1024 **RGB without alpha**: export a 512×512 RGBA version |
| Feature graphic | 1024×500 PNG/JPEG | missing |
| Phone screenshots | 2-8, 16:9 or 9:16, 320-3840 px | can be captured with `GameSceneScreenshotRunner` (`TIMERUSH_SHOT_SIZE=1080x1920`) |
| Category | Games → Action / Arcade | **(you)** |
| Contact email | required on the listing | **(you)** |

## Release notes draft (for the first internal test)
> First test build of Time Rush Nebula: 3D space combat with six weapons, enemy waves and a boss fight across 10 stages. Please report crashes, control problems and performance issues.

## Internal testing checklist
1. [ ] Upload keystore created and the four `ANDROID_*` secrets added; the CI run is green with a signed AAB.
2. [ ] Unity CI secrets (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) added; EditMode tests green in CI.
3. [ ] PlayMode suite green locally (9/9) on the release commit.
4. [ ] `tools/android-device-smoke-test.sh` passes on two devices, and the manual items in `DEVICE_TEST_CHECKLIST.md` are done.
5. [ ] Play Console app created with the final package name. Play App Signing enabled.
6. [ ] Store listing, content rating, target audience, Data safety and privacy policy completed.
7. [ ] AAB uploaded to **Internal testing** with release notes; testers added; install from the Play link checked.
8. [ ] Only then: closed or open testing, then production.
