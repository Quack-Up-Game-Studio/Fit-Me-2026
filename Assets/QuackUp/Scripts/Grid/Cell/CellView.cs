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
        [SerializeField] private Sprite[] whitePatterns;
        [SerializeField] private Sprite[] blackPatterns;
        [SerializeField] private Color whiteColor;
        [SerializeField] private Color blackColor;
        [SerializeField] private Color canBePlacedColor;
        [SerializeField] private Color cannotBePlacedColor;
        
        private CellViewModel _viewModel;
        private Color _originalColor;
        private IDisposable _bindings;
        #endregion
        
        [Inject]
        public void Construct(CellViewModel viewModel)
        {
            _originalColor = spriteRenderer.color;
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
            _viewModel.TransformData.OnChanged
                .Subscribe(OnTransformDataChanged)
                .AddTo(ref disposableBuilder);
            Observable.FromEvent(
                    h => _viewModel.OnDisposed += h,
                    h => _viewModel.OnDisposed -= h)
                .Subscribe(_ => OnViewModelDisposed())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        private void OnTransformDataChanged(TransformData transformData)
        {
            transform.position = transformData.Position.Value;
            transform.localPosition = transformData.LocalPosition.Value;
            transform.rotation = transformData.Rotation.Value;
            transform.localRotation = transformData.LocalRotation.Value;
            transform.localScale = transformData.LocalScale.Value;
        }

        private void OnArrayIndexChanged(Vector2Int arrayIndex)
        {
            var row = arrayIndex.x;
            var column = arrayIndex.y;
            //white first
            if (row % 2 == 0)
            {
                spriteRenderer.sprite = column % 2 == 0
                    ? whitePatterns[0]
                    : blackPatterns[0];
            }
            //black first
            else
            {
                spriteRenderer.sprite = column % 2 == 0
                    ? blackPatterns[1]
                    : whitePatterns[1];
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
                    spriteRenderer.color = canBePlacedColor;
                    break;
                case CellState.CannotBePlaced:
                    spriteRenderer.color = cannotBePlacedColor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
        
        private void OnViewModelDisposed()
        {
            Destroy(gameObject);
        }
    }
}
