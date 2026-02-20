using System;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace QuackUp.Utils
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public class ShaderRectTransformSizeUpdater : MonoBehaviour
    {
        private static readonly int Size = Shader.PropertyToID("_RectTransformSize");
        private Image _image;

        private void Start()
        {
            _image = GetComponent<Graphic>() as Image;
            if (!_image)
            {
                Debug.LogError("ShaderRectTransformSizeUpdater requires a Graphic component.");
                return;
            }
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
            if (!_image || !_image.material) return;
            Debug.Log("Updating material with rect transform size: " + _image.rectTransform.rect.size);
            var imageRect = _image.rectTransform.rect;
            var widthHeight = new Vector2(x: imageRect.width, y: imageRect.height);
            _image.material.SetVector(Size, widthHeight);
        }
    }
}