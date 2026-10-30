using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Utils;
using R3;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FitMe.Panel.Tests
{
    public sealed class GameplayRetryBehaviorTests
    {
        [TestCase(HintSolutionStatus.Feasible, true)]
        [TestCase(HintSolutionStatus.Infeasible, true)]
        [TestCase(HintSolutionStatus.SearchLimitReached, false)]
        [TestCase(HintSolutionStatus.InvalidInput, false)]
        public void HintOutcome_SpendsOnlyForDefinitiveResults(HintSolutionStatus status, bool shouldSpend)
        {
            var spendCalled = false;

            var spendSucceeded = HintConsumptionPolicy.TrySpendIfRequired(status, () =>
            {
                spendCalled = true;
                return true;
            });

            Assert.That(spendSucceeded, Is.True);
            Assert.That(spendCalled, Is.EqualTo(shouldSpend));
        }

        [Test]
        public void InfeasibleHintOutcome_DoesNotProceedWhenHintCannotBeSpent()
        {
            var spendSucceeded = HintConsumptionPolicy.TrySpendIfRequired(
                HintSolutionStatus.Infeasible,
                () => false);

            Assert.That(spendSucceeded, Is.False);
        }

        [TestCase(true, false, false, true)]
        [TestCase(false, true, false, true)]
        [TestCase(false, true, true, false)]
        [TestCase(false, false, false, false)]
        public void HintRequestCompletion_WaitsForQueuedHintSearch(
            bool isHintRequest, bool ownsActiveSearch, bool queuedHintStarted, bool shouldComplete)
        {
            Assert.That(
                HintRequestCompletionPolicy.ShouldComplete(isHintRequest, ownsActiveSearch, queuedHintStarted),
                Is.EqualTo(shouldComplete));
        }

        [UnityTest]
        public IEnumerator HintQueuedDuringAutomaticFeasibilitySearch_RunsAndCompletes()
        {
            var hub = new ReactiveTestHub();
            var gridObject = new GameObject("Queued hint test grid");
            var gridConfig = ScriptableObject.CreateInstance<GridManagerConfig>();
            var gridManager = new GridManager(gridObject.AddComponent<UnityEngine.Grid>(), gridConfig, null, null, hub);
            var cellObject = new GameObject("Queued hint test cell");
            var cellModel = new CellModel();
            var blockObject = new GameObject("Queued hint test block");
            var blockModel = new BlockModel(null, null);
            var blockManagerConfig = ScriptableObject.CreateInstance<BlockManagerConfig>();
            var searchConfig = ScriptableObject.CreateInstance<HintSearchConfig>();
            BlockManager blockManager = null;
            ShapeHintController hintController = null;
            try
            {
                var entry = new ShapePlayerBlockRuntimeEntry(null, new[,] { { 1 } });
                var runtimeData = new ShapeLevelRuntimeData(
                    null, new[,] { { 1 } }, Array.Empty<ShapeObstacleRuntimeData>(), new[] { entry });
                typeof(GridManager).GetProperty(nameof(GridManager.CurrentShapeLevelRuntimeData))
                    .SetValue(gridManager, runtimeData);
                typeof(GridManager).GetField("_cellArray", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(gridManager, new[,] { { new CellInstance(cellModel, null, cellObject) } });

                blockManager = new BlockManager(
                    gridManager, blockManagerConfig, null, Array.Empty<BlockManager.SpawnPointData>(), null,
                    Array.Empty<BlockManager.SpawnPointData>(), hub);
                typeof(BlockManager).GetField("_shapeLevelRuntimeData", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(blockManager, runtimeData);
                typeof(BlockManager).GetField("_traySpawned", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(blockManager, true);
                var trayBlocks = (List<BlockInstance>)typeof(BlockManager)
                    .GetField("_trayBlocks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(blockManager);
                blockModel.SetShapeSchema(entry.Schema);
                blockModel.TrayPiece = new ShapeTrayPiece(
                    0, entry.Schema, new Rect(-2, -2, 4, 4), .2f, 0, Quaternion.identity, Vector2.zero);
                trayBlocks.Add(new BlockInstance(blockModel, null, null, blockObject));

                var levelManager = new LevelManagerMock(GameState.PlaceBlock)
                    { CurrentGameMode = GameMode.LevelShape };
                hintController = new ShapeHintController(
                    gridManager, blockManager, searchConfig, levelManager, levelManager, hub, hub, hub);
                hintController.PostInitialize();
                typeof(ShapeHintController).GetMethod(
                        "CheckBoardFeasibility", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(hintController, null);
                Assert.That(GetPrivateField<bool>(hintController, "_isBusy"), Is.True);
                Assert.That(GetPrivateField<bool>(hintController, "_activeSearchIsHint"), Is.False);

                var requestCompleted = false;
                ObserveHintRequest(hintController.RequestHint(CancellationToken.None), () => requestCompleted = true)
                    .Forget();
                for (var frame = 0; frame < 60 && !requestCompleted; frame++) yield return null;

                Assert.That(requestCompleted, Is.True,
                    "A hint queued behind automatic feasibility must launch and settle its request");
                Assert.That(hintController.RemainingHints.CurrentValue, Is.EqualTo(3));
            }
            finally
            {
                hintController?.Dispose();
                blockManager?.Dispose();
                gridManager.Dispose();
                hub.Dispose();
                cellModel.Dispose();
                blockModel.BlockState.Dispose();
                blockModel.UpdateGridCommand.Dispose();
                UnityEngine.Object.DestroyImmediate(gridObject);
                UnityEngine.Object.DestroyImmediate(cellObject);
                UnityEngine.Object.DestroyImmediate(blockObject);
                UnityEngine.Object.DestroyImmediate(gridConfig);
                UnityEngine.Object.DestroyImmediate(blockManagerConfig);
                UnityEngine.Object.DestroyImmediate(searchConfig);
            }
        }

        [Test]
        public void ShapeHintAllowance_AllowsFourSpendsAndCanResetForNewLevel()
        {
            var remainingHints = ShapeHintAllowancePolicy.Reset();
            Assert.That(remainingHints, Is.EqualTo(4));

            for (var expectedRemaining = 3; expectedRemaining >= 0; expectedRemaining--)
            {
                Assert.That(ShapeHintAllowancePolicy.TrySpend(ref remainingHints), Is.True);
                Assert.That(remainingHints, Is.EqualTo(expectedRemaining));
            }

            Assert.That(ShapeHintAllowancePolicy.TrySpend(ref remainingHints), Is.False);
            Assert.That(remainingHints, Is.Zero);
            Assert.That(ShapeHintAllowancePolicy.Reset(), Is.EqualTo(4));
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void HintButtonVisibility_DependsOnlyOnShapeMode(bool isShapeMode, bool expectedVisible)
        {
            Assert.That(
                GameplayHintButtonPolicy.ShouldShowHintButton(isShapeMode),
                Is.EqualTo(expectedVisible));
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void RetryButtonVisibility_DependsOnlyOnShapeMode(bool isShapeMode, bool expectedVisible)
        {
            Assert.That(GameplayHintButtonPolicy.ShouldShowRetryButton(isShapeMode), Is.EqualTo(expectedVisible));
        }

        [Test]
        public void HintButtonUnavailableTint_IsGray()
        {
            var colors = GameplayHintButtonPolicy.WithUnavailableTint(ColorBlock.defaultColorBlock);

            Assert.That(colors.disabledColor, Is.EqualTo(Color.gray));
        }

        [TestCase(false, false, true, true)]
        [TestCase(true, false, true, false)]
        [TestCase(false, true, true, false)]
        public void HintButton_RemainsAvailableOnlyDuringAutomaticFeasibilitySearch(
            bool isHintSearch, bool hintQueued, bool canRequestHint, bool expected)
        {
            var canUse = GameplayHintButtonPolicy.CanUseHintDuringSearch(
                canRequestHint, true, isHintSearch, hintQueued);

            Assert.That(canUse, Is.EqualTo(expected));
        }

        [TestCase(HintSolutionStatus.Feasible, false)]
        [TestCase(HintSolutionStatus.Infeasible, true)]
        [TestCase(HintSolutionStatus.SearchLimitReached, false)]
        [TestCase(HintSolutionStatus.InvalidInput, false)]
        public void HintNotification_IsShownOnlyWhenHintSearchFindsNoSolution(
            HintSolutionStatus status, bool expected)
        {
            Assert.That(ShapeHintController.ShouldShowNoSolutionNotification(status), Is.EqualTo(expected));
        }

        [Test]
        public void Retry_PublishesContinueThenClearsPlacedBlocksWithoutDestroyingObstacles()
        {
            var gridHub = new RecordingHub();
            var blockHub = new RecordingHub();
            var levelManager = new LevelManagerMock(GameState.PlaceBlock);
            using var viewModel = new GameplayPanelViewModel(null, levelManager, levelManager, null, gridHub, blockHub);

            viewModel.RetryCommand.Execute(Unit.Default);

            Assert.That(blockHub.Published, Has.Count.EqualTo(1));
            Assert.That(blockHub.Published[0], Is.TypeOf<ContinueEvent>());
            Assert.That(gridHub.Published, Has.Count.EqualTo(1));
            Assert.That(gridHub.Published[0], Is.TypeOf<ClearGridEvent>());
            var clearEvent = (ClearGridEvent)gridHub.Published[0];
            Assert.That(clearEvent.ShouldClearGrid, Is.True);
            Assert.That(clearEvent.ShouldDestroyObstacles, Is.False);
        }

        [Test]
        public void Retry_DropsSecondClickUntilClearCompletesAndAcceptsNextRetry()
        {
            using var gridHub = new ReactiveTestHub();
            using var blockHub = new ReactiveTestHub();
            using var state = new RetryGameState();
            var continueCount = 0;
            var clearCount = 0;
            using var continueBinding = blockHub.Subscribe<ContinueEvent>(_ => continueCount++);
            using var clearBinding = gridHub.Subscribe<ClearGridEvent>(_ =>
            {
                clearCount++;
                state.SetGameState(GameState.ClearingGrid);
            });
            using var viewModel = new GameplayPanelViewModel(null, state,
                new LevelManagerMock(GameState.PlaceBlock), null, gridHub, blockHub);

            Assert.That(viewModel.CanRetry.CurrentValue, Is.True);
            viewModel.RetryCommand.Execute(Unit.Default);
            viewModel.RetryCommand.Execute(Unit.Default);

            Assert.That(viewModel.CanRetry.CurrentValue, Is.False);
            Assert.That(clearCount, Is.EqualTo(1), "A pending clear must not admit another Retry.");
            Assert.That(continueCount, Is.EqualTo(1), "Rejected Retry must not change respawn intent.");
            state.SetGameState(GameState.PlaceBlock);
            Assert.That(viewModel.CanRetry.CurrentValue, Is.True);
            viewModel.RetryCommand.Execute(Unit.Default);
            Assert.That(clearCount, Is.EqualTo(2));
            Assert.That(continueCount, Is.EqualTo(2));
        }

        [TestCase(GameState.ClearingGrid, false)]
        [TestCase(GameState.PlaceBlock, true)]
        public void Retry_RejectsUnavailableGameplayBeforePublishing(GameState gameState, bool paused)
        {
            var gridHub = new RecordingHub();
            var blockHub = new RecordingHub();
            using var state = new RetryGameState();
            state.SetGameState(gameState);
            state.SetPause(paused);
            using var viewModel = new GameplayPanelViewModel(null, state,
                new LevelManagerMock(GameState.PlaceBlock), null, gridHub, blockHub);

            viewModel.RetryCommand.Execute(Unit.Default);

            Assert.That(viewModel.CanRetry.CurrentValue, Is.False);
            Assert.That(gridHub.Published, Is.Empty);
            Assert.That(blockHub.Published, Is.Empty);
        }

        private sealed class RetryGameState : IGameStateManager, IDisposable
        {
            private readonly ReactiveProperty<GameState> _state = new(FitMe.Shared.GameState.PlaceBlock);
            private readonly ReactiveProperty<bool> _paused = new(false);
            public ReadOnlyReactiveProperty<GameState> GameState => _state;
            public ReadOnlyReactiveProperty<bool> IsPaused => _paused;
            public void SetGameState(GameState state) => _state.Value = state;
            public void SetPause(bool pause) => _paused.Value = pause;
            public void Dispose() { _state.Dispose(); _paused.Dispose(); }
        }

        private static T GetPrivateField<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static async UniTask ObserveHintRequest(UniTask request, Action onCompleted)
        {
            await request;
            onCompleted();
        }

        private sealed class ReactiveTestHub : IMessageHub, IDisposable
        {
            private readonly Dictionary<Type, IDisposable> _streams = new();

            public void Publish<TMessage>(TMessage message) => GetStream<TMessage>().OnNext(message);

            public IDisposable Subscribe<TMessage>(Action<TMessage> action) => GetStream<TMessage>().Subscribe(action);

            public Observable<TMessage> GetObservable<TMessage>() => GetStream<TMessage>();

            private Subject<TMessage> GetStream<TMessage>()
            {
                if (!_streams.TryGetValue(typeof(TMessage), out var stream))
                {
                    stream = new Subject<TMessage>();
                    _streams.Add(typeof(TMessage), stream);
                }
                return (Subject<TMessage>)stream;
            }

            public void Dispose()
            {
                foreach (var stream in _streams.Values) stream.Dispose();
            }
        }

        private sealed class RecordingHub : IMessageHub
        {
            public List<object> Published { get; } = new();

            public void Publish<TMessage>(TMessage message) => Published.Add(message);

            public IDisposable Subscribe<TMessage>(Action<TMessage> action) => Disposable.Create(() => { });

            public Observable<TMessage> GetObservable<TMessage>() =>
                throw new NotSupportedException("This fixture only records published messages.");
        }
    }
}
