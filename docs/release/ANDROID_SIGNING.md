# Android production signing

**Status:** there is no production or upload keystore yet. Builds made today are signed with Android's **debug key**. They install for testing but can't be uploaded to Google Play.

The pipeline is already set up to sign with your **upload key** as soon as you provide it as GitHub secrets. When signing is enabled, CI also builds the Play Store bundle (`.aab`).

## 1. Create the upload keystore (once, on your own computer)
This uses the `keytool` that comes with Unity's OpenJDK. Choose your own passwords, and don't put them in the command line history of a shared machine.

```bash
KEYTOOL=~/Unity/Hub/Editor/2022.3.62f3/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool
mkdir -p ~/keys && cd ~/keys
$KEYTOOL -genkeypair -v \
  -keystore timerush-upload.keystore \
  -alias timerush-upload \
  -keyalg RSA -keysize 4096 -validity 10000
# keytool prompts for the keystore password and the certificate name fields.
```

Modern `keytool` creates a **PKCS12** keystore, where the key password is always the same as the keystore password. Use that same value for `ANDROID_KEYALIAS_PASS` below.

- **Keep it out of the project.** Store the keystore and passwords outside the repository, with an offline backup. `*.keystore` and `*.jks` are already in `.gitignore`, but never copy the file into the project folder.
- **Use Play App Signing,** which is the default for new apps. Google holds the app signing key, and this keystore is only your upload key. If it's lost or leaked, Google can reset it.

## 2. Add the four GitHub secrets
Go to **Settings → Secrets and variables → Actions → New repository secret**.

| Secret | Value |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | output of `base64 -w0 ~/keys/timerush-upload.keystore` |
| `ANDROID_KEYSTORE_PASS` | the keystore password |
| `ANDROID_KEYALIAS_NAME` | `timerush-upload` (or the alias you chose) |
| `ANDROID_KEYALIAS_PASS` | the key password |

- **All four or none.** If only some are set, the workflow fails with "Incomplete signing secrets". With none, it builds a debug-signed APK and no AAB.
- **Passwords can't start with `-`.** The build script treats an argument starting with `-` as a missing value.

## 3. How the pipeline uses them
- **Decoding:** `.github/workflows/android-apk.yml` passes the secrets to `game-ci/unity-builder`, which decodes the keystore into the runner's workspace.
- **Applying:** `TimeRushBuild.BuildAndroidCI` applies the keystore and passwords **in memory only**, never logs them, and restores the previous signing settings after the build. Nothing is written to `ProjectSettings`.
- **Outputs:** a signed APK (artifact `TimeRushNebula-apk`) and a signed AAB (artifact `TimeRushNebula-aab`). Upload the AAB to Play Console.
- **Logs:** Unity prints its whole command line at the top of its log, and that includes the signing arguments. On GitHub Actions, secret values are masked as `***` automatically. **If you sign locally** with `-androidKeystorePass …`, the local Unity log contains the password, so don't share that log.
- **Local signed builds:** after one, `ProjectSettings.asset` may show `AndroidKeystoreName: '{inproject}: '`. That's Unity normalizing the restored empty name. Discard that one-line change; no password is ever stored there.

Signing was checked end to end on 2026-11-15 with a throwaway test keystore: a signed APK (verified with `apksigner`) and a signed AAB (`jar verified`, both ABIs). The default path signs with the Android debug key and leaves `ProjectSettings` untouched.

## 4. Check a signed build
```bash
BT=~/Unity/Hub/Editor/2022.3.62f3/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/34.0.0
JAVA_HOME=~/Unity/Hub/Editor/2022.3.62f3/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK \
  $BT/apksigner verify --print-certs TimeRushNebula.apk
# The signer DN must be yours, not "CN=Android Debug".
```
