using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using PrimeTween;
using QuackUp.Utils;
using R3;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    public interface IBlockView
    {
        void SetParent([CanBeNull] Transform parent);
        void SetSortingLayer(int layer);
        void SetSortingOrder(int order);
        UniTask Explode(FitType fitType, bool destroy = true);
    }
    
    [ShowOdinSerializedPropertiesInInspector]
    public class BlockView : SerializedMonoBehaviour, IDisposable, IBlockView
    {
        [Serializable]
        private record SkinWrapper
        {
            [ShowInInspector, Sirenix.OdinInspector.ReadOnly] private SkeletonRenderer _skeletonRenderer;
            [ShowInInspector, Sirenix.OdinInspector.ReadOnly]
            public BlockTypes blockType;
            [SpineSkin(dataField: nameof(_skeletonRenderer))] public string skinName;
            
            public SkinWrapper(SkeletonRenderer skeletonRenderer, BlockTypes blockType, string skinName)
            {
                _skeletonRenderer = skeletonRenderer;
                this.blockType = blockType;
                this.skinName = skinName;
            }
        }
        #region Inspectors
        [TitleGroup("References")]
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private SpriteRenderer infectedSpriteRenderer;
        
        [Title("Settings")]
        [SerializeField] private Color originalColor = Color.white;
        [SerializeField] private float pickUpScaleMultiplier = 1.2f;
        [SerializeField] private Vector2 switchIdleTimeRange = new(30f, 60f);
        
        [Title("Animations")]
        [SerializeField, SpineAnimation] string[] idleAnimations;
        [SerializeField, SpineAnimation] string pickUpAnimation;
        [SerializeField, SpineAnimation] string explodeAnimation;
        [SerializeField, SpineAnimation] string preInfectedAnimation;
        
        [Title("VFX")]
        [field: OdinSerialize] private Dictionary<BlockTypes, ParticleSystem> explodeVfx = new();
        
        [TitleGroup("Skins")]
        [ShowInInspector, HideLabel]
        [DetailedInfoBox("Read Me",
            "Due to a certain limitation of Spine handling of the attributes, the skin selector cannot be drawn under dictionary, " +
            "and has to be deconstructed into a list of SkinWrapper objects.\n" +
            "You can deconstruct the dictionary into a list by clicking the 'Deconstruct' button, " +
            "and then save the changes back to the dictionary by clicking the 'Save Changes' button.",
            InfoMessageType.Warning)]
        private InspectorPlaceholder _skinDictionaryInfo;
        [TitleGroup("Skins")]
        [field: OdinSerialize]
        private Dictionary<BlockTypes, string> skinDictionary = new();
        [TitleGroup("Skins")]
        [SerializeField, HideIf("@deconstructed.Count == 0")] private List<SkinWrapper> deconstructed = new();
        [TitleGroup("Skins")]
        [Button("Deconstruct")]
        private void Deconstruct()
        {
            deconstructed = new List<SkinWrapper>();
            foreach (var kvp in skinDictionary)
            {
                deconstructed.Add(new SkinWrapper(skeletonAnimation, kvp.Key, kvp.Value));
            }
        }
        [TitleGroup("Skins")]
        [Button("Save Changes")]
        private void SaveChanges()
        {
            skinDictionary = new Dictionary<BlockTypes, string>();
            foreach (var wrapper in deconstructed)
            {
                skinDictionary.Add(wrapper.blockType, wrapper.skinName);
            }
            deconstructed.Clear();
        }
        #endregion

        #region Fields and Properties

        private MeshRenderer _meshRenderer;
        private Vector3 _originalPosition;
        private Vector3 _originalRotation;
        private Vector3 _mousePositionDifference;
        private Tween _transformTween;
        private BlockTypes _blockType;
        private Vector3 _originalScale;
        private Tween _pickUpTween;
        private IDisposable _switchIdleTimer;
        private CancellationTokenSource _switchIdleCts;

        private BlockViewModel _viewModel;
        private IDisposable _bindings;
        #endregion

        [Inject]
        public void Construct(
            BlockViewModel viewModel)
        {
            _viewModel = viewModel;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.BlockInteractionState
                .Subscribe(OnInteractionStateChanged)
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
        
        public void OnInteractionStateChanged(BlockInteractionState state)
        {
            switch (state)
            {
                case BlockInteractionState.None:
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
            if (!skeletonAnimation)
            {
                Debug.LogError("SkeletonAnimation is not assigned in BlockView.");
                return;
            }
            if (!skeletonAnimation.TryGetComponent(out _meshRenderer))
            {
                Debug.LogError("MeshRenderer is not found on SkeletonAnimation.");
                return;
            }
            if (!infectedSpriteRenderer)
            {
                Debug.LogWarning("InfectedSpriteRenderer is not assigned in BlockView. Infected state will not be visible.");
            }
            else
            {
                infectedSpriteRenderer.enabled = false;
            }
            _originalScale = transform.localScale;
            skeletonAnimation.AnimationState.SetAnimation(0, idleAnimations[0], true);
            StartIdleTimer();
        }
        #endregion

        #region Utils
        private void StartIdleTimer()
        {
            var randomSwitchTime = UnityEngine.Random.Range(switchIdleTimeRange.x, switchIdleTimeRange.y);
            _switchIdleCts = new CancellationTokenSource();
            _switchIdleTimer = Observable.Timer(TimeSpan.FromSeconds(randomSwitchTime), _switchIdleCts.Token)
                .Subscribe(_ =>
                {
                    skeletonAnimation.AnimationState.SetAnimation(0, idleAnimations[1], true);
                    skeletonAnimation.AnimationState.AddAnimation(0, idleAnimations[0], true, 0f);
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

        public void PickUp()
        {
            CancelIdleTimer();
            _pickUpTween = Tween.Scale(transform, _originalScale * pickUpScaleMultiplier, 0.2f);
            skeletonAnimation.AnimationState.SetAnimation(0, pickUpAnimation, true);
        }
        
        public void Place()
        {
            _pickUpTween.Stop();
            _pickUpTween = Tween.Scale(transform, _originalScale, 0.2f);
            skeletonAnimation.AnimationState.SetAnimation(0, idleAnimations[0], true);
            StartIdleTimer();
        }
        
        public async UniTask Explode(FitType fitType, bool destroy = true)
        {
            BlockState = BlockState.Exploding;
            SetColor(originalAtomColor);
            Debug.Log($"Block {BlockType} exploded at position {transform.position}");
            CancelIdleTimer();
            var speedMultiplier = fitType == FitType.FitMe ? 2f : 6.67f;
            var explodeAnim = skeletonAnimation.AnimationState.SetAnimation(0, explodeAnimation, false);
            explodeAnim.TimeScale *= speedMultiplier;
            await explodeAnim.ToUniTask();
            //await UniTask.WaitUntil(() => skeletonAnimation.AnimationState.GetCurrent(0).IsComplete);
            if (explodeVfx.TryGetValue(_blockType, out var vfx))
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

        public void SetParent(Transform parent)
        {
            transform.SetParent(parent, true);
        }
        
        public void SetSortingLayer(int layer)
        {
            _meshRenderer.sortingLayerID = layer;
            if (infectedSpriteRenderer)
            {
                infectedSpriteRenderer.sortingLayerID = layer;
            }
        }
        
        public void SetSortingOrder(int order)
        {
            _meshRenderer.sortingOrder = order;
            if (infectedSpriteRenderer)
            {
                infectedSpriteRenderer.sortingOrder = order;
            }
        }

        public void SetType(BlockTypes type)
        {
            if (!skinDictionary.TryGetValue(type, out var skin))
            {
                Debug.LogWarning($"No skin found for block type: {type}");
                 return;
            }
            _blockType = type;
            skeletonAnimation.Skeleton.SetSkin(skin);
            skeletonAnimation.Skeleton.SetSlotsToSetupPose();
        }
        
        public void PickUpBlock()
        {
            //Tween the block to (1, 1, 1) scale
            if (_transformTween.isAlive)
            {
                _transformTween.Stop();
            }
            var gridSize = GridManager.Instance.Grid.cellSize;
            Tween.Scale(transform, gridSize, 0.2f);
            if (BlockView) BlockView.PickUp();
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
            SetSortingLayer(originalSortingLayer);
            _transformTween = Tween.Position(transform, _originalPosition, 0.2f);
            Tween.Rotation(transform, _originalRotation, 0.2f);
            Tween.Scale(transform, _originalScale, 0.2f);
            if (BlockView) BlockView.Place();
            GridManager.Instance.ResetPreviousValidationCells();
        }
        #endregion
    }
}
