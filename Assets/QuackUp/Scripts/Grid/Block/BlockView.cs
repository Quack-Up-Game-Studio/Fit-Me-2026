using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using PrimeTween;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace FitMe.Grid
{
    [ShowOdinSerializedPropertiesInInspector]
    public class BlockView : SerializedMonoBehaviour, IDisposable, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        #region Inspectors
        [Title("References")]
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private SpriteRenderer obstacleSpriteRenderer;
        //[SerializeField] private SpriteRenderer infectedSpriteRenderer;
        
        [Title("Tween")] 
        [SerializeField] private TweenSettings scaleTweenSettings;
        #endregion

        #region Fields and Properties
        
        public Transform Transform => transform;
        
        private Transform _originalParent;
        private Vector3 _originalPosition;
        private Vector3 _originalEulerAngles;
        private int _originalSortingLayer;
        private Vector3 _mousePositionDifference;
        private Tween _transformTween;
        private BlockColor _blockColor;
        private Vector3 _originalScale;
        private Tween _pickUpTween;
        private IDisposable _switchIdleTimer;
        private CancellationTokenSource _switchIdleCts;
        
        private BlockConfig _blockConfig;
        private BlockManagerConfig _blockManagerConfig;
        private GridManagerConfig _gridConfig;
        private BlockController _blockController;
        private BlockViewModel _viewModel;
        private IDisposable _bindings;
        #endregion

        [Inject]
        public void Construct(
            BlockConfig blockConfig,
            BlockManagerConfig blockManagerConfig,
            GridManagerConfig gridConfig,
            BlockController blockController,
            BlockViewModel viewModel)
        {
            _blockConfig = blockConfig;
            _blockManagerConfig = blockManagerConfig;
            _gridConfig = gridConfig;
            _blockController = blockController;
            _viewModel = viewModel;
            _originalParent = transform.parent;
            _originalPosition = transform.position;
            _originalEulerAngles = transform.eulerAngles;
            _originalSortingLayer = meshRenderer.sortingLayerID;
            Bind();
            Initialize();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.BlockInteractionState
                .IgnoreFirstValueWhenSubscribe()
                .DistinctUntilChanged()
                .Subscribe(OnInteractionStateChanged)
                .AddTo(ref disposableBuilder);
            _viewModel.BlockState
                .DistinctUntilChanged()
                .Subscribe(OnBlockStateChanged)
                .AddTo(ref disposableBuilder); 
            _viewModel.BlockColor
                .IgnoreFirstValueWhenSubscribe()
                .Subscribe(OnBlockColorChanged)
                .AddTo(ref disposableBuilder);
            _viewModel.SetSortingLayerCommand
                .Subscribe(OnSetSortingLayer)
                .AddTo(ref disposableBuilder);
            _viewModel.SetSortingOrderCommand
                .Subscribe(OnSetSortingOrder)
                .AddTo(ref disposableBuilder);
            _viewModel.ScaleInCommand
                .SubscribeAwait( (param, _) =>  ScaleIn(param), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            _viewModel.RotateCommand
                .SubscribeAwait((param, _) => Rotate(param), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            _viewModel.ExplodeCommand
                .SubscribeAwait( (param, _) => Explode(param), AwaitOperation.Drop)
                .AddTo(ref disposableBuilder);
            _viewModel.DestroyCommand
                .Subscribe(_ => Destroy())
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public void Dispose()
        {
            CancelIdleTimer();
            _bindings?.Dispose();
        }
        
        private void OnDestroy()
        {
            Dispose();
        }
        
        private void OnInteractionStateChanged(BlockInteractionState state)
        {
            switch (state)
            {
                case BlockInteractionState.PlacedOnSpawn:
                    ReturnToSpawn();
                    break;
                case BlockInteractionState.PickUp:
                    PickUp();
                    break;
                case BlockInteractionState.PlacedOnGrid:
                    Place(_gridConfig.CellSize);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void OnBlockStateChanged(BlockState state)
        {
            if (state is BlockState.Obstacle)
            {
                obstacleSpriteRenderer.enabled = true;
                obstacleSpriteRenderer.sprite = _blockConfig.ObstacleSprite;
                meshRenderer.enabled = false;
            }
            else
            {
                obstacleSpriteRenderer.enabled = false;
                meshRenderer.enabled = true;
            }
        }

        #region Initalization
        private void Initialize()
        {
            skeletonAnimation.skeletonDataAsset = _blockConfig.SkeletonDataAsset;
            skeletonAnimation.Initialize(true);
            skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.IdleAnimations[0], true);
            StartIdleTimer();
        }
        #endregion
        
        private void StartIdleTimer()
        {
            var randomSwitchTime = _blockManagerConfig.SwitchIdleTimeRange.RandomWithinRange();
            _switchIdleCts = new CancellationTokenSource();
            _switchIdleTimer = Observable.Timer(TimeSpan.FromSeconds(randomSwitchTime), _switchIdleCts.Token)
                .Subscribe(_ =>
                {
                    SwitchIdle(_switchIdleCts.Token).ContinueWith(() =>
                    {
                        if (_switchIdleCts.Token.IsCancellationRequested) return;
                        CancelIdleTimer();
                        StartIdleTimer();
                    });
                });
        }

        private async UniTask SwitchIdle(CancellationToken cancellationToken)
        {
            await skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.IdleAnimations[1], false)
                .WaitUntilComplete(cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.IdleAnimations[0], true);
        }
        
        private void CancelIdleTimer()
        {
            _switchIdleTimer?.Dispose();
            _switchIdleCts?.Cancel();
            _switchIdleCts?.Dispose();
            _switchIdleTimer = null;
            _switchIdleCts = null;
        }

        private void PickUp()
        {
            DebugUtils.Log("Block picked up");
            if (_transformTween.isAlive)
            {
                _transformTween.Stop();
            }
            transform.SetParent(null);
            var gridSize = _gridConfig.CellSize;
            _pickUpTween = Tween.Scale(transform, gridSize, 0.2f);
            CancelIdleTimer();
            //_pickUpTween = Tween.Scale(transform, _originalScale * pickUpScaleMultiplier, 0.2f);
            skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.PickUpAnimation, true);
        }
        
        private void Place(Vector3 scale)
        {
            _pickUpTween.Stop();
            _pickUpTween = Tween.Scale(transform, scale, 0.2f);
            skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.IdleAnimations[0], true);
            StartIdleTimer();
        }
        
        private async UniTask Explode(ExplodeCommandData data)
        {
            DebugUtils.Log($"Block {_blockColor} exploded at position {transform.position}");
            CancelIdleTimer();
            var speedMultiplier = data.FitType == FitType.FitMe ? 2f : 6.67f;
            var explodeAnim = skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.ExplodeAnimation, false);
            explodeAnim.TimeScale *= speedMultiplier;
            await explodeAnim.WaitUntilComplete(); 
            if (_blockConfig.ExplodeVfx.TryGetValue(_blockColor, out var vfx))
            {
                var vfxInstance = Instantiate(vfx, transform.position, Quaternion.identity);
                vfxInstance.Play(true);
            }
            else
            {
                DebugUtils.LogWarning($"No explosion VFX found for block type: {_blockColor}");
            }
            if (data.Destroy) Destroy(gameObject);
            data.Promise.TrySetResult(Unit.Default);
        }

        private void OnSetSortingLayer(int layer)
        {
            meshRenderer.sortingLayerID = layer;
            obstacleSpriteRenderer.sortingLayerID = layer;
        }

        private void OnSetSortingOrder(int order)
        {
            meshRenderer.sortingOrder = order;
            obstacleSpriteRenderer.sortingOrder = order;
        }

        private void OnBlockColorChanged(BlockColor color)
        {
            _blockColor = color;
            if (!_blockConfig.SkinDictionary.TryGetValue(color, out var skin))
            {
                DebugUtils.LogWarning($"No skin found for block type: {color}");
                return;
            }
            DebugUtils.Log($"Block {_blockColor} set to {skin}");
            skeletonAnimation.Skeleton.SetSkin(skin);
            skeletonAnimation.Skeleton.SetSlotsToSetupPose();
        }

        /// <summary>
        /// Return the block to its original position, rotation and scale
        /// </summary>
        private void ReturnToSpawn()
        {
            if (_transformTween.isAlive)
            {
                _transformTween.Stop();
            }
            transform.SetParent(_originalParent);
            //OnSetSortingLayer(_originalSortingLayer);
            _transformTween = Tween.Position(transform, _originalPosition, 0.2f);
            Tween.Rotation(transform, _originalEulerAngles, 0.2f);
            Place(_originalScale);
        }

        private async UniTask ScaleIn(ScaleInCommandData data)
        {
            _originalScale = data.Scale;
            var sequence = Tween.Scale(transform, new TweenSettings<Vector3>(data.Scale, scaleTweenSettings));
            await sequence.ToUniTask();
            data.Promise.TrySetResult(Unit.Default);
        }
        
        private async UniTask Rotate(RotateCommandData data)
        {
            _originalEulerAngles = data.Rotation.eulerAngles;
            var tween = Tween.Rotation(transform, data.Rotation, 0.5f);
            await tween.ToUniTask();
            data.Promise.TrySetResult(Unit.Default);
        }

        private void Destroy()
        {
            Destroy(gameObject);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _blockController.BeingDragCommand.Execute(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _blockController.DragCommand.Execute(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _blockController.EndDragCommand.Execute(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _blockController.ClickCommand.Execute(eventData);
        }
    }
}