using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Grid;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
    public static class GameplayHintButtonPolicy
    {
        public static bool ShouldShowHintButton(bool isShapeMode) => isShapeMode;

        public static bool ShouldShowRetryButton(bool isShapeMode) => isShapeMode;

        public static ColorBlock WithUnavailableTint(ColorBlock colors)
        {
            colors.disabledColor = Color.gray;
            return colors;
        }

        public static bool CanUseHintDuringSearch(
            bool canRequestHint, bool isBusy, bool activeSearchIsHint, bool hintQueued) =>
            canRequestHint && (!isBusy || (!activeSearchIsHint && !hintQueued));
    }

    public static class HintConsumptionPolicy
    {
        public static bool TrySpendIfRequired(HintSolutionStatus status, Func<bool> trySpend)
        {
            if (trySpend == null) throw new ArgumentNullException(nameof(trySpend));
            return status is HintSolutionStatus.Feasible or HintSolutionStatus.Infeasible
                ? trySpend()
                : true;
        }
    }

    public static class HintRequestCompletionPolicy
    {
        public static bool ShouldComplete(bool isHintRequest, bool ownsActiveSearch, bool queuedHintStarted) =>
            isHintRequest || (ownsActiveSearch && !queuedHintStarted);
    }

    public static class ShapeHintAllowancePolicy
    {
        public const int HintsPerLevel = 4;

        public static int Reset() => HintsPerLevel;

        public static bool TrySpend(ref int remainingHints)
        {
            if (remainingHints <= 0) return false;
            remainingHints--;
            return true;
        }
    }

    public sealed class ShapeHintController : IPostInitializable, IDisposable
    {
        public const int ShapeLevelHintCap = ShapeHintAllowancePolicy.HintsPerLevel;
        public static bool ShouldShowNoSolutionNotification(HintSolutionStatus status) =>
            status == HintSolutionStatus.Infeasible;
        private readonly GridManager _gridManager;
        private readonly BlockManager _blockManager;
        private readonly HintSearchConfig _searchConfig;
        private readonly ILevelManager _levelManager;
        private readonly IGameStateManager _gameStateManager;
        private readonly IMessageHub _notificationMessageHub;
        private readonly IMessageHub _blockManagerMessageHub;
        private readonly IMessageHub _gridManagerMessageHub;
        private readonly ReactiveProperty<int> _remainingHints = new(ShapeLevelHintCap);
        private readonly ReactiveProperty<bool> _isVisible = new(false);
        private readonly ReactiveProperty<bool> _canUseHint = new(false);
        private readonly ReactiveProperty<bool> _retryVisible = new(false);
        private readonly ReactiveProperty<bool> _retryFlashVisible = new(false);
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyIsVisible;
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyCanUseHint;
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyRetryVisible;
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyRetryFlashVisible;
        private readonly ReadOnlyReactiveProperty<int> _readOnlyRemainingHints;
        private IDisposable _subscriptions;
        private IDisposable _flashTimer;
        private IDisposable _retryGuidanceTimer;
        private CancellationTokenSource _activeSearchCancellation;
        private UniTaskCompletionSource _hintRequestCompletion;
        private string _observedIdentity;
        private string _lockedIdentity;
        private bool _isBusy;
        private bool _activeSearchIsHint;
        private bool _hintQueued;
        private bool _flashVisible;
        private bool _isGameOver;
        private bool _retryGuidanceActive;
        private bool _disposed;

        public ReadOnlyReactiveProperty<bool> IsVisible => _readOnlyIsVisible;
        public ReadOnlyReactiveProperty<bool> CanUseHint => _readOnlyCanUseHint;
        public ReadOnlyReactiveProperty<bool> RetryVisible => _readOnlyRetryVisible;
        public ReadOnlyReactiveProperty<bool> RetryFlashVisible => _readOnlyRetryFlashVisible;
        public ReadOnlyReactiveProperty<int> RemainingHints => _readOnlyRemainingHints;

        [Inject]
        public ShapeHintController(
            GridManager gridManager,
            BlockManager blockManager,
            HintSearchConfig searchConfig,
            ILevelManager levelManager,
            IGameStateManager gameStateManager,
            [Key(NotificationMessageHub.MessageHubKey)] IMessageHub notificationMessageHub,
            [Key(BlockManagerMessageHub.MessageHubKey)] IMessageHub blockManagerMessageHub,
            [Key(GridManagerMessageHub.GridManagerMessageHubKey)] IMessageHub gridManagerMessageHub)
        {
            _gridManager = gridManager;
            _blockManager = blockManager;
            _searchConfig = searchConfig;
            _levelManager = levelManager;
            _gameStateManager = gameStateManager;
            _notificationMessageHub = notificationMessageHub;
            _blockManagerMessageHub = blockManagerMessageHub;
            _gridManagerMessageHub = gridManagerMessageHub;
            _readOnlyIsVisible = _isVisible.ToReadOnlyReactiveProperty();
            _readOnlyCanUseHint = _canUseHint.ToReadOnlyReactiveProperty();
            _readOnlyRetryVisible = _retryVisible.ToReadOnlyReactiveProperty();
            _readOnlyRetryFlashVisible = _retryFlashVisible.ToReadOnlyReactiveProperty();
            _readOnlyRemainingHints = _remainingHints.ToReadOnlyReactiveProperty();
        }

        public void PostInitialize()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _gridManager.OnBlockPlaced
                .Subscribe(_ =>
                {
                    RefreshSolverState();
                    CheckBoardFeasibility();
                })
                .AddTo(ref disposableBuilder);
            _gridManager.OnAboutToClearGrid.Subscribe(_ => ClearHint()).AddTo(ref disposableBuilder);
            _gridManager.OnCellsCreated.Subscribe(_ => RefreshSolverState()).AddTo(ref disposableBuilder);
            _gridManager.OnClearGrid.Subscribe(_ =>
            {
                ClearRetryGuidance();
                RefreshSolverState();
            }).AddTo(ref disposableBuilder);
            _blockManager.OnSwap.Subscribe(_ => RefreshSolverState()).AddTo(ref disposableBuilder);
            _blockManagerMessageHub.GetObservable<BlockSpawnedEvent>()
                .Subscribe(_ =>
                {
                    RefreshSolverState();
                    CheckBoardFeasibility();
                })
                .AddTo(ref disposableBuilder);
            _gridManagerMessageHub.GetObservable<SpawnWithShapeLevelEvent>()
                .Subscribe(_ =>
                {
                    if (_levelManager.CurrentGameMode != GameMode.LevelShape) return;
                    _remainingHints.Value = ShapeHintAllowancePolicy.Reset();
                    UpdateAvailability();
                })
                .AddTo(ref disposableBuilder);
            _remainingHints.Subscribe(_ => UpdateAvailability()).AddTo(ref disposableBuilder);
            _gameStateManager.GameState
                .Subscribe(state =>
                {
                    _isGameOver = state == GameState.GameOver;
                    if (_isGameOver)
                    {
                        _activeSearchCancellation?.Cancel();
                        ClearHint();
                        ClearRetryGuidance();
                    }
                    UpdateAvailability();
                })
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
            RefreshSolverState();
        }

        public UniTask RequestHint(CancellationToken cancellationToken)
        {
            if (_disposed || !CanRequestHint() || !TryCreateSnapshot(out var snapshot))
                return UniTask.CompletedTask;

            if (_isBusy)
            {
                if (!_activeSearchIsHint)
                {
                    _hintQueued = true;
                    _hintRequestCompletion ??= new UniTaskCompletionSource();
                    UpdateAvailability();
                    return _hintRequestCompletion.Task.AttachExternalCancellation(cancellationToken);
                }
                return UniTask.CompletedTask;
            }

            _hintRequestCompletion = new UniTaskCompletionSource();
            StartSearch(snapshot, _searchConfig.MaxSearchNodes, isHintRequest: true);
            return _hintRequestCompletion.Task.AttachExternalCancellation(cancellationToken);
        }

        private void CheckBoardFeasibility()
        {
            if (_disposed || _isGameOver || _retryGuidanceActive || _isBusy ||
                _levelManager.CurrentGameMode != GameMode.LevelShape || !TryCreateSnapshot(out var snapshot))
                return;

            StartSearch(snapshot, _searchConfig.MaxSearchNodes, isHintRequest: false);
        }

        private void StartSearch(SearchSnapshot snapshot, int maxSearchNodes, bool isHintRequest)
        {
            _isBusy = true;
            _activeSearchIsHint = isHintRequest;
            UpdateAvailability();
            var cancellation = new CancellationTokenSource();
            _activeSearchCancellation = cancellation;
            SearchAsync(snapshot, cancellation, maxSearchNodes, isHintRequest).Forget();
        }

        private async UniTaskVoid SearchAsync(
            SearchSnapshot snapshot, CancellationTokenSource cancellation, int maxSearchNodes, bool isHintRequest)
        {
            try
            {
                var result = await UniTask.RunOnThreadPool(
                    () => HintSolutionFinder.FindFirstMove(
                        snapshot.PlayableMask,
                        snapshot.OccupiedMask,
                        snapshot.ObstacleMask,
                        snapshot.Schemas,
                        snapshot.ActiveHandCount,
                        maxSearchNodes,
                        cancellation.Token),
                    cancellationToken: cancellation.Token);
                await UniTask.SwitchToMainThread(cancellation.Token);

                if (_disposed || _isGameOver || cancellation.IsCancellationRequested ||
                    !TryCreateSnapshot(out var currentSnapshot) ||
                    !string.Equals(currentSnapshot.Identity, snapshot.Identity, StringComparison.Ordinal))
                    return;

                if (isHintRequest && !HintConsumptionPolicy.TrySpendIfRequired(
                        result.Status, TrySpendLocalHint))
                {
                    UpdateAvailability();
                    return;
                }

                if (result.Status == HintSolutionStatus.Feasible)
                {
                    if (isHintRequest)
                    {
                        _lockedIdentity = snapshot.Identity;
                        _gridManager.SetHintHighlights(result.FirstMove.Cells);
                        StartFlash(snapshot.Identity);
                    }
                }
                else if (result.Status == HintSolutionStatus.SearchLimitReached)
                {
                    if (isHintRequest) _lockedIdentity = snapshot.Identity;
                }
                else if (result.Status == HintSolutionStatus.Infeasible)
                {
                    _lockedIdentity = snapshot.Identity;
                    StartRetryGuidance();
                    if (isHintRequest && ShouldShowNoSolutionNotification(result.Status))
                        ShowNotification("No solution found. Tap Retry to clear the board.");
                }
                else
                {
                    if (isHintRequest)
                        _lockedIdentity = snapshot.Identity;
                }
                UpdateAvailability();
            }
            catch (OperationCanceledException)
            {
                // Solver input changed while the immutable snapshot was being searched.
            }
            finally
            {
                var ownsActiveSearch = ReferenceEquals(_activeSearchCancellation, cancellation);
                var queuedHintStarted = false;
                if (ownsActiveSearch)
                {
                    _activeSearchCancellation = null;
                    _isBusy = false;
                    _activeSearchIsHint = false;
                    if (!_disposed && _hintQueued)
                    {
                        _hintQueued = false;
                        if (CanRunQueuedHint() && TryCreateSnapshot(out var queuedSnapshot))
                        {
                            queuedHintStarted = true;
                            StartSearch(queuedSnapshot, _searchConfig.MaxSearchNodes, isHintRequest: true);
                        }
                        else
                            _hintRequestCompletion?.TrySetResult();
                    }
                    if (!_disposed) UpdateAvailability();
                }
                if (HintRequestCompletionPolicy.ShouldComplete(
                        isHintRequest, ownsActiveSearch, queuedHintStarted))
                    CompleteHintRequest();
                cancellation.Dispose();
            }
        }

        private void CompleteHintRequest()
        {
            _hintRequestCompletion?.TrySetResult();
            _hintRequestCompletion = null;
        }

        private void StartFlash(string identity)
        {
            _flashTimer?.Dispose();
            _flashVisible = true;
            _flashTimer = Observable.Interval(TimeSpan.FromSeconds(0.35))
                .Subscribe(_ =>
                {
                    if (_disposed || !string.Equals(_observedIdentity, identity, StringComparison.Ordinal))
                    {
                        ClearHint();
                        return;
                    }
                    _flashVisible = !_flashVisible;
                    _gridManager.SetHintFlashVisible(_flashVisible);
                });
        }

        private void StartRetryGuidance()
        {
            _retryGuidanceTimer?.Dispose();
            _retryGuidanceActive = true;
            _retryVisible.Value = true;
            _retryFlashVisible.Value = true;
            _retryGuidanceTimer = Observable.Interval(TimeSpan.FromSeconds(0.35))
                .Subscribe(_ =>
                {
                    if (_disposed || _isGameOver) return;
                    _retryFlashVisible.Value = !_retryFlashVisible.CurrentValue;
                });
        }

        private void RefreshSolverState()
        {
            var identity = TryCreateSnapshot(out var snapshot) ? snapshot.Identity : null;
            _isVisible.Value = _levelManager.CurrentGameMode == GameMode.LevelShape;
            if (!string.Equals(identity, _observedIdentity, StringComparison.Ordinal))
            {
                _observedIdentity = identity;
                _lockedIdentity = null;
                _activeSearchCancellation?.Cancel();
                _isBusy = false;
                _activeSearchIsHint = false;
                ClearHint();
                if (_hintQueued && CanRunQueuedHint() && TryCreateSnapshot(out var queuedSnapshot))
                {
                    _hintQueued = false;
                    StartSearch(queuedSnapshot, _searchConfig.MaxSearchNodes, isHintRequest: true);
                }
                else if (_hintQueued)
                {
                    _hintQueued = false;
                    CompleteHintRequest();
                }
            }
            UpdateAvailability();
        }

        private void UpdateAvailability()
        {
            var isShapeMode = _levelManager.CurrentGameMode == GameMode.LevelShape;
            _isVisible.Value = GameplayHintButtonPolicy.ShouldShowHintButton(isShapeMode);
            _retryVisible.Value = GameplayHintButtonPolicy.ShouldShowRetryButton(isShapeMode);
            var hasSnapshot = TryCreateSnapshot(out var snapshot);
            _canUseHint.Value = GameplayHintButtonPolicy.CanUseHintDuringSearch(
                                    CanRequestHint(), _isBusy, _activeSearchIsHint, _hintQueued) &&
                                hasSnapshot && !string.Equals(snapshot.Identity, _lockedIdentity,
                                    StringComparison.Ordinal);
        }

        private bool CanRequestHint() => CanRunQueuedHint() && !_retryGuidanceActive;

        private bool CanRunQueuedHint() =>
            _levelManager.CurrentGameMode == GameMode.LevelShape && !_isGameOver &&
            _remainingHints.CurrentValue > 0;

        private bool TryCreateSnapshot(out SearchSnapshot snapshot)
        {
            snapshot = default;
            if (_levelManager.CurrentGameMode != GameMode.LevelShape ||
                !_gridManager.TryCreateHintBoardMasks(
                    out var playableMask, out var occupiedMask, out var obstacleMask) ||
                !_blockManager.TryGetShapeHintSupply(out var schemas, out var activeHandCount) ||
                !HintSnapshotIdentity.TryCreate(
                    playableMask, occupiedMask, obstacleMask, schemas, activeHandCount, out var identity))
                return false;

            snapshot = new SearchSnapshot(
                playableMask, occupiedMask, obstacleMask, schemas, activeHandCount, identity);
            return true;
        }

        private void ShowNotification(string message)
        {
            _notificationMessageHub.Publish(new NotificationDisplayEvent(
                NotificationType.General,
                new GeneralNotificationData { message = message }));
        }

        private void ClearHint()
        {
            _activeSearchCancellation?.Cancel();
            _flashTimer?.Dispose();
            _flashTimer = null;
            _flashVisible = false;
            _gridManager.ClearHintFlash();
        }

        private void ClearRetryGuidance()
        {
            _retryGuidanceTimer?.Dispose();
            _retryGuidanceTimer = null;
            _retryGuidanceActive = false;
            _retryFlashVisible.Value = false;
        }

        private bool TrySpendLocalHint()
        {
            var remainingHints = _remainingHints.CurrentValue;
            if (!ShapeHintAllowancePolicy.TrySpend(ref remainingHints)) return false;
            _remainingHints.Value = remainingHints;
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _activeSearchCancellation?.Cancel();
            CompleteHintRequest();
            _subscriptions?.Dispose();
            ClearHint();
            ClearRetryGuidance();
            _readOnlyIsVisible.Dispose();
            _readOnlyCanUseHint.Dispose();
            _readOnlyRetryVisible.Dispose();
            _readOnlyRetryFlashVisible.Dispose();
            _readOnlyRemainingHints.Dispose();
            _isVisible.Dispose();
            _canUseHint.Dispose();
            _retryVisible.Dispose();
            _retryFlashVisible.Dispose();
            _remainingHints.Dispose();
        }

        private readonly struct SearchSnapshot
        {
            public readonly int[,] PlayableMask;
            public readonly int[,] OccupiedMask;
            public readonly int[,] ObstacleMask;
            public readonly List<int[,]> Schemas;
            public readonly int ActiveHandCount;
            public readonly string Identity;

            public SearchSnapshot(
                int[,] playableMask, int[,] occupiedMask, int[,] obstacleMask,
                List<int[,]> schemas, int activeHandCount, string identity)
            {
                PlayableMask = playableMask;
                OccupiedMask = occupiedMask;
                ObstacleMask = obstacleMask;
                Schemas = schemas;
                ActiveHandCount = activeHandCount;
                Identity = identity;
            }
        }
    }
}
