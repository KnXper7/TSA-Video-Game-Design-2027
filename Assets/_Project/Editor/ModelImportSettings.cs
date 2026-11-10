using UnityEditor;
using UnityEngine;

namespace TSAGame.EditorTools
{
    /// <summary>
    /// Import settings for everything Tools/blender/build_models.py makes: smooth shading worked out by Unity,
    /// no cameras or animation, and "_n" textures treated as normal maps.
    /// </summary>
    class ModelImportSettings : AssetPostprocessor
    {
        public override uint GetVersion() => 10;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/_Project/Models/")) return;
            var importer = (ModelImporter)assetImporter;
            if (assetPath.Contains("/People/"))
            {
                // Rigged characters from Tools/blender/build_people.py: keep their smooth normals and skeleton.
                importer.importNormals = ModelImporterNormals.Import;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importBlendShapes = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                return;
            }
            // Trees carry hand-made normals (leaves are lit as one soft crown), so keep them.
            importer.importNormals = assetPath.Contains("/Tree_") ? ModelImporterNormals.Import : ModelImporterNormals.Calculate;
            importer.normalSmoothingAngle = assetPath.Contains("Title") ? 30f : 50f;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.isReadable = true;       // mesh colliders on scaled models need readable meshes
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/_Project/Models/People/Textures/"))
            {
                var people = (TextureImporter)assetImporter;
                if (assetPath.ToLower().Contains("normal")) people.textureType = TextureImporterType.NormalMap;
                else people.alphaIsTransparency = true;
                people.maxTextureSize = 2048;
                return;
            }
            if (!assetPath.StartsWith("Assets/_Project/Textures/")) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.Contains("Leaves") || assetPath.Contains("GrassTuft") || assetPath.Contains("Flowers") || assetPath.Contains("Skyline")) importer.alphaIsTransparency = true;
            if (assetPath.Contains("Sky_")) { importer.maxTextureSize = 4096; importer.mipmapEnabled = false; }
            if (assetPath.EndsWith("_n.png") || assetPath.Contains("NormalGL")) importer.textureType = TextureImporterType.NormalMap;
            if (assetPath.Contains("Nebula")) importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
        }
    }
}
