using System;
using UnityEditor;

namespace LittleCastle.Editor
{
    /// <summary>Safe local import defaults for synchronised production exports.</summary>
    public sealed class ReadyModelImportPostprocessor : AssetPostprocessor
    {
        private const string ModelRoot = "Assets/_Game/Models/Ready/";
        private const string TextureRoot = "Assets/_Game/Textures/Ready/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelRoot, StringComparison.Ordinal) ||
                !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                return;
            var importer = (ModelImporter)assetImporter;
            importer.isReadable = false;
            importer.importAnimation = false;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureRoot, StringComparison.Ordinal))
                return;
            var importer = (TextureImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool normal = name.EndsWith("_Normal", StringComparison.OrdinalIgnoreCase);
            bool linear = normal ||
                name.EndsWith("_AO", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_Roughness", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_Height", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_Opacity", StringComparison.OrdinalIgnoreCase);
            importer.textureType = normal ? TextureImporterType.NormalMap :
                TextureImporterType.Default;
            importer.sRGBTexture = !linear;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
