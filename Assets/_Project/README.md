# Intro sequence: how it fits together

Press **Play** in `Scenes/01_MissionControl`. The game runs through all three scenes on its own.

| Scene | What happens | Main script |
| --- | --- | --- |
| `01_MissionControl` | Opening cutscene: Mission Control tracks the rocket, a gate opens, the signal is lost | `CutsceneDirector` |
| `02_Cockpit` | The pilot wakes up, solves the power puzzle, sees the planet that is not Earth, picks a dialogue choice, crash-lands | `CockpitSequence` |
| `03_LandingSite` | Scan the air (oxygen), salvage 3 parts, find the ruins, follow the radio signal to a second rocket | `LandingSiteSequence` |

Controls: WASD / mouse (or gamepad), **E** to interact, **1-3** for dialogue choices, hold **Esc** to skip a cutscene.

## Swapping in your 3D models

Every stand-in object sits under a parent named `MODEL_Something` (for example `MODEL_Rocket`, `MODEL_FlightDirector`, `MODEL_Cockpit`).

1. Drag your model into the scene as a child of the `MODEL_` object.
2. Delete the child called **Placeholder**.

Scripts point at the `MODEL_` parent, so nothing breaks. Trees and salvage crates are prefabs in `Prefabs/`: put the new model in the prefab once and every copy updates.

## Editing the cutscene

Select **Cutscene Director** in `01_MissionControl`. Each shot has:
- **From / To**: camera markers (under *Shot Markers*). Move them in the Scene view to reframe a shot.
- **Duration**, **Field Of View**, **Shake**.
- **Lines**: subtitles (speaker + text). Speaker labels live in `Scripts/Dialogue/Dialogue.cs` (`Speakers`).
- **On Start**: events that fire when the shot begins (start the rocket, open the gate, trigger the alarm, cut the feed).

The end title card says `[GAME TITLE]`. Change it in *Cutscene Director > End Title*.

## Stats in this section

Stats live in `GameState` (it is created automatically and carries over between scenes).

- **Engineering 3+**: overclocks the power bus, so the cockpit puzzle can power everything at once.
- **Navigation 3+**: marks salvage on the scanner at the landing site.
- **Survival**: each point above 2 makes oxygen last 10% longer.

XP comes from puzzles, discoveries and solving without hints; each level gives 2 stat points.

## Rebuilding

**TSA > Build Intro Scenes** regenerates the three scenes from `Editor/IntroSceneBuilder.cs`. It **overwrites** them, so once you start hand-editing the scenes, stop re-running it (or move your changes into the builder).
