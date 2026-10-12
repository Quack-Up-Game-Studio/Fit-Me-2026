using System;
using System.IO;
using FitMe.GameData;
using UnityEditor;
using UnityEngine;

namespace FitMe.GameData.Editor
{
    public static class ShapePlaysetGeneratedLevelAssetCreator
    {
        public static ShapeLevelDefinition CreateNewLevelAsset(GeneratedShapePlayset playset, string assetPath)
        {
            if (playset == null) throw new ArgumentNullException(nameof(playset));
            var normalizedAssetPath = NormalizeAssetPath(assetPath);
            var absoluteAssetPath = GetAbsoluteAssetPath(normalizedAssetPath);
            if (AssetDatabase.LoadMainAssetAtPath(normalizedAssetPath) || File.Exists(absoluteAssetPath))
                throw new InvalidOperationException(
                    $"An asset already exists at '{normalizedAssetPath}'. Choose a new path; existing levels are never overwritten.");

            var directory = Path.GetDirectoryName(normalizedAssetPath)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(directory))
                throw new ArgumentException($"The asset folder '{directory}' does not exist.", nameof(assetPath));

            var level = ScriptableObject.CreateInstance<ShapeLevelDefinition>();
            try
            {
                level.name = Path.GetFileNameWithoutExtension(normalizedAssetPath);
                level.SetGeneratedContent(playset);
                var validation = ShapeLevelDataValidator.Validate(level);
                if (!validation.IsValid)
                    throw new InvalidOperationException(
                        $"Generated level cannot be saved because it is invalid: {FormatErrors(validation.Errors)}");

                AssetDatabase.CreateAsset(level, normalizedAssetPath);
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssets();
                return level;
            }
            catch
            {
                if (string.Equals(AssetDatabase.GetAssetPath(level), normalizedAssetPath, StringComparison.Ordinal))
                    AssetDatabase.DeleteAsset(normalizedAssetPath);
                else if (level)
                    UnityEngine.Object.DestroyImmediate(level);
                throw;
            }
        }

        private static string NormalizeAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("An asset path is required.", nameof(assetPath));
            var normalized = assetPath.Replace('\\', '/').Trim();
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(normalized), ".asset", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Generated levels must be saved as .asset files inside Assets/.", nameof(assetPath));
            GetAbsoluteAssetPath(normalized);
            return normalized;
        }

        private static string GetAbsoluteAssetPath(string normalizedAssetPath)
        {
            var assetsRoot = Path.GetFullPath(Application.dataPath);
            var candidate = Path.GetFullPath(Path.Combine(
                assetsRoot, normalizedAssetPath.Substring("Assets/".Length)));
            var rootPrefix = assetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                             Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.Ordinal))
                throw new ArgumentException("Asset paths must remain inside the project's Assets folder.",
                    nameof(normalizedAssetPath));
            return candidate;
        }

        private static string FormatErrors(System.Collections.Generic.IReadOnlyList<ShapeLevelValidationError> errors)
        {
            var messages = new string[errors.Count];
            for (var index = 0; index < errors.Count; index++) messages[index] = errors[index].ToString();
            return string.Join(", ", messages);
        }
    }
}
