using System;
using FitMe.Shared;
using QuackUp.Audio;
using QuackUp.Input;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FitMe.Grid
{
    public class BlockController : IDisposable
    {
        public ReactiveCommand<PointerEventData> BeingDragCommand { get; } = new();
        public ReactiveCommand<PointerEventData> DragCommand { get; } = new();
        public ReactiveCommand<PointerEventData> EndDragCommand { get; } = new();

        private readonly BlockConfig _config;
        private readonly BlockModel _model;
        private readonly GridManager _gridManager;
        private readonly IAudioManager _audioManager;
        private readonly IPointerHandler _pointerHandler;
        
        private IDisposable _bindings;
        private bool _isDragging;
        private Vector2 _mousePositionDifference;

        public BlockController(
            BlockConfig config,
            BlockModel model, 
            GridManager gridManager,
            IAudioManager audioManager,
            IPointerHandler inputHandler)
        {
            _config = config;
            _model = model;
            _gridManager = gridManager;
            _audioManager = audioManager;
            _pointerHandler = inputHandler;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            BeingDragCommand
                .Where(x => x.button is PointerEventData.InputButton.Left)
                .Subscribe(OnBeginDrag)
                .AddTo(ref disposableBuilder);
            DragCommand
                .Where(x => x.button is PointerEventData.InputButton.Left)
                .Subscribe(OnDrag)
                .AddTo(ref disposableBuilder);
            EndDragCommand
                .Where(x => x.button is PointerEventData.InputButton.Left)
                .Subscribe(OnEndDrag)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
        }

        #region Interactions
        /// <summary>
        /// Handle rotation of the block
        /// </summary>
        private void HandleBlockManipulation()
        {
            
        }

        private void OnBeginDrag(PointerEventData eventData)
        {
            if (GameStatic.CurrentGameState is GameState.GameOver or GameState.GameClear)
            {
                OnEndDrag(eventData);
                return;
            }
            if (GameStatic.CurrentGameState is not GameState.PlaceBlock) return;
            if (_model.BlockInteractionState.Value is BlockInteractionState.Placed 
                && !_config.AllowPickUpAfterPlacement) return;
            var position = _model.TransformData.Position.Value;
            var mousePosition = _pointerHandler.MouseWorldPosition;
            _mousePositionDifference = new Vector2(mousePosition.x - position.x,
                mousePosition.y - position.y);
            //ChangeSortingOrder(1);
            //AudioManager.Instance.PlayAudioOneShot(BlockPreset.PickupSfx, transform.position);
            _model.BlockView.SetSortingLayer(_config.PickUpSortingLayer);
        }

        private void OnDrag(PointerEventData eventData)
        {
            if (GameStatic.CurrentGameState is GameState.GameOver or GameState.GameClear)
            {
                OnEndDrag(eventData);
                return;
            }
            if (GameStatic.CurrentGameState is not GameState.PlaceBlock) return;
            if (_model.BlockInteractionState.Value is BlockInteractionState.Placed 
                && !_config.AllowPickUpAfterPlacement) return;
            HandleBlockManipulation();
            _gridManager.ValidatePlacement(_model);
            var mousePosition = _pointerHandler.MouseWorldPosition;
            var position = mousePosition - _mousePositionDifference;
            _model.TransformData.Position.Value = position;
            if (_isDragging) return; //Prevent unnecessary calculations
            _model.BlockInteractionState.Value = BlockInteractionState.PickUp;
            //GridManager.Instance.RemoveBlock(this);
            _isDragging = true;
        }

        private void OnEndDrag(PointerEventData eventData)
        {
            if (GameStatic.CurrentGameState is GameState.CountOff or GameState.Pause) return;
            if (!_isDragging) return;
            var placed = _gridManager.TryPlaceBlock(_model);
            if (placed)
            {
                _model.BlockInteractionState.Value = BlockInteractionState.Placed;
                _audioManager.PlayAudioOneShot(_config.PlaceSucceedSfx, Vector3.zero);
                _mousePositionDifference = Vector3.zero;
            }
            else
            {
                _audioManager.PlayAudioOneShot(_config.PlaceFailSfx, Vector3.zero);
                _model.BlockInteractionState.Value = BlockInteractionState.None;
            }
            _isDragging = false;
        }
        #endregion
    }
}