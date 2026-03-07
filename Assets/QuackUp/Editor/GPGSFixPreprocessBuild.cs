using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class GPGSFixPreprocessBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => 999;

    public void OnPreprocessBuild(BuildReport report)
    {
        const string path = "Assets/GeneratedLocalRepo/GooglePlayGames/com.google.play.games/Editor/m2repository/com/google/games/gpgs-plugin-support/2.1.0/gpgs-plugin-support-2.1.0.aar";
        ((PluginImporter)AssetImporter.GetAtPath(path)).SetCompatibleWithPlatform(BuildTarget.Android, true);
    }
}
