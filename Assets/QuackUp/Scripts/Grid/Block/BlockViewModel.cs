using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using UnityEngine;
using VContainer;

namespace FitMe.Grid
{
    #region Command Data
    public struct ExplodeCommandData
    {
        public Promise<Unit> Promise;
        public FitType FitType;
        public bool Destroy;
        public ChildExplosionAnimationCompletion ChildAnimationCompletion;
        public double ExplosionStartTime;
        public double ExplosionDeadline;
        
        public ExplodeCommandData(
            Promise<Unit> promise,
            FitType fitType,
            bool destroy,
            ChildExplosionAnimationCompletion childAnimationCompletion = null)
        {
            Promise = promise;
            FitType = fitType;
            Destroy = destroy;
            ChildAnimationCompletion = childAnimationCompletion;
            ExplosionStartTime = 0d;
            ExplosionDeadline = 0d;
        }
    }

    internal static class ShapeExplosionTiming
    {
        private const float DefaultAnimationDuration = 2f;
        private const double MaximumFallbackWaitSeconds = 5d;

        internal static double GetDeadline(
            IBlockPresentationConfig blockConfig, float speedMultiplier, double explosionStartTime)
        {
            var animationDuration = GetAnimationDuration(blockConfig);
            if (!IsPositiveFinite(animationDuration))
            {
                DebugUtils.LogWarning(
                    $"Could not determine Shape Mode explosion duration; using {DefaultAnimationDuration}s fallback.");
                animationDuration = DefaultAnimationDuration;
            }

            // Match the authored child rig: game-time scale plus the command's animation speed.
            // Keep the resulting realtime deadline fixed at dispatch; pause/resume must not extend the safety bound.
            var effectiveTimeScale = IsPositiveFinite(speedMultiplier) ? speedMultiplier : 1f;
            if (IsPositiveFinite(Time.timeScale)) effectiveTimeScale *= Time.timeScale;
            else DebugUtils.LogWarning("Shape Mode explosion fallback is using its nominal rate while scaled time is stopped.");

            var expectedDuration = Math.Min((double)animationDuration / effectiveTimeScale, MaximumFallbackWaitSeconds);
            if (expectedDuration == MaximumFallbackWaitSeconds &&
                (double)animationDuration / effectiveTimeScale > MaximumFallbackWaitSeconds)
                DebugUtils.LogWarning("Shape Mode explosion fallback exceeded its safety limit; clamping its deadline.");

            return explosionStartTime + expectedDuration;
        }

        private static float GetAnimationDuration(IBlockPresentationConfig blockConfig)
        {
            if (blockConfig is ArbitraryBlockConfig arbitraryBlockConfig &&
                arbitraryBlockConfig.TryGetAnimation(ArbitraryBlockAnimation.Explode, out var childAnimationName) &&
                !string.IsNullOrEmpty(childAnimationName))
            {
                var longestChildDuration = 0f;
                foreach (var skeletonDataAsset in arbitraryBlockConfig.SkeletonDataAssets.Values)
                {
                    if (!skeletonDataAsset) continue;
                    var childAnimation = skeletonDataAsset.GetSkeletonData(true)?.FindAnimation(childAnimationName);
                    if (childAnimation != null)
                        longestChildDuration = Mathf.Max(longestChildDuration, childAnimation.Duration);
                }

                if (longestChildDuration > 0f) return longestChildDuration;
            }

            var animationName = blockConfig.ExplodeAnimation;
            var animation = blockConfig.SkeletonDataAsset && !string.IsNullOrEmpty(animationName)
                ? blockConfig.SkeletonDataAsset.GetSkeletonData(true)?.FindAnimation(animationName)
                : null;
            return animation != null && animation.Duration > 0f ? animation.Duration : 0f;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class ChildExplosionAnimationCompletion
    {
        private readonly UniTaskCompletionSource<bool> _completionSource = new();
        private readonly HashSet<int> _reportedChildren = new();
        private readonly object _sync = new();
        private int _remainingChildren;
        private bool _allAnimationsCompleted = true;

        public UniTask<bool> Task => _completionSource.Task;

        public ChildExplosionAnimationCompletion(int expectedChildren)
        {
            if (expectedChildren < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedChildren));

            _remainingChildren = expectedChildren;
            if (expectedChildren == 0)
                _completionSource.TrySetResult(false);
        }

        public void Report(int childId, bool animationCompleted)
        {
            var shouldComplete = false;
            var allAnimationsCompleted = false;

            lock (_sync)
            {
                if (_remainingChildren == 0 || !_reportedChildren.Add(childId)) return;

                _allAnimationsCompleted &= animationCompleted;
                _remainingChildren--;
                if (_remainingChildren == 0)
                {
                    shouldComplete = true;
                    allAnimationsCompleted = _allAnimationsCompleted;
                }
            }

            if (shouldComplete)
                _completionSource.TrySetResult(allAnimationsCompleted);
        }

        internal void ReportUnavailableChild()
        {
            var shouldComplete = false;

            lock (_sync)
            {
                if (_remainingChildren == 0) return;

                _allAnimationsCompleted = false;
                _remainingChildren--;
                shouldComplete = _remainingChildren == 0;
            }

            if (shouldComplete)
                _completionSource.TrySetResult(false);
        }
    }

    public static class ChildExplosionAnimationWait
    {
        public static async UniTask<bool> WaitForAnimationOrTimeout(
            Func<CancellationToken, UniTask<bool>> waitForAnimation,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (waitForAnimation == null) throw new ArgumentNullException(nameof(waitForAnimation));

            using var raceCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var animationTask = waitForAnimation(raceCancellation.Token);
            var timeoutTask = UniTask.Delay(timeout, delayType: DelayType.UnscaledDeltaTime,
                    cancellationToken: raceCancellation.Token)
                .ContinueWith(() => false);
            try
            {
                var (winner, animationCompleted, _) = await UniTask.WhenAny(animationTask, timeoutTask);
                return winner == 0 && animationCompleted;
            }
            finally
            {
                raceCancellation.Cancel();
            }
        }
    }

    public struct ScaleInCommandData
    {
        public Promise<Unit> Promise;
        public Vector3 Scale;
        
        public ScaleInCommandData(Promise<Unit> promise, Vector3 scale)
        {
            Promise = promise;
            Scale = scale;
        }
    }

    public struct RotateCommandData
    {
        public Promise<Unit> Promise;
        public Quaternion Rotation;
        
        public RotateCommandData(Promise<Unit> promise, Quaternion rotation)
        {
            Promise = promise;
            Rotation = rotation;
        }
    }
    #endregion
    
    public class BlockViewModel : IDisposable
    {
        public ReactiveProperty<BlockInteractionState> BlockInteractionState { get; private set; } = new(Grid.BlockInteractionState.PlacedOnSpawn);
        public ReactiveProperty<Transform> SpawnReturnTarget { get; private set; } = new();
        public ReadOnlyReactiveProperty<BlockColor> BlockColor => _model.BlockColor;
        public ReadOnlyReactiveProperty<BlockState> BlockState => _model.BlockState;
        public ReactiveCommand<int> SetSortingLayerCommand { get; private set; } = new();
        public ReactiveCommand<int> SetSortingOrderCommand { get; private set; } = new();
        public ReactiveCommand<ExplodeCommandData> ExplodeCommand { get; private set; } = new();
        public ReactiveCommand<ScaleInCommandData> ScaleInCommand { get; private set; } = new();
        public ReactiveCommand<RotateCommandData> RotateCommand { get; private set; } = new();
        public ReactiveCommand<Unit> DestroyCommand { get; private set; } = new();
        
        private readonly BlockModel _model;
        private int _explosionCommandHandlerCount;
        public ShapeTrayPiece TrayPiece => _model.TrayPiece;
        private IDisposable _bindings;

        internal void RegisterExplosionCommandHandler()
        {
            _explosionCommandHandlerCount++;
        }

        internal void UnregisterExplosionCommandHandler()
        {
            if (_explosionCommandHandlerCount > 0) _explosionCommandHandlerCount--;
        }

        internal void ExecuteExplosion(ExplodeCommandData data)
        {
            if (_model.Config is ArbitraryBlockConfig)
            {
                data.ExplosionStartTime = Time.realtimeSinceStartupAsDouble;
                var speedMultiplier = data.FitType == FitType.FitMe ? 2f : 6.67f;
                data.ExplosionDeadline = ShapeExplosionTiming.GetDeadline(
                    _model.Config, speedMultiplier, data.ExplosionStartTime);
            }

            ExplodeCommand.Execute(data);
            if (_explosionCommandHandlerCount == 0)
            {
                if (data.ChildAnimationCompletion != null)
                    SettleExplosionWithoutBlockView(data).Forget();
                else
                    data.Promise.TrySetResult(Unit.Default);
            }
        }

        private async UniTask SettleExplosionWithoutBlockView(ExplodeCommandData data)
        {
            try
            {
                if (data.ExplosionDeadline <= 0d) return;

                var remainingDuration = data.ExplosionDeadline - Time.realtimeSinceStartupAsDouble;
                var childAnimationsCompleted = await ChildExplosionAnimationWait.WaitForAnimationOrTimeout(
                    token => data.ChildAnimationCompletion.Task.AttachExternalCancellation(token),
                    remainingDuration > 0d ? TimeSpan.FromSeconds(remainingDuration) : TimeSpan.Zero,
                    CancellationToken.None);
                if (childAnimationsCompleted) return;

                remainingDuration = data.ExplosionDeadline - Time.realtimeSinceStartupAsDouble;
                if (remainingDuration > 0d)
                    await UniTask.Delay(TimeSpan.FromSeconds(remainingDuration),
                        delayType: DelayType.UnscaledDeltaTime);
            }
            finally
            {
                data.Promise.TrySetResult(Unit.Default);
            }
        }

        [Inject]
        public BlockViewModel(BlockModel model)
        {
            _model = model;
            Bind();
        }

        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _bindings = disposableBuilder.Build();
        }
        
        public void Dispose()
        {
            _bindings?.Dispose();
            SpawnReturnTarget.Dispose();
        }

        public void SetSpawnReturnTarget(Transform target)
        {
            SpawnReturnTarget.Value = target;
        }
    }
}