using System;
using System.Threading;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using Spine.Unity;
using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    public class AtomView : MonoBehaviour, IDisposable
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SpriteOutlineController spriteOutlineController;
        [SerializeField] private AtomSkeletonAnimation skeletonAnimation;
        [SerializeField] private MeshRenderer spineMeshRenderer;
        [SerializeField] private BoxCollider2D boxCollider;
        [SerializeField] private float edgeRadius = 0.25f;
        
        private BlockManagerConfig _blockManagerConfig;
        private AtomViewModel _viewModel;
        private IDisposable _bindings;
        private IDisposable _parentBlockSubscriptions;
        private ArbitraryBlockConfig _arbitraryBlockConfig;
        private IDisposable _idleTimer;
        private CancellationTokenSource _idleCts;
        private int _idleGeneration;
        private Vector2 _defaultPhysicsPositionInheritanceFactor;
        private float _defaultPhysicsRotationInheritanceFactor;
        private bool _hasDefaultPhysicsInheritanceFactors;
        
        public Transform Transform => transform;
        
        [Inject]
        public void Construct(
            BlockManagerConfig blockManagerConfig,
            AtomViewModel viewModel)
        {
            _blockManagerConfig = blockManagerConfig;
            _viewModel = viewModel;
            if (boxCollider)
                boxCollider.edgeRadius = edgeRadius * Mathf.Abs(transform.lossyScale.x);
            spriteRenderer.enabled = false;
            if (skeletonAnimation) skeletonAnimation.enabled = false;
            if (spineMeshRenderer) spineMeshRenderer.enabled = false;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _viewModel.ParentBlock
                .Where(x => x != null)
                .Subscribe(OnParentBlockModelChanged)
                .AddTo(ref disposableBuilder);
            _viewModel.SetOutlineCommand
                .Subscribe(SetOutline)
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }

        private void OnParentBlockModelChanged(BlockInstance instance)
        {
            SetSpinePhysicsSimulationEnabled(true);
            CancelIdleTimer();
            _parentBlockSubscriptions?.Dispose();
            var blockConfig = _viewModel.ParentBlock.CurrentValue.Model.Config;
            _arbitraryBlockConfig = blockConfig as ArbitraryBlockConfig;
            spriteRenderer.enabled = blockConfig.UseAtomSprite || _arbitraryBlockConfig;
            if (_arbitraryBlockConfig && !_arbitraryBlockConfig.UseAtomSprite)
                SetAtomSpriteAlpha(0f);
            if (spineMeshRenderer) spineMeshRenderer.enabled = false;
            if (skeletonAnimation) skeletonAnimation.enabled = false;
            if (_arbitraryBlockConfig && !_arbitraryBlockConfig.UseAtomSprite)
            {
                InitializeArbitrarySpine(instance);
            }
            if (!blockConfig.UsesAtomRenderer) return;
            var disposableBuilder = Disposable.CreateBuilder();
            instance.Model.BlockColor
                .Subscribe(_ => UpdateAtomPresentation(instance))
                .AddTo(ref disposableBuilder);
            instance.Model.BlockState
                .Subscribe(_ => UpdateAtomPresentation(instance))
                .AddTo(ref disposableBuilder);
            instance.ViewModel.BlockInteractionState
                .Pairwise()
                .Subscribe(pair => OnInteractionStateChanged(pair.Previous, pair.Current))
                .AddTo(ref disposableBuilder);
            instance.ViewModel.ExplodeCommand
                .Subscribe(data => PlayArbitraryAnimation(ArbitraryBlockAnimation.Explode, false, false,
                    data.FitType == FitType.FitMe ? 2f : 6.67f))
                .AddTo(ref disposableBuilder);
            instance.ViewModel.SetSortingLayerCommand
                .Subscribe(OnSetSortingLayer)
                .AddTo(ref disposableBuilder);
            instance.ViewModel.SetSortingOrderCommand
                .Subscribe(OnSetSortingOrder)
                .AddTo(ref disposableBuilder);
            _parentBlockSubscriptions = disposableBuilder.Build();
            UpdateAtomPresentation(instance);
        }

        private void InitializeArbitrarySpine(BlockInstance instance)
        {
            if (!skeletonAnimation || !spineMeshRenderer)
            {
                DebugUtils.LogError("Arbitrary atom prefab is missing its dedicated Spine child renderer references.");
                return;
            }

            // Keep per-atom visual variation off Unity's gameplay Random stream.
            var visualRandom = new System.Random(unchecked(Environment.TickCount ^ GetInstanceID()));
            var design = (ArbitraryBlockDesign)visualRandom.Next(Enum.GetValues(typeof(ArbitraryBlockDesign)).Length);
            var skeletonData = _arbitraryBlockConfig.GetSkeletonDataAsset(design);
            if (!skeletonData)
            {
                DebugUtils.LogError($"ArbitraryBlockConfig has no Spine skeleton assigned for design {design}.");
                return;
            }

            skeletonAnimation.skeletonDataAsset = skeletonData;
            skeletonAnimation.enabled = true;
            var skeleton = skeletonAnimation.Skeleton;
            if (skeleton == null || skeleton.Data != skeletonData.GetSkeletonData(true))
            {
                skeletonAnimation.Initialize(true);
                skeleton = skeletonAnimation.Skeleton;
            }
            if (skeleton == null)
            {
                DebugUtils.LogError($"Could not initialize Spine skeleton for design {design}.");
                return;
            }

            if (!_hasDefaultPhysicsInheritanceFactors)
            {
                _defaultPhysicsPositionInheritanceFactor = skeletonAnimation.PhysicsPositionInheritanceFactor;
                _defaultPhysicsRotationInheritanceFactor = skeletonAnimation.PhysicsRotationInheritanceFactor;
                _hasDefaultPhysicsInheritanceFactors = true;
            }
            skeletonAnimation.SetPhysicsSimulationEnabled(true);
            skeletonAnimation.transform.localRotation = Quaternion.Euler(0, 0, visualRandom.Next(4) * 90f);
            spineMeshRenderer.enabled = true;
            if (!ApplyArbitrarySkin(instance.Model.BlockColor.CurrentValue))
            {
                skeletonAnimation.enabled = false;
                spineMeshRenderer.enabled = false;
                spriteRenderer.enabled = true;
                UpdateAtomSpriteColor(instance);
                return;
            }
            PlayArbitraryIdle(0);
            StartIdleTimer();
        }

        private void UpdateAtomPresentation(BlockInstance instance)
        {
            var blockState = instance.Model.BlockState.CurrentValue;
            if (blockState is BlockState.Exploding)
            {
                CancelIdleTimer();
                SetSpinePhysicsSimulationEnabled(true);
            }

            if (blockState is BlockState.Obstacle)
            {
                CancelIdleTimer();
                if (skeletonAnimation) skeletonAnimation.enabled = false;
                spriteRenderer.enabled = true;
                if (spineMeshRenderer) spineMeshRenderer.enabled = false;
                UpdateAtomSpriteColor(instance);
                SetAtomSpriteAlpha(1f);
                return;
            }

            if (_arbitraryBlockConfig)
            {
                if (_arbitraryBlockConfig.UseAtomSprite)
                {
                    if (skeletonAnimation) skeletonAnimation.enabled = false;
                    if (spineMeshRenderer) spineMeshRenderer.enabled = false;
                    spriteRenderer.enabled = true;
                    UpdateAtomSpriteColor(instance);
                    SetAtomSpriteAlpha(1f);
                    return;
                }

                if (skeletonAnimation) skeletonAnimation.enabled = true;
                var skinApplied = ApplyArbitrarySkin(instance.Model.BlockColor.CurrentValue);
                var spineReady = skeletonAnimation && skeletonAnimation.Skeleton != null && spineMeshRenderer;
                spineMeshRenderer.enabled = spineReady && skinApplied;
                skeletonAnimation.enabled = spineReady && skinApplied;
                spriteRenderer.enabled = true;
                if (spineMeshRenderer.enabled)
                {
                    UpdateAtomSpriteColor(instance);
                    SetAtomSpriteAlpha(0f);
                }
                else
                {
                    UpdateAtomSpriteColor(instance);
                    SetAtomSpriteAlpha(1f);
                }
                return;
            }

            UpdateAtomSpriteColor(instance);
        }

        private bool ApplyArbitrarySkin(BlockColor color)
        {
            if (!_arbitraryBlockConfig || !skeletonAnimation || skeletonAnimation.Skeleton == null) return false;
            if (!_arbitraryBlockConfig.TryGetSkin(color, out var skinName))
            {
                DebugUtils.LogWarning($"ArbitraryBlockConfig has no Spine skin mapped for block color {color}.");
                return false;
            }

            var skin = skeletonAnimation.Skeleton.Data.FindSkin(skinName);
            if (skin == null)
            {
                DebugUtils.LogWarning($"ArbitraryBlockConfig skin '{skinName}' was not found in the atom skeleton.");
                return false;
            }

            skeletonAnimation.Skeleton.SetSkin(skin);
            skeletonAnimation.Skeleton.SetSlotsToSetupPose();
            return true;
        }

        private void OnInteractionStateChanged(BlockInteractionState previous, BlockInteractionState current)
        {
            if (!_arbitraryBlockConfig || _arbitraryBlockConfig.UseAtomSprite ||
                !skeletonAnimation || skeletonAnimation.Skeleton == null) return;

            if (current is BlockInteractionState.PickUp)
            {
                SetSpinePhysicsSimulationEnabled(true);
                PlayArbitraryAnimation(ArbitraryBlockAnimation.Drag, false, false);
                return;
            }

            if (previous is BlockInteractionState.PickUp)
            {
                SetSpinePhysicsSimulationEnabled(true);
                PlayArbitraryAnimation(ArbitraryBlockAnimation.Drop, false, true);
            }
        }

        private void PlayArbitraryAnimation(
            ArbitraryBlockAnimation animation,
            bool loop,
            bool resumeIdleOnComplete,
            float timeScale = 1f)
        {
            if (!_arbitraryBlockConfig) return;
            CancelIdleTimer();
            if (_arbitraryBlockConfig.UseAtomSprite || !skeletonAnimation || skeletonAnimation.Skeleton == null) return;
            if (!_arbitraryBlockConfig.TryGetAnimation(animation, out var animationName))
            {
                DebugUtils.LogWarning($"ArbitraryBlockConfig has no animation mapped for {animation}.");
                if (resumeIdleOnComplete) StartIdleTimer();
                return;
            }

            if (animation is ArbitraryBlockAnimation.Drag or ArbitraryBlockAnimation.Drop or ArbitraryBlockAnimation.Explode)
                skeletonAnimation.AnimationState.ClearTrack(0);
            var track = skeletonAnimation.AnimationState.SetAnimation(0, animationName, loop);
            track.TimeScale *= timeScale;
            if (!resumeIdleOnComplete) return;
            var generation = _idleGeneration;
            track.Complete += _ =>
            {
                var instance = _viewModel.ParentBlock.CurrentValue;
                if (generation != _idleGeneration || instance == null ||
                    instance.Model.BlockState.CurrentValue is BlockState.Exploding or BlockState.Obstacle) return;
                PlayArbitraryIdle(0);
                StartIdleTimer();
            };
        }

        private void PlayArbitraryIdle(int index)
        {
            if (!_arbitraryBlockConfig || _arbitraryBlockConfig.UseAtomSprite ||
                !skeletonAnimation || skeletonAnimation.Skeleton == null) return;
            var instance = _viewModel.ParentBlock.CurrentValue;
            if (instance == null || instance.Model.BlockState.CurrentValue is BlockState.Exploding or BlockState.Obstacle) return;
            var idleAnimations = _arbitraryBlockConfig.IdleAnimations;
            if (idleAnimations.Count == 0) return;
            _idleGeneration++;
            skeletonAnimation.AnimationState.SetAnimation(0, idleAnimations[index % idleAnimations.Count], true);
        }

        private void StartIdleTimer()
        {
            var instance = _viewModel.ParentBlock.CurrentValue;
            if (!_arbitraryBlockConfig || _arbitraryBlockConfig.UseAtomSprite ||
                _arbitraryBlockConfig.IdleAnimations.Count < 2 || instance == null ||
                instance.Model.BlockState.CurrentValue is BlockState.Exploding or BlockState.Obstacle) return;
            CancelIdleTimer();
            _idleCts = new CancellationTokenSource();
            var token = _idleCts.Token;
            var generation = _idleGeneration;
            var delay = _blockManagerConfig.SwitchIdleTimeRange.RandomWithinRange();
            _idleTimer = Observable.Timer(TimeSpan.FromSeconds(delay), token)
                .Subscribe(_ =>
                {
                    if (token.IsCancellationRequested || generation != _idleGeneration) return;
                    PlayArbitraryAnimation(ArbitraryBlockAnimation.Idle2, false, true);
                });
        }

        private void SetSpinePhysicsSimulationEnabled(bool enabled)
        {
            if (!skeletonAnimation || !_hasDefaultPhysicsInheritanceFactors) return;

            if (enabled)
            {
                skeletonAnimation.PhysicsPositionInheritanceFactor = _defaultPhysicsPositionInheritanceFactor;
                skeletonAnimation.PhysicsRotationInheritanceFactor = _defaultPhysicsRotationInheritanceFactor;
                skeletonAnimation.SetPhysicsSimulationEnabled(true);
                return;
            }

            skeletonAnimation.PhysicsPositionInheritanceFactor = Vector2.zero;
            skeletonAnimation.PhysicsRotationInheritanceFactor = 0f;
            skeletonAnimation.SetPhysicsSimulationEnabled(false);
        }

        private void CancelIdleTimer()
        {
            _idleTimer?.Dispose();
            _idleCts?.Cancel();
            _idleCts?.Dispose();
            _idleTimer = null;
            _idleCts = null;
            _idleGeneration++;
        }
        
        private void OnSetSortingLayer(int layer)
        {
            spriteRenderer.sortingLayerID = layer;
            if (spineMeshRenderer) spineMeshRenderer.sortingLayerID = layer;
        }
        
        private void OnSetSortingOrder(int order)
        {
            spriteRenderer.sortingOrder = order;
            if (spineMeshRenderer) spineMeshRenderer.sortingOrder = _arbitraryBlockConfig ? order - 1 : order;
        }

        private void UpdateAtomSpriteColor(BlockInstance instance)
        {
            if (instance.Model.BlockState.CurrentValue is BlockState.Obstacle)
            {
                spriteRenderer.color = instance.Model.Config.ObstacleAtomSpriteColor;
                return;
            }

            var color = instance.Model.BlockColor.CurrentValue;
            if (!_blockManagerConfig.AtomColorDict.TryGetValue(color, out var spriteColor))
            {
                DebugUtils.LogError($"BlockType.CurrentValue {color} is not defined");
                return;
            }
            spriteRenderer.color = spriteColor;
        }

        private void SetAtomSpriteAlpha(float alpha)
        {
            var color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        private void SetOutline(SpriteOutlineSettings settings)
        {
            spriteOutlineController.UpdateOutline(settings);
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            SetSpinePhysicsSimulationEnabled(true);
            CancelIdleTimer();
            _bindings?.Dispose();
            _parentBlockSubscriptions?.Dispose();
            _bindings = null;
            _parentBlockSubscriptions = null;
        }
    }
}
