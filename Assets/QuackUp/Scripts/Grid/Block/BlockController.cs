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
        private readonly IPlayerInputHandler _inputHandler;
        
        private IDisposable _bindings;
        private bool _isDragging;
        private Vector3 _mousePositionDifference;

        public BlockController(
            BlockConfig config,
            BlockModel model, 
            GridManager gridManager,
            IAudioManager audioManager,
            IPlayerInputHandler inputHandler)
        {
            _config = config;
            _model = model;
            _gridManager = gridManager;
            _audioManager = audioManager;
            _inputHandler = inputHandler;
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
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (GameStatic.CurrentGameState is GameState.GameOver or GameState.GameClear)
            {
                OnEndDrag(eventData);
                return;
            }
            if (GameStatic.CurrentGameState is not GameState.PlaceBlock) return;
            if (_model.BlockInteractionState.Value is BlockInteractionState.Placed 
                && !_config.AllowPickUpAfterPlacement) return;
            var position = transform.position;
            var mousePosition = PointerManager.Instance.MouseWorldPosition;
            _mousePositionDifference = new Vector3(mousePosition.x - position.x,
                mousePosition.y - position.y, 0);
            //ChangeSortingOrder(1);
            //AudioManager.Instance.PlayAudioOneShot(BlockPreset.PickupSfx, transform.position);
            SetSortingLayer(pickUpSortingLayer);
        }

        public void OnDrag(PointerEventData eventData)
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
            var mousePosition = PointerManager.Instance.MouseWorldPosition;
            transform.position = mousePosition - _mousePositionDifference;
            if (_isDragging) return; //Prevent unnecessary calculations
            PickUpBlock();
            //GridManager.Instance.RemoveBlock(this);
            _isDragging = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (GameStatic.CurrentGameState is GameState.CountOff or GameState.Pause) return;
            if (!_isDragging) return;
            var placed = _gridManager.PlaceBlock(_model);
            if (placed)
            {
                _model.BlockInteractionState.Value = BlockInteractionState.Placed;
                _audioManager.PlayAudioOneShot(_config.PlaceSucceedSfx, Vector3.zero);
                _mousePositionDifference = Vector3.zero;
            }
            else
            {
                _audioManager.PlayAudioOneShot(_config.PlaceFailSfx, Vector3.zero);
                ReturnToOriginal();
                _model.BlockInteractionState.Value = BlockInteractionState.None;
            }
            _isDragging = false;
        }
        #endregion
    }
}