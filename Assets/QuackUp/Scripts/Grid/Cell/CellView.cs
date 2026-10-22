using System;
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
        
        private CellViewModel _viewModel;
        private CellConfig _config;
        private Color _originalColor;
        private CellState _currentState = CellState.None;
        private bool _hintHighlighted;
        private bool _hintFlashVisible;
        private IDisposable _bindings;
        #endregion
        
        public Transform Transform => transform;
        
        [Inject]
        public void Construct(
            CellConfig config,
            CellViewModel viewModel)
        {
            _originalColor = spriteRenderer.color;
            _config = config;
            _viewModel = viewModel;
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
            _bindings = disposableBuilder.Build();
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
                    _originalColor = column % 2 == 0
                        ? _config.WhiteColor
                        : _config.BlackColor;
                }
                //black first
                else
                {
                    _originalColor = column % 2 == 0
                        ? _config.BlackColor
                        : _config.WhiteColor;
                }
            }
            UpdateVisualColor(false);
        }

        private void OnCellStateChanged(CellState state)
        {
            _currentState = state;
            UpdateVisualColor(true);
        }

        private void UpdateVisualColor(bool smooth)
        {
            var targetColor = CellVisualColorPolicy.Resolve(
                _originalColor,
                _currentState,
                _hintHighlighted,
                _hintFlashVisible,
                _config.CanBePlacedColor,
                _config.CannotBePlacedColor,
                _config.HintFlashColor);
            Tween.StopAll(spriteRenderer);
            if (smooth)
                Tween.Color(spriteRenderer, targetColor, 0.15f);
            else
                spriteRenderer.color = targetColor;
        }

        private void Destroy()
        {
            Destroy(gameObject);
        }
    }
}
