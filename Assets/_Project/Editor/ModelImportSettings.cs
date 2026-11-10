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
        public override uint GetVersion() => 3;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/_Project/Models/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.importNormals = ModelImporterNormals.Calculate;
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
            if (!assetPath.StartsWith("Assets/_Project/Textures/")) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("_n.png")) importer.textureType = TextureImporterType.NormalMap;
            if (assetPath.Contains("Nebula")) importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
        }
    }
}
