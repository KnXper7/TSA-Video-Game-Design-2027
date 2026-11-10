using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TSAGame.EditorTools
{
    /// <summary>
    /// Natural ground for the planet scenes: a sculpted terrain with painted grass, mud and rock, a river, real
    /// trees and scattered boulders. The terrain is saved as an asset in Assets/_Project/Terrain and is only
    /// generated when it doesn't exist yet, so it can be re-sculpted and re-painted with Unity's terrain tools.
    /// </summary>
    public static partial class IntroSceneBuilder
    {
        const string TerrainDir = Root + "/Terrain";
        const string GeneratedDir = TexturesDir + "/Generated";

        const float TerrainSize = 700f, TerrainHeight = 90f, TerrainBase = 20f;

        static Texture2D Generated(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{GeneratedDir}/{name}.png");

        /// <summary>Where the river runs (its centre line), as z for a given x.</summary>
        static float RiverZ(float x) => -52f + 9f * Mathf.Sin(x * 0.03f) + 5f * Mathf.Sin(x * 0.011f + 2f);

        /// <summary>Ground height of the landing site at a point: a flat meadow, a shallow river, hills all round.</summary>
        static float LandingHeight(float x, float z)
        {
            float fromCentre = Vector2.Distance(new Vector2(x, z), new Vector2(0, 40));
            float fromRiver = Mathf.Abs(z - RiverZ(x));
            float hilly = Mathf.SmoothStep(0f, 1f, (fromCentre - 105f) / 90f) * Mathf.SmoothStep(0f, 1f, (fromRiver - 14f) / 40f);
            float rolling = Mathf.PerlinNoise(x * 0.006f + 50f, z * 0.006f + 50f) * 52f + Mathf.PerlinNoise(x * 0.02f, z * 0.02f) * 12f
                          + Mathf.PerlinNoise(x * 0.07f, z * 0.07f) * 2.5f;
            float h = rolling * hilly + (Mathf.PerlinNoise(x * 0.09f + 9f, z * 0.09f) - 0.5f) * 0.16f;
            float bed = Mathf.SmoothStep(1f, 0f, (fromRiver - 3.5f) / 9f);          // 1 in the channel, 0 on the banks
            return Mathf.Lerp(h, -1.5f, bed);
        }

        static Terrain LandingTerrain()
        {
            EnsureFolder(TerrainDir);
            string path = $"{TerrainDir}/LandingSite.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null || OverwriteLook)
            {
                if (data == null)
                {
                    data = new TerrainData();
                    AssetDatabase.CreateAsset(data, path);
                }
                const int res = 513;
                data.heightmapResolution = res;
                data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
                var heights = new float[res, res];
                for (int j = 0; j < res; j++)
                for (int i = 0; i < res; i++)
                {
                    float x = -TerrainSize / 2 + i / (res - 1f) * TerrainSize, z = -TerrainSize / 2 + j / (res - 1f) * TerrainSize;
                    heights[j, i] = Mathf.Clamp01((LandingHeight(x, z) + TerrainBase) / TerrainHeight);
                }
                data.SetHeights(0, 0, heights);

                data.terrainLayers = new[]
                {
                    Layer("Grass", "Grass004", 5f), Layer("Riverbank", "Ground037", 5f), Layer("Granite", "Rock030", 9f),
                };
                const int paint = 512;
                data.alphamapResolution = paint;
                var weights = new float[paint, paint, 3];
                for (int j = 0; j < paint; j++)
                for (int i = 0; i < paint; i++)
                {
                    float u = i / (paint - 1f), v = j / (paint - 1f);
                    float x = -TerrainSize / 2 + u * TerrainSize, z = -TerrainSize / 2 + v * TerrainSize;
                    float h = LandingHeight(x, z);
                    float mud = Mathf.SmoothStep(0f, 1f, (-0.05f - h) / 0.5f);
                    mud = Mathf.Max(mud, Mathf.SmoothStep(1f, 0f, (Mathf.Abs(z - RiverZ(x)) - 9f) / 6f) * 0.7f);
                    float rock = Mathf.SmoothStep(0f, 1f, (data.GetSteepness(u, v) - 24f) / 14f) * (1f - mud);
                    weights[j, i, 1] = mud;
                    weights[j, i, 2] = rock;
                    weights[j, i, 0] = Mathf.Max(0f, 1f - mud - rock);
                }
                data.SetAlphamaps(0, 0, weights);

                // Grass and wildflowers: thousands of small cards placed by the terrain, thick on the meadow,
                // none in the river or on rock. Repaint them with the terrain's Paint Details tool.
                const int cells = 1024;
                data.SetDetailResolution(cells, 32);
                data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
                data.detailPrototypes = new[]
                {
                    Detail(Generated("GrassTuft"), 0.7f, 1.3f, 0.45f, 0.9f, new Color(0.7f, 0.95f, 0.55f), new Color(0.95f, 0.9f, 0.5f)),
                    Detail(Generated("Flowers"), 0.7f, 1.0f, 0.75f, 1.05f, Color.white, new Color(0.95f, 0.95f, 0.85f)),
                };
                var grass = new int[cells, cells];
                var flowers = new int[cells, cells];
                var scatter = new System.Random(3);
                for (int j = 0; j < cells; j++)
                for (int i = 0; i < cells; i++)
                {
                    float u = i / (cells - 1f), v = j / (cells - 1f);
                    float x = -TerrainSize / 2 + u * TerrainSize, z = -TerrainSize / 2 + v * TerrainSize;
                    if (LandingHeight(x, z) < -0.12f || Mathf.Abs(z - RiverZ(x)) < 8f || data.GetSteepness(u, v) > 26f) continue;
                    float patch = Mathf.PerlinNoise(x * 0.05f + 7f, z * 0.05f + 3f);
                    grass[j, i] = Mathf.RoundToInt(Mathf.Lerp(1f, 5f, patch));
                    if (patch > 0.4f && scatter.NextDouble() < 0.3) flowers[j, i] = 1;
                }
                data.SetDetailLayer(0, 0, 0, grass);
                data.SetDetailLayer(0, 0, 1, flowers);
                EditorUtility.SetDirty(data);
            }

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.position = new Vector3(-TerrainSize / 2, -TerrainBase, -TerrainSize / 2);
            var terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 400f;
            terrain.detailObjectDistance = 250f;      // grass as far as Unity allows, so distant slopes stay green
            terrain.detailObjectDensity = 1f;
            return terrain;
        }

        static Texture2D PbrMap(string set, string map) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexturesDir}/PBR/{set}_{map}.jpg");

        /// <summary>
        /// Gives materials real surface detail from the CC0 texture sets in Textures/PBR (ambientCG): a normal map
        /// and an occlusion map, and for bare surfaces (walls, floors, rock, bark) the colour picture as well.
        /// Only touches materials created or reset in this build, like every other default.
        /// </summary>
        static void ApplySurfaceDetail()
        {
            (string material, string set, float tile, bool colour)[] plan =
            {
                ("MC_Wall", "Metal032", 4f, false), ("Room_Wall", "Metal032", 5f, false),
                ("Ship_Panels", "Metal032", 2f, false), ("Cockpit_Panel", "Metal032", 2f, false), ("Tower_Concrete", "Concrete034", 3f, true),
                ("MC_Floor", "Concrete034", 6f, true), ("Granite", "Rock030", 1.5f, true),
                ("Site_Ruin", "Rock030", 2f, false), ("Tree_Bark", "Bark012", 1f, true), ("Tree_Dead", "Bark012", 1f, false),
                ("Cockpit_Console", "Plastic010", 2f, false), ("MC_Desk", "Plastic010", 2f, false), ("Desk_Top", "Plastic010", 3f, false),
                ("Desk_Frame", "Metal032", 2f, false), ("Rocket_Metal", "Metal032", 2f, false), ("Tower_Steel", "Metal032", 3f, false),
                ("Crate_Body", "Metal032", 1f, false), ("Portal_Metal", "Metal032", 3f, false), ("Portal_Trim", "Metal032", 3f, false),
                ("Site_Wreck", "Metal032", 3f, false), ("Site_OldRocket", "Metal032", 3f, false), ("Monitor_Bezel", "Plastic010", 2f, false),
                ("Camp_Canvas", "Fabric030", 3f, false), ("Chair_Fabric", "Fabric030", 2f, false), ("Cockpit_Seat", "Fabric030", 2f, false),
                ("Person_SuitTrim", "Fabric030", 2f, false), ("Ship_Padding", "Fabric030", 3f, false),
            };
            foreach (var (material, set, tile, colour) in plan)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{material}.mat");
                if (mat == null || !Fresh(mat)) continue;
                var scale = new Vector2(tile, tile);
                if (colour)
                {
                    mat.SetTexture("_BaseMap", PbrMap(set, "Color"));
                    mat.SetTextureScale("_BaseMap", scale);
                    var c = mat.GetColor("_BaseColor");
                    float brightest = Mathf.Max(c.r, c.g, c.b, 0.01f);
                    if (brightest < 0.7f) mat.SetColor("_BaseColor", c * (0.8f / brightest));     // the picture carries the darkness now
                }
                mat.SetTexture("_BumpMap", PbrMap(set, "NormalGL"));
                mat.SetTextureScale("_BumpMap", scale);
                mat.SetFloat("_BumpScale", 1.2f);
                mat.EnableKeyword("_NORMALMAP");
                var occlusion = PbrMap(set, "AmbientOcclusion");
                if (occlusion != null)
                {
                    mat.SetTexture("_OcclusionMap", occlusion);
                    mat.EnableKeyword("_OCCLUSIONMAP");
                }
                EditorUtility.SetDirty(mat);
            }
        }

        static DetailPrototype Detail(Texture2D texture, float minWidth, float maxWidth, float minHeight, float maxHeight, Color healthy, Color dry) => new()
        {
            prototypeTexture = texture, renderMode = DetailRenderMode.Grass, usePrototypeMesh = false,
            minWidth = minWidth, maxWidth = maxWidth, minHeight = minHeight, maxHeight = maxHeight,
            healthyColor = healthy, dryColor = dry, noiseSpread = 0.3f,
        };

        /// <summary>A sky made from a 360-degree picture (Textures/Generated). Swap the picture on the material to change the weather.</summary>
        static Material PanoSky(string name, string picture, float exposure, float rotation = 0f)
        {
            var mat = LoadOrCreate($"{MaterialsDir}/{name}.mat", () => new Material(Shader.Find("Skybox/Panoramic")));
            if (Fresh(mat))
            {
                mat.SetTexture("_MainTex", Generated(picture));
                mat.SetFloat("_Mapping", 1f);          // latitude-longitude layout
                mat.SetFloat("_ImageType", 0f);        // full 360 degrees
                mat.SetFloat("_Exposure", exposure);
                mat.SetFloat("_Rotation", rotation);
                if (name.Contains("Dust")) mat.SetColor("_Tint", new Color(0.55f, 0.47f, 0.36f));       // pull the red toward amber
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        /// <summary>A far-off skyline painted on tall cards in a ring around a point (cheap, and the fog blends it in).</summary>
        static void SkylineRing(Vector3 centre, float distance, float height, Material mat, float fromAngle, float toAngle, int cards)
        {
            var root = new GameObject("MODEL_DistantCity").transform;
            float step = (toAngle - fromAngle) / cards;
            float width = 2f * distance * Mathf.Tan(step * 0.5f * Mathf.Deg2Rad) * 1.02f;
            for (int i = 0; i < cards; i++)
            {
                float angle = (fromAngle + (i + 0.5f) * step) * Mathf.Deg2Rad;
                var at = centre + new Vector3(Mathf.Sin(angle) * distance, height / 2f - 6f, Mathf.Cos(angle) * distance);
                var card = Prim(PrimitiveType.Quad, "Skyline", root, at, new Vector3(i % 2 == 0 ? width : -width, height, 1), mat, collider: false);
                card.transform.rotation = Quaternion.LookRotation(at - new Vector3(centre.x, at.y, centre.z));
            }
        }

        static TerrainLayer Layer(string name, string set, float tile)
        {
            var layer = LoadOrCreate($"{TerrainDir}/{name}.terrainlayer", () => new TerrainLayer());
            if (Fresh(layer))
            {
                layer.diffuseTexture = PbrMap(set, "Color");
                layer.normalMapTexture = PbrMap(set, "NormalGL");
                layer.normalScale = 1.2f;
                layer.tileSize = new Vector2(tile, tile);
                layer.smoothness = 0.1f;
                EditorUtility.SetDirty(layer);
            }
            return layer;
        }

        /// <summary>A sheet of water at a fixed height. It only shows where the terrain dips below it.</summary>
        static void Water(float height, Vector3 directionToSun)
        {
            var mat = LoadOrCreate($"{MaterialsDir}/Water_River.mat", () => new Material(Shader.Find("TSA/Water")));
            if (Fresh(mat))
            {
                mat.SetVector("_SunDir", directionToSun);
                mat.SetTexture("_SkyTex", Generated("Sky_Morning"));
                EditorUtility.SetDirty(mat);
            }
            Prim(PrimitiveType.Plane, "River Water", null, new Vector3(0, height, 0), new Vector3(TerrainSize / 10f, 1, TerrainSize / 10f), mat, collider: false);
        }

        /// <summary>Trees and boulders across the terrain, keeping the given spots and the river clear.</summary>
        static void Scatter(Terrain terrain, System.Random random, (Vector3 centre, float radius)[] clearings)
        {
            float GroundAt(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
            bool Blocked(Vector3 p, float margin)
            {
                foreach (var (centre, radius) in clearings)
                    if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(centre.x, centre.z)) < radius + margin) return true;
                return Mathf.Abs(p.z - RiverZ(p.x)) < 11f + margin;
            }

            var forest = new GameObject("Forest").transform;
            string[] kinds = { "Tree_1", "Tree_2", "Tree_3" };
            for (int i = 0; i < 700; i++)
            {
                var p = new Vector3(Range(random, -300, 300), 0, Range(random, -230, 300));
                if (Blocked(p, 0f)) continue;
                // thinner in the open meadow, thick on the hills
                float fromCentre = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(0, 40));
                if (fromCentre < 100f && random.NextDouble() < 0.72) continue;
                p.y = GroundAt(p) - 0.15f;
                var tree = Model(kinds[random.Next(kinds.Length)], forest, Vector3.zero, Quaternion.identity, Range(random, 1.3f, 2.2f));
                tree.transform.SetPositionAndRotation(p, Quaternion.Euler(0, Range(random, 0, 360), 0));
                tree.name = "MODEL_Tree";
                var trunk = tree.AddComponent<CapsuleCollider>();
                trunk.center = new Vector3(0, 2.5f, 0);
                trunk.radius = 0.32f;
                trunk.height = 5f;
            }

            var boulders = new GameObject("Boulders").transform;
            var granite = new Dictionary<string, Material> { ["Asteroid_Rock"] = ModelMats["Granite"] };
            for (int i = 0; i < 130; i++)
            {
                // most of them along the river
                float x = Range(random, -260, 260);
                bool byRiver = random.NextDouble() < 0.6;
                var p = byRiver ? new Vector3(x, 0, RiverZ(x) + Range(random, -13, 13)) : new Vector3(x, 0, Range(random, -200, 280));
                if (!byRiver && Blocked(p, 2f)) continue;
                float size = byRiver ? Range(random, 0.25f, 1.3f) : Range(random, 0.4f, 2.6f);
                p.y = GroundAt(p) - size * 0.25f;
                var rock = Model($"Asteroid_{random.Next(1, 6)}", boulders, Vector3.zero, Quaternion.identity, size, granite);
                rock.transform.SetPositionAndRotation(p, Quaternion.Euler(Range(random, 0, 360), Range(random, 0, 360), Range(random, 0, 360)));
                rock.name = "MODEL_Boulder";
                if (size > 0.7f) AddMeshColliders(rock);
            }
        }

        /// <summary>A screen material showing one of the generated pictures (or any texture) with the TSA/Screen shader.</summary>
        static Material ScreenMaterial(string name, Texture texture, float brightness = 1.1f)
        {
            var mat = LoadOrCreate($"{MaterialsDir}/{name}.mat", () => new Material(Shader.Find("TSA/Screen")));
            if (Fresh(mat))
            {
                mat.SetTexture("_MainTex", texture);
                mat.SetFloat("_Brightness", brightness);
                mat.SetFloat("_Phase", Mathf.Abs(name.GetHashCode() % 100) / 100f);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }
    }
}
