using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FitMe.Grid
{
    
    [Serializable]
    public record SpriteOutlineSettings
    {
        public bool OutlineTop = true;
        public bool OutlineBottom = true;
        public bool OutlineLeft = true;
        public bool OutlineRight = true;
    }
    
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteOutlineController : MonoBehaviour
    {
        [Title("Outline Settings")]
        public Color outlineColor = Color.black;
        [PropertyRange(0, 0.5f)] public float outlineWidth = 0.05f;
    
        [Title("Edge Toggles")]
        [SerializeField] private SpriteOutlineSettings outlineSettings = new();

        private MaterialPropertyBlock _propBlock;
        private SpriteRenderer _spriteRenderer;
        private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineTop = Shader.PropertyToID("_OutlineTop");
        private static readonly int OutlineBottom = Shader.PropertyToID("_OutlineBottom");
        private static readonly int OutlineLeft = Shader.PropertyToID("_OutlineLeft");
        private static readonly int OutlineRight = Shader.PropertyToID("_OutlineRight");
        
        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            UpdateOutline(outlineSettings);
        }

        private void OnValidate()
        {
            if (!_spriteRenderer) 
                _spriteRenderer = GetComponent<SpriteRenderer>();
        
            UpdateOutline(outlineSettings);
        }

        public void UpdateOutline(SpriteOutlineSettings settings)
        {
            
            if (!_spriteRenderer) return;
            outlineSettings = settings;
            _propBlock ??= new MaterialPropertyBlock();
            _spriteRenderer.GetPropertyBlock(_propBlock);
        
            // Apply outline settings
            _propBlock.SetColor(OutlineColor, outlineColor);
            _propBlock.SetFloat(OutlineWidth, outlineWidth);
        
            // Convert bools to float (1 or 0) for shader
            _propBlock.SetFloat(OutlineTop, settings.OutlineTop ? 1 : 0);
            _propBlock.SetFloat(OutlineBottom, settings.OutlineBottom ? 1 : 0);
            _propBlock.SetFloat(OutlineLeft, settings.OutlineLeft ? 1 : 0);
            _propBlock.SetFloat(OutlineRight, settings.OutlineRight ? 1 : 0);
        
            _spriteRenderer.SetPropertyBlock(_propBlock);
        }
    }
}