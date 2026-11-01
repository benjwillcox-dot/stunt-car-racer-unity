using UnityEditor;

namespace StuntCar.EditorTools
{
    /// <summary>
    /// Import settings for the Blender grandstand kit in Assets/Models/Grandstands, the same as
    /// the car's (see <see cref="CarModelImport"/>) except that the meshes stay readable: the
    /// game copies them along each side of the stadium (<c>Grandstands</c>).
    /// </summary>
    public class GrandstandImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Models/Grandstands/";

        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(Folder)) CarModelImport.PaletteTexture((TextureImporter)assetImporter, assetPath);
        }

        void OnPreprocessModel()
        {
            if (assetPath.StartsWith(Folder)) CarModelImport.PaletteModel((ModelImporter)assetImporter, true);
        }
    }
}
