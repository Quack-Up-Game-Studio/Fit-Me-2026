using System;
using Coffee.UIExtensions;
using QuackUp.Utils;
using PrimeTween;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    public class CellView : MonoBehaviour, IDisposable
    {
        #region Inspectors
        [Title("References")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteRenderer baseRenderer;
        [SerializeField] private RendererMaterialPropertyInjector hintMaterialInjector;
        
        private CellViewModel _viewModel;
        private CellConfig _config;
        private Color _originalColor;
        private CellState _currentState = CellState.None;
        private bool _hintHighlighted;
        private bool _hintFlashVisible;
        private IDisposable _bindings;
        private Material _baseMaterial;
        private Sprite _baseQuad;
        private Vector4 _baseEdges = Vector4.one;
        private float _baseScale;
        private MaterialPropertyBlock _baseProperties;
        #endregion
        
        public Transform Transform => transform;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public SpriteRenderer BaseRenderer => baseRenderer;
        
        [Inject]
        public void Construct(
            CellConfig config,
            CellViewModel viewModel)
        {
            _originalColor = spriteRenderer.color;
            _config = config;
            _viewModel = viewModel;
            SetupBaseRenderer();
            Bind();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
            if (spriteRenderer != null) Tween.StopAll(spriteRenderer);
        }

        private void OnDestroy()
        {
            Dispose();
            if (_baseMaterial != null) Destroy(_baseMaterial);
            if (_baseQuad != null) Destroy(_baseQuad);
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.ArrayIndex
                .Subscribe(OnArrayIndexChanged)
                .AddTo(ref disposableBuilder);
            _viewModel.State
                .Subscribe(OnCellStateChanged)
                .AddTo(ref disposableBuilder);
            _viewModel.HintHighlighted
                .Subscribe(value =>
                {
                    _hintHighlighted = value;
                    UpdateVisualColor(true);
                })
                .AddTo(ref disposableBuilder);
            _viewModel.HintFlashVisible
                .Subscribe(value =>
                {
                    _hintFlashVisible = value;
                    UpdateVisualColor(true);
                })
                .AddTo(ref disposableBuilder);
            _viewModel.DestroyCommand
                .Subscribe(_ => Destroy())
                .AddTo(ref disposableBuilder);
            _viewModel.BaseOutlineDiagonals.Subscribe(_ => UpdateBaseOutline()).AddTo(ref disposableBuilder);
            _viewModel.BaseOutlineEdges.Subscribe(SetBaseOutlineEdges).AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        private void SetupBaseRenderer()
        {
            if (_config == null || !_config.EnableBaseOutline)
            {
                if (baseRenderer != null) baseRenderer.gameObject.SetActive(false);
                return;
            }

            if (baseRenderer == null)
            {
                var existingBase = transform.Find("Base");
                if (existingBase != null)
                {
                    baseRenderer = existingBase.GetComponent<SpriteRenderer>();
                }
                else
                {
                    var baseObj = new GameObject("Base");
                    baseObj.transform.SetParent(transform, false);
                    baseObj.transform.localPosition = Vector3.zero;
                    baseObj.transform.localRotation = Quaternion.identity;
                    baseRenderer = baseObj.AddComponent<SpriteRenderer>();
                }
            }

            baseRenderer.gameObject.SetActive(true);
            _baseScale = _config.BaseScale;
            _baseMaterial = new Material(Shader.Find("FitMe/CellBaseOutline"));
            baseRenderer.sharedMaterial = _baseMaterial;
            // A full quad, not the sprite's tight mesh: the outline really extends outside it.
            _baseQuad = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1,
                0, SpriteMeshType.FullRect);
            baseRenderer.sprite = _baseQuad;
            baseRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            // All floor bases stay behind all same-order cell fills in both modes.
            // Neighbouring opaque fills intentionally occlude individual shared sides.
            baseRenderer.sortingOrder = spriteRenderer.sortingOrder + Mathf.Min(-1, _config.BaseSortingOrderOffset);
            baseRenderer.color = _config.BaseColor;
            UpdateBaseOutline();
        }

        private void UpdateBaseOutline()
        {
            if (_baseMaterial == null || spriteRenderer.sprite == null) return;
            var bounds = spriteRenderer.sprite.bounds;
            var source = spriteRenderer.transform;
            var size = Vector2.Scale(bounds.size, source.localScale);
            size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
            var thickness = Mathf.Min(size.x, size.y) * Mathf.Max(0, _baseScale - 1) * .5f;
            baseRenderer.transform.localPosition = source.localPosition + Vector3.Scale(bounds.center, source.localScale);
            baseRenderer.transform.localRotation = source.localRotation;
            baseRenderer.transform.localScale = new Vector3(size.x + 2 * thickness, size.y + 2 * thickness, 1);
            _baseProperties ??= new MaterialPropertyBlock();
            _baseProperties.SetVector("_CellSize", size);
            _baseProperties.SetVector("_Edges", _config.CheckAdjacentCellsForOutline ? _baseEdges : Vector4.one);
            _baseProperties.SetVector("_Diagonals", _config.CheckAdjacentCellsForOutline ? _viewModel.BaseOutlineDiagonals.CurrentValue : Vector4.zero);
            _baseProperties.SetFloat("_Thickness", thickness);
            _baseProperties.SetFloat("_Radius", Mathf.Min(size.x, size.y) * _config.BaseCornerRadius);
            // Legacy seam settings are retained in serialized assets, but rows now
            // always have a smooth continuous contour when adjacency is checked.
            _baseProperties.SetFloat("_SeamInsetDepth", 0);
            _baseProperties.SetFloat("_SeamInsetWidth", 0);
            baseRenderer.SetPropertyBlock(_baseProperties);
        }

        public void SetBaseOutlineEdges(Vector4 edges)
        {
            _baseEdges = edges; // left, right, top, bottom; independent of atom occupancy
            UpdateBaseOutline();
        }
        
        private void OnArrayIndexChanged(Vector2Int arrayIndex)
        {
            var row = arrayIndex.x;
            var column = arrayIndex.y;
            if (_config.UseDedicatedSprite)
            {
                //white first
                if (row % 2 == 0)
                {
                    spriteRenderer.sprite = column % 2 == 0
                        ? _config.WhitePatterns[0]
                        : _config.BlackPatterns[0];
                }
                //black first
                else
                {
                    spriteRenderer.sprite = column % 2 == 0
                        ? _config.BlackPatterns[1]
                        : _config.WhitePatterns[1];
                }
            }
            else
            {
                //white first
                if (row % 2 == 0)
                {
                    spriteRenderer.color = column % 2 == 0
                        ? _config.WhiteColor
                        : _config.BlackColor;
                }
                //black first
                else
                {
                    spriteRenderer.color = column % 2 == 0
                        ? _config.BlackColor
                        : _config.WhiteColor;
                }
                _originalColor = spriteRenderer.color;
            }

            UpdateBaseOutline();
            UpdateVisualColor(false);
        }

        private void OnCellStateChanged(CellState state)
        {
            _currentState = state;
            UpdateVisualColor(true);
        }

        private void UpdateVisualColor(bool smooth)
        {
            var overlayActive = _currentState != CellState.None || (_hintHighlighted && _hintFlashVisible);
            var overlayColor = CellVisualColorPolicy.Resolve(
                _originalColor,
                _currentState,
                _hintHighlighted,
                _hintFlashVisible,
                _config.CanBePlacedColor,
                _config.CannotBePlacedColor,
                _config.HintFlashColor);
            if (hintMaterialInjector)
            {
                hintMaterialInjector.SetColor("_OverlayColor", overlayColor);
                hintMaterialInjector.SetFloat("_OverlayAmount", overlayActive ? Mathf.Clamp01(_config.OverlayAmount) : 0f);
            }

            // Keep the sprite's base color intact; state/hint colors are applied by the cell-only shader.
            Tween.StopAll(spriteRenderer);
            if (smooth)
                Tween.Color(spriteRenderer, _originalColor, 0.15f);
            else
                spriteRenderer.color = _originalColor;
        }

        public void SetBaseColor(Color color)
        {
            if (baseRenderer != null)
            {
                baseRenderer.color = color;
            }
        }

        public void SetBaseScale(float scale)
        {
            _baseScale = scale;
            UpdateBaseOutline();
        }

        public void SetCellColor(Color color)
        {
            _originalColor = color;
            spriteRenderer.color = color;
        }

        private void Destroy()
        {
            Destroy(gameObject);
        }
    }
}
