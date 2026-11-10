# THE GATEWAY: how the game fits together

Press **Play** in `Scenes/01_MissionControl`. The game starts at the main menu and runs through every scene on its own, from the opening cutscene to one of four endings, then returns to the menu.

| Scene | Story | What the player does | Main script |
| --- | --- | --- | --- |
| `01_MissionControl` | Menu + prologue | Main menu (Play, Options, Save, Load). Play slides in the black bars and the rocket flies past; Mission Control watches the portal appear and swallow it | `MainMenu`, `CutsceneDirector` |
| `02_Memory` | Flashback | Launch day on a dying Earth: talk to people, collect the water ration, seed vault and guidance core, receive the grip ball, board. Launch plays as 1990s home video, then the 3D title flies in | `MemorySequence` |
| `03_Adrift` | Act I | The rocket hits a rock; the pilot wakes up floating outside. Throw the grip ball from rock to rock to reach the hatch before the air runs out | `AdriftSequence`, `GripBall` |
| `04_Cockpit` | Act I | Power puzzle, the planet that is not Earth, crash landing | `CockpitSequence` |
| `05_LandingSite` | Act I | Scan the air, salvage, find the ruins and the gate, meet the colony pilot (first Trust choice) | `LandingSiteSequence` |
| `06_FuelMoon` | Act II | No air: drill, coolant valve puzzle, refine fuel, decide how to split it | `FuelMoonSequence` |
| `07_GateWorld` | Act II | Find coordinate tablets, align the gate (wrong guesses burn fuel) | `GateWorldSequence` |
| `08_Harvester` | Acts III-IV | Get into the station (4 solutions), Architect records, the target list, the lost transmissions | `HarvesterSequence` |
| `09_Control` | Act V | Power the control console, make the final decision | `ControlSequence` |

Controls: WASD / mouse (or gamepad), **E** interact, **1-4** dialogue choices, **Tab** stats, hold **Esc** to skip the opening cutscene. In zero gravity: **left mouse** throws the grip ball, hold **right mouse** to reel in, **R** calls it back, **Space** pushes off a rock you are touching.

You can press Play in any scene from 02 on to test it by itself; the later chapters start with 4 fuel cells and default stats.

## Menu, saving and options

- The game **autosaves** whenever a chapter starts. **Save** on the menu keeps that progress in the save slot; **Load** continues from it. (`Scripts/Core/SaveSystem.cs`; stored in PlayerPrefs.)
- **Options** has four volume sliders, remembered between sessions.
- Fonts: **Michroma** for titles and **Rajdhani** for everything else (both from Google Fonts, SIL Open Font License; the licence files sit next to them in `Resources/Fonts/`). Change them in `UIFactory.DefaultFont` / `TitleFont`.

## The grip ball (zero gravity)

`Scripts/Player/GripBall.cs` and `ZeroGPlayer.cs`. There are no thrusters: once you move, you keep moving. Throw the ball at a rock or the hull and it grips; hold reel to pull yourself along the line and grab on; every throw also nudges you backwards a little. The ball returns by itself if it hits nothing within range.

| Stat | Effect in zero gravity |
| --- | --- |
| Navigation 3 | 30% longer throw |
| Engineering 3 | Faster reel |
| Survival | Suit air lasts longer |

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

## 3D models

The rocket, portal, grip ball, asteroids, people, launch tower, dead trees, crates and the 3D title letters are made in Blender **from code**: `Tools/blender/build_models.py`. Edit a model's function there and re-run:

```
/Applications/Blender.app/Contents/MacOS/Blender --background --python Tools/blender/build_models.py
```

Add `-- --preview rocket` (or `portal`, `ball`, `people`, ...) to render a quick picture into `Tools/blender/previews/` instead of exporting. Models land in `Models/`, textures in `Textures/`. The material names inside a model (for example `Rocket_White`) are swapped for the project material of the same name in `Materials/`, so colours and shine are tuned in Unity.

Everything in a scene still sits under a parent named `MODEL_Something` (for example `MODEL_Rocket`, `MODEL_ColonyPilot`). To use a different model:

1. Drag your model into the scene as a child of the `MODEL_` object.
2. Delete the old child (the built model, or **Placeholder** in the later chapters, which are still simple shapes).

Scripts point at the `MODEL_` parent, so nothing breaks. Trees and salvage crates in the landing site are prefabs in `Prefabs/`.

## Editing the story

- **Dialogue and choices**: each scene's script in `Scripts/Sequences/`. Lines are plain text in the order they play.
- **Speaker names**: `Speakers` in `Scripts/Dialogue/Dialogue.cs`.
- **Cutscene shots**: select *Cutscene Director* in `01_MissionControl`; move the markers under *Shot Markers* to reframe. The launch and title in `02_Memory` are timed in `MemorySequence.Launch()`.
- **Gate addresses**: the *Target* list on each gate console's `GlyphPuzzle`.

## Sound and voice (placeholder MVP)

- **Sound effects and music** are synthesized in code (`Scripts/Audio/SoundLibrary.cs`), so there are no audio files to manage yet. Each scene gets an ambience loop and a music pad (set in `AudioManager.SceneAudio`). To replace a sound with a real recording, put a clip named after it (for example `alarm.wav`, `footstep.wav`; names are in the `Sfx` class) in `Assets/_Project/Resources/Sfx/`.
- **Voice**: every line of dialogue has a placeholder recording made with the Mac's built-in text-to-speech (the voice for each speaker is set at the top of `Tools/generate_voices.py`; Mission Control is heard through a radio filter). Subtitles keep pace with the voice.
- **After changing or adding dialogue**, run `python3 Tools/generate_voices.py` (on a Mac) to record the new lines. A line with no recording still works; it just plays silently with a typing sound.
- **Recording real voices**: `Tools/voice_lines.csv` is the full script, one row per line, with the file name each recording must be saved as in `Assets/_Project/Resources/Voice/`.
- Volumes (master, music, effects, voice) are on the `[AudioManager]` object while playing.

## Controls notes

- Menus and panels (puzzles, the stats panel, dialogue choices) block walking and looking while open, and free the mouse. Everything goes through `Scripts/Player/InputLock.cs`.
- In the editor, **Esc** frees the mouse; click the Game view to capture it again.
- The prompt says why **E** does nothing: "(not yet)" for objects the story hasn't reached, "(after the conversation)" while someone is talking.

## Rebuilding

**TSA > Build Intro Scenes** regenerates all nine scenes from `Editor/OpeningSceneBuilder.cs` (01-03), `Editor/IntroSceneBuilder.cs` (04-05) and `Editor/StorySceneBuilder.cs` (06-09). It **overwrites** them, so once you start hand-editing scenes or adding models, stop re-running it (or move your changes into the builder).
