using UnityEditor;
using UnityEngine;

namespace StuntCar.EditorTools
{
    /// <summary>
    /// Import settings for the Blender car in Assets/Models/StuntCar, applied automatically so a
    /// fresh clone (or CI) imports it the same way. Every face's UVs point at one flat-colour cell
    /// of a small palette texture, so the textures want point filtering, no mipmaps and no
    /// compression; the model brings no materials of its own (BuildScript makes the liveries).
    /// </summary>
    public class CarModelImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Models/StuntCar/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.mipmapEnabled = false;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            // R = metallic, A = smoothness: data, not colour.
            t.sRGBTexture = !assetPath.Contains("MetallicSmoothness");
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = false;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = 1f;
            m.useFileScale = true;
            m.bakeAxisConversion = false;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importAnimation = false;
            m.animationType = ModelImporterAnimationType.None;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.importNormals = ModelImporterNormals.Import;
            m.isReadable = false;
            m.addCollider = false;
        }
    }
}
