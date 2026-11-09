# THE GATEWAY: how the game fits together

Press **Play** in `Scenes/01_MissionControl`. The game runs through every scene on its own, from the opening cutscene to one of four endings, then restarts.

| Scene | Story | What the player does | Main script |
| --- | --- | --- | --- |
| `01_MissionControl` | Prologue | Opening cutscene: the rocket vanishes near Planet X | `CutsceneDirector` |
| `02_Cockpit` | Act I | Power puzzle, the planet that is not Earth, crash landing | `CockpitSequence` |
| `03_LandingSite` | Act I | Scan the air, salvage, find the ruins and the gate, meet the colony pilot (first Trust choice) | `LandingSiteSequence` |
| `04_FuelMoon` | Act II | No air: drill, coolant valve puzzle, refine fuel, decide how to split it | `FuelMoonSequence` |
| `05_GateWorld` | Act II | Find coordinate tablets, align the gate (wrong guesses burn fuel) | `GateWorldSequence` |
| `06_Harvester` | Acts III-IV | Get into the station (4 solutions), Architect records, the target list, the lost transmissions | `HarvesterSequence` |
| `07_Control` | Act V | Power the control console, make the final decision | `ControlSequence` |

Controls: WASD / mouse (or gamepad), **E** interact, **1-4** dialogue choices, **Tab** stats, hold **Esc** to skip the cutscene.

You can press Play in any scene from 04 on to test it by itself; it starts with 4 fuel cells and default stats.

## RPG systems

All of it lives in `Scripts/Core/GameState.cs` and carries between scenes.

- **Stats** (Engineering, Navigation, Diplomacy, Survival) start at 2. Each level gives 2 points, spent in the Tab panel.
- **XP** comes from puzzles, discoveries, records and solving without hints or mistakes.
- **Trust** (hidden, 0-100, starts at 30) is the colony pilot's trust in the player.
- **Fuel cells**: every gate jump and every wrong gate alignment burns one.

Where stats change how a problem is solved:

| Place | Stat | Effect |
| --- | --- | --- |
| Cockpit power puzzle | Engineering 3 | Extra power cell |
| Landing site | Navigation 3 | Salvage marked on the scanner |
| Anywhere with oxygen | Survival | 10% slower drain per point above 2 |
| Moon coolant valves | Engineering 3 | Shows the valve order |
| Gate world | Navigation 3 | First ring of the address for free |
| Station door | Engineering 3 / Navigation 3 / Trust 40 | Three ways in; searching outside always works |
| Final decision | Diplomacy 4 (with Trust 40+) | Persuade the pilot to share the network |

## Trust and the endings

Trust changes: approaching openly (+15) or watching first (-10), being honest about Earth, the fuel split (+8 even, +12 give, -12 keep), opening the station door together (+5), and what you tell the pilot about the lost transmissions (+12 everything, +4 part, -5 nothing).

| Ending | How to get it |
| --- | --- |
| **A New Beginning** | Choose to share the network with Trust 60+ (or Trust 40+ and Diplomacy 4), and you did not hide the transmissions |
| **Earth Above All** | Choose to take the network for Earth |
| **The Architects' Mistake** | Choose to walk away |
| **Lost in the Network** | Run out of fuel cells. With Trust 50+ the pilot shares 2 cells once; otherwise the journey ends |

Ending text is in `Scripts/Sequences/Endings.cs`. The endings are title cards for now; swap in cutscenes later.

## Swapping in your 3D models

Every stand-in object sits under a parent named `MODEL_Something` (for example `MODEL_Rocket`, `MODEL_ColonyPilot`, `MODEL_Gate`).

1. Drag your model into the scene as a child of the `MODEL_` object.
2. Delete the child called **Placeholder**.

Scripts point at the `MODEL_` parent, so nothing breaks. Trees and salvage crates are prefabs in `Prefabs/`.

## Editing the story

- **Dialogue and choices**: each scene's script in `Scripts/Sequences/`. Lines are plain text in the order they play.
- **Speaker names**: `Speakers` in `Scripts/Dialogue/Dialogue.cs` (currently COMMANDER, COLONY PILOT, SHIP AI).
- **Cutscene shots**: select *Cutscene Director* in `01_MissionControl`; move the markers under *Shot Markers* to reframe.
- **Gate addresses**: the *Target* list on each gate console's `GlyphPuzzle`.

## Rebuilding

**TSA > Build Intro Scenes** regenerates all seven scenes from `Editor/IntroSceneBuilder.cs` and `Editor/StorySceneBuilder.cs`. It **overwrites** them, so once you start hand-editing scenes or adding models, stop re-running it (or move your changes into the builder).
