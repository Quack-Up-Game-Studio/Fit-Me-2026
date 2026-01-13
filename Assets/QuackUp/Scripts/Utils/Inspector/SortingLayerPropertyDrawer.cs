using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace QuackUp.Utils
{
    
    public class SortingLayerAttribute : PropertyAttribute
    {
    }

    #if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(SortingLayerAttribute))]
    public class SortingLayerPropertyDrawer : PropertyDrawer
    {
        private const string SORTING_LAYER_FIELD_METHOD_NAME = "SortingLayerField";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer &&
                property.propertyType != SerializedPropertyType.String)
            {
                Debug.LogError("SortingLayer property should be an integer or string (the layer ID)");
                EditorGUI.LabelField(position, label.text, "Use an int or string.");
            }
            else
            {
                SortingLayerField(position, label, property, EditorStyles.popup, EditorStyles.label);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight; // Ensures no extra spacing
        }

        private static void SortingLayerField(
            Rect position, GUIContent label, SerializedProperty layerID, GUIStyle style, GUIStyle labelStyle
        )
        {
            var methodInfo = typeof(EditorGUI).GetMethod(SORTING_LAYER_FIELD_METHOD_NAME,
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(Rect), typeof(GUIContent), typeof(SerializedProperty), typeof(GUIStyle), typeof(GUIStyle)
                },
                null
            );

            if (methodInfo == null)
            {
                Debug.LogWarning($"{nameof(SortingLayerPropertyDrawer)} can't find {SORTING_LAYER_FIELD_METHOD_NAME}.");
                return;
            }

            var parameters = new object[] { position, label, layerID, style, labelStyle };
            methodInfo.Invoke(null, parameters);
        }
    }
    #endif
}