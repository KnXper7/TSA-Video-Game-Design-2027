using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TSAGame.EditorTools
{
    /// <summary>
    /// Builds the opening of the game: the main menu and Mission Control cutscene (01), the launch-day memory (02)
    /// and the spacewalk (03). These scenes use the models made by Tools/blender/build_models.py.
    /// </summary>
    public static partial class IntroSceneBuilder
    {
        const string ModelsDir = Root + "/Models";
        const string TexturesDir = Root + "/Textures";

        /// <summary>Project materials by the material names used inside the models.</summary>
        static Dictionary<string, Material> ModelMats;

        // =====================================================================
        // 01 Main menu + opening cutscene
        // =====================================================================

        static void BuildMissionControl(Mats m, VolumeProfile post)
        {
            var scene = NewScene(SpacePostProfile(), ambient: new Color(0.06f, 0.068f, 0.085f), skybox: false);
            // The sun sits ahead and to the upper left, so rocks and the rocket are edge-lit against it.
            // Three lights, as in the target picture:
            //  key  - the blue-white star, upper left and a little behind the rocks, so it catches their edges;
            //  fill - soft, almost neutral light from the camera side, so the faces toward us show their detail;
            //  bounce - a trace of warm orange coming up off the planet.
            var sun = Sun(Quaternion.Euler(12f, 148f, 0f), 3.6f);
            sun.color = new Color(0.78f, 0.88f, 1f);
            SunFlare(sun);
            RimLight(Quaternion.Euler(18f, -22f, 0f), new Color(0.7f, 0.78f, 0.92f), 0.4f).name = "Fill Light";
            // The star again, from further round to the side: it paints the left-hand edges of the rocks and the
            // planet's left limb cool white, and leaves their right-hand sides in shadow.
            RimLight(Quaternion.Euler(24f, 112f, 0f), new Color(0.72f, 0.86f, 1f), 1.25f).name = "Star Side Light";
            RimLight(Quaternion.Euler(-35f, 120f, 0f), new Color(1f, 0.62f, 0.38f), 0.3f).name = "Planet Bounce";      // a trace of warm light bounced off the planet

            // ---- The room ----
            var room = new GameObject("Mission Control Room").transform;
            var wall = ModelMats["Room_Wall"];
            Prim(PrimitiveType.Cube, "Floor", room, new Vector3(0, -0.1f, 0), new Vector3(24, 0.2f, 18), ModelMats["Room_Floor"]);
            Prim(PrimitiveType.Cube, "Ceiling", room, new Vector3(0, 7.1f, 0), new Vector3(24, 0.2f, 18), m.Wall);
            Prim(PrimitiveType.Cube, "Wall Left", room, new Vector3(-12, 3.5f, 0), new Vector3(0.2f, 7, 18), wall);
            Prim(PrimitiveType.Cube, "Wall Right", room, new Vector3(12, 3.5f, 0), new Vector3(0.2f, 7, 18), wall);
            Prim(PrimitiveType.Cube, "Wall Back", room, new Vector3(0, 3.5f, -9), new Vector3(24, 7, 0.2f), wall);
            Prim(PrimitiveType.Cube, "Wall Front", room, new Vector3(0, 3.5f, 9), new Vector3(24, 7, 0.2f), wall);
            // Ceiling beams, light panels and a glowing strip along the foot of each wall
            for (int i = -2; i <= 2; i++)
                Prim(PrimitiveType.Cube, "Ceiling Beam", room, new Vector3(0, 6.85f, i * 3.6f), new Vector3(24, 0.3f, 0.35f), ModelMats["Rocket_Dark"], collider: false);
            foreach (var p in new[] { new Vector3(-6, 6.98f, -4), new Vector3(6, 6.98f, -4), new Vector3(-6, 6.98f, 4), new Vector3(6, 6.98f, 4) })
                Prim(PrimitiveType.Cube, "Light Panel", room, p, new Vector3(3.2f, 0.04f, 1.2f), ModelMats["Tower_Light"], collider: false);
            foreach (float x in new[] { -11.86f, 11.86f })
                Prim(PrimitiveType.Cube, "Floor Strip", room, new Vector3(x, 0.08f, 0), new Vector3(0.05f, 0.05f, 17.6f), ModelMats["Strip_Warm"], collider: false);
            var probe = new GameObject("Reflection Probe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(room, false);
            probe.transform.position = new Vector3(0, 3.5f, 0);
            probe.size = new Vector3(24, 7, 18);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.boxProjection = true;

            // What the desk monitors and wall screens show
            var screens = new[]
            {
                ScreenMaterial("Screen_Telemetry", Generated("Screen_Telemetry")), ScreenMaterial("Screen_StarChart", Generated("Screen_StarChart")),
                ScreenMaterial("Screen_Vitals", Generated("Screen_Vitals")), ScreenMaterial("Screen_Schematic", Generated("Screen_Schematic")),
            };

            // Big screen fed by the tracking camera, with data screens all around it
            var feed = LoadOrCreateRenderTexture($"{MaterialsDir}/TrackingFeed.renderTexture");
            m.Screen.SetTexture("_BaseMap", feed);
            var screenSlot = Slot("MainScreen", room, new Vector3(0, 4f, 8.85f), Vector3.zero);
            var screen = Prim(PrimitiveType.Quad, "Placeholder", screenSlot, new Vector3(0, -0.15f, 0), new Vector3(9.4f, 5.2f, 1), m.Screen, collider: false);
            Prim(PrimitiveType.Cube, "Main Screen Frame", room, new Vector3(0, 3.85f, 8.97f), new Vector3(9.9f, 5.7f, 0.2f), ModelMats["Monitor_Bezel"], collider: false);
            var status = WorldText("Screen Status", room, new Vector3(0, 6.72f, 8.75f), new Vector2(1000, 50), 0.01f, 36, new Color(0.6f, 0.9f, 1f));
            var wallScreens = new[] { new Vector3(-8.3f, 5.15f, 8.88f), new Vector3(-8.3f, 2.3f, 8.88f), new Vector3(8.3f, 5.15f, 8.88f), new Vector3(8.3f, 2.3f, 8.88f) };
            for (int i = 0; i < 4; i++)
            {
                Prim(PrimitiveType.Quad, "Wall Screen", room, wallScreens[i], new Vector3(4.9f, 2.74f, 1), screens[i], collider: false);
                Prim(PrimitiveType.Cube, "Wall Screen Frame", room, wallScreens[i] + new Vector3(0, 0, 0.1f), new Vector3(5.3f, 3.14f, 0.16f), ModelMats["Monitor_Bezel"], collider: false);
            }
            string[] wallData = { "TRAJECTORY\nON COURSE", "GUIDANCE\nGREEN", "POWER\n98%", "LIFE SUPPORT\nGREEN", "DEEP SPACE NET\nLOCKED", "THERMAL\nNOMINAL" };
            // Server racks with blinking panels down both side walls
            for (int i = 0; i < 6; i++)
            foreach (float side in new[] { -1f, 1f })
            {
                var at = new Vector3(side * 11.45f, 1.7f, 7f - i * 2.7f);
                Prim(PrimitiveType.Cube, "Server Rack", room, at, new Vector3(0.9f, 3.4f, 2.2f), ModelMats["Monitor_Bezel"]);
                Prim(PrimitiveType.Quad, "Rack Panel", room, at + new Vector3(-side * 0.46f, 0.2f, 0), new Vector3(1.9f, 2.6f, 1), ModelMats["Ship_Instruments"],
                    Quaternion.Euler(0, side * 90f, 0), collider: false);
            }

            // Desks, monitors and people watching them, in three rows
            var desks = new GameObject("Desks").transform;
            desks.SetParent(room);
            Transform alarmOperator = null;
            string[] operators = { "OperatorA", "OperatorB", "OperatorC", "Technician" };
            Color[] shirts = { Color.white, new Color(0.75f, 0.85f, 1f), new Color(1f, 0.9f, 0.8f), new Color(0.7f, 0.95f, 0.9f), new Color(0.9f, 0.9f, 0.9f) };
            int n = 0;
            foreach (float z in new[] { 4f, 0f, -3.5f })
            foreach (float x in new[] { -5.5f, 5.5f })
            {
                n++;
                var desk = Slot($"Desk{n}", desks, new Vector3(x, 0, z), Vector3.zero);
                Model("Desk", desk, Vector3.zero, Quaternion.identity, 1f);
                var deskBox = desk.gameObject.AddComponent<BoxCollider>();
                deskBox.center = new Vector3(0, 0.4f, 0);
                deskBox.size = new Vector3(7, 0.8f, 1.25f);
                if (n % 2 == 1) PointLight("Monitor Glow", desk, new Vector3(x, 1.25f, z - 0.25f), new Color(0.5f, 0.8f, 1f), 3f, 4f);
                for (int s = -1; s <= 1; s++)
                {
                    // Every third monitor shows the live tracking feed; the others cycle through the data screens.
                    int slotNumber = n * 3 + s;
                    var picture = slotNumber % 4 == 2 ? m.Screen : screens[slotNumber % 4];
                    Model("Monitor", desk, new Vector3(s * 2.2f, 1.0f, 0.42f), Quaternion.identity, 1f, new() { ["Monitor_Screen"] = picture });
                    var seatPosition = new Vector3(x + s * 2.2f, 0, z - 0.78f);
                    Model("Chair", desk, new Vector3(s * 2.2f, 0, -0.8f), Quaternion.identity, 1f);
                    bool raisesAlarm = n == 4 && s == 0;
                    string who = raisesAlarm ? "Controller" : operators[(n * 3 + s + 1) % operators.Length];
                    var op = Person($"Operator{n}_{s + 2}", desk, seatPosition, 0f, true, who, raisesAlarm ? Speakers.Controller : null,
                        screenSlot, tint: shirts[(n + s * 2 + 6) % shirts.Length]);
                    if (n == 4 && s == 0) alarmOperator = op;
                }
            }

            Person("FlightDirector", room, new Vector3(0, 0, -6.5f), 0f, false, "FlightDirector", Speakers.FlightDirector, screenSlot);
            Person("Technician1", room, new Vector3(-2.6f, 0, -1.9f), 20f, false, "Technician", Speakers.Technician, screenSlot);
            Person("Technician2", room, new Vector3(7.4f, 0, -5.9f), 10f, false, "OperatorB", null, screenSlot, tint: new Color(0.8f, 1f, 0.85f));
            Person("Observer1", room, new Vector3(-8.2f, 0, -6.8f), 20f, false, "Quartermaster", null, screenSlot);
            Person("Observer2", room, new Vector3(-7.2f, 0, -7.2f), 10f, false, "OperatorC", null, screenSlot, tint: new Color(1f, 0.85f, 0.85f));

            // Lights and alarm
            var normalLights = new List<Light>();
            foreach (var p in new[] { new Vector3(-6, 6.5f, -4), new Vector3(6, 6.5f, -4), new Vector3(-6, 6.5f, 4), new Vector3(6, 6.5f, 4) })
            {
                // Warm downlights that pool on the floor, as in the target image.
                var down = PointLight("Ceiling Light", room, p, new Color(1f, 0.84f, 0.64f), 55f, 20f);
                normalLights.Add(down);
            }
            PointLight("Room Fill", room, new Vector3(0, 5.5f, -2), new Color(1f, 0.85f, 0.7f), 14f, 24f);
            var alarmLights = new List<Light>();
            foreach (var p in new[] { new Vector3(-11, 6, -6), new Vector3(11, 6, -6), new Vector3(-11, 6, 6), new Vector3(11, 6, 6) })
                alarmLights.Add(PointLight("Alarm Light", room, p, new Color(1f, 0.1f, 0.05f), 0f, 18f));
            var alarm = new GameObject("Alarm System").AddComponent<AlarmLights>();
            Set(alarm, ("alarmLights", alarmLights.Cast<Object>().ToArray()), ("normalLights", normalLights.Cast<Object>().ToArray()));

            InteriorVolume(room, new Vector3(0, 3.5f, 0), new Vector3(26, 9, 20));
            Motes(room, new Vector3(0, 3.5f, 0), new Vector3(22, 6, 16), 400, new Color(0.8f, 0.9f, 1f, 0.35f), 0.02f, 0.03f, true);

            // ---- Space: also the backdrop of the main menu ----
            var space = new GameObject("Space").transform;
            space.position = Space;
            Starfield(space, m.Stars, 4800f, 3500);
            Nebula(space, Space + new Vector3(-1800, 700, 4200), 4200f, "Nebula_Violet", new Color(0.5f, 0.25f, 0.9f));
            Nebula(space, Space + new Vector3(2200, -300, 4000), 3000f, "Nebula_Blue", new Color(0.15f, 0.4f, 0.9f));
            Nebula(space, Space + new Vector3(300, 1500, 4400), 2600f, "Nebula_Teal", new Color(0.1f, 0.6f, 0.6f));

            var planet = Slot("PlanetX", space, Space + new Vector3(-1100, -620, 3900), Vector3.zero);
            var planetBody = Prim(PrimitiveType.Sphere, "Placeholder", planet, Vector3.zero, Vector3.one * 1500f, m.PlanetX, collider: false);
            planetBody.AddComponent<Spinner>();
            DressPlanet(planet, 1500f, "PlanetX", new Color(1f, 0.55f, 0.3f), -sun.transform.forward, rings: true);
            Debris(space, Space + new Vector3(0, 0, 420), new Vector3(700, 300, 1000), 2200, new Vector2(0.2f, 2.2f), new Vector2(-16, -7), 26f);

            // Asteroids drifting through the menu view; a clear lane is left for the rocket
            var rocks = new GameObject("Asteroids").transform;
            rocks.SetParent(space, true);
            var random = new System.Random(21);
            var lane = new Vector3(-16, -7, 0);
            foreach (var (p, s) in new[] { (new Vector3(11, -4, 30), 5f), (new Vector3(-30, 9, 70), 9f), (new Vector3(18, 9, 105), 12f), (new Vector3(3, -14, 60), 6f) })
                Asteroid(rocks, random, Space + p, s, collider: false);
            for (int i = 0; i < 70; i++)
            {
                var p = new Vector3(Range(random, -300, 300), Range(random, -130, 130), Range(random, 40, 1100));
                if (new Vector2(p.x - lane.x, p.y - lane.y).magnitude < 32f) continue;
                Asteroid(rocks, random, Space + p, Range(random, 2f, 9f) * (1f + p.z / 500f), collider: false);
            }

            // The portal: it only appears during the cutscene
            var gateSlot = Slot("Portal", space, Space + new Vector3(0, 0, 2000), Vector3.zero);
            gateSlot.localScale = Vector3.one * 8f;
            var gateRing = Portal(gateSlot, m, 70f);

            // The rocket
            var rocket = Slot("Rocket", space, Space + new Vector3(-16, -7, -115), Vector3.zero);
            // Rolled a quarter turn so the solar wings lie flat and don't block the over-the-nose shot.
            var rocketModel = Model("Rocket", rocket, Vector3.zero, Quaternion.Euler(0, 0, 90) * Quaternion.Euler(90, 0, 0), 1f);
            Child(rocketModel, "Boosters").gameObject.SetActive(false);
            var plume = Plume("Engine Plume", rocket, new Vector3(0, 0, -2.2f), Quaternion.Euler(180, 0, 0), 2.6f, 26f, 0.55f, 300f, false,
                new Color(0.75f, 0.85f, 1f), new Color(1f, 0.45f, 0.1f));
            PointLight("Engine Light", rocket, rocket.TransformPoint(new Vector3(0, 0, -5f)), new Color(1f, 0.6f, 0.3f), 30f, 40f);
            var centre = new GameObject("Centre").transform;
            centre.SetParent(rocket, false);
            centre.localPosition = new Vector3(0, 0, 16);
            var flight = rocket.gameObject.AddComponent<RocketFlight>();
            Set(flight, ("gate", gateSlot), ("exhaust", new Object[] { plume }));

            // Tracking camera that feeds the big screen
            var trackingCam = NewCamera("Tracking Camera", rocket.position + new Vector3(26, 9, -18), Quaternion.identity, 40f, listener: false, post: false);
            trackingCam.targetTexture = feed;
            trackingCam.transform.SetParent(space, true);
            Set(trackingCam.gameObject.AddComponent<TrackTarget>(), ("target", centre), ("offset", new Vector3(26, 9, -34)), ("smoothing", 30f));

            var monitor = screenSlot.gameObject.AddComponent<MonitorScreen>();
            Set(monitor, ("screen", screen.GetComponent<Renderer>()), ("lostSignalMaterial", m.LostSignal), ("statusText", status));

            // ---- Inside the rocket, for the last shot ----
            var cabinOrigin = new Vector3(0, 300, 0);
            var cabin = Slot("CabinInterior", null, cabinOrigin, Vector3.zero);
            var cabinShell = new GameObject("Placeholder").transform;
            cabinShell.SetParent(cabin, false);
            Prim(PrimitiveType.Cube, "Floor", cabinShell, new Vector3(0, -0.05f, 0), new Vector3(3.2f, 0.1f, 5f), m.Cockpit, collider: false);
            Prim(PrimitiveType.Cube, "Ceiling", cabinShell, new Vector3(0, 2.4f, 0), new Vector3(3.2f, 0.1f, 5f), m.Cockpit, collider: false);
            Prim(PrimitiveType.Cube, "Wall Left", cabinShell, new Vector3(-1.6f, 1.2f, 0), new Vector3(0.1f, 2.4f, 5f), m.Cockpit, collider: false);
            Prim(PrimitiveType.Cube, "Wall Right", cabinShell, new Vector3(1.6f, 1.2f, 0), new Vector3(0.1f, 2.4f, 5f), m.Cockpit, collider: false);
            Prim(PrimitiveType.Cube, "Wall Back", cabinShell, new Vector3(0, 1.2f, -1.2f), new Vector3(3.2f, 2.4f, 0.1f), m.Cockpit, collider: false);
            Prim(PrimitiveType.Cube, "Seat", cabinShell, new Vector3(0, 0.22f, -0.05f), new Vector3(0.7f, 0.44f, 0.6f), m.Seat, collider: false);
            Prim(PrimitiveType.Cube, "Seat Back", cabinShell, new Vector3(0, 0.95f, -0.42f), new Vector3(0.7f, 1.5f, 0.14f), m.Seat, collider: false);
            Prim(PrimitiveType.Cube, "Console", cabinShell, new Vector3(0, 0.4f, 0.95f), new Vector3(2.4f, 0.8f, 0.5f), m.Console, collider: false);
            foreach (var r in cabinShell.GetComponentsInChildren<Renderer>())
                if (r.name.StartsWith("Wall") || r.name == "Ceiling") r.sharedMaterial = ModelMats["Ship_Padding"];
            ShipInterior(cabinShell, new Vector3(3.2f, 2.4f, 5f), Vector3.zero, 2.5f);
            Prim(PrimitiveType.Cube, "Hatch", cabinShell, new Vector3(0.9f, 1.15f, -1.13f), new Vector3(1.0f, 2.0f, 0.06f), ModelMats["Ship_Panels"], collider: false);
            Prim(PrimitiveType.Cube, "Hatch Frame", cabinShell, new Vector3(0.9f, 1.15f, -1.15f), new Vector3(1.16f, 2.16f, 0.05f), ModelMats["Rocket_Metal"], collider: false);
            Prim(PrimitiveType.Cube, "Headrest", cabinShell, new Vector3(0, 1.72f, -0.38f), new Vector3(0.42f, 0.3f, 0.16f), m.Seat, collider: false);
            for (int i = -1; i <= 1; i++)
                Prim(PrimitiveType.Quad, "Panel", cabinShell, new Vector3(i * 1.0f, 1.5f, -1.14f), new Vector3(0.7f, 0.45f, 1), m.ConsoleScreen, collider: false);
            Person("PilotInCabin", cabin, cabinOrigin + new Vector3(0, 0, 0), 0f, true, "Astronaut", Speakers.Commander, typing: false);
            InteriorVolume(cabin, cabinOrigin + new Vector3(0, 1.2f, 0.5f), new Vector3(6, 5, 8));
            var flash = PointLight("Portal Light", cabin, cabinOrigin + new Vector3(0.6f, 1.7f, 2.4f), new Color(0.6f, 0.85f, 1f), 6f, 9f);
            flash.gameObject.AddComponent<FlickerLight>();
            PointLight("Cabin Alarm", cabin, cabinOrigin + new Vector3(-1.2f, 2f, -0.6f), new Color(1f, 0.15f, 0.1f), 3f, 5f);

            // ---- Menu + cutscene ----
            var menuPosition = Space;
            var menuLook = Space + new Vector3(-8, -5, 300);
            var cam = NewCamera("Cutscene Camera", menuPosition, Quaternion.LookRotation(menuLook - menuPosition), 55f, listener: true, post: true);
            var markers = new GameObject("Shot Markers").transform;
            var fx = new GameObject("Cutscene Effects").AddComponent<CutsceneFx>();
            var cutscene = new GameObject("Cutscene Director").AddComponent<CutsceneDirector>();
            Set(cutscene, ("cutsceneCamera", cam), ("playOnStart", false), ("letterboxDuration", 1.2f), ("endTitle", ""),
                ("cutToBlackAtEnd", true), ("blackHold", 2.5f), ("nextScene", SceneFlow.Memory));
            var menu = new GameObject("Main Menu").AddComponent<MainMenu>();
            Set(menu, ("cutscene", cutscene));

            var screenCentre = new Vector3(0, 4f, 8.8f);
            var shots = cutscene.Shots;
            shots.Clear();

            // 0: straight out of the menu view. The rocket comes in from behind and the camera swings to follow it.
            var menuMarker = Marker("Shot0 Menu View", markers, menuPosition, menuLook);
            var flyby = Shot("0 Fly-by", menuMarker, menuMarker, 10f);
            flyby.fieldOfView = 55f;
            flyby.shake = 0.02f;
            flyby.lookAt = centre;
            flyby.lookAtDelay = 2.7f;
            flyby.lookAtBlend = 2.6f;
            flyby.waitForLines = false;
            UnityEventTools.AddPersistentListener(flyby.onStart, new UnityAction(flight.BeginApproach));
            shots.Add(flyby);

            shots.Add(Shot("1 Mission Control", Marker("Shot1 From", markers, new Vector3(0, 5.6f, -8.5f), screenCentre),
                Marker("Shot1 To", markers, new Vector3(0, 4.4f, -5f), screenCentre), 7f,
                Line(Speakers.FlightDirector, "All stations, this is Flight. Fourteen months out, and this is it. Final approach to Planet X."),
                Line(Speakers.Controller, "Telemetry is green across the board, Flight.")));

            shots.Add(Shot("2 The Team", Marker("Shot2 From", markers, new Vector3(-8f, 2.4f, 2f), new Vector3(-5.5f, 1.2f, -1.1f)),
                Marker("Shot2 To", markers, new Vector3(-3f, 2.4f, 2f), new Vector3(-0.5f, 1.2f, -1.1f)), 7f,
                Line(Speakers.Technician, "He's actually going to make it."),
                Line(Speakers.Controller, "Fourteen months, one pilot, and not a single missed burn. Of course he is."),
                Line(Speakers.Technician, "My kids are watching this at school. The whole planet is.")));

            shots.Add(Shot("3 Main Screen", Marker("Shot3 From", markers, new Vector3(1.4f, 2.3f, -5.2f), screenCentre),
                Marker("Shot3 To", markers, new Vector3(0.7f, 2.6f, -3f), screenCentre), 6f,
                Line(Speakers.FlightDirector, "Commander, you're forty thousand kilometers out. The whole world is watching you."),
                Line(Speakers.Commander, "Copy, Flight. Tell the world I can see it from here. We're going to make it.")));

            // 4: the portal appears ahead of the rocket
            var beforePortal = Marker("Rocket Before Portal", markers, Space + new Vector3(0, 0, 1300), Space + new Vector3(0, 0, 2000));
            var mover = new GameObject("Move Rocket To Portal").AddComponent<MoveTo>();
            Set(mover, ("target", rocket), ("destination", beforePortal));
            var portalPoint = Space + new Vector3(0, 0, 2000);
            var s4 = Shot("4 The Portal Appears", Marker("Shot4 From", markers, Space + new Vector3(48, 16, 1250), portalPoint),
                Marker("Shot4 To", markers, Space + new Vector3(40, 12, 1300), portalPoint), 7f,
                Line(Speakers.Controller, "Flight, I have a gravity spike. Dead ahead of the rocket."));
            s4.fieldOfView = 42f;
            s4.shake = 0.03f;
            s4.linesDelay = 2.5f;
            UnityEventTools.AddPersistentListener(s4.onStart, new UnityAction(mover.Go));
            UnityEventTools.AddPersistentListener(s4.onStart, new UnityAction(gateRing.Appear));
            shots.Add(s4);

            // 5: confusion in the room
            var operatorMonitor = alarmOperator.position + new Vector3(0, 1.15f, 1.4f);
            var s5 = Shot("5 Confusion", Marker("Shot5 From", markers, alarmOperator.position + new Vector3(2.1f, 1.8f, -1.3f), operatorMonitor),
                Marker("Shot5 To", markers, alarmOperator.position + new Vector3(1.4f, 1.65f, -0.8f), operatorMonitor), 6f,
                Line(Speakers.Technician, "What is that?"),
                Line(Speakers.Controller, "It's not on any chart. It wasn't there a second ago."),
                Line(Speakers.FlightDirector, "Commander, abort the approach. Pull away. Pull away now!"));
            UnityEventTools.AddPersistentListener(s5.onStart, new UnityAction(alarm.Trigger));
            UnityEventTools.AddPersistentListener(s5.onStart, new UnityAction(monitor.ShowAlert));
            shots.Add(s5);

            // 6: the rocket's own view as it is dragged in
            var closeToPortal = Marker("Rocket Close To Portal", markers, Space + new Vector3(0, 0, 1420), Space + new Vector3(0, 0, 2000));
            var mover2 = new GameObject("Move Rocket Close").AddComponent<MoveTo>();
            Set(mover2, ("target", rocket), ("destination", closeToPortal));
            var ahead = rocket.TransformPoint(new Vector3(0, 0, 400));
            var s6 = Shot("6 Rocket View", Marker("Shot6 From", rocket, rocket.TransformPoint(new Vector3(0, 5.5f, 4)), ahead),
                Marker("Shot6 To", rocket, rocket.TransformPoint(new Vector3(0, 4.2f, 15)), ahead), 7f,
                Line(Speakers.Commander, "Flight, there's a ring out here. It's pulling me in. I can't break away—"));
            s6.fieldOfView = 68f;
            s6.shake = 0.1f;
            s6.waitForLines = false;
            UnityEventTools.AddPersistentListener(s6.onStart, new UnityAction(mover2.Go));
            UnityEventTools.AddPersistentListener(s6.onStart, new UnityAction(flight.PullIntoGate));
            shots.Add(s6);

            // 7: inside the cabin, shaking hard, then black
            var pilotHead = cabinOrigin + new Vector3(0, 1.2f, 0);
            var s7 = Shot("7 Inside The Rocket", Marker("Shot7 From", markers, cabinOrigin + new Vector3(0.75f, 1.35f, 1.9f), pilotHead),
                Marker("Shot7 To", markers, cabinOrigin + new Vector3(0.35f, 1.25f, 1.25f), pilotHead), 4.5f,
                Line(Speakers.Commander, "Flight? Flight, do you read—"));
            s7.fieldOfView = 55f;
            s7.shake = 0.09f;
            s7.linesDelay = 1f;
            UnityEventTools.AddPersistentListener(s7.onStart, new UnityAction(fx.FlashWhite));
            shots.Add(s7);

            EditorUtility.SetDirty(cutscene);
            AddHud();
            Save(scene, SceneFlow.MissionControl);
        }

        // =====================================================================
        // 02 Memory: launch day on a dying Earth
        // =====================================================================

        static void BuildMemory(Mats m, VolumeProfile post)
        {
            var scene = NewScene(MemoryPostProfile(), ambient: Color.clear, skybox: true);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.56f, 0.42f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.45f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.24f, 0.19f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.003f;
            RenderSettings.fogColor = new Color(0.88f, 0.47f, 0.27f);      // dust the colour of the sky
            var sun = Sun(Quaternion.Euler(34f, 38f, 0f), 1.6f);
            sun.color = new Color(1f, 0.78f, 0.55f);
            RenderSettings.skybox = PanoSky("Sky_DustClouds", "Sky_Dust", 2.6f);

            var mm = ModelMats;
            var pad = new Vector3(0, 0, 70);
            var random = new System.Random(4);

            // ---- Ground: dry, cracked, almost no water left ----
            var land = new GameObject("Land").transform;
            Prim(PrimitiveType.Plane, "Ground", land, Vector3.zero, new Vector3(90, 1, 90), mm["Ground_Cracked"]);
            var basinCentre = new Vector3(82, 0, 34);
            Prim(PrimitiveType.Cylinder, "Dry Lake Bed", land, basinCentre + new Vector3(0, 0.01f, 0), new Vector3(92, 0.01f, 70), mm["Ground_Mud"], collider: false);
            Prim(PrimitiveType.Cylinder, "Last Water", land, new Vector3(68, 0.03f, 37), new Vector3(9, 0.01f, 6), mm["Water"], collider: false);
            var pier = new GameObject("Old Pier").transform;
            pier.SetParent(land);
            for (int i = 0; i < 8; i++)
            {
                Prim(PrimitiveType.Cube, "Plank", pier, new Vector3(41 + i * 2f, 1.1f, 44), new Vector3(1.9f, 0.1f, 2.2f), m.Bark);
                if (i % 2 == 0)
                    foreach (float side in new[] { -1f, 1f })
                        Prim(PrimitiveType.Cylinder, "Post", pier, new Vector3(41 + i * 2f, 0.55f, 44 + side), new Vector3(0.18f, 0.55f, 0.18f), m.Bark);
            }
            var boat = Slot("StrandedBoat", land, new Vector3(60, 0.35f, 24), new Vector3(0, 35, 14));
            Prim(PrimitiveType.Cube, "Placeholder", boat, Vector3.zero, new Vector3(1.6f, 0.7f, 4.5f), mm["Tower_Steel"]);
            Prim(PrimitiveType.Cube, "Cabin", boat, new Vector3(0, 0.6f, -0.8f), new Vector3(1.2f, 0.7f, 1.4f), m.Ruin);

            // Dead trees
            var trees = new GameObject("Dead Trees").transform;
            Vector3[] keepClear = { pad, new Vector3(2, 0, 22), new Vector3(-24, 0, 44), new Vector3(-30, 0, 14), new Vector3(34, 0, 30), new Vector3(46, 0, -24), new Vector3(23, 0, 56) };
            for (int i = 0; i < 90; i++)
            {
                var p = new Vector3(Range(random, -190, 190), 0, Range(random, -120, 210));
                if (Vector3.Distance(p, pad) < 30f || keepClear.Any(c => Vector3.Distance(p, c) < 13f)) continue;
                if (Vector3.Distance(p, basinCentre) < 50f) continue;
                var tree = Model(random.Next(2) == 0 ? "DeadTree_1" : "DeadTree_2", trees, p, Quaternion.Euler(0, Range(random, 0, 360), 0), Range(random, 1.1f, 2.2f));
                tree.name = "MODEL_DeadTree";
            }

            // A ruined city on the horizon, half lost in the dust
            SkylineRing(pad, 420f, 150f, mm["Skyline"], -110f, 110f, 4);

            // ---- Launch pad, tower and rocket ----
            var padSlot = Slot("LaunchPad", null, pad + new Vector3(0, 0.02f, 0), Vector3.zero);
            Model("LaunchTower", padSlot, Vector3.zero, Quaternion.identity, 1f);
            var towerBox = padSlot.gameObject.AddComponent<BoxCollider>();
            towerBox.center = new Vector3(-8, 18, 0);
            towerBox.size = new Vector3(4.4f, 36, 4.4f);

            var rocket = Slot("Rocket", null, pad + new Vector3(0, 4.5f, 0), Vector3.zero);
            var rocketModel = Model("Rocket", rocket, Vector3.zero, Quaternion.identity, 1f);
            Child(rocketModel, "SolarPanels").gameObject.SetActive(false);
            var hullCollider = rocket.gameObject.AddComponent<CapsuleCollider>();
            hullCollider.center = new Vector3(0, 12, 0);
            hullCollider.radius = 3.4f;
            hullCollider.height = 34f;
            var rocketCentre = new GameObject("Centre").transform;
            rocketCentre.SetParent(rocket, false);
            rocketCentre.localPosition = new Vector3(0, 14, 0);
            var down = Quaternion.Euler(90, 0, 0);
            var white = new Color(1f, 0.95f, 0.8f);
            var orange = new Color(1f, 0.4f, 0.08f);
            var plumes = new Object[]
            {
                Plume("Core Plume", rocket, new Vector3(0, -2.2f, 0), down, 4.5f, 42f, 0.9f, 110f, true, white, orange),
                Plume("Booster Plume L", rocket, new Vector3(-2.78f, -0.9f, 0), down, 3f, 40f, 0.8f, 80f, true, white, orange),
                Plume("Booster Plume R", rocket, new Vector3(2.78f, -0.9f, 0), down, 3f, 40f, 0.8f, 80f, true, white, orange),
            };
            var launchLight = PointLight("Launch Light", rocket, rocket.position + new Vector3(0, -4, 0), new Color(1f, 0.65f, 0.3f), 0f, 120f);
            var padSmoke = Smoke("Pad Smoke", null, pad + new Vector3(0, 1.5f, 0));

            // Cargo lift and the engineer
            var liftSlot = Slot("CargoLift", null, pad + new Vector3(-3.5f, 0, -7.5f), Vector3.zero);
            Prim(PrimitiveType.Cube, "Placeholder", liftSlot, new Vector3(0, 0.15f, 0), new Vector3(2.6f, 0.3f, 2.6f), mm["Tower_Steel"]);
            foreach (var corner in new[] { new Vector3(-1.2f, 0.9f, -1.2f), new Vector3(1.2f, 0.9f, -1.2f), new Vector3(-1.2f, 0.9f, 1.2f), new Vector3(1.2f, 0.9f, 1.2f) })
                Prim(PrimitiveType.Cube, "Rail Post", liftSlot, corner, new Vector3(0.08f, 1.2f, 0.08f), mm["Crate_Trim"], collider: false);
            Prim(PrimitiveType.Cube, "Control Box", liftSlot, new Vector3(-1.2f, 1.1f, -1.35f), new Vector3(0.4f, 0.5f, 0.2f), mm["Crate_Light"]);
            var lift = liftSlot.gameObject.AddComponent<Interactable>();
            Set(lift, ("prompt", "Load the cargo"), ("holdDuration", 1.5f));
            var liftBeacon = Beacon(liftSlot.position, m);

            var engineerSlot = Person("Engineer", null, pad + new Vector3(-0.8f, 0, -9.6f), 180f, false, "Engineer", Speakers.Engineer, lookAtPlayer: true);
            var engineer = Talkable(engineerSlot, "Talk to the engineer");
            var ballSlot = Slot("GripBallGift", engineerSlot, engineerSlot.TransformPoint(new Vector3(0.3f, 1.2f, 0.55f)), Vector3.zero);
            Model("GripBall", ballSlot, Vector3.zero, Quaternion.identity, 1f);
            ballSlot.gameObject.AddComponent<Spinner>();
            Set(ballSlot.GetComponent<Spinner>(), ("degreesPerSecond", new Vector3(20, 45, 0)));

            // ---- The camp: workshop, greenhouse, pump ----
            var workshop = Slot("Workshop", null, new Vector3(-24, 0, 44), Vector3.zero);
            Shelter(workshop, new Vector3(7, 3, 5), mm["Canvas"], mm["Tower_Steel"]);
            Prim(PrimitiveType.Cube, "Bench", workshop, new Vector3(0, 0.45f, 1f), new Vector3(3.5f, 0.9f, 1f), m.Desk);
            Prim(PrimitiveType.Cube, "Tool Cabinet", workshop, new Vector3(-2.6f, 1f, 1.6f), new Vector3(1f, 2f, 0.6f), mm["Tower_Steel"]);
            var coreSlot = Slot("GuidanceCore", workshop, workshop.position + new Vector3(0.4f, 0.9f, 1f), new Vector3(0, 20, 0));
            Model("Crate", coreSlot, Vector3.zero, Quaternion.identity, 0.8f);
            coreSlot.gameObject.AddComponent<BoxCollider>().center = new Vector3(0, 0.2f, 0);
            coreSlot.GetComponent<BoxCollider>().size = new Vector3(0.8f, 0.5f, 0.6f);
            var guidanceCore = coreSlot.gameObject.AddComponent<Interactable>();
            Set(guidanceCore, ("prompt", "Take the guidance core"), ("hideOnUse", true));
            PointLight("Workshop Light", workshop, workshop.position + new Vector3(0, 2.5f, 0), new Color(1f, 0.85f, 0.6f), 4f, 9f);
            var coreBeacon = Beacon(workshop.position + new Vector3(0, 3.2f, 0), m);

            var greenhouse = Slot("Greenhouse", null, new Vector3(-31, 0, 13), new Vector3(0, -90, 0));
            Shelter(greenhouse, new Vector3(8, 2.8f, 5), mm["Glasshouse"], mm["Tower_Steel"]);
            for (int i = -1; i <= 1; i++)
            {
                Prim(PrimitiveType.Cube, "Planter", greenhouse, new Vector3(i * 2.4f, 0.3f, 0.6f), new Vector3(1.8f, 0.6f, 1.2f), m.Bark);
                for (int k = 0; k < 4; k++)
                    Prim(PrimitiveType.Cube, "Dead Plant", greenhouse, new Vector3(i * 2.4f - 0.6f + k * 0.4f, 0.85f, 0.6f), new Vector3(0.04f, 0.5f, 0.04f), mm["Tree_Dead"],
                        Quaternion.Euler(Range(random, -20, 20), 0, Range(random, -20, 20)), collider: false);
            }
            Prim(PrimitiveType.Sphere, "Last Seedling", greenhouse, new Vector3(0.2f, 0.75f, 0.6f), new Vector3(0.22f, 0.3f, 0.22f), m.Leaves, collider: false);
            var botanistSlot = Person("Botanist", null, new Vector3(-26.5f, 0, 14.5f), 80f, false, "Botanist", Speakers.Botanist, lookAtPlayer: true,
                tint: new Color(0.75f, 1f, 0.75f));
            var botanist = Talkable(botanistSlot, "Talk to the botanist");
            var seedCase = Slot("SeedVault", null, new Vector3(-25.9f, 0, 13.5f), new Vector3(0, 60, 0));
            Model("Crate", seedCase, Vector3.zero, Quaternion.identity, 1f, new() { ["Crate_Light"] = m.Leaves });
            var botanistBeacon = Beacon(botanistSlot.position + new Vector3(0, 2.4f, 0), m);

            var pumpSlot = Slot("ReservoirPump", null, new Vector3(34, 0, 30), new Vector3(0, 100, 0));
            Prim(PrimitiveType.Cube, "Placeholder", pumpSlot, new Vector3(0, 0.7f, 0), new Vector3(1.6f, 1.4f, 1.2f), mm["Tower_Steel"]);
            Prim(PrimitiveType.Cylinder, "Tank", pumpSlot, new Vector3(1.6f, 0.9f, 0), new Vector3(1.1f, 0.9f, 1.1f), m.TankBlue);
            Prim(PrimitiveType.Cylinder, "Pipe", pumpSlot, new Vector3(-0.2f, 0.25f, 9f), new Vector3(0.25f, 9f, 0.25f), mm["Crate_Trim"], Quaternion.Euler(90, 0, 0), collider: false);
            var sign = WorldText("Reservoir Sign", pumpSlot, pumpSlot.TransformPoint(new Vector3(0, 1.75f, -0.62f)), new Vector2(360, 120), 0.005f, 34, new Color(1f, 0.85f, 0.5f));
            sign.transform.parent.rotation = pumpSlot.rotation;
            sign.text = "RESERVOIR 3\nCAPACITY 2%";
            var pump = pumpSlot.gameObject.AddComponent<Interactable>();
            Set(pump, ("prompt", "Fill the water canister"), ("holdDuration", 2.5f));
            Person("Quartermaster", null, new Vector3(32.2f, 0, 28.2f), 270f, false, "Quartermaster", Speakers.Quartermaster, lookAtPlayer: true);
            var pumpBeacon = Beacon(pumpSlot.position + new Vector3(0, 1.6f, 0), m);

            // Supplies and the crowd that came to watch
            var supplies = new GameObject("Supplies").transform;
            foreach (var (p, yaw, size) in new[]
                     {
                         (new Vector3(12, 1.3f, 36), 15f, new Vector3(6, 2.6f, 2.5f)), (new Vector3(15, 1.3f, 30), -70f, new Vector3(6, 2.6f, 2.5f)),
                         (new Vector3(-10, 1.3f, 30), 80f, new Vector3(6, 2.6f, 2.5f)),
                     })
                Prim(PrimitiveType.Cube, "Container", supplies, p, size, mm["Container_Rust"], Quaternion.Euler(0, yaw, 0));
            for (int i = 0; i < 9; i++)
            {
                var crate = Model("Crate", supplies, new Vector3(Range(random, -8, 10), 0, Range(random, 27, 40)), Quaternion.Euler(0, Range(random, 0, 360), 0), Range(random, 0.9f, 1.4f));
                crate.name = "MODEL_Crate";
            }
            // Supply cases scattered across the ground between the player and the pad
            for (int i = 0; i < 14; i++)
            {
                var crate = Model("Crate", supplies, new Vector3(Range(random, -16, 20), 0, Range(random, 14, 52)), Quaternion.Euler(0, Range(random, 0, 360), 0), Range(random, 1.2f, 1.9f));
                crate.name = "MODEL_Crate";
            }
            var crowd = new GameObject("Crowd").transform;
            string[] onlookers = { "OperatorA", "OperatorC", "Technician", "OperatorB", "Controller", "FlightDirector" };
            Color[] clothes = { Color.white, new Color(1f, 0.8f, 0.7f), new Color(0.8f, 0.9f, 0.7f), new Color(0.85f, 0.8f, 1f), new Color(0.7f, 0.7f, 0.7f) };
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(37 + i * 2.1f + Range(random, -0.4f, 0.4f), 0, -22 + Range(random, -1.5f, 1.5f));
                float yaw = Mathf.Atan2(pad.x - p.x, pad.z - p.z) * Mathf.Rad2Deg;
                Person($"Onlooker{i + 1}", crowd, p, yaw, false, onlookers[i % onlookers.Length], null, rocketCentre, tint: clothes[i % clothes.Length]);
            }
            for (int i = 0; i < 12; i++)
                Prim(PrimitiveType.Cube, "Fence Post", crowd, new Vector3(34 + i * 2.2f, 0.55f, -19.5f), new Vector3(0.1f, 1.1f, 0.1f), mm["Crate_Trim"]);
            Prim(PrimitiveType.Cube, "Fence Rail", crowd, new Vector3(46, 1f, -19.5f), new Vector3(25f, 0.08f, 0.08f), mm["Crate_Trim"]);

            // ---- Player ----
            var (player, playerCam) = Player(new Vector3(2, 0, 22), 1.65f, seatedYawLimit: 0f);
            // ---- Practice range: the grip ball tutorial (targets behind a rail the player can't cross) ----
            var range = new GameObject("Practice Range").transform;
            Wall(range, new Vector3(23, 2, 51), new Vector3(20, 4, 0.3f));
            Wall(range, new Vector3(23, 2, 67), new Vector3(20, 4, 0.3f));
            Wall(range, new Vector3(13, 2, 59), new Vector3(0.3f, 4, 16));
            Wall(range, new Vector3(33, 2, 59), new Vector3(0.3f, 4, 16));
            Prim(PrimitiveType.Cube, "Rail", range, new Vector3(23, 1f, 51), new Vector3(20, 0.08f, 0.08f), mm["Crate_Trim"], collider: false);
            for (int i = 0; i <= 10; i++)
                Prim(PrimitiveType.Cube, "Rail Post", range, new Vector3(13 + i * 2f, 0.5f, 51), new Vector3(0.1f, 1f, 0.1f), mm["Crate_Trim"], collider: false);
            var rangeSign = WorldText("Range Sign", range, new Vector3(23, 2.3f, 51), new Vector2(900, 120), 0.004f, 60, new Color(1f, 0.8f, 0.5f));
            rangeSign.transform.parent.rotation = Quaternion.Euler(0, 0, 0);
            rangeSign.text = "PRACTICE RANGE";
            // 1: a straight shot
            Prim(PrimitiveType.Cube, "Target Post", range, new Vector3(27, 0.5f, 63.2f), new Vector3(0.15f, 1f, 0.15f), mm["Tower_Steel"], collider: false);
            var target1 = Surface(Prim(PrimitiveType.Cube, "MODEL_Target_Straight", range, new Vector3(27, 1.7f, 63), new Vector3(1.6f, 1.6f, 0.15f), mm["Range_Target"]), BallSurface.Kind.Target);
            // 2: a loose crate on a post, to knock off
            Prim(PrimitiveType.Cube, "Crate Post", range, new Vector3(30.5f, 0.6f, 58), new Vector3(0.5f, 1.2f, 0.5f), mm["Tower_Concrete"]);
            var looseSlot = Slot("Target_Loose", range, new Vector3(30.5f, 1.21f, 58), Vector3.zero);
            Model("Crate", looseSlot, Vector3.zero, Quaternion.identity, 1f, new() { ["Crate_Light"] = mm["Range_Target"] });
            var looseBox = looseSlot.gameObject.AddComponent<BoxCollider>();
            looseBox.center = new Vector3(0, 0.25f, 0);
            looseBox.size = new Vector3(0.9f, 0.5f, 0.6f);
            looseSlot.gameObject.AddComponent<Rigidbody>().mass = 6f;
            var target2 = Surface(looseSlot.gameObject, BallSurface.Kind.Loose);
            // 3: hidden behind a wall; only a throw banked off the frost plate reaches it
            Prim(PrimitiveType.Cube, "Blind Wall", range, new Vector3(16.25f, 1.6f, 58.6f), new Vector3(5.5f, 3.2f, 0.3f), mm["Tower_Concrete"]);
            Prim(PrimitiveType.Cube, "Blind Wall Back", range, new Vector3(13.9f, 1.6f, 60.5f), new Vector3(0.3f, 3.2f, 4.1f), mm["Tower_Concrete"]);
            Prim(PrimitiveType.Cube, "Blind Wall Far", range, new Vector3(16.25f, 1.6f, 62.4f), new Vector3(5.5f, 3.2f, 0.3f), mm["Tower_Concrete"]);
            var target3 = Surface(Prim(PrimitiveType.Cube, "MODEL_Target_Banked", range, new Vector3(14.5f, 1.7f, 60.5f), new Vector3(0.15f, 2f, 2.6f), mm["Range_Target"]), BallSurface.Kind.Target);
            Prim(PrimitiveType.Cube, "Frost Post", range, new Vector3(21, 0.3f, 60.5f), new Vector3(0.2f, 0.6f, 0.2f), mm["Tower_Steel"], collider: false);
            var frostPlate = Surface(Prim(PrimitiveType.Cube, "MODEL_FrostPlate", range, new Vector3(21, 1.8f, 60.5f), new Vector3(2.8f, 2.8f, 0.15f), mm["Frost_Glaze"],
                Quaternion.Euler(0, 45, 0)), BallSurface.Kind.Frost);
            var rangeMarker = Beacon(new Vector3(23, 0, 49.5f), m);
            var rangeBall = GripRig(playerCam, null);

            // Grit blowing across the dry ground, carried along with the player
            Motes(playerCam.transform, new Vector3(0, 0, 6), new Vector3(40, 8, 40), 500, new Color(0.9f, 0.75f, 0.55f, 0.55f), 0.06f, 0.4f, false, new Vector3(3.5f, 0.1f, 1f));

            // ---- Launch cameras, VHS filter and the title stage ----
            var cutsceneCam = NewCamera("Cutscene Camera", Vector3.zero, Quaternion.identity, 32f, listener: true, post: true);
            cutsceneCam.gameObject.SetActive(false);
            var markers = new GameObject("Shot Markers").transform;
            var wide = Marker("Launch Wide", markers, new Vector3(56, 1.7f, -38), pad + new Vector3(0, 18, 0));
            var low = Marker("Launch Low", markers, pad + new Vector3(-13, 1f, -13), pad + new Vector3(0, 30, 0));
            var vhs = new GameObject("VHS Filter").AddComponent<VhsFilter>();
            Set(vhs, ("shader", Shader.Find("TSA/Vhs")));

            var stage = new Vector3(0, 3000, 0);
            var stageRoot = new GameObject("Title Stage").transform;
            stageRoot.position = stage;
            Starfield(stageRoot, m.Stars, 900f);
            Nebula(stageRoot, stage + new Vector3(-250, 80, 760), 700f, "Nebula_Violet", new Color(0.5f, 0.25f, 0.9f));
            Nebula(stageRoot, stage + new Vector3(300, -120, 760), 600f, "Nebula_Blue", new Color(0.15f, 0.4f, 0.9f));
            Asteroid(stageRoot, random, stage + new Vector3(-15, 6, 34), 4f, collider: false);
            Asteroid(stageRoot, random, stage + new Vector3(17, -7, 40), 6f, collider: false);
            var titleSlot = Slot("TitleLetters", stageRoot, stage + new Vector3(0, 0, 15), new Vector3(0, 180, 0));
            titleSlot.localScale = Vector3.one * 1.9f;
            Model("TitleLetters", titleSlot, Vector3.zero, Quaternion.identity, 1f);
            var title = titleSlot.gameObject.AddComponent<TitleLetters>();
            PointLight("Title Key Light", stageRoot, stage + new Vector3(-7, 4, 6), new Color(0.8f, 0.9f, 1f), 60f, 40f);
            PointLight("Title Rim Light", stageRoot, stage + new Vector3(8, -3, 20), new Color(0.3f, 0.55f, 1f), 80f, 40f);
            var titleShot = Marker("Title Shot", markers, stage, stage + new Vector3(0, 0, 15));

            var sequence = new GameObject("Memory Sequence").AddComponent<MemorySequence>();
            Set(sequence, ("player", player.GetComponent<FirstPersonController>()), ("engineer", engineer), ("pump", pump), ("botanist", botanist),
                ("guidanceCore", guidanceCore), ("lift", lift), ("pumpBeacon", pumpBeacon), ("botanistBeacon", botanistBeacon),
                ("coreBeacon", coreBeacon), ("liftBeacon", liftBeacon), ("seedCase", seedCase.gameObject), ("ballProp", ballSlot.gameObject),
                ("cutsceneCamera", cutsceneCam), ("vhs", vhs), ("rocket", rocket), ("rocketCentre", rocketCentre), ("plumes", plumes),
                ("padSmoke", padSmoke), ("launchLight", launchLight), ("wideShot", wide), ("lowShot", low), ("titleShot", titleShot), ("title", title), ("titleStage", stageRoot.gameObject),
                ("engineerPerson", engineerSlot.GetComponentInChildren<PersonAnimator>()), ("rangeBall", rangeBall),
                ("rangeTargets", new Object[] { target1, target2, target3 }), ("rangeFrost", frostPlate), ("rangeMarker", rangeMarker));

            AddHud();
            Save(scene, SceneFlow.Memory);
        }

        // =====================================================================
        // 03 Adrift: thrown out of the rocket, with only the grip ball to get back
        // =====================================================================

        static void BuildAdrift(Mats m, VolumeProfile post)
        {
            var scene = NewScene(SpacePostProfile(), ambient: new Color(0.06f, 0.068f, 0.085f), skybox: false);
            // Lit like the title scene: a blue-white star as the key, the same star from further round as a side
            // light that picks out edges, a soft neutral fill so shadowed faces keep their detail, and here a
            // blue-green glow coming up off the ocean world below.
            var sun = Sun(Quaternion.Euler(22f, 24f, 0f), 3.2f);
            sun.color = new Color(0.78f, 0.88f, 1f);
            SunFlare(sun);
            RimLight(Quaternion.Euler(24f, 70f, 0f), new Color(0.72f, 0.86f, 1f), 1.1f).name = "Star Side Light";
            RimLight(Quaternion.Euler(10f, 190f, 0f), new Color(0.7f, 0.78f, 0.92f), 0.4f).name = "Fill Light";
            RimLight(Quaternion.LookRotation(-new Vector3(-500, -620, 1300)), new Color(0.35f, 0.65f, 1f), 0.45f).name = "Planet Bounce";
            var random = new System.Random(9);

            var outside = new GameObject("Space").transform;
            Starfield(outside, m.Stars, 2600f);
            Nebula(outside, new Vector3(-1500, 500, 1900), 2400f, "Nebula_Violet", new Color(0.5f, 0.25f, 0.9f));
            Nebula(outside, new Vector3(1700, -500, -1400), 2200f, "Nebula_Teal", new Color(0.1f, 0.6f, 0.6f));
            var planet = Slot("NotEarth", outside, new Vector3(-500, -620, 1300), new Vector3(0, 0, 20));
            Prim(PrimitiveType.Sphere, "Placeholder", planet, Vector3.zero, Vector3.one * 1100f, m.NotEarth, collider: false).AddComponent<Spinner>();
            DressPlanet(planet, 1100f, "NotEarth", new Color(0.35f, 0.6f, 1f), -sun.transform.forward, rings: false);
            Debris(outside, new Vector3(0, 0, 60), new Vector3(500, 260, 500), 1600, new Vector2(0.1f, 1.2f));
            var moon = Slot("BurntMoon", outside, new Vector3(700, 300, 1500), Vector3.zero);
            Prim(PrimitiveType.Sphere, "Placeholder", moon, Vector3.zero, Vector3.one * 120f, m.Moon, collider: false);

            // ---- The rocket, where it ends up after the strike ----
            var restPosition = new Vector3(34, 10, 118);
            var restRotation = Quaternion.Euler(8, -65, 0);
            var rocket = Slot("Rocket", null, restPosition, restRotation.eulerAngles);
            var rocketModel = Model("Rocket", rocket, Vector3.zero, Quaternion.Euler(90, 0, 0), 1f);
            Child(rocketModel, "Boosters").gameObject.SetActive(false);
            foreach (var part in new[] { "Hull", "SolarPanels" })
            {
                var filter = Child(rocketModel, part).GetComponent<MeshFilter>();
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
            var hatch = Child(rocketModel, "Anchor_Hatch");
            var hatchLight = PointLight("Hatch Light", hatch, hatch.position - rocket.right * 1.2f, new Color(0.3f, 1f, 0.45f), 8f, 30f);
            Set(hatchLight.gameObject.AddComponent<FlickerLight>(), ("minIntensity", 3f), ("maxIntensity", 14f), ("speed", 1.5f));
            hatch.gameObject.AddComponent<CompassMarker>();
            Prim(PrimitiveType.Sphere, "Hatch Lamp", hatch, new Vector3(0, 0, 0), Vector3.one * 0.35f, m.Leaves, collider: false)
                .GetComponent<Renderer>().sharedMaterial = ModelMats["Hatch_Lamp"];
            var plume = Plume("Engine Plume", rocket, new Vector3(0, 0, -2.2f), Quaternion.Euler(180, 0, 0), 2.6f, 26f, 0.55f, 300f, false,
                new Color(0.75f, 0.85f, 1f), new Color(1f, 0.45f, 0.1f));

            var markers = new GameObject("Markers").transform;
            var rocketRest = Pose("Rocket Rest", markers, restPosition, restRotation);
            var rocketFlyStart = Pose("Rocket Fly Start", markers, restPosition - rocket.forward * 240f, restRotation);

            // The rock that hit it, wedged into the engine section
            var rockRestPosition = restPosition + rocket.forward * 5f - rocket.right * 3.2f + rocket.up * 1.5f;
            var rockSlot = Slot("ImpactRock", null, rockRestPosition, new Vector3(20, 40, 10));
            var rockModel = Model("Asteroid_2", rockSlot, Vector3.zero, Quaternion.identity, 4.5f);
            AddMeshColliders(rockModel);
            var rockRest = Pose("Rock Rest", markers, rockRestPosition, rockSlot.rotation);
            var rockStart = Pose("Rock Start", markers, rockRestPosition - rocket.right * 150f + rocket.up * 60f + rocket.forward * 30f, rockSlot.rotation);

            var damage = new GameObject("Damage Effects").transform;
            damage.SetParent(rocket, false);
            damage.position = rockRestPosition + rocket.right * 2.5f;
            var sparkLight = PointLight("Sparks Light", damage, damage.position, new Color(1f, 0.5f, 0.15f), 6f, 22f);
            Set(sparkLight.gameObject.AddComponent<FlickerLight>(), ("minIntensity", 0f), ("maxIntensity", 16f), ("speed", 9f));
            var sparks = Plume("Sparks", damage, Vector3.zero, Quaternion.LookRotation(-rocket.right), 0.35f, 9f, 1.2f, 30f, true,
                new Color(1f, 0.9f, 0.6f), new Color(1f, 0.3f, 0.05f));
            var sparksMain = sparks.main;
            sparksMain.playOnAwake = true;
            var burst = Burst("Debris Burst", null, rockRestPosition);

            var impactShot = Marker("Impact Shot", markers, restPosition + rocket.forward * 46f - rocket.right * 50f + rocket.up * 14f, restPosition + rocket.forward * 10f);
            var cutsceneCam = NewCamera("Cutscene Camera", impactShot.position, impactShot.rotation, 45f, listener: true, post: true);
            cutsceneCam.gameObject.SetActive(false);

            // ---- Rocks to throw the ball at: a rough path from the player to the hull, plus spares ----
            var rocks = new GameObject("Asteroids").transform;
            foreach (var (p, s) in new[]
                     {
                         // a zigzag, so the next rock is visible past the one you are holding on to
                         (new Vector3(-9, 3, 24), 3.5f), (new Vector3(9, -3, 44), 4f), (new Vector3(-7, 6, 66), 4f), (new Vector3(10, 10, 88), 3.5f),
                         (new Vector3(-20, -8, 40), 5f), (new Vector3(22, 4, 62), 4f), (new Vector3(-14, 14, 96), 5f), (new Vector3(28, -6, 100), 5f),
                         (new Vector3(0, -10, 14), 3f), (new Vector3(16, 8, 20), 3f),
                     })
            {
                var pathRock = Asteroid(rocks, random, p, s, collider: true, spin: false);
                // Two of the stepping stones are glazed with frost: the ball skips off them, so the route has to go round.
                if (p == new Vector3(9, -3, 44) || p == new Vector3(10, 10, 88))
                {
                    foreach (var r in pathRock.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ModelMats["Frost_Glaze"];
                    Surface(pathRock, BallSurface.Kind.Frost);
                    pathRock.name = "MODEL_FrostRock";
                }
            }
            // Loose debris wedged against the hatch: it has to be knocked clear before you can get in.
            var blockerRock = Asteroid(rocks, random, hatch.position - rocket.right * 3.4f, 2f, collider: false, spin: false);
            blockerRock.name = "MODEL_HatchDebris";
            blockerRock.AddComponent<SphereCollider>().radius = 1.05f;
            var blockerBody = blockerRock.AddComponent<Rigidbody>();
            blockerBody.useGravity = false;
            blockerBody.mass = 400f;
            blockerBody.linearDamping = 0.15f;
            var hatchBlocker = Surface(blockerRock, BallSurface.Kind.Loose);
            Set(hatchBlocker, ("awayFrom", rocket));
            for (int i = 0; i < 70; i++)
            {
                var p = new Vector3(Range(random, -1, 1), Range(random, -0.5f, 0.5f), Range(random, -1, 1)).normalized * Range(random, 170, 700);
                Asteroid(rocks, random, p + new Vector3(0, 0, 60), Range(random, 5f, 40f), collider: false);
            }

            // ---- Player ----
            var player = new GameObject("Player") { layer = 2 };          // "Ignore Raycast": the ball never sticks to you
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, -105, 0));
            var body = player.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 100f;
            body.linearDamping = 0f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            player.AddComponent<SphereCollider>().radius = 0.6f;
            var head = new GameObject("Head").transform;
            head.SetParent(player.transform, false);
            var cam = NewCamera("Player Camera", Vector3.zero, Quaternion.identity, 72f, listener: true, post: true);
            cam.nearClipPlane = 0.05f;
            cam.transform.SetParent(head, false);
            var shake = cam.gameObject.AddComponent<CameraShake>();
            PointLight("Helmet Lamp", cam.transform, Vector3.zero, new Color(0.85f, 0.92f, 1f), 1.2f, 22f).transform.localPosition = new Vector3(0, 0.1f, 0);
            var zeroG = player.AddComponent<ZeroGPlayer>();
            Set(zeroG, ("cameraPivot", head));
            var oxygen = player.AddComponent<OxygenSystem>();
            Set(oxygen, ("baseDrainPerSecond", 0.42f));

            var ball = GripRig(cam, zeroG);
            Motes(cam.transform, Vector3.zero, new Vector3(30, 30, 30), 160, new Color(0.8f, 0.85f, 1f, 0.5f), 0.015f, 0.05f, false);

            var sequence = new GameObject("Adrift Sequence").AddComponent<AdriftSequence>();
            Set(sequence, ("player", zeroG), ("ball", ball), ("oxygen", oxygen), ("playerShake", shake), ("hatch", hatch),
                ("cutsceneCamera", cutsceneCam), ("impactShot", impactShot), ("rocket", rocket), ("rocketFlyStart", rocketFlyStart),
                ("rocketRest", rocketRest), ("rock", rockSlot), ("rockStart", rockStart), ("rockRest", rockRest),
                ("plumes", new Object[] { plume }), ("debrisBurst", burst), ("damageEffects", damage.gameObject), ("hatchBlocker", hatchBlocker));

            AddHud();
            Save(scene, SceneFlow.Adrift);
        }

        // =====================================================================
        // Pieces shared by the opening scenes
        // =====================================================================

        static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        /// <summary>The grip ball, the player's glove and the controller that runs them. zeroG is null on the ground.</summary>
        static GripBall GripRig(Camera cam, ZeroGPlayer zeroG)
        {
            var ballSlot = Slot("GripBall", null, Vector3.zero, Vector3.zero);
            Model("GripBall", ballSlot, Vector3.zero, Quaternion.identity, 0.6f);
            var ball = new GameObject("Grip Ball Controller").AddComponent<GripBall>();
            // The player's own suit glove (an AI-generated model, see Tools/blender/import_generated.py).
            var gloveSlot = Slot("PlayerHand", null, Vector3.zero, Vector3.zero);
            var glove = (GameObject)PrefabUtility.InstantiatePrefab(PersonPrefab("AstronautGlove", animated: false), gloveSlot);
            glove.transform.localRotation = Quaternion.Euler(0, 0, 90) * Quaternion.Euler(0, 90, 0);
            glove.transform.localPosition = new Vector3(0, -0.07f, -0.1f);
            // The open, palm-up pose the hand was given by eye in the editor.
            var gloveMesh = glove.transform.Find("Model");
            gloveMesh.localPosition = new Vector3(0.001f, 0.154f, 0.166f);
            gloveMesh.localRotation = Quaternion.Euler(67.678f, -222.57f, 65.762f);
            gloveMesh.localScale = new Vector3(0.263f, 0.263f, -0.263f);
            var impactDust = DustBurst("Grip Impact Dust", null, Vector3.zero, new Color(0.62f, 0.58f, 0.54f), 26, 0.12f, 3.5f);
            Set(ball, ("view", cam), ("ball", ballSlot), ("hand", gloveSlot), ("impactDust", impactDust));
            if (zeroG != null) Set(ball, ("player", zeroG));
            return ball;
        }

        /// <summary>Marks an object as a puzzle surface for the grip ball (see BallSurface).</summary>
        static BallSurface Surface(GameObject go, BallSurface.Kind kind)
        {
            var surface = go.AddComponent<BallSurface>();
            Set(surface, ("kind", (int)kind));
            return surface;
        }

        static void Wall(Transform parent, Vector3 centre, Vector3 size)
        {
            var go = new GameObject("Range Barrier") { layer = 2 };        // stops the player, lets the ball through
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>Drops a model from Assets/_Project/Models into the scene and gives it the project's materials.</summary>
        static GameObject Model(string file, Transform parent, Vector3 localPosition, Quaternion localRotation, float scale,
            Dictionary<string, Material> overrides = null)
        {
            var asset = ModelPrefab(file);
            if (asset == null) return new GameObject(file);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = Vector3.one * scale;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string key = materials[i] != null ? materials[i].name : "";
                    if (overrides != null && overrides.TryGetValue(key, out var replacement)) materials[i] = replacement;
                }
                if (overrides != null) renderer.sharedMaterials = materials;
            }
            return go;
        }

        static Transform Child(GameObject model, string name)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            Debug.LogError($"{model.name} has no part named '{name}'.");
            return model.transform;
        }

        static void AddMeshColliders(GameObject model)
        {
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }

        static Transform Pose(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.SetPositionAndRotation(position, rotation);
            return marker;
        }

        /// <summary>
        /// Places one of the characters from Tools/blender/build_people.py. Its prefab (Prefabs/People) carries the
        /// PersonAnimator that makes it breathe, look around and talk.
        /// </summary>
        static Transform Person(string name, Transform parent, Vector3 worldPosition, float yaw, bool seated, string character,
            string speaker = null, Transform lookAt = null, bool lookAtPlayer = false, Color? tint = null, bool astronaut = false, bool typing = true)
        {
            var slot = Slot(name, parent, worldPosition, new Vector3(0, yaw, 0));
            PersonInto(slot, character, seated, speaker, lookAt, lookAtPlayer, tint, astronaut, typing);
            return slot;
        }

        static void PersonInto(Transform slot, string character, bool seated, string speaker = null, Transform lookAt = null,
            bool lookAtPlayer = false, Color? tint = null, bool astronaut = false, bool typing = true)
        {
            var prefab = PersonPrefab(character);
            if (prefab == null) return;
            var person = (GameObject)PrefabUtility.InstantiatePrefab(prefab, slot);
            person.transform.localPosition = Vector3.zero;
            person.transform.localRotation = Quaternion.identity;
            var animator = person.GetComponent<PersonAnimator>();
            Set(animator, ("pose", seated ? 1 : 0), ("speaker", speaker ?? ""), ("lookAtPlayer", lookAtPlayer), ("typing", typing),
                ("clothesTint", tint ?? Color.white));
            if (lookAt != null) Set(animator, ("lookAt", lookAt));
            if (astronaut) AddSpaceGear(person.transform);
        }

        [System.Serializable] class PersonPart { public string name, kind, texture, normal; public float[] color; }
        [System.Serializable] class PersonManifest { public PersonPart[] parts; }

        /// <summary>The prefab for a character: the rigged model with its materials and a PersonAnimator. Made once.</summary>
        static GameObject PersonPrefab(string character, bool animated = true)
        {
            EnsureFolder($"{PrefabsDir}/People");
            EnsureFolder($"{MaterialsDir}/People");
            string path = $"{PrefabsDir}/People/{character}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;

            string folder = $"{ModelsDir}/People";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{character}.fbx");
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>($"{folder}/{character}.json");
            if (fbx == null || json == null)
            {
                Debug.LogError($"Character '{character}' is missing. Run Tools/blender/build_people.py.");
                return null;
            }
            var manifest = JsonUtility.FromJson<PersonManifest>(json.text);

            var root = new GameObject(character);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.name = "Model";
            foreach (var part in manifest.parts)
            {
                var renderer = model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == part.name || r.name.StartsWith(part.name + "."));
                if (renderer == null) continue;
                renderer.sharedMaterial = PersonMaterial(character, part, folder);
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;     // posed far from the rest pose
            }
            if (animated) root.AddComponent<PersonAnimator>();
            prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Material PersonMaterial(string character, PersonPart part, string folder)
        {
            var mat = LoadOrCreate($"{MaterialsDir}/People/{character}_{part.name}.mat", () => new Material(Shader.Find("Universal Render Pipeline/Lit")));
            if (!Fresh(mat)) return mat;

            var texture = string.IsNullOrEmpty(part.texture) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Textures/{part.texture}");
            var normal = string.IsNullOrEmpty(part.normal) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/Textures/{part.normal}");
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", texture != null || part.color == null ? Color.white : new Color(part.color[0], part.color[1], part.color[2]));
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Smoothness", part.kind switch { "skin" => 0.38f, "eyes" => 0.9f, "teeth" => 0.6f, "hair" => 0.3f, _ => 0.18f });
            // Hair, lashes, brows and cloth edges are cut out of flat cards by their texture's transparency.
            if (part.kind != "skin" && part.kind != "teeth" && part.kind != "tongue")
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", part.kind == "eyes" ? 0.05f : 0.35f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0f);
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Puts the helmet and life-support pack on a character, attached to the head and chest bones.</summary>
        static void AddSpaceGear(Transform person)
        {
            var source = ModelPrefab("Person_Standing");
            Transform Bone(string boneName) => person.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith(":" + boneName) || t.name == boneName);
            void Attach(string partName, Transform bone, Vector3 offset, float scale)
            {
                var part = Child(source, partName);
                if (bone == null || part == null) return;
                var sourceRenderer = part.GetComponent<MeshRenderer>();
                var go = new GameObject(partName);
                go.AddComponent<MeshFilter>().sharedMesh = part.GetComponent<MeshFilter>().sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
                go.transform.SetParent(bone, false);
                go.transform.rotation = person.rotation * part.rotation;
                float parentScale = bone.lossyScale.x;
                go.transform.localScale = part.lossyScale * (scale / Mathf.Max(parentScale, 1e-5f));
                // Line the middle of the part up with the bone (plus an offset in the person's own space).
                go.transform.position = bone.position + person.rotation * (offset + (part.position - sourceRenderer.bounds.center) * scale);
            }
            Attach("Helmet", Bone("Head"), new Vector3(0, 0.2f, 0.03f), 1.12f);
            Attach("Backpack", Bone("Spine2"), new Vector3(0, 0.0f, -0.08f), 1.05f);
        }

        /// <summary>Makes a standing person something the player can talk to.</summary>
        static Interactable Talkable(Transform person, string prompt)
        {
            var capsule = person.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 0.9f, 0);
            capsule.radius = 0.35f;
            capsule.height = 1.8f;
            var interactable = person.gameObject.AddComponent<Interactable>();
            Set(interactable, ("prompt", prompt));
            return interactable;
        }

        static void DataScreen(Transform parent, Vector3 position, float yaw, Vector2 size, Mats m, string text)
        {
            var rotation = Quaternion.Euler(0, yaw, 0);
            var screen = Prim(PrimitiveType.Quad, "Data Screen", parent, position, new Vector3(size.x, size.y, 1), ModelMats["DataScreen"], rotation, collider: false);
            screen.name = "Data Screen";
            var label = WorldText("Readout", parent, position - rotation * Vector3.forward * 0.03f, size * 100f, 0.01f, 54, new Color(0.75f, 0.93f, 1f));
            label.transform.parent.rotation = rotation;
            label.text = text;
        }

        /// <summary>A place to head for: shown on the compass line at the top of the screen while it is active.</summary>
        static GameObject Beacon(Vector3 basePosition, Mats m)
        {
            var marker = new GameObject("Objective Marker");
            marker.transform.position = basePosition + new Vector3(0, 1.2f, 0);
            marker.AddComponent<CompassMarker>();
            return marker;
        }

        /// <summary>Four posts and a roof.</summary>
        static void Shelter(Transform parent, Vector3 size, Material roof, Material frame)
        {
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Post", parent, new Vector3(x * size.x / 2, size.y / 2, z * size.z / 2), new Vector3(0.12f, size.y, 0.12f), frame);
            Prim(PrimitiveType.Cube, "Roof", parent, new Vector3(0, size.y + 0.05f, 0), new Vector3(size.x + 0.6f, 0.1f, size.z + 0.6f), roof, collider: false);
            Prim(PrimitiveType.Cube, "Back Wall", parent, new Vector3(0, size.y / 2, size.z / 2), new Vector3(size.x, size.y, 0.06f), roof);
        }

        static GameObject Asteroid(Transform parent, System.Random random, Vector3 position, float scale, bool collider, bool spin = true)
        {
            var rock = Model($"Asteroid_{random.Next(1, 6)}", parent, Vector3.zero, Quaternion.identity, scale);
            rock.transform.SetPositionAndRotation(position, Quaternion.Euler(Range(random, 0, 360), Range(random, 0, 360), Range(random, 0, 360)));
            rock.name = "MODEL_Asteroid";
            if (collider) AddMeshColliders(rock);
            if (spin)
                Set(rock.AddComponent<Spinner>(), ("degreesPerSecond", new Vector3(Range(random, -4, 4), Range(random, -4, 4), Range(random, -4, 4))));
            return rock;
        }

        /// <summary>The portal model with its vortex and light, under the given MODEL_ slot.</summary>
        static PortalGate Portal(Transform slot, Mats m, float glowIntensity)
        {
            var model = Model("Portal", slot, Vector3.zero, Quaternion.identity, 1f);
            var vortex = Prim(PrimitiveType.Quad, "Vortex", slot, Vector3.zero, Vector3.one * 20.4f, ModelMats["Portal_Vortex"], collider: false);
            var light = PointLight("Portal Glow", slot, slot.position - slot.forward * 6f * slot.lossyScale.x, new Color(0.4f, 0.75f, 1f), 0f, 45f * slot.lossyScale.x);
            var inflow = Motes(slot, Vector3.zero, new Vector3(34, 34, 6), 600, new Color(0.5f, 0.85f, 1f, 0.9f), 0.5f, 0.6f, true, new Vector3(0, 0, 3f));
            inflow.transform.localScale = Vector3.one;
            var gate = slot.gameObject.AddComponent<PortalGate>();
            Set(gate, ("ringMain", Child(model, "RingMain")), ("ringOuter", Child(model, "RingOuter")), ("ringInner", Child(model, "RingInner")),
                ("vortex", vortex.GetComponent<Renderer>()), ("glow", light), ("glowIntensity", glowIntensity));
            return gate;
        }

        static void Nebula(Transform parent, Vector3 position, float size, string materialName, Color tint)
        {
            var mat = ParticleMaterial(materialName, AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/Nebula.png"), tint * 0.55f, additive: true);
            var quad = Prim(PrimitiveType.Quad, "Nebula", parent, Vector3.zero, Vector3.one * size, mat, collider: false);
            quad.transform.position = position;
            quad.transform.rotation = Quaternion.LookRotation(position - parent.position);
        }

        static ParticleSystem Plume(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, float size, float speed, float life,
            float rate, bool worldSpace, Color start, Color end)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = start;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.maxParticles = 3000;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = worldSpace ? 5f : 3f;
            shape.radius = size * 0.22f;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 0.45f), new GradientColorKey(end * 0.6f, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, worldSpace ? 2.6f : 0.35f));
            var plumeRenderer = go.GetComponent<ParticleSystemRenderer>();
            plumeRenderer.sharedMaterial = ModelMats["Fire"];
            if (!worldSpace)
            {
                // Engines in space: long streaks instead of round puffs.
                plumeRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                plumeRenderer.lengthScale = 3.2f;
                plumeRenderer.velocityScale = 0.03f;
            }
            return ps;
        }

        static ParticleSystem Smoke(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(-90, 0, 0));
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(7f, 16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.82f, 0.78f, 0.72f, 0.75f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            var emission = ps.emission;
            emission.rateOverTime = 45f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 80f;
            shape.radius = 4f;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.8f, 0.55f), 0f), new GradientColorKey(Color.white, 0.25f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.15f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.5f));
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ModelMats["Smoke"];
            return ps;
        }

        /// <summary>A short puff of grit thrown off a surface (the grip ball biting in, a foot pushing off).</summary>
        static ParticleSystem DustBurst(string name, Transform parent, Vector3 position, Color color, int count, float size, float speed)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.2f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.4f, size * 1.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.05f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.08f;
            drag.limit = speed * 0.3f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ModelMats["Smoke"];
            return ps;
        }

        /// <summary>Tiny specks drifting in a box of air: dust in a beam of light, pollen over a meadow, grit blowing past.</summary>
        static ParticleSystem Motes(Transform parent, Vector3 centre, Vector3 size, int count, Color color, float moteSize, float speed, bool glow, Vector3 wind = default)
        {
            var go = new GameObject("Motes");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 16f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(moteSize * 0.5f, moteSize * 1.5f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            var emission = ps.emission;
            emission.rateOverTime = count / 12f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(wind.x - speed, wind.x + speed);
            velocity.y = new ParticleSystem.MinMaxCurve(wind.y - speed, wind.y + speed);
            velocity.z = new ParticleSystem.MinMaxCurve(wind.z - speed, wind.z + speed);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ModelMats[glow ? "Fire" : "Smoke"];
            return ps;
        }

        static ParticleSystem Burst(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 30f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 2.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.4f), new Color(1f, 0.35f, 0.1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 160) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2f;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = ModelMats["Fire"];
            return ps;
        }

        // ---------- Materials for the models ----------

        static Dictionary<string, Material> CreateModelMaterials()
        {
            Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/{name}.png");
            var panels = Tex("HullPanels_n");
            var gasGiant = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/Planet_X.mat");
            if (Fresh(gasGiant) && Generated("PlanetX_Bands") != null)
            {
                gasGiant.SetColor("_BaseColor", Color.white);
                gasGiant.SetFloat("_ReceiveShadows", 0f);
                gasGiant.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                Textured(gasGiant, Generated("PlanetX_Bands"), null, Vector2.one, 1f);
                // A little self-glow so the cloud bands still read on the night side, as they do in long-exposure photos.
                gasGiant.EnableKeyword("_EMISSION");
                gasGiant.SetTexture("_EmissionMap", Generated("PlanetX_Bands"));
                gasGiant.SetColor("_EmissionColor", Color.white * 0.12f);
                freshAssets.Remove(gasGiant);      // keep the generic planet setup below from replacing it
            }
            // The ocean world gets the same treatment as Planet X: a detailed surface picture, no shadow banding,
            // and a faint glow so the night side still shows its clouds. Applied once, when the picture changes.
            var oceanWorld = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/Planet_NotEarth.mat");
            var oceanMap = Generated("NotEarth_Surface");
            if (oceanWorld != null && oceanMap != null && oceanWorld.GetTexture("_BaseMap") != oceanMap)
            {
                oceanWorld.SetColor("_BaseColor", Color.white);
                oceanWorld.SetTexture("_BaseMap", oceanMap);
                oceanWorld.SetFloat("_Smoothness", 0.45f);
                oceanWorld.SetFloat("_ReceiveShadows", 0f);
                oceanWorld.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                oceanWorld.EnableKeyword("_EMISSION");
                oceanWorld.SetTexture("_EmissionMap", oceanMap);
                oceanWorld.SetColor("_EmissionColor", Color.white * 0.1f);
                EditorUtility.SetDirty(oceanWorld);
                freshAssets.Remove(oceanWorld);
            }
            foreach (var (planetMat, texture) in new[] { ("Planet_X", "PlanetX"), ("Planet_NotEarth", "NotEarth") })
            {
                var planetMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{planetMat}.mat");
                if (!Fresh(planetMaterial)) continue;
                planetMaterial.SetColor("_BaseColor", Color.white);
                planetMaterial.SetFloat("_ReceiveShadows", 0f);            // far too big for the shadow map: it leaves a hard line
                planetMaterial.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                Textured(planetMaterial, Tex(texture), null, Vector2.one, 1f);
            }
            var mats = new Dictionary<string, Material>
            {
                ["Rocket_White"] = Textured(Mat("Rocket_White", new Color(0.9f, 0.9f, 0.92f), 0.6f, metallic: 0.15f), null, panels, new Vector2(1, 1), 0.8f),
                ["Rocket_Dark"] = Textured(Mat("Rocket_Dark", new Color(0.05f, 0.05f, 0.06f), 0.45f, metallic: 0.2f), null, panels, new Vector2(1, 1), 0.6f),
                ["Rocket_Accent"] = Mat("Rocket_Accent", new Color(0.92f, 0.33f, 0.06f), 0.5f),
                ["Rocket_Metal"] = Mat("Rocket_Metal", new Color(0.62f, 0.64f, 0.68f), 0.7f, metallic: 0.9f),
                ["Rocket_Glass"] = Mat("Rocket_Glass", new Color(0.05f, 0.1f, 0.15f), 0.95f, emission: new Color(0.35f, 0.75f, 1f) * 1.6f),
                ["Rocket_Nozzle"] = Mat("Rocket_Nozzle", new Color(0.16f, 0.15f, 0.15f), 0.55f, metallic: 0.85f),
                ["Rocket_EngineGlow"] = Mat("Rocket_EngineGlow", new Color(1f, 0.5f, 0.2f), emission: new Color(1f, 0.5f, 0.15f) * 5f),
                ["Rocket_Solar"] = Mat("Rocket_Solar", new Color(0.03f, 0.07f, 0.3f), 0.9f, metallic: 0.6f),
                ["Portal_Metal"] = Mat("Portal_Metal", new Color(0.1f, 0.11f, 0.16f), 0.65f, metallic: 0.85f),
                ["Portal_Trim"] = Mat("Portal_Trim", new Color(0.32f, 0.35f, 0.45f), 0.7f, metallic: 0.9f),
                ["Portal_Glow"] = Mat("Portal_Glow", new Color(0.1f, 0.5f, 1f), emission: new Color(0.1f, 0.55f, 1f) * 3.2f),
                ["GripBall_Metal"] = Mat("GripBall_Metal", new Color(0.78f, 0.74f, 0.62f), 0.78f, metallic: 1f),
                ["Asteroid_Rock"] = Textured(Mat("Asteroid_Rock", Color.white, 0.12f), Tex("Rock"), Tex("Rock_n"), new Vector2(1, 1), 1.3f),
                ["Person_Skin"] = Mat("Person_Skin", new Color(0.78f, 0.58f, 0.46f), 0.3f),
                ["Person_Cloth"] = Mat("Person_Cloth", new Color(0.35f, 0.45f, 0.6f)),
                ["Person_Pants"] = Mat("Person_Pants", new Color(0.12f, 0.13f, 0.16f)),
                ["Person_Eye"] = Mat("Person_Eye", new Color(0.02f, 0.02f, 0.03f), 0.8f),
                ["Person_Shoes"] = Mat("Person_Shoes", new Color(0.06f, 0.06f, 0.07f), 0.4f),
                ["Person_Hair"] = Mat("Person_Hair", new Color(0.12f, 0.08f, 0.06f), 0.4f),
                ["Person_Visor"] = Glass(Mat("Person_Visor", new Color(0.55f, 0.75f, 0.95f, 0.16f), 0.97f, metallic: 0.1f)),
                ["Person_SuitTrim"] = Mat("Person_SuitTrim", new Color(0.82f, 0.83f, 0.86f), 0.4f),
                ["Person_Suit"] = Mat("Person_SpaceSuit", new Color(0.9f, 0.9f, 0.88f), 0.35f),
                ["Person_Orange"] = Mat("Person_Orange", new Color(0.85f, 0.42f, 0.1f)),
                ["Person_Green"] = Mat("Person_Green", new Color(0.3f, 0.45f, 0.28f)),
                ["Person_Grey"] = Mat("Person_Grey", new Color(0.45f, 0.43f, 0.4f)),
                ["Person_Teal"] = Mat("Person_Teal", new Color(0.15f, 0.45f, 0.5f)),
                ["Tower_Steel"] = Mat("Tower_Steel", new Color(0.42f, 0.2f, 0.14f), 0.3f, metallic: 0.5f),
                ["Tower_Light"] = Mat("Tower_Light", new Color(1f, 0.95f, 0.8f), emission: new Color(1f, 0.9f, 0.65f) * 4f),
                ["Tower_Concrete"] = Mat("Tower_Concrete", new Color(0.5f, 0.48f, 0.45f), 0.1f),
                ["Tower_Scorch"] = Mat("Tower_Scorch", new Color(0.1f, 0.09f, 0.08f), 0.05f),
                ["Tree_Dead"] = Mat("Tree_Dead", new Color(0.28f, 0.22f, 0.17f), 0.05f),
                ["Crate_Body"] = Mat("Crate_Body", new Color(0.8f, 0.8f, 0.78f), 0.4f, metallic: 0.3f),
                ["Crate_Trim"] = Mat("Crate_Trim", new Color(0.16f, 0.17f, 0.2f), 0.5f, metallic: 0.6f),
                ["Crate_Light"] = Mat("Crate_Light", new Color(0.3f, 0.8f, 1f), emission: new Color(0.3f, 0.85f, 1f) * 3f),
                ["Title_Face"] = Mat("Title_Face", new Color(0.95f, 0.97f, 1f), 0.5f, emission: new Color(0.8f, 0.9f, 1f) * 1.25f),
                ["Title_Side"] = Mat("Title_Side", new Color(0.1f, 0.3f, 0.65f), 0.7f, metallic: 0.5f, emission: new Color(0.1f, 0.35f, 0.9f) * 0.8f),
                ["Ground_Cracked"] = Textured(Mat("Ground_Cracked", Color.white, 0.05f), Tex("CrackedGround"), Tex("CrackedGround_n"), new Vector2(110, 110), 1.2f),
                ["Ground_Mud"] = Textured(Mat("Ground_Mud", new Color(0.5f, 0.42f, 0.36f), 0.2f), Tex("CrackedGround"), Tex("CrackedGround_n"), new Vector2(14, 11), 1.5f),
                ["Water"] = Mat("Water_Last", new Color(0.16f, 0.2f, 0.2f), 0.96f, metallic: 0.3f),
                ["Canvas"] = Mat("Camp_Canvas", new Color(0.6f, 0.55f, 0.45f), 0.05f),
                ["Glasshouse"] = Mat("Camp_Glasshouse", new Color(0.7f, 0.78f, 0.75f), 0.8f),
                ["DataScreen"] = Mat("MC_DataScreen", new Color(0.02f, 0.05f, 0.09f), 0.8f, emission: new Color(0.03f, 0.12f, 0.24f)),
                ["Room_Wall"] = Textured(Mat("Room_Wall", new Color(0.75f, 0.78f, 0.85f), 0.45f, metallic: 0.35f), Generated("WallPanels"), null, new Vector2(6, 2), 1f),
                ["Room_Floor"] = Textured(Mat("Room_Floor", new Color(0.55f, 0.56f, 0.6f), 0.78f, metallic: 0.05f), Generated("FloorTiles"), null, new Vector2(8, 6), 1f),
                ["Ship_Panels"] = Textured(Mat("Ship_Panels", new Color(0.85f, 0.87f, 0.92f), 0.5f, metallic: 0.45f), Generated("WallPanels"), null, new Vector2(2, 1), 1f),
                ["Ship_Instruments"] = ScreenMaterial("Ship_Instruments", Generated("InstrumentPanel"), 0.95f),
                ["Granite"] = Textured(Mat("Granite", Color.white, 0.15f), Generated("Granite"), Tex("Rock_n"), new Vector2(1, 1), 1.2f),
                ["Tree_Bark"] = Textured(Mat("Tree_Bark", new Color(0.85f, 0.8f, 0.75f), 0.08f), Generated("Bark"), null, new Vector2(1, 1), 1f),
                ["Tree_Leaves"] = Cutout(Textured(Mat("Tree_Leaves", new Color(0.82f, 0.95f, 0.75f), 0.12f), Generated("Leaves"), null, new Vector2(1, 1), 1f)),
                ["Desk_Top"] = Mat("Desk_Top", new Color(0.09f, 0.1f, 0.12f), 0.7f, metallic: 0.2f),
                ["Desk_Frame"] = Mat("Desk_Frame", new Color(0.26f, 0.28f, 0.32f), 0.6f, metallic: 0.8f),
                ["Desk_Keys"] = Mat("Desk_Keys", new Color(0.03f, 0.03f, 0.035f), 0.5f),
                ["Desk_Light"] = Mat("Desk_Light", new Color(0.3f, 0.8f, 1f), emission: new Color(0.3f, 0.8f, 1f) * 2.5f),
                ["Monitor_Bezel"] = Mat("Monitor_Bezel", new Color(0.025f, 0.025f, 0.03f), 0.6f, metallic: 0.3f),
                ["Monitor_Screen"] = ScreenMaterial("Monitor_Screen", Generated("Screen_Telemetry")),
                ["Chair_Fabric"] = Mat("Chair_Fabric", new Color(0.07f, 0.08f, 0.1f), 0.2f),
                ["Chair_Frame"] = Mat("Chair_Frame", new Color(0.2f, 0.21f, 0.23f), 0.65f, metallic: 0.8f),
                ["Glove_Suit"] = Mat("Glove_Suit", new Color(0.86f, 0.86f, 0.84f), 0.3f),
                ["Glove_Grip"] = Mat("Glove_Grip", new Color(0.1f, 0.1f, 0.12f), 0.35f),
                ["Tether_Cable"] = Mat("Tether_Cable", new Color(0.55f, 0.57f, 0.6f), 0.7f, metallic: 0.9f),
                ["Container_Rust"] = Textured(Mat("Container_Rust", new Color(0.85f, 0.8f, 0.78f), 0.2f, metallic: 0.3f), Generated("Container"), null, new Vector2(3, 1), 1f),
                ["Skyline"] = Cutout(Textured(Mat("Skyline_Ruins", new Color(0.8f, 0.72f, 0.62f), 0f), Generated("Skyline"), null, new Vector2(1, 1), 1f)),
                ["Frost_Glaze"] = Mat("Frost_Glaze", new Color(0.35f, 0.75f, 1f), 0.96f, metallic: 0.25f, emission: new Color(0.05f, 0.4f, 0.9f) * 0.9f),
                ["Range_Target"] = Mat("Range_Target", new Color(0.95f, 0.16f, 0.05f), emission: new Color(1f, 0.12f, 0.02f) * 0.45f),
                ["Ship_Padding"] = Mat("Ship_Padding", new Color(0.09f, 0.095f, 0.11f), 0.25f),
                ["Strip_Warm"] = Mat("Strip_Warm", new Color(1f, 0.6f, 0.25f), emission: new Color(1f, 0.55f, 0.2f) * 4f),
                ["Lamp_Red"] = Mat("Lamp_Red", new Color(1f, 0.15f, 0.1f), emission: new Color(1f, 0.12f, 0.06f) * 5f),
                ["Hatch_Lamp"] = Mat("Hatch_Lamp", new Color(0.3f, 1f, 0.45f), emission: new Color(0.3f, 1f, 0.45f) * 5f),
                ["Portal_Vortex"] = VortexMaterial(),
                ["Fire"] = ParticleMaterial("Fire", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"), Color.white * 1.6f, additive: true),
                ["Smoke"] = ParticleMaterial("Smoke", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"), Color.white, additive: false),
            };
            return mats;
        }

        /// <summary>Turns a material into see-through glass (its colour's alpha is how solid it looks).</summary>
        static Material Glass(Material mat)
        {
            if (!Fresh(mat)) return mat;
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Cuts a material out by its texture's transparency and shows both sides (leaves).</summary>
        static Material Cutout(Material mat)
        {
            if (!Fresh(mat)) return mat;
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.4f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", 0f);
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Dresses a plain box of a spacecraft cabin: structural ribs, instrument panels down both walls and overhead,
        /// and strip lights. size is the room's inside (width, height, length); it is centred on x and starts at the floor.
        /// </summary>
        static void ShipInterior(Transform parent, Vector3 size, Vector3 centre, float frontZ)
        {
            var rib = ModelMats["Rocket_Metal"];
            var instruments = ModelMats["Ship_Instruments"];
            float halfWidth = size.x / 2f - 0.06f;
            for (float z = centre.z - size.z / 2f + 0.6f; z < centre.z + size.z / 2f; z += 1.1f)
            {
                Prim(PrimitiveType.Cube, "Rib Top", parent, new Vector3(centre.x, size.y - 0.08f, z), new Vector3(size.x, 0.12f, 0.1f), rib, collider: false);
                foreach (float side in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "Rib Side", parent, new Vector3(centre.x + side * halfWidth, size.y / 2f, z), new Vector3(0.1f, size.y, 0.1f), rib, collider: false);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Quad, "Wall Instruments", parent, new Vector3(centre.x + side * (halfWidth - 0.02f), 1.25f, centre.z + 0.4f),
                    new Vector3(2.6f, 0.9f, 1), instruments, Quaternion.Euler(0, side * 90f, 0), collider: false);
                Prim(PrimitiveType.Cube, "Strip Light", parent, new Vector3(centre.x + side * (halfWidth - 0.05f), size.y - 0.25f, centre.z), new Vector3(0.04f, 0.04f, size.z - 0.6f),
                    ModelMats["Tower_Light"], collider: false);
            }
            // Pipes and a cable run under the ceiling, stowage bags, and an emergency lamp
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cylinder, "Pipe", parent, new Vector3(centre.x + side * (halfWidth - 0.14f), size.y - 0.42f, centre.z), new Vector3(0.08f, size.z / 2f - 0.2f, 0.08f),
                    ModelMats["Rocket_Metal"], Quaternion.Euler(90, 0, 0), collider: false);
                Prim(PrimitiveType.Cylinder, "Cable", parent, new Vector3(centre.x + side * (halfWidth - 0.1f), size.y - 0.56f, centre.z), new Vector3(0.03f, size.z / 2f - 0.2f, 0.03f),
                    ModelMats["Rocket_Accent"], Quaternion.Euler(90, 0, 0), collider: false);
            }
            for (int k = 0; k < 2; k++)
            {
                var bag = new Vector3(centre.x - halfWidth + 0.15f, 1.55f - k * 0.72f, centre.z - size.z / 2f + 1.0f);
                Prim(PrimitiveType.Cube, "Stowage Bag", parent, bag, new Vector3(0.2f, 0.56f, 0.95f), ModelMats["Chair_Fabric"], collider: false);
                Prim(PrimitiveType.Cube, "Bag Strap", parent, bag, new Vector3(0.22f, 0.58f, 0.07f), ModelMats["Rocket_Accent"], collider: false);
            }
            var lampAt = new Vector3(centre.x + halfWidth - 0.22f, size.y - 0.55f, centre.z - size.z / 2f + 0.35f);
            Prim(PrimitiveType.Sphere, "Emergency Lamp", parent, lampAt, Vector3.one * 0.17f, ModelMats["Lamp_Red"], collider: false);
            Prim(PrimitiveType.Quad, "Overhead Instruments", parent, new Vector3(centre.x, size.y - 0.3f, frontZ - 0.08f), new Vector3(size.x - 0.8f, 0.42f, 1), instruments,
                Quaternion.Euler(-25f, 0, 0), collider: false);
        }

        static Material Textured(Material mat, Texture2D albedo, Texture2D normal, Vector2 tiling, float normalStrength)
        {
            if (!Fresh(mat)) return mat;
            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTextureScale("_BaseMap", tiling);
            }
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.SetFloat("_BumpScale", normalStrength);
                mat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material VortexMaterial()
        {
            string path = $"{MaterialsDir}/Portal_Vortex.mat";
            var shader = Shader.Find("TSA/PortalVortex");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (!OverwriteLook) return mat;
            else mat.shader = shader;
            mat.SetFloat("_Intensity", 0.9f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material ParticleMaterial(string name, Texture2D texture, Color color, bool additive)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (!OverwriteLook) return mat;
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material DustSky(Vector3 directionToSun)
        {
            string path = $"{MaterialsDir}/Sky_Dust.mat";
            var shader = Shader.Find("TSA/GradientSky");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (!OverwriteLook) return mat;
            else mat.shader = shader;
            mat.SetVector("_SunDir", directionToSun);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Post-processing for the memory: warm, faded and soft at the edges.</summary>
        static VolumeProfile MemoryPostProfile()
        {
            string path = $"{SettingsDir}/MemoryPostProcess.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1.4f);
            bloom.threshold.Override(0.95f);
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.5f);
            var colour = profile.Add<ColorAdjustments>(true);
            colour.saturation.Override(-28f);
            colour.contrast.Override(8f);
            colour.colorFilter.Override(new Color(1f, 0.93f, 0.82f));
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
