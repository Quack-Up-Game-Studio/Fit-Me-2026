using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using FitMe.Grid;
using FitMe.Shared;
using QuackUp.Utils;
using R3;
using VContainer;
using VContainer.Unity;

namespace FitMe.Panel
{
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

    public sealed class ShapeHintController : IPostInitializable, IDisposable
    {
        private readonly GridManager _gridManager;
        private readonly BlockManager _blockManager;
        private readonly HintManager _hintManager;
        private readonly HintSearchConfig _searchConfig;
        private readonly ILevelManager _levelManager;
        private readonly IGameStateManager _gameStateManager;
        private readonly IMessageHub _notificationMessageHub;
        private readonly IMessageHub _blockManagerMessageHub;
        private readonly ReactiveProperty<bool> _isVisible = new(false);
        private readonly ReactiveProperty<bool> _canUseHint = new(false);
        private readonly ReactiveProperty<bool> _retryVisible = new(false);
        private readonly ReactiveProperty<bool> _retryFlashVisible = new(false);
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyIsVisible;
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyCanUseHint;
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyRetryVisible;
        private readonly ReadOnlyReactiveProperty<bool> _readOnlyRetryFlashVisible;
        private IDisposable _subscriptions;
        private IDisposable _flashTimer;
        private IDisposable _retryGuidanceTimer;
        private CancellationTokenSource _activeSearchCancellation;
        private string _observedIdentity;
        private string _lockedIdentity;
        private bool _isBusy;
        private bool _flashVisible;
        private bool _isGameOver;
        private bool _disposed;

        public ReadOnlyReactiveProperty<bool> IsVisible => _readOnlyIsVisible;
        public ReadOnlyReactiveProperty<bool> CanUseHint => _readOnlyCanUseHint;
        public ReadOnlyReactiveProperty<bool> RetryVisible => _readOnlyRetryVisible;
        public ReadOnlyReactiveProperty<bool> RetryFlashVisible => _readOnlyRetryFlashVisible;

        [Inject]
        public ShapeHintController(
            GridManager gridManager,
            BlockManager blockManager,
            HintManager hintManager,
            HintSearchConfig searchConfig,
            ILevelManager levelManager,
            IGameStateManager gameStateManager,
            [Key(NotificationMessageHub.MessageHubKey)] IMessageHub notificationMessageHub,
            [Key(BlockManagerMessageHub.MessageHubKey)] IMessageHub blockManagerMessageHub)
        {
            _gridManager = gridManager;
            _blockManager = blockManager;
            _hintManager = hintManager;
            _searchConfig = searchConfig;
            _levelManager = levelManager;
            _gameStateManager = gameStateManager;
            _notificationMessageHub = notificationMessageHub;
            _blockManagerMessageHub = blockManagerMessageHub;
            _readOnlyIsVisible = _isVisible.ToReadOnlyReactiveProperty();
            _readOnlyCanUseHint = _canUseHint.ToReadOnlyReactiveProperty();
            _readOnlyRetryVisible = _retryVisible.ToReadOnlyReactiveProperty();
            _readOnlyRetryFlashVisible = _retryFlashVisible.ToReadOnlyReactiveProperty();
        }

        public void PostInitialize()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _gridManager.OnBlockPlaced.Subscribe(_ => RefreshSolverState()).AddTo(ref disposableBuilder);
            _gridManager.OnAboutToClearGrid.Subscribe(_ => ClearHint()).AddTo(ref disposableBuilder);
            _gridManager.OnCellsCreated.Subscribe(_ => RefreshSolverState()).AddTo(ref disposableBuilder);
            _gridManager.OnClearGrid.Subscribe(_ => RefreshSolverState()).AddTo(ref disposableBuilder);
            _blockManager.OnSwap.Subscribe(_ => RefreshSolverState()).AddTo(ref disposableBuilder);
            _blockManagerMessageHub.GetObservable<BlockSpawnedEvent>()
                .Subscribe(_ => RefreshSolverState())
                .AddTo(ref disposableBuilder);
            _hintManager.CurrentHints.Subscribe(_ => UpdateAvailability()).AddTo(ref disposableBuilder);
            _hintManager.Readiness.Subscribe(_ => UpdateAvailability()).AddTo(ref disposableBuilder);
            _gameStateManager.GameState
                .Subscribe(state =>
                {
                    _isGameOver = state == GameState.GameOver;
                    if (_isGameOver)
                    {
                        _activeSearchCancellation?.Cancel();
                        ClearHint();
                    }
                    UpdateAvailability();
                })
                .AddTo(ref disposableBuilder);
            _subscriptions = disposableBuilder.Build();
            RefreshSolverState();
        }

        public void RequestHint()
        {
            if (_disposed || !_canUseHint.CurrentValue || !TryCreateSnapshot(out var snapshot)) return;

            var maxSearchNodes = _searchConfig.MaxSearchNodes;
            _isBusy = true;
            UpdateAvailability();
            var cancellation = new CancellationTokenSource();
            _activeSearchCancellation = cancellation;
            SearchAsync(snapshot, cancellation, maxSearchNodes).Forget();
        }

        private async UniTaskVoid SearchAsync(
            SearchSnapshot snapshot, CancellationTokenSource cancellation, int maxSearchNodes)
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

                _isBusy = false;
                _activeSearchCancellation = null;
                if (!HintConsumptionPolicy.TrySpendIfRequired(
                        result.Status, () => _hintManager.TrySpendHints(1)))
                {
                    UpdateAvailability();
                    return;
                }

                if (result.Status == HintSolutionStatus.Feasible)
                {
                    _lockedIdentity = snapshot.Identity;
                    _gridManager.SetHintHighlights(result.FirstMove.Cells);
                    if (result.FirstMove.RequiresSwap)
                    {
                        var placementInstruction = result.FirstMove.BlockIndex == 1
                            ? "Swap the first hand block with the preview, then place the second hand block on the highlighted cells."
                            : "Swap the first hand block with the preview before placing the highlighted move.";
                        ShowNotification(placementInstruction);
                    }
                    else if (result.FirstMove.BlockIndex == 1)
                    {
                        ShowNotification("Place the second hand block on the highlighted cells.");
                    }
                    StartFlash(snapshot.Identity);
                }
                else if (result.Status == HintSolutionStatus.SearchLimitReached)
                {
                    _lockedIdentity = snapshot.Identity;
                    ShowNotification("Hint search reached its limit. Change the board to try again.");
                }
                else if (result.Status == HintSolutionStatus.Infeasible)
                {
                    _lockedIdentity = snapshot.Identity;
                    StartRetryGuidance(snapshot.Identity);
                    ShowNotification("No solution found. Tap Retry to clear the board.");
                }
                else
                {
                    ShowNotification("Hint is temporarily unavailable.");
                }
                UpdateAvailability();
            }
            catch (OperationCanceledException)
            {
                // Solver input changed while the immutable snapshot was being searched.
            }
            finally
            {
                if (ReferenceEquals(_activeSearchCancellation, cancellation))
                {
                    _activeSearchCancellation = null;
                    _isBusy = false;
                    if (!_disposed) UpdateAvailability();
                }
                cancellation.Dispose();
            }
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

        private void StartRetryGuidance(string identity)
        {
            _retryGuidanceTimer?.Dispose();
            _retryVisible.Value = true;
            _retryFlashVisible.Value = true;
            _retryGuidanceTimer = Observable.Interval(TimeSpan.FromSeconds(0.35))
                .Subscribe(_ =>
                {
                    if (_disposed || !string.Equals(_observedIdentity, identity, StringComparison.Ordinal))
                    {
                        ClearRetryGuidance();
                        return;
                    }

                    _retryFlashVisible.Value = !_retryFlashVisible.CurrentValue;
                });
        }

        public void DismissRetryGuidance()
        {
            ClearRetryGuidance();
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
                ClearHint();
                ClearRetryGuidance();
            }
            UpdateAvailability();
        }

        private void UpdateAvailability()
        {
            var isShapeMode = _levelManager.CurrentGameMode == GameMode.LevelShape;
            _isVisible.Value = isShapeMode;
            var hasSnapshot = TryCreateSnapshot(out var snapshot);
            _canUseHint.Value = isShapeMode && !_isGameOver && _hintManager.IsReady &&
                                _hintManager.CurrentHints.CurrentValue > 0 && !_isBusy &&
                                hasSnapshot && !string.Equals(snapshot.Identity, _lockedIdentity,
                                    StringComparison.Ordinal);
        }

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
            ClearRetryGuidance();
        }

        private void ClearRetryGuidance()
        {
            _retryGuidanceTimer?.Dispose();
            _retryGuidanceTimer = null;
            _retryVisible.Value = false;
            _retryFlashVisible.Value = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _activeSearchCancellation?.Cancel();
            _subscriptions?.Dispose();
            ClearHint();
            _readOnlyIsVisible.Dispose();
            _readOnlyCanUseHint.Dispose();
            _readOnlyRetryVisible.Dispose();
            _readOnlyRetryFlashVisible.Dispose();
            _isVisible.Dispose();
            _canUseHint.Dispose();
            _retryVisible.Dispose();
            _retryFlashVisible.Dispose();
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
