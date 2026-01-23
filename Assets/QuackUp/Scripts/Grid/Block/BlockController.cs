using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using QuackUp.Audio;
using QuackUp.Input;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace FitMe.Grid
{
    public interface IBlockController
    {
        void SetActive(bool isActive);
    }
    
    public class BlockController : IDisposable, IBlockController
    {
        public ReactiveCommand<PointerEventData> BeingDragCommand { get; } = new();
        public ReactiveCommand<PointerEventData> DragCommand { get; } = new();
        public ReactiveCommand<PointerEventData> EndDragCommand { get; } = new();
        public ReactiveCommand<PointerEventData> ClickCommand { get; } = new();
        
        private readonly BlockConfig _config;
        private readonly GridManager _gridManager;
        private readonly BlockModel _model;
        private readonly IAudioManager _audioManager;
        private readonly IPointerHandler _pointerHandler;
        
        private IDisposable _bindings;
        private bool _isActive;
        private bool _isRotating;
        private bool _isDragging;
        private Vector2 _mousePositionDifference;

        [Inject]
        public BlockController(
            BlockConfig config,
            GridManager gridManager,
            BlockModel model,
            IAudioManager audioManager,
            IPointerHandler pointerHandler)
        {
            _config = config;
            _gridManager = gridManager;
            _model = model;
            model.BlockController = this;
            _audioManager = audioManager;
            _pointerHandler = pointerHandler;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            BeingDragCommand
                .Where(x => _isActive && x.button is PointerEventData.InputButton.Left)
                .Subscribe(OnBeginDrag)
                .AddTo(ref disposableBuilder);
            DragCommand
                .Where(x => _isActive && x.button is PointerEventData.InputButton.Left)
                .Subscribe(OnDrag)
                .AddTo(ref disposableBuilder);
            EndDragCommand
                .Where(x => _isActive && x.button is PointerEventData.InputButton.Left)
                .Subscribe(OnEndDrag)
                .AddTo(ref disposableBuilder);
            ClickCommand
                .Where(x => _isActive && x.button is PointerEventData.InputButton.Left)
                .SubscribeAwait((x, _) => OnClickToRotate(x), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
        }

        #region Interactions
        private void OnBeginDrag(PointerEventData eventData)
        {
            if (GameStatic.CurrentGameState is GameState.GameOver or GameState.GameClear)
            {
                OnEndDrag(eventData);
                return;
            }
            if (GameStatic.CurrentGameState is not GameState.PlaceBlock) return;
            if (_isRotating) return;
            if (_model.BlockInteractionState.Value is BlockInteractionState.PlacedOnGrid 
                && !_config.AllowPickUpAfterPlacement) return;
            var position = _model.BlockView.Transform.position;
            var mousePosition = _pointerHandler.MouseWorldPosition;
            _mousePositionDifference = new Vector2(mousePosition.x - position.x,
                mousePosition.y - position.y);
            //ChangeSortingOrder(1);
            //AudioManager.Instance.PlayAudioOneShot(BlockPreset.PickupSfx, transform.position);
            _model.SetSortingLayerCommand.Execute(_config.PickUpSortingLayer);
        }

        private void OnDrag(PointerEventData eventData)
        {
            if (GameStatic.CurrentGameState is GameState.GameOver or GameState.GameClear)
            {
                OnEndDrag(eventData);
                return;
            }
            if (GameStatic.CurrentGameState is not GameState.PlaceBlock) return;
            if (_isRotating) return;
            if (_model.BlockInteractionState.Value is BlockInteractionState.PlacedOnGrid 
                && !_config.AllowPickUpAfterPlacement) return;
            _gridManager.ValidatePlacement(_model);
            var mousePosition = _pointerHandler.MouseWorldPosition;
            var position = mousePosition - _mousePositionDifference;
            _model.BlockView.Transform.position = position;
            if (_isDragging) return; //Prevent unnecessary calculations
            if (_model.BlockInteractionState.Value is BlockInteractionState.PlacedOnGrid)
                _gridManager.RemoveBlock(_model, FitType.None, false).Forget();
            _model.BlockInteractionState.Value = BlockInteractionState.PickUp;
            _isDragging = true;
        }

        private void OnEndDrag(PointerEventData eventData)
        {
            if (GameStatic.CurrentGameState is GameState.CountOff or GameState.Pause) return;
            if (!_isDragging) return;
            if (_isRotating) return;
            var placed = _gridManager.TryPlaceBlock(_model);
            if (placed)
            {
                _model.BlockInteractionState.Value = BlockInteractionState.PlacedOnGrid;
                _model.SetSortingLayerCommand.Execute(_config.OriginalSortingLayer);
                _audioManager.PlayAudioOneShot(_config.PlaceSucceedSfx, Vector3.zero);
                _mousePositionDifference = Vector3.zero;
            }
            else
            {
                _audioManager.PlayAudioOneShot(_config.PlaceFailSfx, Vector3.zero);
                _model.BlockInteractionState.Value = BlockInteractionState.PlacedOnSpawn;
            }
            _isDragging = false;
        }
        
        private async UniTask OnClickToRotate(PointerEventData eventData)
        {
            if (GameStatic.CurrentGameState is GameState.CountOff or GameState.Pause or GameState.GameOver) return;
            if (_isDragging) return;
            if (_model.BlockInteractionState.Value is BlockInteractionState.PlacedOnGrid) return;
            var rotateClockwise = _config.RotateClockwise ? -1f : 1f;
            var rotation = Quaternion.Euler(0f, 0f,
                _model.BlockView.Transform.rotation.eulerAngles.z + rotateClockwise * 90f);
            _isRotating = true;
            await _model.BlockView.Rotate(rotation);
            _isRotating = false;
        }
        #endregion

        public void SetActive(bool isActive)
        {
            _isActive = isActive;
        }
    }
}