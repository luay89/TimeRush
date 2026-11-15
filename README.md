# Time Rush: Nebula Edition

A fast arcade space runner for phones and desktop, built with Unity 2022.3 LTS. Fly through
a space battle, switch lanes and depth to dodge falling warships, shoot them with your laser,
grab shields, and chain near misses for a score multiplier.

## Modes
- **Endless run.** Survive as long as you can while the pace ramps up for two minutes. Best
  score, ranks, daily best and daily missions are tracked locally.
- **Stages.** 10 stages, each one a run with a finish line: survive the goal time (30–80 s) to
  clear it and unlock the next. Later stages start further along the difficulty curve. Clearing
  earns 1 star; scoring well (kills, near misses) earns 2 or 3.

## Features
- Hangar with ship colours bought with coins earned from runs.
- Continue once per run (rewarded-ad hook, mock service for now) or with coins.
- Settings: sound on/off, music and effects volume, camera shake, calm effects (less
  flashing), vibration, replay tutorial, credits.
- First-run "how to fly" card before the countdown; full How to Play page in the menu.
- Pause menu with accessibility toggles and a confirmed "Main Menu" exit; the game pauses itself
  when the app goes to the background. Android Back closes panels, then asks before quitting.
- Music is composed and synthesised in code at runtime (menu, run and results tracks); sound
  effects are Kenney CC0 clips plus a few synthesised interface sounds.

## Controls
| Action | Touch | Keyboard |
| --- | --- | --- |
| Change lane | Swipe left / right | A / D or arrow keys |
| Change depth | Swipe up / down | W / S or arrow keys |
| Fire | Tap | Space |
| Pause | Pause button | Esc / P |

## Scenes and flow
`Boot` → `MenuHub` → `Game` → `Results` (→ `Game` again or back to `MenuHub`).
`GameStateMachine` (Boot scene, DontDestroyOnLoad) owns every scene transition and pause state.

## Project layout
```
Assets/_Project/
  Scenes/            Boot, MenuHub, Game, Results
  Scripts/           runtime code
    Stages/          Stage mode: catalogue, saved stars, current selection
    Feedback/        HUD feel, audio (GameAudio), backdrop and VFX
    Cosmos/          menu/results universe, procedural soundtrack
    Combat/          enemies, countdown + tutorial, death sequence, announcer
    Fairness/, Patterns/   obstacle generation that always leaves a way out
  Editor/
    Tests/           EditMode tests (NUnit)
    Validation/      batch tools: screenshots, Android build, ad checks
    AndroidIconSetup.cs    assigns Android adaptive/round/legacy icons
  Art/Icons/         Android icon layers generated from Art/AppIcon.png
```

## Requirements
- Unity **2022.3.62f3** with Android Build Support (SDK/NDK/OpenJDK from the Hub) for phone builds.

## Building
- **Desktop:** File ▸ Build Settings, keep `Boot` first, Build.
- **Android:** switch platform to Android, then File ▸ Build Settings ▸ Build, or run
  `TimeRush ▸ Validation ▸ Build Android Release Artifacts`. Package id is
  `com.luay.timerush.nebula`. Before publishing to Google Play, set your own upload keystore in
  Player Settings ▸ Publishing Settings and replace the mock ad service with a real SDK.

## Checking the game without pressing Play
Run the EditMode tests:
```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```
Capture screenshots of the real flow (menu panels, a run, pause, results):
```bash
TIMERUSH_SHOT_MENU=settings,stages TIMERUSH_SHOT_STAGE=1 TIMERUSH_SHOT_GODMODE=1 \
TIMERUSH_SHOT_TIMES=5,20 TIMERUSH_SHOT_PAUSE=1 TIMERUSH_SHOT_RESULTS=1 TIMERUSH_SHOT_SIZE=720x1280 \
Unity -batchmode -projectPath . -executeMethod GameSceneScreenshotRunner.RunBatch
```
Images land in `Logs/Screenshots` (or `TIMERUSH_SHOT_DIR`). All options are documented at the top
of `Assets/_Project/Editor/Validation/GameSceneScreenshotRunner.cs`. Note that these runs play a
real run, so they update the editor's saved scores and coins.

## License
This repository is **proprietary / all rights reserved**.
