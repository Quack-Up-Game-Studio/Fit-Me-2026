using UnityEditor;
using UnityEngine;

namespace FitMe.GameData.Editor
{
    [FilePath("ProjectSettings/FitMeShapeTilingValidationSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ShapeTilingValidationSettings : ScriptableSingleton<ShapeTilingValidationSettings>
    {
        private const int DefaultMaxSearchNodes = 100_000;
        private const int MaximumMaxSearchNodes = 10_000_000;

        [SerializeField] private int _maxSearchNodes = DefaultMaxSearchNodes;

        public int MaxSearchNodes => Mathf.Clamp(_maxSearchNodes, 1, MaximumMaxSearchNodes);

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
                    if (!EditorGUI.EndChangeCheck()) return;

                    settings._maxSearchNodes = Mathf.Clamp(maxSearchNodes, 1, MaximumMaxSearchNodes);
                    settings.Save(saveAsText: true);
                }
            };
        }
    }
}
