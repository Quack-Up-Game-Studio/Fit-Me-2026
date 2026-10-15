using UnityEditor;
using UnityEngine;

namespace FitMe.GameData.Editor
{
    [FilePath("ProjectSettings/FitMeShapeTilingValidationSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ShapeTilingValidationSettings : ScriptableSingleton<ShapeTilingValidationSettings>
    {
        private const int DefaultMaxSearchNodes = 100_000;
        private const int MaximumMaxSearchNodes = 10_000_000;
        private const int DefaultMaxStoredSolutions = 64;
        private const int MaximumMaxStoredSolutions = 512;

        [SerializeField] private int _maxSearchNodes = DefaultMaxSearchNodes;
        [SerializeField] private int _maxStoredSolutions = DefaultMaxStoredSolutions;

        public int MaxSearchNodes => Mathf.Clamp(_maxSearchNodes, 1, MaximumMaxSearchNodes);
        public int MaxStoredSolutions => Mathf.Clamp(_maxStoredSolutions, 1, MaximumMaxStoredSolutions);

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/Fit Me/Shape Level Validation", SettingsScope.Project)
            {
                label = "Shape Level Validation",
                guiHandler = _ =>
                {
                    var settings = instance;
                    EditorGUI.BeginChangeCheck();
                    var maxSearchNodes = EditorGUILayout.IntField(
                        "Maximum Search Nodes", settings.MaxSearchNodes);
                    var maxStoredSolutions = EditorGUILayout.IntField(
                        "Maximum Stored Solution Previews", settings.MaxStoredSolutions);
                    if (!EditorGUI.EndChangeCheck()) return;

                    settings._maxSearchNodes = Mathf.Clamp(maxSearchNodes, 1, MaximumMaxSearchNodes);
                    settings._maxStoredSolutions = Mathf.Clamp(
                        maxStoredSolutions, 1, MaximumMaxStoredSolutions);
                    settings.Save(saveAsText: true);
                }
            };
        }
    }
}
