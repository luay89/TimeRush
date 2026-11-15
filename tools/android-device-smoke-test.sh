#!/usr/bin/env bash
# Automated on-device smoke test for Time Rush Nebula (needs one Android device or emulator with
# USB debugging enabled). It installs the APK, then:
#   - launches the app and checks it is still running after 20 s (no crash on start-up)
#   - backgrounds it with HOME, brings it back, and checks the process survived
#   - records memory (PSS) and frame statistics while running
#   - scans logcat for fatal exceptions, ANRs and native crashes
# Gameplay itself (touch controls, weapons, boss, results) still needs a person: see
# docs/release/DEVICE_TEST_CHECKLIST.md.
#
# Usage: tools/android-device-smoke-test.sh path/to/TimeRushNebula.apk [output-dir]
set -euo pipefail

APK="${1:?usage: $0 path/to/app.apk [output-dir]}"
OUT="${2:-device-smoke-$(date +%Y%m%d-%H%M%S)}"
PKG="com.luay.timerush.nebula"
ACTIVITY="com.unity3d.player.UnityPlayerActivity"
UNITY_ANDROID="${UNITY_ANDROID:-$HOME/Unity/Hub/Editor/2022.3.62f3/Editor/Data/PlaybackEngines/AndroidPlayer}"
ADB="${ADB:-$UNITY_ANDROID/SDK/platform-tools/adb}"
command -v "$ADB" >/dev/null 2>&1 || ADB=adb

mkdir -p "$OUT"
fail=0
step() { echo "== $*"; }
check() { if eval "$2"; then echo "PASS  $1"; else echo "FAIL  $1"; fail=1; fi; }

devices=$("$ADB" devices | awk 'NR>1 && $2=="device"{print $1}')
if [ "$(echo "$devices" | grep -c .)" -ne 1 ]; then
  echo "Expected exactly one connected device in 'device' state, found: ${devices:-none}" >&2
  exit 2
fi

step "Device"
"$ADB" shell getprop ro.product.model | tee "$OUT/device.txt"
"$ADB" shell getprop ro.build.version.release | tee -a "$OUT/device.txt"
"$ADB" shell getprop ro.product.cpu.abi | tee -a "$OUT/device.txt"

step "Install"
"$ADB" install -r "$APK" | tee "$OUT/install.txt"
check "APK installs" "grep -q Success '$OUT/install.txt'"

step "Launch"
"$ADB" logcat -c
"$ADB" shell am force-stop "$PKG"
"$ADB" shell am start -W -n "$PKG/$ACTIVITY" | tee "$OUT/launch.txt"
sleep 20
check "Still running 20 s after launch" "[ -n \"\$('$ADB' shell pidof $PKG)\" ]"
"$ADB" shell dumpsys meminfo "$PKG" > "$OUT/meminfo-after-launch.txt" || true
"$ADB" shell dumpsys gfxinfo "$PKG" > "$OUT/gfxinfo.txt" || true
"$ADB" exec-out screencap -p > "$OUT/after-launch.png" || true

step "Background and resume"
pid_before=$("$ADB" shell pidof "$PKG" || true)
"$ADB" shell input keyevent KEYCODE_HOME
sleep 5
"$ADB" shell am start -n "$PKG/$ACTIVITY" >/dev/null
sleep 5
pid_after=$("$ADB" shell pidof "$PKG" || true)
check "Same process after background/resume" "[ -n '$pid_after' ] && [ '$pid_before' = '$pid_after' ]"
"$ADB" exec-out screencap -p > "$OUT/after-resume.png" || true

step "Logs"
"$ADB" logcat -d > "$OUT/logcat.txt"
check "No FATAL EXCEPTION" "! grep -q 'FATAL EXCEPTION' '$OUT/logcat.txt'"
check "No native crash (SIGSEGV/SIGABRT)" "! grep -qE 'Fatal signal (11|6)' '$OUT/logcat.txt'"
check "No ANR" "! grep -q 'ANR in $PKG' '$OUT/logcat.txt'"
grep -E "TOTAL PSS|TOTAL:" "$OUT/meminfo-after-launch.txt" | head -2 || true

echo "Artifacts in $OUT"
exit $fail
