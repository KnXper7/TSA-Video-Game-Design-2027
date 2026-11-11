# THE GATEWAY: how the game fits together

Press **Play** in `Scenes/01_MissionControl`. The game starts at the main menu and runs through every scene on its own, from the opening cutscene to one of four endings, then returns to the menu.

| Scene | Story | What the player does | Main script |
| --- | --- | --- | --- |
| `01_MissionControl` | Menu + prologue | Main menu (title, Play, Options). Play slides in the black bars and the rocket flies past; Mission Control watches the portal appear and swallow it | `MainMenu`, `CutsceneDirector` |
| `02_Memory` | Flashback | Launch day on a dying Earth: talk to people, collect the water ration, seed vault and guidance core, receive the grip ball, board. Launch plays as 1990s home video, then the 3D title flies in | `MemorySequence` |
| `03_Adrift` | Act I | The rocket hits a rock; the pilot wakes up floating outside. Throw the grip ball from rock to rock to reach the hatch before the air runs out | `AdriftSequence`, `GripBall` |
| `04_Cockpit` | Act I | Power puzzle, the planet that is not Earth, crash landing | `CockpitSequence` |
| `05_LandingSite` | Act I | Scan the air, salvage, find the ruins and the gate, meet the colony pilot (first Trust choice) | `LandingSiteSequence` |
| `06_FuelMoon` | Act II | No air: drill, coolant valve puzzle, refine fuel, decide how to split it | `FuelMoonSequence` |
| `07_GateWorld` | Act II | Find coordinate tablets, align the gate (wrong guesses burn fuel) | `GateWorldSequence` |
| `08_Harvester` | Acts III-IV | Get into the station (4 solutions), Architect records, the target list, the lost transmissions | `HarvesterSequence` |
| `09_Control` | Act V | Power the control console, make the final decision | `ControlSequence` |

Controls: WASD / mouse (or gamepad), **E** interact, **1-4** dialogue choices, **Tab** stats, hold **Esc** to skip the opening cutscene. In zero gravity: **left mouse** throws the grip ball; when it grips it pulses, then hold **left mouse** to pull yourself in; **R** calls it back, **Space** pushes off a rock you are touching.

You can press Play in any scene from 02 on to test it by itself; the later chapters start with 4 fuel cells and default stats.

## Menu, saving and options

- The game still **autosaves** whenever a chapter starts (`Scripts/Core/SaveSystem.cs`; stored in PlayerPrefs), but the menu no longer has Save or Load buttons, so there is currently no way to continue a saved game: Play always starts a new one.
- **Options** has four volume sliders, remembered between sessions.
- Fonts: **Michroma** for titles and **Rajdhani** for everything else (both from Google Fonts, SIL Open Font License; the licence files sit next to them in `Resources/Fonts/`). Change them in `UIFactory.DefaultFont` / `TitleFont`.

## The grip ball (zero gravity)

`Scripts/Player/GripBall.cs` and `ZeroGPlayer.cs`. There are no thrusters: once you move, you keep moving. Throw the ball at a rock or the hull and it grips; hold reel to pull yourself along the line and grab on; every throw also nudges you backwards a little. The ball returns by itself if it hits nothing within range.

| Stat | Effect in zero gravity |
| --- | --- |
| Navigation 3 | 30% longer throw |
| Engineering 3 | Faster reel |
| Survival | Suit air lasts longer |

### Ball puzzles (`BallSurface`)

Every puzzle is built from what the ball does when it hits something. Add a `BallSurface` component to any object with a collider:

- **Grip** (the default, no component needed): the ball bites in and you can pull yourself to it.
- **Frost** (pale blue glaze): nothing grips it. The ball skips off at the mirror angle, up to 4 times. Bank shots round corners; also a rock you can't use as a stepping stone.
- **Loose** (needs a Rigidbody): the ball can't hold it, but the hit shoves it away. Clear a path.
- **Target**: a switch. `WasHit` / `Hit` tell a sequence it was struck.

Level 1 (practice range, east of the pad) teaches all three. Level 2 uses frost rocks on the route and loose debris wedged on the hatch.

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

## TSA rules to keep in mind

From the event guide (`HS - Video Game Design`): the game must be **playable in a web browser from a link** (no downloads or .exe), judges score the **first three levels**, the rating must be **E**, and a **portfolio PDF** (hand-drawn storyboard, control list, copyright checklist, Resources and AI Reflection) and a **demo video under 5 minutes** are required or the entry is not judged. Every asset not made by the team, and every use of AI, has to be listed. HDRP cannot be used because it does not run in a browser; the project stays on URP.

## AI-generated and third-party assets (list these in the portfolio)

- `Textures/Generated/*`: screen graphics and ground, bark, leaf, rock, wall and floor textures, made with Higgsfield (GPT Image).
- `Models/People/Astronaut.fbx` and `AstronautGlove.fbx`: made with Higgsfield (Meshy text-to-3D), converted by `Tools/blender/import_generated.py`.
- `Models/People/*` (other characters): MakeHuman / MPFB, CC0.
- Fonts: Michroma and Rajdhani, SIL Open Font License.
- `Resources/Sfx/*.wav`: music (`pad_tense`, `pad_calm`, `pad_mystery`, `pad_dark`, made with Higgsfield / ElevenLabs Music) and sound effects (`rocket_roar`, `explosion`, `clank`, `throw`, `whoosh`, `portal_open`, `wind`, `ricochet`, made with Higgsfield / Mirelo). A file here replaces the synthesized sound of the same name; delete it to go back.
- `Docs/Targets/*`: realistic target images (Higgsfield GPT Image) and `glove_throw_seedance.mp4` (Higgsfield Seedance), used only as references for art and animation.
- `Textures/PBR/*`: surface textures from ambientCG, CC0.
- Voices: macOS text-to-speech placeholders (replace with team recordings).
- Code and scene building were written with an AI assistant (Claude).

## People

The characters are real human models made with **MPFB** (MakeHuman for Blender, free and CC0) from recipes in `Tools/blender/build_people.py`: body shape, age, skin, hair and clothes are a few lines each. Change a recipe and run:

```
/Applications/Blender.app/Contents/MacOS/Blender --background --python Tools/blender/build_people.py
```

Add `-- --preview` for a line-up picture in `Tools/blender/previews/People.png`. Each character becomes a prefab in `Prefabs/People/` with its materials in `Materials/People/`.

They move without animation clips: the **Person Animator** component on each prefab makes them breathe, shift their weight, look at a target (or at the player), sit and type, and gesture, nod and move their jaw while their **Speaker** has a line of dialogue. Its settings (pose, speaker, look target, liveliness, clothes tint) are per person in the scene. To use real animation clips (for example from Mixamo, which these rigs are named for), remove the component and add an Animator Controller.

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

## Editing safely (nothing gets overwritten behind your back)

Scenes, prefabs and materials are generated once and are then yours to edit.

- **Scenes**: open and change them like any Unity scene. The builder only touches a scene when you ask: **TSA > Build Missing Scenes** creates scenes that don't exist; **TSA > Rebuild Scene (overwrites it)** regenerates one scene after a warning; **TSA > Rebuild All Scenes** regenerates all nine.
- **Prefabs**: every model has a prefab in `Prefabs/Models/` (a variant of the Blender file). Edit the prefab and every copy in every scene changes. Re-exporting a model from Blender updates its shape without losing your prefab edits.
- **Materials**: in `Materials/`. A rebuild uses them as they are. **TSA > Reset Look To Defaults** puts them back to the built-in values.
- **Cutscenes**: the opening is a list of shots on *Cutscene Director* in `01_MissionControl`. Each shot has a camera start and end marker (move them in the scene), a duration, field of view, shake, an optional object to follow, its lines, and events to fire. Reorder, add or delete shots in the Inspector.
- **Colour and mood**: `Settings/Post_Space.asset` (space scenes), `Settings/MemoryPostProcess.asset` (launch day) and `Settings/IntroPostProcess.asset` (the rest) are Volume profiles: bloom, colour grade, grain and vignette are sliders there.
- **Sun, planets, debris**: the *Sun Glare* object holds the glow and streak quads; planets have *Atmosphere* and *Rings* children with their own materials; *Debris Field* objects have count, size and a clear lane.
- The scene layouts themselves are code in `Editor/OpeningSceneBuilder.cs` (01-03), `Editor/IntroSceneBuilder.cs` (04-05), `Editor/StorySceneBuilder.cs` (06-09) and `Editor/LookBuilder.cs` (lighting and look), for when a scene should be regenerated differently.

## Direction notes (why the intro is cut the way it is)

Taken from how space films and trailers build their feeling:

- **Open wide and slow, then tighten.** The menu shot and fly-by are long and calm; shots get shorter and shakier as the portal takes the rocket.
- **Sound from inside the suit.** In the spacewalk you hear breathing, the line and thuds through contact, not explosions. Breathing speeds up as the air runs out.
- **Handheld in small spaces.** The cabin and launch shots shake; the wide space shots stay still.
- **A beat of silence before the title.** The launch roar cuts to black and nothing, then the title lands with the first sound.
- **Cut to black before the answer.** The opening ends mid-sentence, on the question, not the explanation.
