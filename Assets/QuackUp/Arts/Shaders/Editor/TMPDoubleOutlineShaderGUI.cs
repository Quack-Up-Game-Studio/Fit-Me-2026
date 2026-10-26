using UnityEditor;
using UnityEngine;

namespace QuackUp.Editor
{
    // Keep the total in TMP's standard property: mesh padding, material
    // presets and stencil-derived materials then use TMP's native path.
    public sealed class TMPDoubleOutlineShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var innerProperty = FindProperty("_InnerOutlineWidth", properties);
            var totalProperty = FindProperty("_OutlineWidth", properties);
            float innerWidth = innerProperty.floatValue;
            float outerWidth = Mathf.Max(0, totalProperty.floatValue - innerWidth);

            EditorGUILayout.LabelField("Double Outline", EditorStyles.boldLabel);
            EditorGUI.showMixedValue = innerProperty.hasMixedValue || totalProperty.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            innerWidth = EditorGUILayout.Slider("Inner Outline Width", innerWidth, 0, 1);
            outerWidth = EditorGUILayout.Slider("Outer Outline Width", outerWidth, 0, 1 - innerWidth);
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo("Double Outline Widths");
                innerProperty.floatValue = innerWidth;
                totalProperty.floatValue = innerWidth + outerWidth;
            }
            EditorGUI.showMixedValue = false;
            EditorGUILayout.HelpBox("Inner + outer width share the font's SDF padding budget (maximum 1). " +
                "For clean colors, set the TMP component's Vertex Color to white. " +
                "Shadow Dilate must cover both rings if the shadow should follow the outer silhouette.", MessageType.Info);
            materialEditor.PropertiesDefaultGUI(properties);
        }
    }
}
