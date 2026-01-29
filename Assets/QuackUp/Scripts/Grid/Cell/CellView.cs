using System;
using QuackUp.Utils;
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
        }

        private void OnCellStateChanged(CellState state)
        {
            switch (state)
            {
                case CellState.None:
                    spriteRenderer.color = _originalColor;
                    break;
                case CellState.CanBePlaced:
                    spriteRenderer.color = _config.CanBePlacedColor;
                    break;
                case CellState.CannotBePlaced:
                    spriteRenderer.color = _config.CannotBePlacedColor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void Destroy()
        {
            Destroy(gameObject);
        }
    }
}
