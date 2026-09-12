using System;
using System.IO;
using UnityEditor;

namespace OrbitOrchard.Editor
{
    /// <summary>Only clinic models use these import settings; previous game assets keep their metadata.</summary>
    public sealed class ClinicModelImporter : AssetPostprocessor
    {
        private const string Prefix = "Assets/IdleClinic/Art/Resources/Clinic/Models/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Prefix, StringComparison.Ordinal)
                || !string.Equals(Path.GetExtension(assetPath), ".fbx", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            var stem = Path.GetFileNameWithoutExtension(assetPath);
            var character = string.Equals(stem, "Patient", StringComparison.OrdinalIgnoreCase)
                || string.Equals(stem, "Receptionist", StringComparison.OrdinalIgnoreCase)
                || string.Equals(stem, "Nurse", StringComparison.OrdinalIgnoreCase);
            importer.importAnimation = character;
            importer.animationType = character ? ModelImporterAnimationType.Legacy : ModelImporterAnimationType.None;
            if (character) importer.animationCompression = ModelImporterAnimationCompression.Off;
        }
    }
}
