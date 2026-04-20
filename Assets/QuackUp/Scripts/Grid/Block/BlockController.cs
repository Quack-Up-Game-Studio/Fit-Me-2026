using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using PrimeTween;
using QuackUp.Audio;
using QuackUp.Input;
using QuackUp.Utils;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace FitMe.Grid
{
    public class BlockController : IDisposable
    {
        public ReactiveCommand<PointerEventData> BeingDragCommand { get; } = new();
        public ReactiveCommand<PointerEventData> DragCommand { get; } = new();
        public ReactiveCommand<PointerEventData> EndDragCommand { get; } = new();
        public ReactiveCommand<PointerEventData> ClickCommand { get; } = new();
        
        public bool AllowRotation { get; set; } = true;
        
        private readonly BlockManagerConfig _config;
        private readonly GridManager _gridManager;
        private readonly ILevelManager _levelManager;
        private readonly IAudioManager _audioManager;
        private readonly IPointerHandler _pointerHandler;
        
        private BlockInstance _blockInstance;
        private IDisposable _bindings;
        private bool _isActive = true;
        private bool _dragWhileRotating;
        private bool _isRotating;
        private bool _isDragging;
        private bool _dragSequencePlayed;
        private Vector2 _mousePositionDifference;
        private Sequence _blockDragSequence;
        private TimeSpan _blockDragTimeStamp;

        [Inject]
        public BlockController(
            BlockManagerConfig config,
            GridManager gridManager,
            ILevelManager levelManager,
            IAudioManager audioManager,
            IPointerHandler pointerHandler)
        {
            _config = config;
            _gridManager = gridManager;
            _levelManager = levelManager;
            _audioManager = audioManager;
            _pointerHandler = pointerHandler;
        }
        
        public void Initialize(BlockInstance blockInstance)
        {
            _blockInstance = blockInstance;
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
            if (_dragWhileRotating) return;
            if (_levelManager.GameState.CurrentValue is not GameState.PlaceBlock)
            {
                OnEndDrag(eventData);
                return;
            }
            if (_isRotating)
            {
                _dragWhileRotating = true;
                return;
            }
            if (_blockInstance.ViewModel.BlockInteractionState.Value is BlockInteractionState.PlacedOnGrid 
                && !_config.AllowPickUpAfterPlacement) return;
            var position = _blockInstance.GameObject.transform.position;
            var mousePosition = _pointerHandler.MouseWorldPosition;
            _mousePositionDifference = new Vector2(mousePosition.x - position.x,
                mousePosition.y - position.y);
            //ChangeSortingOrder(1);
            _audioManager.PlayAudioOneShot(_blockInstance.Model.BlockPreset.PickupSfx, _blockInstance.GameObject.transform.position);
            _blockInstance.ViewModel.SetSortingLayerCommand.Execute(_config.PickUpSortingLayer);
            _dragSequencePlayed = false;
            _blockDragTimeStamp = TimeSpan.FromSeconds(Time.timeSinceLevelLoad);
        }

        private void OnDrag(PointerEventData eventData)
        {
            if (_dragWhileRotating) return;
            if (_levelManager.GameState.CurrentValue is not GameState.PlaceBlock)
            {
                OnEndDrag(eventData);
                return;
            }
            if (_isRotating)
            {
                _dragWhileRotating = true;
                return;
            }
            if (_blockInstance.ViewModel.BlockInteractionState.Value is BlockInteractionState.PlacedOnGrid 
                && !_config.AllowPickUpAfterPlacement) return;
            _gridManager.ValidatePlacement(_blockInstance.Model);
            var mousePosition = _pointerHandler.MouseWorldPosition;
            var position = (mousePosition - _mousePositionDifference) + _config.BlockDragOffset;
            _blockDragSequence.Stop();
            if (!_dragSequencePlayed)
            {
                var elapsed = TimeSpan.FromSeconds(Time.timeSinceLevelLoad) - _blockDragTimeStamp;
                if (elapsed.TotalSeconds < _config.BlockDragTweenSettings.duration)
                {
                    var settings = _config.BlockDragTweenSettings;
                    settings.duration -= (float)elapsed.TotalSeconds;
                    _blockDragSequence = Sequence.Create()
                        .Group(Tween.Position(_blockInstance.GameObject.transform, new (position, settings)));
                    _blockDragSequence.OnComplete(() => _dragSequencePlayed = true);
                }
                else
                {
                    _dragSequencePlayed = true;
                }
            }
            else
            {
                if (_config.BlockDragInertia <= 0)
                {
                    _blockInstance.GameObject.transform.position = position;
                }
                else
                {
                    var settings = _config.BlockDragTweenSettings;
                    settings.duration = _config.BlockDragInertia;
                    _blockDragSequence = Sequence.Create()
                        .Group(Tween.Position(_blockInstance.GameObject.transform, new (position, settings)));
                }
            }
            if (_isDragging) return; //Prevent unnecessary calculations
            if (_blockInstance.ViewModel.BlockInteractionState.Value is BlockInteractionState.PlacedOnGrid)
                _gridManager.RemoveBlock(_blockInstance, FitType.None, false).Forget();
            _blockInstance.ViewModel.BlockInteractionState.Value = BlockInteractionState.PickUp;
            _isDragging = true;
        }

        private void OnEndDrag(PointerEventData eventData)
        {
            if (_dragWhileRotating)
            {
                _dragWhileRotating = false;
                return;
            }
            if (_levelManager.GameState.CurrentValue is not GameState.PlaceBlock) return;
            if (!_isDragging) return;
            if (_isRotating)
            {
                _dragWhileRotating = true;
                return;
            }
            _blockDragSequence.Stop();
            var placed = _gridManager.TryPlaceBlock(_blockInstance);
            if (placed)
            {
                //_model.BlockInteractionState.Value = BlockInteractionState.PlacedOnGrid;
                _blockInstance.ViewModel.SetSortingLayerCommand.Execute(_config.GridSortingLayer);
                _audioManager.PlayAudioOneShot(_config.PlaceSucceedSfx, Vector3.zero);
            }
            else
            {
                _audioManager.PlayAudioOneShot(_config.PlaceFailSfx, Vector3.zero);
                _blockInstance.ViewModel.SetSortingLayerCommand.Execute(_config.SpawnSortingLayer);
                _blockInstance.ViewModel.BlockInteractionState.Value = BlockInteractionState.PlacedOnSpawn;
            }
            _mousePositionDifference = Vector3.zero;
            _isDragging = false;
        }
        
        private async UniTask OnClickToRotate(PointerEventData eventData)
        {
            if (!AllowRotation) return;
            if (_levelManager.GameState.CurrentValue is not GameState.PlaceBlock) return;
            if (_isDragging) return;
            if (_blockInstance.ViewModel.BlockInteractionState.Value is BlockInteractionState.PlacedOnGrid) return;
            var rotateClockwise = _config.RotateClockwise ? -1f : 1f;
            var rotation = Quaternion.Euler(0f, 0f,
                _blockInstance.GameObject.transform.rotation.eulerAngles.z + rotateClockwise * 90f);
            _isRotating = true;
            var promise = new Promise<Unit>();
            _blockInstance.ViewModel.RotateCommand.Execute(new(promise, rotation));
            await promise.Task;
            _isRotating = false;
        }
        #endregion

        public void SetActive(bool isActive)
        {
            _isActive = isActive;
        }
    }
}