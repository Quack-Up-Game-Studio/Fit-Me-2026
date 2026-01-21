using System;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    public interface ICellView : ITransformProvider
    {
        void Destroy();
    }
    public class CellView : MonoBehaviour, IDisposable, ICellView
    {
        #region Inspectors
        [Title("References")]
        [SerializeField] private bool useDedicatedSprite = true;
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
        
        public Transform Transform => transform;
        
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
            _bindings = disposableBuilder.Build();
        }
        
        private void OnArrayIndexChanged(Vector2Int arrayIndex)
        {
            var row = arrayIndex.x;
            var column = arrayIndex.y;
            if (useDedicatedSprite)
            {
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
            else
            {
                //white first
                if (row % 2 == 0)
                {
                    spriteRenderer.color = column % 2 == 0
                        ? whiteColor
                        : blackColor;
                }
                //black first
                else
                {
                    spriteRenderer.color = column % 2 == 0
                        ? blackColor
                        : whiteColor;
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
                    spriteRenderer.color = canBePlacedColor;
                    break;
                case CellState.CannotBePlaced:
                    spriteRenderer.color = cannotBePlacedColor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public void Destroy()
        {
            Destroy(gameObject);
        }
    }
}
