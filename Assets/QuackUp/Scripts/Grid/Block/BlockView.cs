using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
    public interface IBlockView : ITransformProvider
    {
        void Destroy();
        UniTask ScaleIn(Vector3 scale);
        UniTask Rotate(Quaternion rotation);
        UniTask Explode(FitType fitType, bool destroy = true);
    }
    
    [ShowOdinSerializedPropertiesInInspector]
    public class BlockView : SerializedMonoBehaviour, IDisposable, IBlockView, 
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        #region Inspectors
        [Title("Tween")] 
        [SerializeField] private TweenSettings scaleTweenSettings;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        //[SerializeField] private SpriteRenderer infectedSpriteRenderer;
        
        [Title("Settings")]
        [SerializeField] private Color originalColor = Color.white;
        [SerializeField] private float pickUpScaleMultiplier = 1.2f;
        [SerializeField] private Vector2 switchIdleTimeRange = new(30f, 60f);
        #endregion

        #region Fields and Properties
        
        public Transform Transform => transform;
        
        private Transform _originalParent;
        private Vector3 _originalPosition;
        private Vector3 _originalRotation;
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
            _originalRotation = transform.eulerAngles;
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
            _viewModel.BlockType
                .Subscribe(OnBlockTypeChanged)
                .AddTo(ref disposableBuilder);
            _viewModel.SetSortingLayerCommand
                .Subscribe(OnSetSortingLayer)
                .AddTo(ref disposableBuilder);
            _viewModel.SetSortingOrderCommand
                .Subscribe(OnSetSortingOrder)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        public void Dispose()
        {
            _bindings?.Dispose();
        }
        
        private void OnDestroy()
        {
            CancelIdleTimer();
            Dispose();
        }
        
        private void OnInteractionStateChanged(BlockInteractionState state)
        {
            switch (state)
            {
                case BlockInteractionState.PlacedOnSpawn:
                    ReturnToOriginal();
                    break;
                case BlockInteractionState.PickUp:
                    PickUp();
                    break;
                case BlockInteractionState.PlacedOnGrid:
                    Place();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
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
            var randomSwitchTime = UnityEngine.Random.Range(switchIdleTimeRange.x, switchIdleTimeRange.y);
            _switchIdleCts = new CancellationTokenSource();
            _switchIdleTimer = Observable.Timer(TimeSpan.FromSeconds(randomSwitchTime), _switchIdleCts.Token)
                .Subscribe(_ =>
                {
                    skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.IdleAnimations[1], true);
                    skeletonAnimation.AnimationState.AddAnimation(0, _blockConfig.IdleAnimations[0], true, 0f);
                    CancelIdleTimer();
                    StartIdleTimer();
                });
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
        
        private void Place()
        {
            _pickUpTween.Stop();
            _pickUpTween = Tween.Scale(transform, _originalScale, 0.2f);
            skeletonAnimation.AnimationState.SetAnimation(0, _blockConfig.IdleAnimations[0], true);
            StartIdleTimer();
        }
        
        public async UniTask Explode(FitType fitType, bool destroy = true)
        {
            DebugUtils.Log($"Block {_blockColor} exploded at position {transform.position}");
            CancelIdleTimer();
            var speedMultiplier = fitType == FitType.FitMe ? 2f : 6.67f;
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
            if (destroy) Destroy(gameObject);
        }

        private void OnSetSortingLayer(int layer)
        {
            meshRenderer.sortingLayerID = layer;
        }

        private void OnSetSortingOrder(int order)
        {
            meshRenderer.sortingOrder = order;
        }

        private void OnBlockTypeChanged(BlockColor color)
        {
            _blockColor = color;
            if (!_blockConfig.SkinDictionary.TryGetValue(color, out var skin))
            {
                DebugUtils.LogWarning($"No skin found for block type: {color}");
                return;
            }
            
            skeletonAnimation.Skeleton.SetSkin(skin);
            skeletonAnimation.Skeleton.SetSlotsToSetupPose();
        }

        /// <summary>
        /// Return the block to its original position, rotation and scale
        /// </summary>
        private void ReturnToOriginal()
        {
            if (_transformTween.isAlive)
            {
                _transformTween.Stop();
            }
            transform.SetParent(_originalParent);
            OnSetSortingLayer(_originalSortingLayer);
            _transformTween = Tween.Position(transform, _originalPosition, 0.2f);
            Tween.Rotation(transform, _originalRotation, 0.2f);
            Tween.Scale(transform, _originalScale, 0.2f);
            Place();
        }

        public UniTask ScaleIn(Vector3 scale)
        {
            _originalScale = scale;
            var sequence = Tween.Scale(transform, new TweenSettings<Vector3>(scale, scaleTweenSettings));
            return sequence.ToUniTask();
        }
        
        public UniTask Rotate(Quaternion rotation)
        {
            _originalRotation = rotation.eulerAngles;
            var tween = Tween.Rotation(transform, rotation, 0.5f);
            return tween.ToUniTask();
        }

        public void Destroy()
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