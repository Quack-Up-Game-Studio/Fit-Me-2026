using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using QuackUp.Audio;

public class AudioSettingsBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("[AudioSettingsBuildProcessor] Running pre-build validation to ensure audio settings are unmuted.");

        string[] guids = AssetDatabase.FindAssets("AudioSettings t:ScriptableObject");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[AudioSettingsBuildProcessor] No AudioSettings asset found in the project.");
            return;
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Debug.Log($"[AudioSettingsBuildProcessor] Found asset path: {path}");

            QuackUp.Audio.AudioSettings audioSettings = AssetDatabase.LoadAssetAtPath<QuackUp.Audio.AudioSettings>(path);
            if (audioSettings == null)
            {
                Debug.LogWarning($"[AudioSettingsBuildProcessor] Failed to load asset as AudioSettings at: {path}");
                continue;
            }

            if (audioSettings.BusData == null)
            {
                Debug.LogWarning($"[AudioSettingsBuildProcessor] BusData is null on loaded asset at: {path}");
                continue;
            }

            Debug.Log($"[AudioSettingsBuildProcessor] Successfully loaded asset with {audioSettings.BusData.Count} buses.");

            bool isModified = false;
            foreach (var kvp in audioSettings.BusData)
            {
                BusData busData = kvp.Value;
                if (busData == null)
                {
                    Debug.LogWarning($"[AudioSettingsBuildProcessor] BusData entry for '{kvp.Key}' is null!");
                    continue;
                }

                Debug.Log($"[AudioSettingsBuildProcessor] Bus '{kvp.Key}' current IsMuted = {busData.IsMuted}");

                if (busData.IsMuted)
                {
                    SetIsMutedBackingField(busData, false);
                    isModified = true;
                    Debug.Log($"[AudioSettingsBuildProcessor] Force-unmuted bus: '{kvp.Key}' in '{path}'");
                }
            }

            if (isModified)
            {
                EditorUtility.SetDirty(audioSettings);
            }
        }

        AssetDatabase.SaveAssets();
    }

    private void SetIsMutedBackingField(BusData busData, bool isMuted)
    {
        var type = typeof(BusData);
        var field = type.GetField("<IsMuted>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(busData, isMuted);
        }
        else
        {
            Debug.LogError("[AudioSettingsBuildProcessor] Could not find '<IsMuted>k__BackingField' via reflection.");
        }
    }
}
