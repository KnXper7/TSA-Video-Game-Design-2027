using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TSAGame.EditorTools
{
    /// <summary>
    /// The "look" of the space scenes: colour grade, sun flare, rim light, planets with air and rings, and
    /// fields of small debris. Every asset here is created once and then left alone, so it can be tuned by hand.
    /// </summary>
    public static partial class IntroSceneBuilder
    {
        /// <summary>
        /// When false (the default), materials and other look assets that already exist are used as they are,
        /// so hand edits survive a scene rebuild. "TSA > Reset Look To Defaults" turns this on for one build.
        /// </summary>
        public static bool OverwriteLook;

        static readonly HashSet<Object> freshAssets = new();

        /// <summary>True for assets made (or reset) during this build: only those get default values written to them.</summary>
        static bool Fresh(Object asset) => freshAssets.Contains(asset);

        static T LoadOrCreate<T>(string path, System.Func<T> create) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = create();
                AssetDatabase.CreateAsset(asset, path);
                freshAssets.Add(asset);
            }
            else if (OverwriteLook) freshAssets.Add(asset);
            return asset;
        }

        /// <summary>Cold, high-contrast grade for space: strong bloom, a blue tint in the shadows, film grain.</summary>
        static VolumeProfile SpacePostProfile()
        {
            string path = $"{SettingsDir}/Post_Space.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1.5f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.75f);
            bloom.tint.Override(new Color(0.9f, 0.95f, 1f));
            bloom.highQualityFiltering.Override(true);
            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            var colour = profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(0.45f);
            colour.contrast.Override(18f);
            colour.saturation.Override(-8f);
            colour.colorFilter.Override(new Color(0.97f, 0.98f, 1f));
            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.44f, 0.5f, 0.58f));
            split.highlights.Override(new Color(0.96f, 0.97f, 1f));
            split.balance.Override(-20f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.45f);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium2);
            grain.intensity.Override(0.28f);
            grain.response.Override(0.7f);
            var aberration = profile.Add<ChromaticAberration>(true);
            aberration.intensity.Override(0.1f);
            var distortion = profile.Add<LensDistortion>(true);
            distortion.intensity.Override(-0.08f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>Warm, softer grade for rooms, with a shallow depth of field so faces stand out from the background.</summary>
        static VolumeProfile InteriorPostProfile()
        {
            string path = $"{SettingsDir}/Post_Interior.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1.1f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(Color.white);
            var colour = profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(0.75f);
            colour.contrast.Override(22f);
            colour.saturation.Override(-6f);
            colour.colorFilter.Override(new Color(1f, 0.97f, 0.92f));
            var split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.42f, 0.5f, 0.6f));
            split.highlights.Override(new Color(1f, 0.9f, 0.75f));
            split.balance.Override(10f);
            var depth = profile.Add<DepthOfField>(true);
            depth.mode.Override(DepthOfFieldMode.Gaussian);
            depth.gaussianStart.Override(22f);
            depth.gaussianEnd.Override(50f);
            depth.gaussianMaxRadius.Override(1.2f);
            depth.highQualitySampling.Override(true);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>A box inside which the interior grade replaces the scene's own (the camera only has to be inside it).</summary>
        static void InteriorVolume(Transform parent, Vector3 centre, Vector3 size)
        {
            var go = new GameObject("Interior Look");
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.priority = 5f;
            volume.blendDistance = 0f;
            volume.sharedProfile = InteriorPostProfile();
        }

        /// <summary>Gives the sun a visible glare: a soft glow, a hot core and a wide anamorphic streak (see SunGlare).</summary>
        static void SunFlare(Light sun, float size = 1f)
        {
            var particle = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            var glow = ParticleMaterial("Sun_Glow", particle, new Color(0.45f, 0.7f, 1f) * 0.6f, additive: true);
            var core = ParticleMaterial("Sun_Core", particle, new Color(0.9f, 0.95f, 1f) * 2.2f, additive: true);
            var streak = ParticleMaterial("Sun_Streak", particle, new Color(0.4f, 0.65f, 1f) * 1.6f, additive: true);

            var glare = new GameObject("Sun Glare").AddComponent<SunGlare>();
            Set(glare, ("sun", sun));
            var raysMat = LoadOrCreate($"{MaterialsDir}/Sun_Rays.mat", () => new Material(Shader.Find("TSA/SunRays")));
            Prim(PrimitiveType.Quad, "Rays", glare.transform, Vector3.zero, new Vector3(1900, 1900, 1) * size, raysMat, collider: false);
            Prim(PrimitiveType.Quad, "Glow", glare.transform, Vector3.zero, new Vector3(1500, 1500, 1) * size, glow, collider: false);
            Prim(PrimitiveType.Quad, "Core", glare.transform, Vector3.zero, new Vector3(300, 300, 1) * size, core, collider: false);
            Prim(PrimitiveType.Quad, "Streak", glare.transform, Vector3.zero, new Vector3(9000, 110, 1) * size, streak, collider: false);
            Prim(PrimitiveType.Quad, "Streak Fine", glare.transform, Vector3.zero, new Vector3(14000, 30, 1) * size, streak, collider: false);
        }

        /// <summary>A second, dim light from behind, so shapes have a bright edge against the black.</summary>
        static Light RimLight(Quaternion rotation, Color color, float intensity)
        {
            var light = new GameObject("Rim Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = rotation;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>Adds a glowing layer of air to a planet, and optionally rings.</summary>
        static void DressPlanet(Transform slot, float diameter, string name, Color air, Vector3 directionToSun, bool rings)
        {
            var airMat = LoadOrCreate($"{MaterialsDir}/Atmosphere_{name}.mat", () => new Material(Shader.Find("TSA/Atmosphere")));
            if (Fresh(airMat))
            {
                airMat.SetColor("_Color", air);
                airMat.SetVector("_SunDir", directionToSun);
                EditorUtility.SetDirty(airMat);
            }
            Prim(PrimitiveType.Sphere, "Atmosphere", slot, Vector3.zero, Vector3.one * diameter * 1.035f, airMat, collider: false);
            if (!rings) return;

            var ringMat = LoadOrCreate($"{MaterialsDir}/Rings_{name}.mat", () => new Material(Shader.Find("TSA/PlanetRing")));
            if (Fresh(ringMat))
            {
                ringMat.SetVector("_SunDir", directionToSun);
                EditorUtility.SetDirty(ringMat);
            }
            Prim(PrimitiveType.Quad, "Rings", slot, Vector3.zero, Vector3.one * diameter * 2.5f, ringMat, Quaternion.Euler(83f, 0f, 14f), collider: false);
        }

        /// <summary>A box full of small tumbling rocks (see DebrisField).</summary>
        static DebrisField Debris(Transform parent, Vector3 centre, Vector3 size, int count, Vector2 sizeRange, Vector2 clearLane = default, float clearRadius = 0f)
        {
            var go = new GameObject("Debris Field");
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{ModelsDir}/Asteroid_5.fbx");
            renderer.sharedMaterial = ModelMats["Asteroid_Rock"];
            renderer.sharedMaterial.enableInstancing = true;
            renderer.enableGPUInstancing = true;
            renderer.alignment = ParticleSystemRenderSpace.World;
            var field = go.AddComponent<DebrisField>();
            Set(field, ("count", count), ("size", size), ("clearRadius", clearRadius));
            var so = new SerializedObject(field);
            so.FindProperty("sizeRange").vector2Value = sizeRange;
            so.FindProperty("clearLane").vector2Value = clearLane;
            so.ApplyModifiedPropertiesWithoutUndo();
            return field;
        }

        /// <summary>
        /// The prefab for a model: a variant of the FBX with the project's materials on it. Made once; after that
        /// it is yours to edit (re-exporting the FBX from Blender still updates its shape).
        /// </summary>
        static GameObject ModelPrefab(string file)
        {
            EnsureFolder($"{PrefabsDir}/Models");
            string path = $"{PrefabsDir}/Models/{file}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsDir}/{file}.fbx");
            if (asset == null)
            {
                Debug.LogError($"Model '{file}' is missing. Run Tools/blender/build_models.py.");
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string key = materials[i] != null ? materials[i].name : "";
                    if (ModelMats.TryGetValue(key, out var shared)) materials[i] = shared;
                    else Debug.LogWarning($"Model '{file}' uses material '{key}', which the scene builder doesn't know.");
                }
                renderer.sharedMaterials = materials;
            }
            prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        // ---------- Menu ----------

        [MenuItem("TSA/Reset Look To Defaults (materials, flares)", priority = 40)]
        static void ResetLook()
        {
            if (!EditorUtility.DisplayDialog("Reset look?",
                    "This puts every material made by the scene builder back to its default colours and settings. Your own changes to those materials will be lost.\n\nScenes and prefabs are not touched.",
                    "Reset materials", "Cancel")) return;
            OverwriteLook = true;
            try
            {
                CreateMaterials();
                ModelMats = CreateModelMaterials();
                AssetDatabase.SaveAssets();
            }
            finally { OverwriteLook = false; }
        }

        [MenuItem("TSA/Apply Recommended Graphics Settings", priority = 41)]
        public static void ApplyGraphicsSettings()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset urp) return;
            urp.msaaSampleCount = 4;
            urp.mainLightShadowmapResolution = 4096;
            urp.shadowDistance = 160f;
            urp.supportsHDR = true;
            EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssets();
            Debug.Log($"Graphics settings applied to {urp.name}: 4x anti-aliasing, 160 m shadows, HDR.");
        }
    }
}
