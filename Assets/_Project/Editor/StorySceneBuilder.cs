using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TSAGame.EditorTools
{
    /// <summary>Scenes 04-07 (Acts II to V), built from the same placeholder kit as the intro scenes.</summary>
    public static partial class IntroSceneBuilder
    {
        // =====================================================================
        // 04 Fuel moon: drill, coolant valves, refinery, the fuel split
        // =====================================================================

        static void BuildFuelMoon(Mats m, VolumeProfile post)
        {
            var scene = NewScene(post, ambient: new Color(0.07f, 0.07f, 0.09f), skybox: false);
            Sun(Quaternion.Euler(20f, 30f, 0f), 1.3f);

            var terrain = new GameObject("Terrain").transform;
            Prim(PrimitiveType.Plane, "Ground", terrain, Vector3.zero, new Vector3(30, 1, 30), m.Moon);
            var random = new System.Random(11);
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3(((float)random.NextDouble() - 0.5f) * 160f, 0, ((float)random.NextDouble() - 0.5f) * 160f);
                if (p.magnitude < 12f || (Mathf.Abs(p.x) < 22f && p.z > 10f && p.z < 40f)) continue;
                float size = 3f + (float)random.NextDouble() * 8f;
                Prim(PrimitiveType.Sphere, "Rock", terrain, p + Vector3.down * size * 0.25f, new Vector3(size, size * 0.6f, size), m.Moon);
            }

            var sky = new GameObject("Sky").transform;
            Starfield(sky, m.Stars, 2500f);
            var planet = Slot("NotEarth", sky, new Vector3(300, 500, 900), new Vector3(0, 0, 20));
            Prim(PrimitiveType.Sphere, "Placeholder", planet, Vector3.zero, Vector3.one * 500f, m.NotEarth, collider: false);

            // The colony pilot's ship, with an oxygen refill point
            var shipSlot = Slot("PilotShip", null, new Vector3(-8, 0, -6), Vector3.zero);
            Prim(PrimitiveType.Capsule, "Placeholder", shipSlot, new Vector3(0, 6, 0), new Vector3(2.6f, 6f, 2.6f), m.OldRocket);
            PointLight("Ship Light", shipSlot, new Vector3(-6, 3, -3), new Color(1f, 0.9f, 0.7f), 5f, 14f);
            var ship = shipSlot.gameObject.AddComponent<Interactable>();
            Set(ship, ("prompt", "Fly to the gate"));

            var oxygenSlot = Slot("OxygenStation", null, new Vector3(-4, 0, -3), Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Placeholder", oxygenSlot, new Vector3(0, 0.7f, 0), new Vector3(0.6f, 0.7f, 0.6f), m.Sensor);
            var oxygenStation = oxygenSlot.gameObject.AddComponent<Interactable>();
            Set(oxygenStation, ("prompt", "Refill oxygen"), ("singleUse", false));

            // Drill
            var drillSlot = Slot("IceDrill", null, new Vector3(14, 0, 18), Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Placeholder", drillSlot, new Vector3(0, 3, 0), new Vector3(1.6f, 3f, 1.6f), m.Wreck);
            Prim(PrimitiveType.Cube, "Drill Base", drillSlot, new Vector3(0, 0.4f, 0), new Vector3(4f, 0.8f, 4f), m.Console);
            PointLight("Drill Light", drillSlot, new Vector3(14, 3, 15), new Color(1f, 0.8f, 0.5f), 5f, 12f);
            var drill = drillSlot.gameObject.AddComponent<Interactable>();
            Set(drill, ("prompt", "Start the ice drill"), ("holdDuration", 2f));

            // Coolant tanks, placed out of order so the player has to read the colours
            var tanks = new GameObject("Coolant Tanks").transform;
            Interactable Tank(string colour, float x, Material material)
            {
                var slot = Slot($"Tank{colour}", tanks, new Vector3(x, 0, 32), Vector3.zero);
                Prim(PrimitiveType.Cylinder, "Placeholder", slot, new Vector3(0, 2, 0), new Vector3(3f, 2f, 3f), material);
                Prim(PrimitiveType.Cube, "Valve", slot, new Vector3(0, 1.2f, -1.7f), new Vector3(0.6f, 0.6f, 0.4f), m.Console);
                var valve = slot.gameObject.AddComponent<Interactable>();
                Set(valve, ("prompt", $"Open the {colour.ToUpper()} valve"), ("singleUse", false));
                return valve;
            }
            var red = Tank("Red", -7f, m.TankRed);
            var blue = Tank("Blue", 0f, m.TankBlue);
            var white = Tank("White", 7f, m.TankWhite);
            PointLight("Tank Light", tanks, new Vector3(0, 5, 27), new Color(0.85f, 0.9f, 1f), 7f, 18f);

            // Refinery
            var refinerySlot = Slot("Refinery", null, new Vector3(-14, 0, 22), Vector3.zero);
            Prim(PrimitiveType.Cube, "Placeholder", refinerySlot, new Vector3(0, 1.5f, 0), new Vector3(3.5f, 3f, 3.5f), m.Console);
            Prim(PrimitiveType.Cylinder, "Chimney", refinerySlot, new Vector3(1f, 4f, 1f), new Vector3(0.6f, 1.2f, 0.6f), m.Wreck, collider: false);
            PointLight("Refinery Light", refinerySlot, new Vector3(-11, 3, 20), new Color(1f, 0.6f, 0.3f), 5f, 12f);
            var refinery = refinerySlot.gameObject.AddComponent<Interactable>();
            Set(refinery, ("prompt", "Start the refinery"), ("holdDuration", 2f));

            // Old Architect mining machines
            var wrecks = Slot("ArchitectMachines", null, new Vector3(30, 0, 40), new Vector3(0, 30, 0));
            var wreckPlaceholder = new GameObject("Placeholder").transform;
            wreckPlaceholder.SetParent(wrecks, false);
            Prim(PrimitiveType.Cube, "Machine A", wreckPlaceholder, new Vector3(0, 3, 0), new Vector3(8, 6, 5), m.Ruin, Quaternion.Euler(0, 0, 8));
            Prim(PrimitiveType.Cube, "Machine B", wreckPlaceholder, new Vector3(-12, 2, 6), new Vector3(5, 4, 5), m.Ruin, Quaternion.Euler(10, 20, 0));

            var (player, _) = Player(new Vector3(0, 0, 0), 1.65f, seatedYawLimit: 0f);
            var oxygen = player.AddComponent<OxygenSystem>();
            Pilot(m, new Vector3(2.5f, 0, -2f), player.transform);

            var sequence = new GameObject("Fuel Moon Sequence").AddComponent<FuelMoonSequence>();
            Set(sequence, ("player", player.GetComponent<FirstPersonController>()), ("oxygen", oxygen), ("oxygenStation", oxygenStation),
                ("drill", drill), ("valves", new Object[] { blue, white, red }), ("refinery", refinery), ("ship", ship));

            AddHud();
            Save(scene, SceneFlow.FuelMoon);
        }

        // =====================================================================
        // 05 Gate world: learning how gate addresses work
        // =====================================================================

        static void BuildGateWorld(Mats m, VolumeProfile post)
        {
            var scene = NewScene(post, ambient: Color.clear, skybox: true);
            Sun(Quaternion.Euler(48f, 20f, 0f), 1.7f);

            var terrain = new GameObject("Terrain").transform;
            Prim(PrimitiveType.Plane, "Ground", terrain, Vector3.zero, new Vector3(40, 1, 40), m.Sand);
            var random = new System.Random(5);
            for (int i = 0; i < 9; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = 90f + (float)random.NextDouble() * 80f;
                float size = 40f + (float)random.NextDouble() * 50f;
                Prim(PrimitiveType.Sphere, "Dune", terrain, new Vector3(Mathf.Cos(angle) * distance, -size * 0.38f, Mathf.Sin(angle) * distance),
                    new Vector3(size, size * 0.5f, size), m.Sand);
            }

            // Landed ships
            var ships = new GameObject("Ships").transform;
            var shipA = Slot("PlayerShip", ships, new Vector3(-7, 0, -12), Vector3.zero);
            Prim(PrimitiveType.Capsule, "Placeholder", shipA, new Vector3(0, 5, 0), new Vector3(2.2f, 5f, 2.2f), m.Wreck);
            var shipB = Slot("PilotShip", ships, new Vector3(7, 0, -12), Vector3.zero);
            Prim(PrimitiveType.Capsule, "Placeholder", shipB, new Vector3(0, 6, 0), new Vector3(2.6f, 6f, 2.6f), m.OldRocket);

            // Gate and its console
            Ring("Gate", null, new Vector3(0, 0, 46), 11f, m.Ruin, m.Glyph);
            var gateConsole = Console("GateConsole", null, new Vector3(0, 0, 28), 0f, m, "Use the gate console", singleUse: false);
            var puzzle = gateConsole.gameObject.AddComponent<GlyphPuzzle>();
            SetInts(puzzle, "target", 3, 0, 4);

            // Three ruin sites, each with a coordinate tablet
            var tablets = new List<Object>();
            var sites = new[] { new Vector3(-38, 0, 22), new Vector3(34, 0, 8), new Vector3(-12, 0, -42) };
            for (int i = 0; i < sites.Length; i++)
            {
                var site = Slot($"RuinSite{i + 1}", null, sites[i], new Vector3(0, i * 40f, 0));
                var placeholder = new GameObject("Placeholder").transform;
                placeholder.SetParent(site, false);
                Prim(PrimitiveType.Cube, "Tower A", placeholder, new Vector3(-4, 6, 3), new Vector3(3, 12, 3), m.Ruin);
                Prim(PrimitiveType.Cube, "Tower B", placeholder, new Vector3(4, 4, 4), new Vector3(3, 8, 3), m.Ruin, Quaternion.Euler(0, 20, 6));
                Prim(PrimitiveType.Cube, "Fallen Wall", placeholder, new Vector3(0, 0.6f, 6), new Vector3(9, 1.2f, 1.5f), m.Ruin, Quaternion.Euler(0, -10, 0));
                tablets.Add(Pedestal($"Tablet{i + 1}", site, sites[i], m, "Read the tablet", 1.5f));
            }

            var (player, _) = Player(new Vector3(0, 0, 0), 1.65f, seatedYawLimit: 0f);
            Pilot(m, new Vector3(2.5f, 0, -2f), player.transform);

            var sequence = new GameObject("Gate World Sequence").AddComponent<GateWorldSequence>();
            Set(sequence, ("player", player.GetComponent<FirstPersonController>()), ("tablets", tablets.ToArray()),
                ("gateConsole", gateConsole), ("puzzle", puzzle));

            AddHud();
            Save(scene, SceneFlow.GateWorld);
        }

        // =====================================================================
        // 06 Harvester: the station, the records, the target list, the lost transmissions
        // =====================================================================

        static void BuildHarvester(Mats m, VolumeProfile post)
        {
            var scene = NewScene(post, ambient: Color.clear, skybox: true);
            Sun(Quaternion.Euler(30f, -50f, 0f), 1.3f);

            var terrain = new GameObject("Terrain").transform;
            Prim(PrimitiveType.Plane, "Ground", terrain, Vector3.zero, new Vector3(50, 1, 50), m.Seabed);
            // The old shoreline: high ground far above the drained seabed
            foreach (var p in new[] { new Vector3(-200, 0, 60), new Vector3(200, 0, 100), new Vector3(0, 0, 260), new Vector3(-120, 0, -180), new Vector3(150, 0, -160) })
                Prim(PrimitiveType.Cube, "Old Shoreline", terrain, p + Vector3.up * 50f, new Vector3(160, 100, 120), m.Hill, Quaternion.Euler(0, p.x * 0.2f, 0));

            // The harvester tower
            var tower = Slot("HarvesterTower", null, new Vector3(70, 0, 130), Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Placeholder", tower, new Vector3(0, 45, 0), new Vector3(26, 45, 26), m.Wreck);
            Prim(PrimitiveType.Cylinder, "Intake Ring", tower, new Vector3(0, 12, 0), new Vector3(34, 1.5f, 34), m.Glyph, collider: false);
            Prim(PrimitiveType.Cube, "Pipe", tower, new Vector3(-40, 3, -30), new Vector3(70, 4, 4), m.Wreck, Quaternion.Euler(0, 35, 0));
            PointLight("Tower Glow", tower, new Vector3(70, 14, 105), new Color(0.4f, 0.9f, 1f), 30f, 80f);

            // Station building: a 20 x 16 room with a door in the front wall
            var station = Slot("Station", null, new Vector3(0, 0, 40), Vector3.zero);
            var shell = new GameObject("Placeholder").transform;
            shell.SetParent(station, false);
            Prim(PrimitiveType.Cube, "Floor", shell, new Vector3(0, 0.05f, 0), new Vector3(20, 0.1f, 16), m.Cockpit);
            Prim(PrimitiveType.Cube, "Roof", shell, new Vector3(0, 6.1f, 0), new Vector3(20.4f, 0.2f, 16.4f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Wall Back", shell, new Vector3(0, 3, 8), new Vector3(20, 6, 0.4f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Wall Left", shell, new Vector3(-10, 3, 0), new Vector3(0.4f, 6, 16), m.Cockpit);
            Prim(PrimitiveType.Cube, "Wall Right", shell, new Vector3(10, 3, 0), new Vector3(0.4f, 6, 16), m.Cockpit);
            Prim(PrimitiveType.Cube, "Front Left", shell, new Vector3(-6, 3, -8), new Vector3(8, 6, 0.4f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Front Right", shell, new Vector3(6, 3, -8), new Vector3(8, 6, 0.4f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Front Top", shell, new Vector3(0, 5, -8), new Vector3(4, 2, 0.4f), m.Cockpit);
            var door = Prim(PrimitiveType.Cube, "Station Door", station, new Vector3(0, 2, -8), new Vector3(4, 4, 0.5f), m.Shutter);
            PointLight("Door Light", station, new Vector3(0, 5, 30), new Color(1f, 0.2f, 0.1f), 6f, 10f);
            PointLight("Room Light A", station, new Vector3(-5, 5, 40), new Color(0.6f, 0.85f, 1f), 6f, 14f);
            PointLight("Room Light B", station, new Vector3(5, 5, 44), new Color(0.6f, 0.85f, 1f), 6f, 14f);
            var entranceZone = TriggerBox("Entrance Trigger", station, new Vector3(0, 2, 27), new Vector3(10, 4, 6));

            // Inside the station
            var records = new List<Object>
            {
                Pedestal("Record1", station, new Vector3(-7, 0, 36), m, "Play the record", 1f),
                Pedestal("Record2", station, new Vector3(-7, 0, 44), m, "Play the record", 1f),
                Pedestal("Record3", station, new Vector3(7, 0, 44), m, "Play the record", 1f),
            };
            var targetTerminal = Console("MainTerminal", station, new Vector3(0, 0, 46), 0f, m, "Read the main terminal", hold: 1.5f);
            var commsTerminal = Console("CommsTerminal", station, new Vector3(7.5f, 0, 36), -90f, m, "Read the communications log", hold: 1.5f);

            // Access key on a wrecked drone behind the station (the "search" solution)
            var keySlot = Slot("AccessKey", null, new Vector3(5, 0, 54), new Vector3(0, 25, 0));
            Prim(PrimitiveType.Cube, "Placeholder", keySlot, new Vector3(0, 0.4f, 0), new Vector3(1.6f, 0.8f, 1.2f), m.Wreck, Quaternion.Euler(0, 0, 14));
            Prim(PrimitiveType.Cube, "Key Crystal", keySlot, new Vector3(0, 1f, 0), new Vector3(0.25f, 0.4f, 0.25f), m.Glyph, collider: false);
            var accessKey = keySlot.gameObject.AddComponent<Interactable>();
            Set(accessKey, ("prompt", "Take the access key"));

            // Gate beside the station
            Ring("Gate", null, new Vector3(-48, 0, 40), 11f, m.Ruin, m.Glyph, yaw: 90f);
            var gateConsole = Console("GateConsole", null, new Vector3(-30, 0, 40), -90f, m, "Use the gate console", singleUse: false);
            var puzzle = gateConsole.gameObject.AddComponent<GlyphPuzzle>();
            SetInts(puzzle, "target", 1, 2, 0);

            var (player, _) = Player(new Vector3(0, 0, 0), 1.65f, seatedYawLimit: 0f);
            Pilot(m, new Vector3(2.5f, 0, -2f), player.transform);

            var sequence = new GameObject("Harvester Sequence").AddComponent<HarvesterSequence>();
            Set(sequence, ("player", player.GetComponent<FirstPersonController>()), ("entranceZone", entranceZone), ("door", door),
                ("accessKey", accessKey), ("records", records.ToArray()), ("targetTerminal", targetTerminal), ("commsTerminal", commsTerminal),
                ("gateConsole", gateConsole), ("puzzle", puzzle));

            AddHud();
            Save(scene, SceneFlow.Harvester);
        }

        // =====================================================================
        // 07 Control centre: the last puzzle and the final decision
        // =====================================================================

        static void BuildControl(Mats m, VolumeProfile post)
        {
            var scene = NewScene(post, ambient: new Color(0.06f, 0.07f, 0.1f), skybox: false);
            Sun(Quaternion.Euler(35f, 140f, 0f), 0.8f);

            var space = new GameObject("Space").transform;
            Starfield(space, m.Stars, 2500f);
            // Gates of the network, hanging in the distance
            for (int i = 0; i < 6; i++)
            {
                float angle = i / 6f * Mathf.PI * 2f + 0.4f;
                var position = new Vector3(Mathf.Cos(angle) * 260f, -40f + i * 22f, Mathf.Sin(angle) * 260f);
                Ring($"NetworkGate{i + 1}", space, position, 30f, m.Ruin, m.Beacon, yaw: 90f - angle * Mathf.Rad2Deg, colliders: false);
            }

            var platform = Slot("ControlPlatform", null, Vector3.zero, Vector3.zero);
            Prim(PrimitiveType.Cube, "Placeholder", platform, new Vector3(0, -0.5f, 8), new Vector3(36, 1, 44), m.Platform);
            foreach (float x in new[] { -18f, 18f })
                Prim(PrimitiveType.Cube, "Rail", platform, new Vector3(x, 0.6f, 8), new Vector3(0.3f, 1.2f, 44), m.Cockpit);
            Prim(PrimitiveType.Cube, "Rail Back", platform, new Vector3(0, 0.6f, -14), new Vector3(36, 1.2f, 0.3f), m.Cockpit);
            Prim(PrimitiveType.Cube, "Rail Front", platform, new Vector3(0, 0.6f, 30), new Vector3(36, 1.2f, 0.3f), m.Cockpit);

            var spire = Slot("ControlSpire", platform, new Vector3(0, 0, 24), Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Placeholder", spire, new Vector3(0, 14, 0), new Vector3(5, 14, 5), m.Ruin);
            for (int g = 0; g < 6; g++)
                Prim(PrimitiveType.Cylinder, "Glyph Band", spire, new Vector3(0, 3 + g * 4f, 0), new Vector3(5.2f, 0.12f, 5.2f), m.Glyph, collider: false);
            PointLight("Spire Light", spire, new Vector3(0, 6, 18), new Color(0.5f, 0.85f, 1f), 12f, 30f);

            var coreSlot = Slot("NetworkCore", platform, new Vector3(0, 0, 17), Vector3.zero);
            Prim(PrimitiveType.Sphere, "Placeholder", coreSlot, new Vector3(0, 1.6f, 0), Vector3.one * 1.6f, m.GateCore);
            Prim(PrimitiveType.Cylinder, "Core Stand", coreSlot, new Vector3(0, 0.35f, 0), new Vector3(1.2f, 0.35f, 1.2f), m.Console);
            var core = coreSlot.gameObject.AddComponent<Interactable>();
            Set(core, ("prompt", "Decide the future of the network"), ("holdDuration", 2f));

            var console = Console("ControlConsole", platform, new Vector3(-7, 0, 12), 0f, m, "Use the control console", singleUse: false);
            var puzzle = console.gameObject.AddComponent<PowerRoutingPuzzle>();
            SetSystems(puzzle, "CONTROL CONSOLE POWER", 4,
                (ControlSequence.SystemCoreLink, 2), (ControlSequence.SystemGateControl, 2), (ControlSequence.SystemOverride, 2));
            PointLight("Console Light", platform, new Vector3(-7, 3, 9), new Color(0.6f, 1f, 0.8f), 5f, 10f);
            PointLight("Platform Light", platform, new Vector3(6, 6, 0), new Color(0.8f, 0.85f, 1f), 8f, 26f);

            var ships = new GameObject("Ships").transform;
            var shipA = Slot("PlayerShip", ships, new Vector3(-9, 0, -8), Vector3.zero);
            Prim(PrimitiveType.Capsule, "Placeholder", shipA, new Vector3(0, 5, 0), new Vector3(2.2f, 5f, 2.2f), m.Wreck);
            var shipB = Slot("PilotShip", ships, new Vector3(9, 0, -8), Vector3.zero);
            Prim(PrimitiveType.Capsule, "Placeholder", shipB, new Vector3(0, 6, 0), new Vector3(2.6f, 6f, 2.6f), m.OldRocket);

            var (player, _) = Player(new Vector3(0, 0, 0), 1.65f, seatedYawLimit: 0f);
            Pilot(m, new Vector3(2.5f, 0, -2f), player.transform);

            var sequence = new GameObject("Control Sequence").AddComponent<ControlSequence>();
            Set(sequence, ("player", player.GetComponent<FirstPersonController>()), ("console", console), ("puzzle", puzzle), ("core", core));

            AddHud();
            Save(scene, SceneFlow.Control);
        }

        // =====================================================================
        // Shared pieces for the story scenes
        // =====================================================================

        /// <summary>A standing ring gate made of stone segments. worldBase is the point on the ground under its centre.</summary>
        static Transform Ring(string name, Transform parent, Vector3 worldBase, float radius, Material stone, Material glow,
            float yaw = 0f, bool colliders = true)
        {
            var slot = Slot(name, parent, worldBase, new Vector3(0, yaw, 0));
            var placeholder = new GameObject("Placeholder").transform;
            placeholder.SetParent(slot, false);
            const int segments = 20;
            float chord = 2f * radius * Mathf.Sin(Mathf.PI / segments) * 1.08f;
            float thickness = radius * 0.13f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                var position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius + radius + thickness, 0);
                var rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + 90f);
                Prim(PrimitiveType.Cube, "Segment", placeholder, position, new Vector3(chord, thickness, thickness * 1.3f), stone, rotation, colliders);
                if (i % 2 == 0)
                    Prim(PrimitiveType.Cube, "Glyph", placeholder, position + new Vector3(0, 0, -thickness * 0.7f),
                        new Vector3(chord * 0.5f, thickness * 0.15f, 0.1f), glow, rotation, collider: false);
            }
            return slot;
        }

        /// <summary>A console the player can use. Its screen faces -Z when yaw is 0.</summary>
        static Interactable Console(string name, Transform parent, Vector3 worldPosition, float yaw, Mats m, string prompt,
            float hold = 0f, bool singleUse = true)
        {
            var slot = Slot(name, parent, worldPosition, new Vector3(0, yaw, 0));
            Prim(PrimitiveType.Cube, "Placeholder", slot, new Vector3(0, 0.6f, 0), new Vector3(1.6f, 1.2f, 0.8f), m.Console);
            Prim(PrimitiveType.Quad, "Screen", slot, new Vector3(0, 1.55f, 0.1f), new Vector3(1.4f, 0.7f, 1), m.ConsoleScreen, Quaternion.Euler(-15, 0, 0), collider: false);
            var interactable = slot.gameObject.AddComponent<Interactable>();
            Set(interactable, ("prompt", prompt), ("holdDuration", hold), ("singleUse", singleUse));
            return interactable;
        }

        /// <summary>A glowing tablet or record on a stand.</summary>
        static Interactable Pedestal(string name, Transform parent, Vector3 worldPosition, Mats m, string prompt, float hold)
        {
            var slot = Slot(name, parent, worldPosition, Vector3.zero);
            Prim(PrimitiveType.Cube, "Placeholder", slot, new Vector3(0, 0.5f, 0), new Vector3(0.9f, 1f, 0.9f), m.Ruin);
            Prim(PrimitiveType.Cube, "Tablet", slot, new Vector3(0, 1.45f, 0), new Vector3(0.7f, 0.9f, 0.12f), m.Glyph);
            PointLight("Glow", slot, worldPosition + new Vector3(0, 2.2f, 0), new Color(0.4f, 0.9f, 1f), 3f, 7f);
            var interactable = slot.gameObject.AddComponent<Interactable>();
            Set(interactable, ("prompt", prompt), ("holdDuration", hold));
            return interactable;
        }

        /// <summary>The second pilot, following the player.</summary>
        static Companion Pilot(Mats m, Vector3 worldPosition, Transform target)
        {
            var slot = Slot("ColonyPilot", null, worldPosition, Vector3.zero);
            Prim(PrimitiveType.Capsule, "Placeholder", slot, new Vector3(0, 1f, 0), new Vector3(0.7f, 1f, 0.7f), m.Pilot, collider: false);
            var companion = slot.gameObject.AddComponent<Companion>();
            Set(companion, ("target", target));
            return companion;
        }

        static void SetInts(Object target, string field, params int[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).intValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetSystems(PowerRoutingPuzzle puzzle, string title, int capacity, params (string name, int required)[] systems)
        {
            var so = new SerializedObject(puzzle);
            so.FindProperty("panelTitle").stringValue = title;
            so.FindProperty("baseCapacity").intValue = capacity;
            var list = so.FindProperty("systems");
            list.arraySize = systems.Length;
            for (int i = 0; i < systems.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("name").stringValue = systems[i].name;
                element.FindPropertyRelative("required").intValue = systems[i].required;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
