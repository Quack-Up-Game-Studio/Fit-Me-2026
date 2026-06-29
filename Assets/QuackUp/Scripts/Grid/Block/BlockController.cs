using System;
using System.Threading;
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
        public ReactiveCommand<PointerEventData> PointerDownCommand { get; } = new();
        public ReactiveCommand<PointerEventData> PointerUpCommand { get; } = new();
        public ReactiveCommand<PointerEventData> DragCommand { get; } = new();
        public ReactiveCommand<PointerEventData> EndDragCommand { get; } = new();
        public ReactiveCommand<PointerEventData> ClickCommand { get; } = new();
        
        public bool AllowRotation { get; set; } = true;
        public bool AllowDrag { get; set; } = true;
        public bool IsRotating => _isRotating;
        public Observable<Unit> OnRotate => _onRotate;
        
        private readonly Subject<Unit> _onRotate = new();
        private readonly BlockManagerConfig _config;
        private readonly GridManager _gridManager;
        private readonly IGameStateManager _gameStateManager;
        private readonly IAudioManager _audioManager;
        private readonly IPointerHandler _pointerHandler;
        
        private BlockInstance _blockInstance;
        private IDisposable _bindings;
        private bool _isActive = true;
        private bool _dragWhileRotating;
        private bool _isRotating;
        private bool _isDragging;
        private bool _isPressedDown;
        private bool _isPickedUp;
        private bool _dragSequencePlayed;
        private Vector2 _mousePositionDifference;
        private Vector2 _dragOffset;
        private Sequence _blockDragSequence;
        private TimeSpan _blockDragTimeStamp;
        private CancellationTokenSource _longTapCts;

        [Inject]
        public BlockController(
            BlockManagerConfig config,
            GridManager gridManager,
            IGameStateManager gameStateManager,
            IAudioManager audioManager,
            IPointerHandler pointerHandler)
        {
            _config = config;
            _gridManager = gridManager;
            _gameStateManager = gameStateManager;
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
            PointerDownCommand
                .Where(x => _isActive && x.button is PointerEventData.InputButton.Left)
                .Subscribe(OnPointerDown)
                .AddTo(ref disposableBuilder);
            PointerUpCommand
                .Where(x => _isActive && x.button is PointerEventData.InputButton.Left)
                .Subscribe(OnPointerUp)
                .AddTo(ref disposableBuilder);
            DragCommand
                .Where(x => _isActive && x.button is PointerEventData.InputButton.Left)
                .Subscribe(OnDrag)
                .AddTo(ref disposableBuilder);
            EndDragCommand
                .Where(x => _isActive && x.button is PointerEventData.InputButton.Left)
                .Subscribe(OnEndDrag)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
            _longTapCts?.Cancel();
            _longTapCts?.Dispose();
        }

        #region Interactions
        private void OnPointerDown(PointerEventData eventData)
        {
            if (_dragWhileRotating) return;
            if (_gameStateManager.GameState.CurrentValue is not GameState.PlaceBlock ||
                _gameStateManager.IsPaused.CurrentValue) return;
            if (_isRotating) return;
            if (_blockInstance.ViewModel.BlockInteractionState.Value is BlockInteractionState.PlacedOnGrid 
                && !_config.AllowPickUpAfterPlacement) return;
            
            _isPressedDown = true;
            _isDragging = false;
            _isPickedUp = false;
            
            if (!AllowDrag) return;
            StartLongTapTimer(eventData).Forget();
        }

        private async UniTaskVoid StartLongTapTimer(PointerEventData eventData)
        {
            _longTapCts?.Cancel();
            _longTapCts?.Dispose();
            _longTapCts = new CancellationTokenSource();
            var token = _longTapCts.Token;
            
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_config.LongTapDuration), cancellationToken: token);
                if (_isPressedDown && !_isDragging && !_isPickedUp)
                {
                    PickUpBlock(eventData);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void PickUpBlock(PointerEventData eventData)
        {
            if (_isPickedUp) return;
            _isPickedUp = true;

            var position = _blockInstance.GameObject.transform.position;
            var mousePosition = _pointerHandler.MouseWorldPosition;
            _mousePositionDifference = new Vector2(mousePosition.x - position.x,
                mousePosition.y - position.y);
            
            _audioManager.PlayAudioOneShot(_blockInstance.Model.BlockPreset.PickupSfx, _blockInstance.GameObject.transform.position);
            _blockInstance.ViewModel.SetSortingLayerCommand.Execute(_config.PickUpSortingLayer);
            _dragSequencePlayed = false;
            _blockDragTimeStamp = TimeSpan.FromSeconds(Time.timeSinceLevelLoad);

            UpdateDragOffset(eventData);
            UpdatePosition(eventData);
        }

        private void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPressedDown) return;
            _isPressedDown = false;
            
            _longTapCts?.Cancel();
            
            if (_isPickedUp)
            {
                if (!_isDragging)
                {
                    _blockDragSequence.Stop();
                    _blockInstance.ViewModel.BlockInteractionState.Value = BlockInteractionState.PlacedOnSpawn;
                    _mousePositionDifference = Vector3.zero;
                    _dragOffset = Vector2.zero;
                    _isPickedUp = false;
                }
            }
            else
            {
                RotateBlock().Forget();
            }
        }

        private void OnBeginDrag(PointerEventData eventData)
        {
            if (!AllowDrag) return;
            if (_dragWhileRotating) return;
            if (_gameStateManager.GameState.CurrentValue is not GameState.PlaceBlock ||
                _gameStateManager.IsPaused.CurrentValue)
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

            _longTapCts?.Cancel();
            _isDragging = true;
            PickUpBlock(eventData);
        }

        private void OnDrag(PointerEventData eventData)
        {
            if (!AllowDrag) return;
            if (_dragWhileRotating) return;
            if (_gameStateManager.GameState.CurrentValue is not GameState.PlaceBlock ||
                _gameStateManager.IsPaused.CurrentValue)
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

            _longTapCts?.Cancel();
            if (!_isDragging)
            {
                _isDragging = true;
            }
            PickUpBlock(eventData);
            
            UpdateDragOffset(eventData);
            UpdatePosition(eventData);
        }

        private void UpdatePosition(PointerEventData eventData)
        {
            _gridManager.ValidatePlacement(_blockInstance.Model);
            var mousePosition = _pointerHandler.MouseWorldPosition;
            var position = (mousePosition - _mousePositionDifference) + _dragOffset;
            _blockDragSequence.Stop();
            if (!_dragSequencePlayed)
            {
                if (!_config.UseBlockDragTween)
                {
                    _dragSequencePlayed = true;
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
                else
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
            if (_blockInstance.ViewModel.BlockInteractionState.Value is BlockInteractionState.PickUp) return;
            if (_blockInstance.ViewModel.BlockInteractionState.Value is BlockInteractionState.PlacedOnGrid)
                _gridManager.RemoveBlock(_blockInstance, FitType.None, false).Forget();
            _blockInstance.ViewModel.BlockInteractionState.Value = BlockInteractionState.PickUp;
        }

        private void UpdateDragOffset(PointerEventData eventData)
        {
            _dragOffset = Vector2.zero;
            if (_config.UseDynamicDragOffset)
            {
                var normalizedY = Mathf.Clamp01(eventData.position.y / Mathf.Max(1f, Screen.height));
                var curveY = _config.DynamicDragOffsetCurve != null ? _config.DynamicDragOffsetCurve.Evaluate(normalizedY) : normalizedY;
                var dynamicY = Mathf.Lerp(_config.DynamicOffsetY.x, _config.DynamicOffsetY.y, curveY);
                
                var middleX = Screen.width / 2f;
                var normalizedX = (eventData.position.x - middleX) / Mathf.Max(1f, middleX);
                normalizedX = Mathf.Clamp(normalizedX, -1f, 1f);
                var curveX = _config.DynamicDragOffsetCurve != null ? _config.DynamicDragOffsetCurve.Evaluate(Mathf.Abs(normalizedX)) : Mathf.Abs(normalizedX);
                var dynamicX = Mathf.Lerp(_config.DynamicOffsetX.x, _config.DynamicOffsetX.y, curveX);
                if (normalizedX < 0f)
                {
                    dynamicX = -dynamicX;
                }
                
                _dragOffset.y = dynamicY;
                _dragOffset.x = dynamicX;
            }
        }

        private void OnEndDrag(PointerEventData eventData)
        {
            if (!AllowDrag) return;
            if (_dragWhileRotating)
            {
                _dragWhileRotating = false;
                return;
            }
            if (_gameStateManager.GameState.CurrentValue is not GameState.PlaceBlock ||
                _gameStateManager.IsPaused.CurrentValue) return;
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
                _blockInstance.ViewModel.BlockInteractionState.Value = BlockInteractionState.PlacedOnSpawn;
            }
            _mousePositionDifference = Vector3.zero;
            _dragOffset = Vector2.zero;
            _isDragging = false;
            _isPressedDown = false;
            _isPickedUp = false;
        }
        
        private async UniTaskVoid RotateBlock()
        {
            if (AllowRotation && 
                _gameStateManager.GameState.CurrentValue is GameState.PlaceBlock &&
                !_gameStateManager.IsPaused.CurrentValue &&
                !_isRotating &&
                _blockInstance.ViewModel.BlockInteractionState.Value is not BlockInteractionState.PlacedOnGrid)
            {
                var rotateClockwise = _config.RotateClockwise ? -1f : 1f;
                var rotation = Quaternion.Euler(0f, 0f,
                    _blockInstance.GameObject.transform.rotation.eulerAngles.z + rotateClockwise * 90f);
                _isRotating = true;
                var promise = new Promise<Unit>();
                _blockInstance.ViewModel.RotateCommand.Execute(new(promise, rotation));
                await promise.Task;
                _isRotating = false;
                _onRotate?.OnNext(Unit.Default);
            }
        }
        #endregion

        public void SetActive(bool isActive)
        {
            _isActive = isActive;
        }
    }
}