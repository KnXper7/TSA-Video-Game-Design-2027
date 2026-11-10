using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TSAGame.EditorTools
{
    /// <summary>
    /// Builds every story scene with placeholder geometry: TSA > Build Intro Scenes.
    /// The menu, opening cutscene, launch-day memory and spacewalk are in OpeningSceneBuilder.cs; the cockpit and
    /// landing site are in this file; the later chapters are in StorySceneBuilder.cs.
    /// Every stand-in object sits under a "MODEL_..." parent. To swap in a real model, drop it under that parent
    /// and delete the child named "Placeholder". Scripts reference the MODEL_ parent, so nothing breaks.
    /// Re-running the builder overwrites these scenes, so make layout changes after you are happy with the structure.
    /// </summary>
    public static partial class IntroSceneBuilder
    {
        const string Root = "Assets/_Project";
        const string ScenesDir = Root + "/Scenes";
        const string MaterialsDir = Root + "/Materials";
        const string PrefabsDir = Root + "/Prefabs";
        const string SettingsDir = Root + "/Settings";

        public static string ScenePath(string sceneName) => $"{ScenesDir}/{sceneName}.unity";

        // Space for the Mission Control cutscene sits far from the room so the two never see each other.
        static readonly Vector3 Space = new(0f, 0f, 8000f);

        class Mats
        {
            public Material Floor, Wall, Desk, Screen, ScreenBlue, Operator, Director, Suit, Rocket, EngineGlow,
                GateRing, GateCore, PlanetX, LostSignal, Cockpit, Console, ConsoleScreen, Seat, Shutter,
                NotEarth, Moon, Grass, Hill, Bark, Leaves, Wreck, Scorch, Sensor, Crate, Beacon, Ruin, Glyph, OldRocket, Stars,
                Pilot, Sand, Seabed, TankBlue, TankWhite, TankRed, Platform;
        }

        /// <summary>Each scene and the function that builds it.</summary>
        static readonly (string scene, System.Action<Mats, VolumeProfile> build)[] Builders =
        {
            (SceneFlow.MissionControl, BuildMissionControl), (SceneFlow.Memory, BuildMemory), (SceneFlow.Adrift, BuildAdrift),
            (SceneFlow.Cockpit, BuildCockpit), (SceneFlow.LandingSite, BuildLandingSite), (SceneFlow.FuelMoon, BuildFuelMoon),
            (SceneFlow.GateWorld, BuildGateWorld), (SceneFlow.Harvester, BuildHarvester), (SceneFlow.Control, BuildControl),
        };

        /// <summary>Builds only the scenes that don't exist yet. Never touches a scene you have edited.</summary>
        [MenuItem("TSA/Build Missing Scenes", priority = 0)]
        public static void BuildMissing() => Build(s => !File.Exists(ScenePath(s)));

        [MenuItem("TSA/Rebuild Scene (overwrites it)/01 Menu and Opening", priority = 20)] static void Rebuild1() => RebuildOne(SceneFlow.MissionControl);
        [MenuItem("TSA/Rebuild Scene (overwrites it)/02 Memory", priority = 21)] static void Rebuild2() => RebuildOne(SceneFlow.Memory);
        [MenuItem("TSA/Rebuild Scene (overwrites it)/03 Adrift", priority = 22)] static void Rebuild3() => RebuildOne(SceneFlow.Adrift);
        [MenuItem("TSA/Rebuild Scene (overwrites it)/04 Cockpit", priority = 23)] static void Rebuild4() => RebuildOne(SceneFlow.Cockpit);
        [MenuItem("TSA/Rebuild Scene (overwrites it)/05 Landing Site", priority = 24)] static void Rebuild5() => RebuildOne(SceneFlow.LandingSite);
        [MenuItem("TSA/Rebuild Scene (overwrites it)/06 Fuel Moon", priority = 25)] static void Rebuild6() => RebuildOne(SceneFlow.FuelMoon);
        [MenuItem("TSA/Rebuild Scene (overwrites it)/07 Gate World", priority = 26)] static void Rebuild7() => RebuildOne(SceneFlow.GateWorld);
        [MenuItem("TSA/Rebuild Scene (overwrites it)/08 Harvester", priority = 27)] static void Rebuild8() => RebuildOne(SceneFlow.Harvester);
        [MenuItem("TSA/Rebuild Scene (overwrites it)/09 Control", priority = 28)] static void Rebuild9() => RebuildOne(SceneFlow.Control);

        static void RebuildOne(string scene)
        {
            if (EditorUtility.DisplayDialog("Rebuild scene?",
                    $"This replaces {scene} with a freshly generated copy. Anything you changed by hand in that scene will be lost.\n\nPrefabs and materials are not touched.",
                    "Rebuild", "Cancel"))
                Build(s => s == scene);
        }

        [MenuItem("TSA/Rebuild All Scenes (overwrites them)", priority = 30)]
        static void RebuildAllWithWarning()
        {
            if (EditorUtility.DisplayDialog("Rebuild every scene?",
                    "This replaces all nine scenes with freshly generated copies. Anything you changed by hand in the scenes will be lost.\n\nPrefabs and materials are not touched.",
                    "Rebuild all", "Cancel"))
                BuildAll();
        }

        /// <summary>Rebuilds every scene without asking (for scripts).</summary>
        public static void BuildAll() => Build(_ => true);

        static void Build(System.Func<string, bool> wanted)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play mode before building scenes.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            freshAssets.Clear();

            AssetDatabase.Refresh();
            foreach (var dir in new[] { ScenesDir, MaterialsDir, PrefabsDir, SettingsDir }) EnsureFolder(dir);

            var mats = CreateMaterials();
            var post = CreatePostProfile();

            ModelMats = CreateModelMaterials();
            ApplySurfaceDetail();
            RemoveOldScenes();

            int built = 0;
            foreach (var (scene, build) in Builders)
            {
                if (!wanted(scene)) continue;
                build(mats, post);
                built++;
            }
            RegisterScenes();

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath(SceneFlow.MissionControl));
            Debug.Log($"{built} scene(s) built. Press Play in 01_MissionControl to start from the main menu.");
        }

        [MenuItem("TSA/Open Intro Scene", priority = 1)]
        public static void OpenIntro()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath(SceneFlow.MissionControl));
        }

        // =====================================================================
        // Cockpit: power puzzle, the view, crash landing
        // =====================================================================

        static void BuildCockpit(Mats m, VolumeProfile post)
        {
            var scene = NewScene(post, ambient: new Color(0.05f, 0.06f, 0.09f), skybox: false);
            var sunAngle = Quaternion.Euler(12f, -65f, 0f);
            Sun(sunAngle, 1.6f);

            var cockpit = Slot("Cockpit", null, Vector3.zero, Vector3.zero);
            var shell = new GameObject("Placeholder").transform;
            shell.SetParent(cockpit, false);
            Prim(PrimitiveType.Cube, "Floor", shell, new Vector3(0, -0.05f, 0.25f), new Vector3(4, 0.1f, 5.5f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Ceiling", shell, new Vector3(0, 2.65f, 0.25f), new Vector3(4, 0.1f, 5.5f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Wall Left", shell, new Vector3(-2, 1.3f, 0.25f), new Vector3(0.1f, 2.6f, 5.5f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Wall Right", shell, new Vector3(2, 1.3f, 0.25f), new Vector3(0.1f, 2.6f, 5.5f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Wall Back", shell, new Vector3(0, 1.3f, -2.5f), new Vector3(4, 2.6f, 0.1f), m.Cockpit);
            // Front wall with a window opening (3 m x 1.4 m)
            Prim(PrimitiveType.Cube, "Front Lower", shell, new Vector3(0, 0.4f, 3f), new Vector3(4, 0.8f, 0.1f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Front Upper", shell, new Vector3(0, 2.4f, 3f), new Vector3(4, 0.5f, 0.1f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Front Left", shell, new Vector3(-1.75f, 1.45f, 3f), new Vector3(0.5f, 1.4f, 0.1f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Front Right", shell, new Vector3(1.75f, 1.45f, 3f), new Vector3(0.5f, 1.4f, 0.1f), m.Cockpit);
            foreach (var renderer in shell.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = ModelMats[renderer.name == "Floor" ? "Ship_Panels" : "Ship_Padding"];
            ShipInterior(shell, new Vector3(4f, 2.6f, 5.5f), new Vector3(0, 0, 0.25f), 3f);
            var shutter = Prim(PrimitiveType.Cube, "Window Shutter", cockpit, new Vector3(0, 1.45f, 2.92f), new Vector3(3.1f, 1.5f, 0.06f), m.Shutter);

            var seat = Slot("PilotSeat", cockpit, new Vector3(0, 0, -0.9f), Vector3.zero);
            Prim(PrimitiveType.Cube, "Placeholder", seat, new Vector3(0, 0.35f, 0), new Vector3(0.8f, 0.7f, 0.7f), m.Seat, collider: false);
            Prim(PrimitiveType.Cube, "Seat Back", seat, new Vector3(0, 1f, -0.35f), new Vector3(0.8f, 1.3f, 0.15f), m.Seat, collider: false);

            var consoleSlot = Slot("MainConsole", cockpit, new Vector3(0, 0, 1.2f), Vector3.zero);
            Prim(PrimitiveType.Cube, "Placeholder", consoleSlot, new Vector3(0, 0.75f, 0), new Vector3(2.6f, 0.2f, 0.9f), m.Console, Quaternion.Euler(-20, 0, 0));
            Prim(PrimitiveType.Quad, "Console Screen", consoleSlot, new Vector3(0, 0.87f, 0.02f), new Vector3(1.4f, 0.6f, 1), m.ConsoleScreen, Quaternion.Euler(70, 0, 0), collider: false);
            var console = consoleSlot.gameObject.AddComponent<Interactable>();
            var consoleCollider = consoleSlot.gameObject.AddComponent<BoxCollider>();
            consoleCollider.center = new Vector3(0, 0.8f, 0);
            consoleCollider.size = new Vector3(2.6f, 0.4f, 0.9f);
            Set(console, ("prompt", "Use power console"), ("singleUse", false));
            var puzzle = consoleSlot.gameObject.AddComponent<PowerRoutingPuzzle>();

            var interior = PointLight("Cabin Light", cockpit, new Vector3(0, 2.3f, 0), new Color(0.8f, 0.9f, 1f), 1.5f, 7f);
            var alarmA = PointLight("Alarm Light L", cockpit, new Vector3(-1.6f, 2.2f, 0.8f), new Color(1f, 0.1f, 0.05f), 0f, 6f);
            var alarmB = PointLight("Alarm Light R", cockpit, new Vector3(1.6f, 2.2f, 0.8f), new Color(1f, 0.1f, 0.05f), 0f, 6f);
            var alarm = new GameObject("Alarm System").AddComponent<AlarmLights>();
            alarm.gameObject.AddComponent<AudioSource>().playOnAwake = false;
            Set(alarm, ("alarmLights", new Object[] { alarmA, alarmB }), ("normalLights", new Object[] { interior }),
                ("alarmIntensity", 3f), ("alarmSound", alarm.GetComponent<AudioSource>()));

            // Outside the window
            var outside = new GameObject("Outside").transform;
            Starfield(outside, m.Stars, 2500f);
            // Off to the lower right so its curved edge and the burnt moon both show through the window
            var planet = Slot("NotEarth", outside, new Vector3(250, -350, 1100), new Vector3(0, 0, 20));
            Prim(PrimitiveType.Sphere, "Placeholder", planet, Vector3.zero, Vector3.one * 1000f, m.NotEarth, collider: false);
            planet.gameObject.AddComponent<Spinner>();
            var moon = Slot("BurntMoon", outside, new Vector3(-420, 160, 1300), Vector3.zero);
            Prim(PrimitiveType.Sphere, "Placeholder", moon, Vector3.zero, Vector3.one * 110f, m.Moon, collider: false);

            // Player, seated
            var (player, cam) = Player(new Vector3(0, 0, -0.6f), 1.15f, seatedYawLimit: 75f);
            var shake = cam.gameObject.AddComponent<CameraShake>();
            var interactor = player.GetComponent<Interactor>();

            var sequence = new GameObject("Cockpit Sequence").AddComponent<CockpitSequence>();
            Set(sequence, ("player", player.GetComponent<FirstPersonController>()), ("interactor", interactor), ("console", console),
                ("puzzle", puzzle), ("alarms", alarm), ("cameraShake", shake), ("windowShutter", shutter.transform), ("planet", planet));

            AddHud();
            Save(scene, SceneFlow.Cockpit);
        }

        // =====================================================================
        // Landing site: survival objectives on the planet that looks like Earth
        // =====================================================================

        static void BuildLandingSite(Mats m, VolumeProfile post)
        {
            var scene = NewScene(post, ambient: Color.clear, skybox: true);
            // Early-morning light: a low, warm sun and a sky with real clouds.
            var sun = Sun(Quaternion.Euler(24f, -35f, 0f), 1.9f);
            sun.color = new Color(1f, 0.9f, 0.72f);
            RenderSettings.skybox = PanoSky("Sky_MorningClouds", "Sky_Morning", 1.1f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.85f, 0.95f, 1.1f);
            RenderSettings.ambientEquatorColor = new Color(0.7f, 0.8f, 0.7f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.4f, 0.28f);

            // Ground, river, forest and boulders (see WorldBuilder.cs). The meadow around the crash is flat at height 0.
            var random = new System.Random(7);
            var terrain = LandingTerrain();
            Water(-0.55f, -sun.transform.forward);
            Scatter(terrain, random, new[]
            {
                (new Vector3(0, 0, 7), 20f), (new Vector3(45, 0, 95), 15f), (new Vector3(-40, 0, 88), 18f), (new Vector3(-40, 0, 58), 16f),
                (new Vector3(-12, 0, 14), 3f), (new Vector3(15, 0, -8), 3f), (new Vector3(-7, 0, -17), 3f),
            });
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            // Light haze in the colour of the sky, so far hills turn blue with distance, not grey.
            RenderSettings.fogDensity = 0.0011f;
            RenderSettings.fogColor = new Color(0.55f, 0.74f, 0.98f);

            // Burnt moon in the sky
            var moon = Slot("BurntMoon", null, new Vector3(500, 650, 1500), Vector3.zero);
            Prim(PrimitiveType.Sphere, "Placeholder", moon, Vector3.zero, Vector3.one * 160f, m.Moon, collider: false);

            // Crash site
            var crash = Slot("CrashedRocket", null, new Vector3(0, 1.1f, 7f), new Vector3(0, 25f, 0));
            var wreck = Model("Rocket", crash, new Vector3(0, -1.2f, -13.9f), Quaternion.Euler(82, 0, 0), 1f, new() { ["Rocket_White"] = m.Wreck });
            Child(wreck, "Boosters").gameObject.SetActive(false);
            Child(wreck, "SolarPanels").gameObject.SetActive(false);
            var wreckCollider = wreck.AddComponent<CapsuleCollider>();
            wreckCollider.center = new Vector3(0, 15, 0);
            wreckCollider.radius = 1.9f;
            wreckCollider.height = 33f;
            Prim(PrimitiveType.Cylinder, "Scorch Mark", null, new Vector3(0, 0.01f, 7f), new Vector3(9, 0.01f, 13), m.Scorch, Quaternion.Euler(0, 25f, 0), collider: false);
            PointLight("Smoulder", crash, crash.position + new Vector3(0, 1.5f, -4f), new Color(1f, 0.45f, 0.15f), 3f, 8f);

            var sensorSlot = Slot("SensorKit", null, new Vector3(2.5f, 0, 3.2f), Vector3.zero);
            Prim(PrimitiveType.Cube, "Placeholder", sensorSlot, new Vector3(0, 0.25f, 0), new Vector3(0.6f, 0.5f, 0.4f), m.Sensor);
            var sensor = sensorSlot.gameObject.AddComponent<Interactable>();
            Set(sensor, ("prompt", "Scan the atmosphere"), ("holdDuration", 2.5f));

            // Salvage
            var cratePrefab = CratePrefab(m);
            var salvageRoot = new GameObject("Salvage").transform;
            var salvage = new List<Object>();
            var beacons = new List<Object>();
            foreach (var p in new[] { new Vector3(-12, 0, 14), new Vector3(15, 0, -8), new Vector3(-7, 0, -17) })
            {
                var crate = (GameObject)PrefabUtility.InstantiatePrefab(cratePrefab, salvageRoot);
                crate.transform.position = p;
                salvage.Add(crate.GetComponent<Interactable>());
                var beacon = Beacon(p, m);
                beacon.transform.SetParent(salvageRoot, true);
                beacons.Add(beacon);
            }

            // Ruins
            var ruins = Slot("Ruins", null, new Vector3(-40, 0, 55), Vector3.zero);
            var ruinsPlaceholder = new GameObject("Placeholder").transform;
            ruinsPlaceholder.SetParent(ruins, false);
            int t = 0;
            foreach (var p in new[] { new Vector3(-6, 0, 0), new Vector3(5, 0, -3), new Vector3(1, 0, 8), new Vector3(-9, 0, 10) })
            {
                float height = 14f + t * 4f;
                var tower = Prim(PrimitiveType.Cube, $"Tower {++t}", ruinsPlaceholder, p + new Vector3(0, height / 2, 0), new Vector3(3.5f, height, 3.5f), m.Ruin,
                    Quaternion.Euler(0, t * 17f, 0));
                for (int g = 0; g < 4; g++)
                    Prim(PrimitiveType.Cube, "Glyph", tower.transform, new Vector3(0, -0.3f + g * 0.12f, -0.51f), new Vector3(0.6f, 0.02f, 0.02f), m.Glyph, collider: false);
            }
            var ruinsZone = TriggerBox("Ruins Trigger", ruins, ruins.position + new Vector3(0, 4, 3), new Vector3(30, 10, 30));

            // The ancient gate, behind the ruins
            // The same ring that swallowed the rocket, but ancient, half buried and switched off.
            var oldGate = Slot("AncientGate", null, new Vector3(-40, 9.5f, 92), Vector3.zero);
            Model("Portal", oldGate, Vector3.zero, Quaternion.identity, 1f,
                new() { ["Portal_Metal"] = m.Ruin, ["Portal_Trim"] = m.Ruin, ["Portal_Glow"] = m.Glyph });
            var gateConsole = Console("GateConsole", null, new Vector3(-40, 0, 78), 0f, m, "Examine the ring", hold: 2f);

            // Second rocket and its radio signal
            var oldRocket = Slot("SecondRocket", null, new Vector3(45, 0, 95), Vector3.zero);
            var otherShip = Model("Rocket", oldRocket, new Vector3(0, 2.2f, 0), Quaternion.identity, 1f,
                new() { ["Rocket_White"] = m.OldRocket, ["Rocket_Accent"] = m.Pilot });
            Child(otherShip, "Boosters").gameObject.SetActive(false);
            Child(otherShip, "SolarPanels").gameObject.SetActive(false);
            var shipCollider = oldRocket.gameObject.AddComponent<CapsuleCollider>();
            shipCollider.center = new Vector3(0, 14, 0);
            shipCollider.radius = 2.3f;
            shipCollider.height = 34f;
            var signalMarker = new GameObject("Signal Marker").transform;
            signalMarker.position = oldRocket.position;
            signalMarker.gameObject.AddComponent<CompassMarker>();
            var signalZone = TriggerBox("Signal Trigger", oldRocket, oldRocket.position + new Vector3(0, 4, 0), new Vector3(18, 10, 18));
            var pilotShip = oldRocket.gameObject.AddComponent<Interactable>();
            Set(pilotShip, ("prompt", "Board the ship"));
            var pilot = Slot("ColonyPilot", null, new Vector3(41, 0, 89.5f), new Vector3(0, 200f, 0));
            PersonInto(pilot, "Pilot", false, Speakers.Pilot, lookAtPlayer: true, tint: new Color(1f, 0.62f, 0.3f));

            // Player
            var (player, landingCam) = Player(new Vector3(4.5f, 0, 1.5f), 1.65f, seatedYawLimit: 0f);
            // Pollen and seeds drifting in the sunlight
            Motes(landingCam.transform, new Vector3(0, 1, 5), new Vector3(30, 8, 30), 350, new Color(1f, 0.98f, 0.85f, 0.7f), 0.025f, 0.15f, false, new Vector3(0.4f, 0.05f, 0.2f));
            player.transform.rotation = Quaternion.Euler(0, -35f, 0); // facing the sensor kit, wreck to the side
            var oxygen = player.AddComponent<OxygenSystem>();

            var sequence = new GameObject("Landing Site Sequence").AddComponent<LandingSiteSequence>();
            Set(sequence, ("player", player.GetComponent<FirstPersonController>()), ("oxygen", oxygen), ("airScanner", sensor),
                ("salvage", salvage.ToArray()), ("salvageBeacons", beacons.ToArray()), ("ruinsZone", ruinsZone),
                ("signalZone", signalZone), ("signalMarker", signalMarker.gameObject),
                ("gateConsole", gateConsole), ("pilotShip", pilotShip));

            AddHud();
            Save(scene, SceneFlow.LandingSite);
        }

        // =====================================================================
        // Shared pieces
        // =====================================================================

        static (GameObject player, Camera cam) Player(Vector3 position, float eyeHeight, float seatedYawLimit)
        {
            var player = new GameObject("Player");
            player.transform.position = position;
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0, 0.9f, 0);

            var head = new GameObject("Head").transform;
            head.SetParent(player.transform, false);
            head.localPosition = new Vector3(0, eyeHeight, 0);

            var cam = NewCamera("Player Camera", Vector3.zero, Quaternion.identity, 70f, listener: true, post: true);
            cam.nearClipPlane = 0.05f;
            cam.transform.SetParent(head, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;

            var fps = player.AddComponent<FirstPersonController>();
            Set(fps, ("cameraPivot", head), ("seatedYawLimit", seatedYawLimit));
            var interactor = player.AddComponent<Interactor>();
            Set(interactor, ("viewCamera", cam));
            return (player, cam);
        }

        static void AddHud()
        {
            var hud = new GameObject("HUD").AddComponent<HUD>();
            hud.BuildDefaultLayout();
        }

        static Scene NewScene(VolumeProfile post, Color ambient, bool skybox)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (skybox)
            {
                RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
                RenderSettings.ambientMode = AmbientMode.Skybox;
            }
            else
            {
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambient;
            }
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = post;
            return scene;
        }

        static void Save(Scene scene, string sceneName)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath(sceneName));
        }

        /// <summary>Deletes scene files left over from before the chapters were renumbered.</summary>
        static void RemoveOldScenes()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { ScenesDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!SceneFlow.All.Contains(Path.GetFileNameWithoutExtension(path))) AssetDatabase.DeleteAsset(path);
            }
        }

        static void RegisterScenes()
        {
            var ours = SceneFlow.All.Select(ScenePath).ToList();
            var others = EditorBuildSettings.scenes.Where(s => !ours.Contains(s.path));
            EditorBuildSettings.scenes = ours.Select(p => new EditorBuildSettingsScene(p, true)).Concat(others).ToArray();
        }

        static Light Sun(Quaternion rotation, float intensity)
        {
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = rotation;
            light.intensity = intensity;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
            return light;
        }

        static Light PointLight(string name, Transform parent, Vector3 worldPosition, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = worldPosition;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        static Camera NewCamera(string name, Vector3 position, Quaternion rotation, float fov, bool listener, bool post)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, rotation);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 6000f;
            if (RenderSettings.skybox == null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }
            if (listener) go.AddComponent<AudioListener>();
            if (name != "Tracking Camera") go.tag = "MainCamera";
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = post;
            return cam;
        }

        /// <summary>An empty parent for a model. The placeholder geometry goes inside it.</summary>
        static Transform Slot(string name, Transform parent, Vector3 worldPosition, Vector3 euler)
        {
            var slot = new GameObject("MODEL_" + name).transform;
            if (parent != null) slot.SetParent(parent, false);
            slot.SetPositionAndRotation(worldPosition, Quaternion.Euler(euler));
            return slot;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material,
            Quaternion? localRotation = null, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Transform Marker(string name, Transform parent, Vector3 worldPosition, Vector3 lookAt)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.SetPositionAndRotation(worldPosition, Quaternion.LookRotation(lookAt - worldPosition));
            return marker;
        }

        static CutsceneShot Shot(string name, Transform from, Transform to, float duration, params DialogueLine[] lines) =>
            new() { name = name, from = from, to = to, duration = duration, lines = lines.ToList() };

        static DialogueLine Line(string speaker, string text, float hold = 0f) => new(speaker, text, hold);

        static TriggerZone TriggerBox(string name, Transform parent, Vector3 worldPosition, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            go.transform.rotation = Quaternion.identity;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            return go.AddComponent<TriggerZone>();
        }

        static Text WorldText(string name, Transform parent, Vector3 worldPosition, Vector2 size, float scale, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.position = worldPosition;
            rt.localScale = Vector3.one * scale;
            var text = UIFactory.CreateText("Text", go.transform, "", fontSize, TextAnchor.MiddleCenter, color);
            UIFactory.Stretch(text.rectTransform);
            return text;
        }

        static void Starfield(Transform parent, Material material, float radius, int count = 1500)
        {
            var go = new GameObject("Starfield");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.maxParticleSize = 0.02f;
            // Stars further away are drawn bigger so they look the same from the middle.
            var stars = go.AddComponent<Starfield>();
            Set(stars, ("radius", radius), ("starCount", count));
            var so = new SerializedObject(stars);
            so.FindProperty("sizeRange").vector2Value = new Vector2(2f, 9f) * (radius / 2500f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, params (string field, object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in fields)
            {
                var property = so.FindProperty(field);
                if (property == null)
                {
                    Debug.LogError($"{target.GetType().Name} has no serialized field '{field}'.");
                    continue;
                }
                switch (value)
                {
                    case Object[] array:
                        property.arraySize = array.Length;
                        for (int i = 0; i < array.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                        break;
                    case Object reference: property.objectReferenceValue = reference; break;
                    case float f: property.floatValue = f; break;
                    case int i: property.intValue = i; break;
                    case bool b: property.boolValue = b; break;
                    case string s: property.stringValue = s; break;
                    case Vector3 v: property.vector3Value = v; break;
                    case Color c: property.colorValue = c; break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Prefabs ----------

        static GameObject TreePrefab(Mats m)
        {
            string path = $"{PrefabsDir}/Tree_Placeholder.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var tree = new GameObject("MODEL_Tree");
            var placeholder = new GameObject("Placeholder").transform;
            placeholder.SetParent(tree.transform, false);
            Prim(PrimitiveType.Cylinder, "Trunk", placeholder, new Vector3(0, 3f, 0), new Vector3(0.6f, 3f, 0.6f), m.Bark);
            Prim(PrimitiveType.Sphere, "Canopy", placeholder, new Vector3(0, 7.5f, 0), new Vector3(5f, 5.5f, 5f), m.Leaves, collider: false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(tree, path);
            Object.DestroyImmediate(tree);
            return prefab;
        }

        static GameObject CratePrefab(Mats m)
        {
            string path = $"{PrefabsDir}/SalvageCrate_Placeholder.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var crate = new GameObject("MODEL_SalvageCrate");
            Prim(PrimitiveType.Cube, "Placeholder", crate.transform, new Vector3(0, 0.4f, 0), new Vector3(1.1f, 0.8f, 0.8f), m.Crate);
            Set(crate.AddComponent<Interactable>(), ("prompt", "Salvage parts"), ("hideOnUse", true));
            var prefab = PrefabUtility.SaveAsPrefabAsset(crate, path);
            Object.DestroyImmediate(crate);
            return prefab;
        }

        // ---------- Assets ----------

        static Mats CreateMaterials() => new()
        {
            Floor = Mat("MC_Floor", new Color(0.12f, 0.13f, 0.15f), 0.5f),
            Wall = Mat("MC_Wall", new Color(0.2f, 0.22f, 0.26f)),
            Desk = Mat("MC_Desk", new Color(0.25f, 0.27f, 0.3f), 0.6f),
            Screen = Mat("MC_MainScreen", Color.white, unlit: true),
            ScreenBlue = Mat("MC_ScreenGlow", new Color(0.03f, 0.08f, 0.14f), 0.85f, emission: new Color(0.1f, 0.3f, 0.55f) * 0.45f),
            Operator = Mat("Person_Operator", new Color(0.35f, 0.45f, 0.6f)),
            Director = Mat("Person_FlightDirector", new Color(0.85f, 0.85f, 0.88f)),
            Suit = Mat("Person_Suit", new Color(0.08f, 0.08f, 0.1f)),
            Rocket = Mat("Rocket_Hull", new Color(0.88f, 0.88f, 0.9f), 0.7f, metallic: 0.3f),
            EngineGlow = Mat("Rocket_Engine", new Color(1f, 0.5f, 0.2f), emission: new Color(1f, 0.45f, 0.1f) * 6f),
            GateRing = Mat("Gate_Ring", new Color(0.25f, 0.75f, 1f) * 2.5f, unlit: true),
            GateCore = Mat("Gate_Core", new Color(0.02f, 0.06f, 0.15f), emission: new Color(0.15f, 0.5f, 1f) * 1.3f),
            PlanetX = Mat("Planet_X", new Color(0.55f, 0.45f, 0.4f), 0.1f),
            LostSignal = Mat("MC_SignalLost", new Color(0.08f, 0.02f, 0.02f), unlit: true),
            Cockpit = Mat("Cockpit_Panel", new Color(0.28f, 0.3f, 0.33f), 0.5f, metallic: 0.4f),
            Console = Mat("Cockpit_Console", new Color(0.15f, 0.16f, 0.18f), 0.6f),
            ConsoleScreen = Mat("Cockpit_Screen", new Color(0.05f, 0.15f, 0.1f), emission: new Color(0.2f, 1f, 0.6f) * 1.2f),
            Seat = Mat("Cockpit_Seat", new Color(0.3f, 0.12f, 0.08f)),
            Shutter = Mat("Cockpit_Shutter", new Color(0.18f, 0.18f, 0.2f), 0.4f, metallic: 0.6f),
            NotEarth = Mat("Planet_NotEarth", new Color(0.15f, 0.4f, 0.75f), 0.6f),
            Moon = Mat("Moon_Burnt", new Color(0.4f, 0.22f, 0.15f), 0.05f),
            Grass = Mat("Site_Grass", new Color(0.25f, 0.45f, 0.2f), 0.1f),
            Hill = Mat("Site_Hill", new Color(0.22f, 0.4f, 0.18f), 0.1f),
            Bark = Mat("Site_Bark", new Color(0.3f, 0.2f, 0.12f), 0.1f),
            Leaves = Mat("Site_Leaves", new Color(0.15f, 0.38f, 0.15f), 0.1f),
            Wreck = Mat("Site_Wreck", new Color(0.55f, 0.55f, 0.57f), 0.5f, metallic: 0.4f),
            Scorch = Mat("Site_Scorch", new Color(0.16f, 0.12f, 0.08f), 0f),
            Sensor = Mat("Site_SensorKit", new Color(0.9f, 0.6f, 0.1f), emission: new Color(1f, 0.7f, 0.1f) * 0.8f),
            Crate = Mat("Site_Crate", new Color(0.7f, 0.55f, 0.2f), 0.3f),
            Beacon = Mat("Site_Beacon", new Color(0.4f, 0.85f, 1f) * 3f, unlit: true),
            Ruin = Mat("Site_Ruin", new Color(0.75f, 0.75f, 0.7f), 0.8f),
            Glyph = Mat("Site_Glyph", new Color(0.4f, 0.9f, 1f), emission: new Color(0.4f, 0.9f, 1f) * 2.5f),
            OldRocket = Mat("Site_OldRocket", new Color(0.5f, 0.45f, 0.38f), 0.3f, metallic: 0.2f),
            Stars = StarMaterial(),
            Pilot = Mat("Person_ColonyPilot", new Color(0.85f, 0.45f, 0.15f)),
            Sand = Mat("World_Sand", new Color(0.76f, 0.62f, 0.4f), 0.15f),
            Seabed = Mat("World_Seabed", new Color(0.36f, 0.36f, 0.34f), 0.1f),
            TankBlue = Mat("Moon_TankBlue", new Color(0.2f, 0.45f, 0.95f), 0.5f),
            TankWhite = Mat("Moon_TankWhite", new Color(0.9f, 0.9f, 0.92f), 0.5f),
            TankRed = Mat("Moon_TankRed", new Color(0.9f, 0.2f, 0.15f), 0.5f),
            Platform = Mat("Control_Platform", new Color(0.16f, 0.18f, 0.22f), 0.6f, metallic: 0.5f),
        };

        static Material Mat(string name, Color color, float smoothness = 0.3f, Color? emission = null, bool unlit = false, float metallic = 0f)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (!OverwriteLook) return mat;      // already exists: keep whatever it has been tuned to
            else mat.shader = shader;
            freshAssets.Add(mat);

            mat.SetColor("_BaseColor", color);
            if (!unlit)
            {
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Metallic", metallic);
            }
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material StarMaterial()
        {
            string path = $"{MaterialsDir}/Stars.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (!OverwriteLook) return mat;
            mat.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            mat.SetColor("_BaseColor", Color.white * 2f);
            // Additive transparency
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static RenderTexture LoadOrCreateRenderTexture(string path)
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (rt != null) return rt;
            rt = new RenderTexture(1280, 720, 24) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(rt, path);
            return rt;
        }

        static VolumeProfile CreatePostProfile()
        {
            string path = $"{SettingsDir}/IntroPostProcess.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1.1f);
            bloom.threshold.Override(1f);
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    /// <summary>Builds the intro scenes automatically the first time this project opens with these scripts.</summary>
    [InitializeOnLoad]
    static class IntroSceneAutoBuild
    {
        const string SessionKey = "TSA.IntroSceneAutoBuild";

        static IntroSceneAutoBuild()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            if (File.Exists(IntroSceneBuilder.ScenePath(SceneFlow.MissionControl))) return;
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) IntroSceneBuilder.BuildMissing();
            };
        }
    }
}
