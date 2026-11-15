# Android device test checklist

**Legend:**
- **A**: automated, already runs without a device (Unity EditMode/PlayMode tests).
- **D**: automated, needs a connected device (`tools/android-device-smoke-test.sh <apk>`).
- **M**: manual, needs a person holding the phone.

## How to run
- Unity tests (A):
  ```bash
  Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults editmode.xml
  Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults playmode.xml
  ```
  PlayMode runs real games, so it changes saved scores and coins in the editor. Back up and restore `~/.config/unity3d/DefaultCompany/Time Rush Nebula/prefs` around it.
- Device smoke test (D): plug in one phone with USB debugging on, then run `tools/android-device-smoke-test.sh Builds/release/<apk>`. It exits 0 when every check passes, and writes its logs, meminfo, gfxinfo and screenshots to the output folder.
- **Test devices:** at least one recent arm64 phone (Android 13 or later) and one low-end or older device (Android 8-10, 3 GB RAM or less). armeabi-v7a is included for the older ones.

## Installation and first launch
- [ ] D: APK installs (`adb install` reports Success)
- [ ] D: app starts and is still running after 20 s
- [ ] M: app icon and name "Time Rush Nebula" look right on the home screen (adaptive and round icons)
- [ ] M: first launch shows the "how to fly" card before the countdown
- [ ] M: portrait and landscape layouts fit; nothing is hidden by the notch or camera cutout (safe area)

## Main menu
- [ ] A: menu loads after Boot (every PlayMode test starts from Boot → MenuHub)
- [ ] M: every panel opens and closes: Stages, Settings, How to Play, Credits, Hangar
- [ ] M: Android Back closes panels, then asks before quitting

## Gameplay and controls
- [ ] A: combat mode installs; the player ship moves in X/Y/Z (`CombatPlayerController_Supports3DMovement`)
- [ ] M: touch: dragging on the left half moves the ship, holding on the right fires, swiping on the right switches weapon
- [ ] M: controls stay responsive with two fingers at once
- [ ] M: countdown 3-2-1, then the run starts

## Weapons
- [ ] A: all six weapons fire and damage a target (`AllSixWeapons_CanFire_AndDealDamage`)
- [ ] A: the first weapon is bound to the player team at start (regression for the laser fix)
- [ ] M: each weapon's visuals and sound are distinct and readable on a small screen

## Enemies and boss
- [ ] A: enemy AI moves and fires (`EnemyAI_CanMove_AndFireAtPlayer`)
- [ ] A: enemies are destroyed with an explosion (`EnemyHealth_CanBeDestroyed_AndExplosionSpawns`)
- [ ] A: waves progress, the boss spawns at the stage goal, and defeating it clears the stage (`Waves_…_ForStageVictory`)
- [ ] M: difficulty feels fair on stage 1. A player who doesn't move or fire dies in under 10 s; check whether that's intended.

## Results and progress
- [ ] A: death leads to Results; shields absorb damage before health (`PlayerHealth_…`)
- [ ] A: Continue-by-ad never grants a reward without a completed ad (`RewardedContinueSafetyTests`, `RewardedAdProviderSelectionTests`)
- [ ] A: stage stars, ranks, wallet and milestones save correctly (`StageModeTests`, `Progression*Tests`, `RankProgressionTests`)
- [ ] M: release build shows "Continue unavailable" for the ad option, and the coin continue works
- [ ] M: force-stop and reopen the app: best score, coins, stars and settings are kept

## Audio
- [ ] M: music in the menu, during a run and on Results; effects play; the volume sliders and mute work
- [ ] M: audio stops when the app is backgrounded and comes back on resume
- [ ] M: vibration toggle works (the `VIBRATE` permission is declared)

## Background, resume and interruptions
- [ ] A: backgrounding pauses the run and it stays paused on return (`BackgroundingTheApp_PausesTheRun_UntilThePlayerResumes`)
- [ ] D: same process survives HOME and relaunch
- [ ] M: an incoming call or notification shade mid-run pauses the game
- [ ] M: screen lock and unlock mid-run, then resume

## Performance, crashes and memory
- [ ] A: hot gameplay paths are allocation-free and LINQ-free (`MobilePerformanceTests`)
- [ ] D: no FATAL EXCEPTION, native crash or ANR in logcat
- [ ] D: memory (TOTAL PSS) recorded after launch. Track it across builds; investigate growth.
- [ ] M: steady frame rate during a busy boss fight on the low-end device; no overheating within 10 minutes
- [ ] M: 15-minute play session without a crash; battery drain noted
