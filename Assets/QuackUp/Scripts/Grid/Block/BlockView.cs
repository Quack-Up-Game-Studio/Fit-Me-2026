using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace FitMe.Grid
{
    public interface IBlockView
    {
        void SetSortingLayer(int layer);
        void SetSortingOrder(int order);
        void Destroy();
        UniTask ScaleIn(Vector3 scale);
        UniTask Explode(FitType fitType, bool destroy = true);
    }
    
    [ShowOdinSerializedPropertiesInInspector]
    public class BlockView : SerializedMonoBehaviour, IDisposable, IBlockView, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // [Serializable]
        // private record SkinWrapper
        // {
        //     [ShowInInspector, Sirenix.OdinInspector.ReadOnly] private SkeletonRenderer _skeletonRenderer;
        //     [ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        //     public BlockTypes blockType;
        //     [SpineSkin(dataField: nameof(_skeletonRenderer))] public string skinName;
        //     
        //     public SkinWrapper(SkeletonRenderer skeletonRenderer, BlockTypes blockType, string skinName)
        //     {
        //         _skeletonRenderer = skeletonRenderer;
        //         this.blockType = blockType;
        //         this.skinName = skinName;
        //     }
        // }
        #region Inspectors

        [Title("Tween")] 
        [SerializeField] private TweenSettings scaleTweenSettings;
        [SerializeField] private MeshRenderer meshRenderer;
        //[SerializeField] private SkeletonAnimation skeletonAnimation;
        //[SerializeField] private SpriteRenderer infectedSpriteRenderer;
        
        [Title("Settings")]
        [SerializeField] private Color originalColor = Color.white;
        [SerializeField] private float pickUpScaleMultiplier = 1.2f;
        [SerializeField] private Vector2 switchIdleTimeRange = new(30f, 60f);
        
        // [Title("Animations")]
        // [SerializeField, SpineAnimation] string[] idleAnimations;
        // [SerializeField, SpineAnimation] string pickUpAnimation;
        // [SerializeField, SpineAnimation] string explodeAnimation;
        // [SerializeField, SpineAnimation] string preInfectedAnimation;
        
        // [TitleGroup("Skins")]
        // [ShowInInspector, HideLabel]
        // [DetailedInfoBox("Read Me",
        //     "Due to a certain limitation of Spine handling of the attributes, the skin selector cannot be drawn under dictionary, " +
        //     "and has to be deconstructed into a list of SkinWrapper objects.\n" +
        //     "You can deconstruct the dictionary into a list by clicking the 'Deconstruct' button, " +
        //     "and then save the changes back to the dictionary by clicking the 'Save Changes' button.",
        //     InfoMessageType.Warning)]
        // private InspectorPlaceholder _skinDictionaryInfo;
        // [TitleGroup("Skins")]
        // [field: OdinSerialize]
        // private Dictionary<BlockTypes, string> skinDictionary = new();
        // [TitleGroup("Skins")]
        // [SerializeField, HideIf("@deconstructed.Count == 0")] private List<SkinWrapper> deconstructed = new();
        // [TitleGroup("Skins")]
        // [Button("Deconstruct")]
        // private void Deconstruct()
        // {
        //     deconstructed = new List<SkinWrapper>();
        //     foreach (var kvp in skinDictionary)
        //     {
        //         deconstructed.Add(new SkinWrapper(skeletonAnimation, kvp.Key, kvp.Value));
        //     }
        // }
        // [TitleGroup("Skins")]
        // [Button("Save Changes")]
        // private void SaveChanges()
        // {
        //     skinDictionary = new Dictionary<BlockTypes, string>();
        //     foreach (var wrapper in deconstructed)
        //     {
        //         skinDictionary.Add(wrapper.blockType, wrapper.skinName);
        //     }
        //     deconstructed.Clear();
        // }
        #endregion

        #region Fields and Properties
        
        private Transform _originalParent;
        private Vector3 _originalPosition;
        private Vector3 _originalRotation;
        private Color _originalAtomColor;
        private int _originalSortingLayer;
        private Vector3 _mousePositionDifference;
        private Tween _transformTween;
        private BlockTypes _blockType;
        private Vector3 _originalScale;
        private Tween _pickUpTween;
        private IDisposable _switchIdleTimer;
        private CancellationTokenSource _switchIdleCts;
        
        private BlockConfig _config;
        private GridManagerConfig _gridConfig;
        private BlockController _blockController;
        private BlockViewModel _viewModel;
        private IDisposable _bindings;
        #endregion

        [Inject]
        public void Construct(
            BlockConfig config,
            GridManagerConfig gridConfig,
            BlockController blockController,
            BlockViewModel viewModel)
        {
            _config = config;
            _gridConfig = gridConfig;
            _blockController = blockController;
            _viewModel = viewModel;
            _originalParent = transform.parent;
            _originalPosition = transform.position;
            _originalRotation = transform.eulerAngles;
            _originalSortingLayer = meshRenderer.sortingLayerID;
            _originalAtomColor = originalColor;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.BlockInteractionState
                .DistinctUntilChanged()
                .Subscribe(OnInteractionStateChanged)
                .AddTo(ref disposableBuilder);
            _viewModel.BlockType
                .Subscribe(OnBlockTypeChanged)
                .AddTo(ref disposableBuilder);
            _viewModel.TransformData.OnChanged
                .Subscribe(OnTransformDataChanged)
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
        
        private void OnTransformDataChanged(TransformData transformData)
        {
            transform.position = transformData.Position.Value;
            transform.rotation = transformData.Rotation.Value;
            transform.localScale = transformData.LocalScale.Value;
        }
        
        private void OnInteractionStateChanged(BlockInteractionState state)
        {
            switch (state)
            {
                case BlockInteractionState.None:
                    ReturnToOriginal();
                    break;
                case BlockInteractionState.PickUp:
                    PickUp();
                    break;
                case BlockInteractionState.Placed:
                    Place();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        #region Initalization
        private void Awake()
        {
            // if (!skeletonAnimation)
            // {
            //     Debug.LogError("SkeletonAnimation is not assigned in BlockView.");
            //     return;
            // }
            // if (!skeletonAnimation.TryGetComponent(out _meshRenderer))
            // {
            //     Debug.LogError("MeshRenderer is not found on SkeletonAnimation.");
            //     return;
            // }
            // if (!infectedSpriteRenderer)
            // {
            //     Debug.LogWarning("InfectedSpriteRenderer is not assigned in BlockView. Infected state will not be visible.");
            // }
            // else
            // {
            //     infectedSpriteRenderer.enabled = false;
            // }
            //_originalScale = transform.localScale;
            //skeletonAnimation.AnimationState.SetAnimation(0, idleAnimations[0], true);
            //StartIdleTimer();
        }
        #endregion

        #region Utils
        // private void StartIdleTimer()
        // {
        //     var randomSwitchTime = UnityEngine.Random.Range(switchIdleTimeRange.x, switchIdleTimeRange.y);
        //     _switchIdleCts = new CancellationTokenSource();
        //     _switchIdleTimer = Observable.Timer(TimeSpan.FromSeconds(randomSwitchTime), _switchIdleCts.Token)
        //         .Subscribe(_ =>
        //         {
        //             skeletonAnimation.AnimationState.SetAnimation(0, idleAnimations[1], true);
        //             skeletonAnimation.AnimationState.AddAnimation(0, idleAnimations[0], true, 0f);
        //             CancelIdleTimer();
        //             StartIdleTimer();
        //         });
        // }
        
        private void CancelIdleTimer()
        {
            _switchIdleTimer?.Dispose();
            _switchIdleCts?.Cancel();
            _switchIdleCts?.Dispose();
            _switchIdleTimer = null;
            _switchIdleCts = null;
        }

        public void PickUp()
        {
            Debug.Log("Block picked up");
            if (_transformTween.isAlive)
            {
                _transformTween.Stop();
            }
            transform.SetParent(null);
            //var gridSize = GridManager.Instance.Grid.cellSize;
            var gridSize = _gridConfig.CellSize;
            Tween.Scale(transform, gridSize, 0.2f);
            CancelIdleTimer();
            _pickUpTween = Tween.Scale(transform, _originalScale * pickUpScaleMultiplier, 0.2f);
            //skeletonAnimation.AnimationState.SetAnimation(0, pickUpAnimation, true);
        }
        
        public void Place()
        {
            _pickUpTween.Stop();
            _pickUpTween = Tween.Scale(transform, _originalScale, 0.2f);
            //skeletonAnimation.AnimationState.SetAnimation(0, idleAnimations[0], true);
            //StartIdleTimer();
        }
        
        public async UniTask Explode(FitType fitType, bool destroy = true)
        {
            SetColor(_originalAtomColor);
            Debug.Log($"Block {_blockType} exploded at position {transform.position}");
            CancelIdleTimer();
            // var speedMultiplier = fitType == FitType.FitMe ? 2f : 6.67f;
            // var explodeAnim = skeletonAnimation.AnimationState.SetAnimation(0, explodeAnimation, false);
            // explodeAnim.TimeScale *= speedMultiplier;
            // await explodeAnim.ToUniTask();
            //await UniTask.WaitUntil(() => skeletonAnimation.AnimationState.GetCurrent(0).IsComplete);
            if (_config.ExplodeVfx.TryGetValue(_blockType, out var vfx))
            {
                var vfxInstance = Instantiate(vfx, transform.position, Quaternion.identity);
                vfxInstance.Play(true);
            }
            else
            {
                Debug.LogWarning($"No explosion VFX found for block type: {_blockType}");
            }
            if (destroy) Destroy(gameObject);
        }
        
        public void SetSortingLayer(int layer)
        {
            meshRenderer.sortingLayerID = layer;
            // if (infectedSpriteRenderer)
            // {
            //     infectedSpriteRenderer.sortingLayerID = layer;
            // }
        }
        
        public void SetSortingOrder(int order)
        {
            meshRenderer.sortingOrder = order;
            // if (infectedSpriteRenderer)
            // {
            //     infectedSpriteRenderer.sortingOrder = order;
            // }
        }

        public void OnBlockTypeChanged(BlockTypes type)
        {
            _blockType = type;
            // if (!skinDictionary.TryGetValue(type, out var skin))
            // {
            //     Debug.LogWarning($"No skin found for block type: {type}");
            //      return;
            // }
            //
            // skeletonAnimation.Skeleton.SetSkin(skin);
            // skeletonAnimation.Skeleton.SetSlotsToSetupPose();
        }
        
        private void SetColor(Color color)
        {
            //skeletonAnimation.Skeleton.SetColor(color);
        }

        /// <summary>
        /// Return the block to its original position, rotation and scale
        /// </summary>
        public void ReturnToOriginal()
        {
            if (_transformTween.isAlive)
            {
                _transformTween.Stop();
            }
            transform.SetParent(_originalParent);
            SetSortingLayer(_originalSortingLayer);
            _transformTween = Tween.Position(transform, _originalPosition, 0.2f);
            Tween.Rotation(transform, _originalRotation, 0.2f);
            Tween.Scale(transform, _originalScale, 0.2f);
            Place();
        }
        #endregion

        public UniTask ScaleIn(Vector3 scale)
        {
            _originalScale = scale;
            var sequence = Tween.Scale(transform, new TweenSettings<Vector3>(scale, scaleTweenSettings));
            return sequence.ToUniTask();
        }

        public void Destroy()
        {
            Destroy(gameObject);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Debug.Log("OnBeginDrag called in BlockView");
            _blockController.BeingDragCommand.Execute(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Debug.Log("OnDrag called in BlockView");
            _blockController.DragCommand.Execute(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Debug.Log("OnEndDrag called in BlockView");
            _blockController.EndDragCommand.Execute(eventData);
        }
    }
}
