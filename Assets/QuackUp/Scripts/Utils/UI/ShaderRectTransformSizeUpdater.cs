using System;
using Coffee.UIExtensions;
using Debug = QuackUp.Utils.DebugUtils;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace QuackUp.Utils
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic), typeof(UIMaterialPropertyInjector))]
    public class ShaderRectTransformSizeUpdater : MonoBehaviour
    {
        private Image _image;
        private UIMaterialPropertyInjector _materialPropertyInjector;

        private void Awake()
        {
            _image = GetComponent<Graphic>() as Image;
            _materialPropertyInjector = GetComponent<UIMaterialPropertyInjector>();
            if (!_image)
            {
                Debug.LogError("ShaderRectTransformSizeUpdater requires a Graphic component.");
                return;
            }
            UpdateMaterial();
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdateMaterial();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateMaterial();
        }
#endif

        private void UpdateMaterial()
        {
            if (!_image) _image = GetComponent<Graphic>() as Image;
            if (!_materialPropertyInjector) _materialPropertyInjector = GetComponent<UIMaterialPropertyInjector>();
            if (!_image || !_materialPropertyInjector) return;

            var imageRect = _image.rectTransform.rect;
            var widthHeight = new Vector2(x: imageRect.width, y: imageRect.height);
            Debug.Log("Updating injected material with rect transform size: " + widthHeight);
            _materialPropertyInjector.SetVector("_RectTransformSize", widthHeight);
        }
    }
}