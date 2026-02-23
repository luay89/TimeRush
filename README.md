# TimeRush

Fast-paced third-person runner prototype built with Unity 2022.3 LTS. Dash through procedurally placed obstacles, chase high scores, and jump straight into playtests with a single scene load.

## Highlights
- **Instant Boot Flow** – `BootLoader` skips the splash and loads the `MenuHub` scene automatically.
- **Fail-Fast Gameplay** – `PlayerDeath` freezes time on collisions, shows a results screen, and gracefully restores the run.
- **Modular Scenes** – Menu, gameplay, and results hubs remain independent, making it easy to iterate on specific loops.
- **Lightweight Codebase** – Only the essentials are committed, so builds stay lean and reviews stay readable.

## Game Loop
1. Boot straight into `MenuHub`.
2. Hop into a run from the menu.
3. Survive obstacle waves till you crash.
4. Results scene summarizes the run and routes you back to the menu.

## Controls (default)
| Action | Input |
| --- | --- |
| Move | WASD / Left Stick |
| Jump | Space / South Button |
| Pause | Esc |

> Update the bindings inside `ProjectSettings/InputManager.asset` to match your control scheme.

## Project Layout
```
Assets/
 ├─ _Project/
 │   ├─ BootLoader.cs          // Loads MenuHub on boot
 │   └─ Scripts/
 │        └─ PlayerDeath.cs    // Handles collisions + results routing
 ├─ Obstacle.prefab            // Basic hazard block
 └─ PlayerHit.cs               // Placeholder for player hurt logic
```

## Requirements
- Unity **2022.3.62f3** (LTS)
- .NET Standard 2.1 scripting runtime (default for this Unity line)

## Getting Started
1. **Clone locally**
   ```bash
   git clone git@github.com:<your-org>/TimeRush.git
   cd TimeRush
   ```
2. **Open in Unity Hub** and select the 2022.3.62f3 editor.
3. **Set your scenes** in `File ▸ Build Settings` (MenuHub, Gameplay loop, Results).
4. **Press Play** in the editor to validate collisions & routing.

## Build & Run
1. Open `File ▸ Build Settings…`
2. Switch Platform (PC/Android/etc.) if needed.
3. Ensure `MenuHub` is first in the list (BootLoader relies on it).
4. Click **Build** (or **Build And Run** for a quick smoke test).

## Customization Hooks
- **Obstacle Tag** – Change `obstacleTag` in `PlayerDeath` to match new hazards.
- **Results Scene** – Update `resultsSceneName` when you rename or duplicate the summary screen.
- **Transition Timing** – Adjust the `WaitForSecondsRealtime` delay for fancier hit VFX.

## Troubleshooting
| Symptom | Fix |
| --- | --- |
| Player never transitions to results | Ensure the colliding object actually carries the `Obstacle` tag. |
| Time stays frozen after death | `OnDisable` in `PlayerDeath` should reset the scale. Confirm no other scripts pause time. |
| Scene fails to load | Add `Results` (or your renamed scene) to **File ▸ Build Settings**. |

## Roadmap Ideas
- Replace `PlayerHit` placeholder with actual health/shield logic.
- Add pooled obstacle spawner for endless runs.
- Integrate ScriptableObject-based difficulty curves.
- Ship a UI Toolkit HUD for cleaner readings.

## License
This repository is currently **proprietary / all rights reserved**. Please keep the GitHub project private unless you intentionally open-source it.
