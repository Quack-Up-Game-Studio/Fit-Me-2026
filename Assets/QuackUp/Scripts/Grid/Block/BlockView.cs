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
    public class BlockView : SerializedMonoBehaviour, IDisposable, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        #region Inspectors
        [Title("References")]
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private SpriteRenderer obstacleSpriteRenderer;
        //[SerializeField] private SpriteRenderer infectedSpriteRenderer;
        
        [Title("Tween")] 
        [SerializeField] private TweenSettings scaleTweenSettings;
        [SerializeField] private TweenSettings rotateScaleTweenSettings;
        [SerializeField] private TweenSettings rotateTweenSettings;
        #endregion

        #region Fields and Properties
        
        public Transform Transform => transform;
        
        private Transform _originalParent;
        private Vector3 _originalPosition;
        private Vector3 _originalEulerAngles;
        private int _originalSortingLayer;
        private int _originalSortingOrder;
        private Vector3 _mousePositionDifference;
        private Tween _transformTween;
        private BlockColor _blockColor;
        private Vector3 _originalScale;
        private Tween _pickUpTween;
        private IDisposable _switchIdleTimer;
        private CancellationTokenSource _switchIdleCts;
        private ParticleSystem _placingVfxInstance;
        //private Sequence _rotationSequence;
        
        private IBlockPresentationConfig _blockConfig;
        private BlockManagerConfig _blockManagerConfig;
        private GridManagerConfig _gridConfig;
        private BlockController _blockController;
        private BlockViewModel _viewModel;
        private ExplosionVfxPool _blockVfxPool;
        private IDisposable _bindings;
        #endregion

        [Inject]
        public void Construct(
            IBlockPresentationConfig blockConfig,
            BlockManagerConfig blockManagerConfig,
            GridManagerConfig gridConfig,
            BlockController blockController,
            BlockViewModel viewModel,
            ExplosionVfxPool blockVfxPool)
        {
            _blockConfig = blockConfig;
            _blockManagerConfig = blockManagerConfig;
            _gridConfig = gridConfig;
            _blockController = blockController;
            _viewModel = viewModel;
            _blockVfxPool = blockVfxPool;
            _originalParent = transform.parent;
            _originalPosition = transform.position;
            _originalEulerAngles = transform.eulerAngles;
            _viewModel.SetSpawnReturnTarget(_originalParent);
            _originalSortingLayer = meshRenderer.sortingLayerID;
            _originalSortingOrder = meshRenderer.sortingOrder;
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
                .Prepend(BlockState.Normal)
                .Pairwise()
                .Subscribe(x => OnBlockStateChanged(x.Previous, x.Current))
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
            if (_placingVfxInstance)
            {
                _blockVfxPool?.CancelReturnCallback(_placingVfxInstance, OnPlacementVfxReturned);
                _placingVfxInstance = null;
            }

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
                    if (_viewModel.BlockState.CurrentValue is BlockState.Normal)
                        PlayPlaceVFX();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void OnBlockStateChanged(BlockState previous, BlockState current)
        {
            if (previous is BlockState.Obstacle && current is BlockState.Exploding)
            {
                return;
            }
            if (current is BlockState.Obstacle)
            {
                var usesAtomRenderer = _blockConfig.UsesAtomRenderer;
                obstacleSpriteRenderer.enabled = !usesAtomRenderer;
                if (!usesAtomRenderer)
                    obstacleSpriteRenderer.sprite = _blockConfig.ObstacleSprite;
                SetSpineRendererVisible(false);
            }
            else
            {
                obstacleSpriteRenderer.enabled = false;
                SetSpineRendererVisible(true);
            }
        }

        private void SetSpineRendererVisible(bool visible)
        {
            meshRenderer.enabled = visible && !_blockConfig.UsesAtomRenderer;
            if (_blockConfig.UsesAtomRenderer && skeletonAnimation) skeletonAnimation.enabled = false;
        }

        #region Initalization
        private void Initialize()
        {
            skeletonAnimation.skeletonDataAsset = _blockConfig.SkeletonDataAsset;
            skeletonAnimation.Initialize(true);
            if (!_blockConfig.UsesAtomRenderer)
            {
                skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.IdleAnimations[0], true);
                StartIdleTimer();
            }
            SetSpineRendererVisible(true);
        }
        #endregion
        
        private void StartIdleTimer()
        {
            if (_blockConfig.UsesAtomRenderer) return;
            var randomSwitchTime = _blockManagerConfig.SwitchIdleTimeRange.RandomWithinRange();
            _switchIdleCts = new CancellationTokenSource();
            _switchIdleTimer = Observable.Timer(TimeSpan.FromSeconds(randomSwitchTime), _switchIdleCts.Token)
                .Subscribe(_ =>
                {
                    if (_switchIdleCts == null || _switchIdleCts.Token.IsCancellationRequested) return;
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
            if (_blockConfig.UsesAtomRenderer) return;
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
            Tween.StopAll(transform);
            transform.SetParent(null);
            if (_viewModel.TrayPiece != null)
                transform.rotation = _viewModel.TrayPiece.GameplayRotation;
            var gridSize = new Vector3(_gridConfig.CellSize.x, _gridConfig.CellSize.y, _originalScale.z);
            if (_blockManagerConfig.UseBlockScaleTween)
            {
                _pickUpTween = Tween.Scale(transform, gridSize, 0.2f);
            }
            else
            {
                transform.localScale = gridSize;
            }
            CancelIdleTimer();
            if (!_blockConfig.UsesAtomRenderer)
                skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.PickUpAnimation, true);
        }
        
        private void Place(Vector3 scale)
        {
            _pickUpTween.Stop();
            var targetScale = new Vector3(scale.x, scale.y, _originalScale.z);
            if (_blockManagerConfig.UseBlockScaleTween)
            {
                _pickUpTween = Tween.Scale(transform, targetScale, 0.2f);
            }
            else
            {
                transform.localScale = targetScale;
            }
            skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.IdleAnimations[0], true);
            StartIdleTimer();
        }

        private void PlayPlaceVFX()
        {
            if (!_blockConfig.PlaceVFX.TryGetValue(_blockColor, out var vfx)) return;

            if (_blockVfxPool == null)
            {
                _placingVfxInstance = Instantiate(vfx, transform.position, Quaternion.identity);
                _placingVfxInstance.Play(true);
                return;
            }

            _placingVfxInstance = _blockVfxPool.PlayPlacement(vfx, transform.position, OnPlacementVfxReturned);
        }

        private void OnPlacementVfxReturned(ParticleSystem instance)
        {
            if (_placingVfxInstance == instance) _placingVfxInstance = null;
        }
        
        private async UniTask Explode(ExplodeCommandData data)
        {
            DebugUtils.Log($"Block {_blockColor} exploded at position {transform.position}");
            CancelIdleTimer();
            var usesAtomRenderer = _blockConfig.UsesAtomRenderer;
            if (usesAtomRenderer) skeletonAnimation.enabled = true;
            var speedMultiplier = data.FitType == FitType.FitMe ? 2f : 6.67f;
            skeletonAnimation.AnimationState.ClearTrack(0);
            var explodeAnim = skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.ExplodeAnimation, false);
            explodeAnim.TimeScale *= speedMultiplier;
            await explodeAnim.WaitUntilComplete(); 
            if (usesAtomRenderer) skeletonAnimation.enabled = false;
            if (_blockConfig.ExplodeVfx.TryGetValue(_blockColor, out var vfx))
            {
                _blockVfxPool?.Play(vfx, transform.position);
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
            if (!_placingVfxInstance) return;
            var psr = _placingVfxInstance.GetComponent<ParticleSystemRenderer>();
            psr.sortingOrder = order - 1;
        }

        private void OnBlockColorChanged(BlockColor color)
        {
            _blockColor = color;
            if (_blockConfig.UsesAtomRenderer) return;
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
        /// Return the block to its current spawn slot, restoring its saved rotation and scale.
        /// </summary>
        private void ReturnToSpawn()
        {
            Tween.StopAll(transform);
            var trayPiece = _viewModel.TrayPiece;
            if (trayPiece != null)
            {
                trayPiece.DropInTray(trayPiece.Position);
                transform.SetParent(_originalParent, true);
                transform.position = trayPiece.Position;
                transform.rotation = trayPiece.TrayRotation;
                _originalScale = new Vector3(trayPiece.Scale, trayPiece.Scale, 1f);
                transform.localScale = _originalScale;
                Place(_originalScale);
                _viewModel.SetSortingLayerCommand.Execute(_blockManagerConfig.SpawnSortingLayer);
                _viewModel.SetSortingOrderCommand.Execute(trayPiece.SortingOrder);
                return;
            }
            var returnTarget = _viewModel.SpawnReturnTarget.CurrentValue;
            var returnParent = returnTarget ? returnTarget : _originalParent;
            var returnPosition = returnTarget ? returnTarget.position : _originalPosition;
            transform.SetParent(returnParent);
            _transformTween = Tween.Position(transform, returnPosition, 0.2f)
                .OnComplete(this, target =>
                {
                    target.OnSetSortingLayer(_blockManagerConfig.SpawnSortingLayer);
                    target.OnSetSortingOrder(target._originalSortingOrder);
                });
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
            var gridSize = new Vector3(_gridConfig.CellSize.x, _gridConfig.CellSize.y, _originalScale.z);
            var scaleSettings = new TweenSettings<Vector3>(transform.localScale, gridSize, rotateScaleTweenSettings);
            var sequence = Sequence.Create(Tween.Rotation(transform, new TweenSettings<Quaternion>(data.Rotation, rotateTweenSettings)))
                .Group(Tween.Scale(transform, scaleSettings))
                .Chain(Tween.Scale(transform, scaleSettings.WithDirection(false)));
            try
            {
                await sequence.ToUniTask();
            }
            catch (Exception)
            {
                // Handle cancellation safely when the tween is stopped early (e.g. on pickup)
            }
            finally
            {
                if (_viewModel.TrayPiece != null && _viewModel.BlockInteractionState.Value == BlockInteractionState.PlacedOnSpawn)
                {
                    var piece = _viewModel.TrayPiece;
                    piece.DropInTray(piece.Position);
                    transform.position = piece.Position;
                    transform.rotation = piece.TrayRotation;
                    transform.localScale = new Vector3(piece.Scale, piece.Scale, 1f);
                }
                data.Promise.TrySetResult(Unit.Default);
            }
        }

        private void Destroy()
        {
            Destroy(gameObject);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _blockController.PointerDownCommand.Execute(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _blockController.PointerUpCommand.Execute(eventData);
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
