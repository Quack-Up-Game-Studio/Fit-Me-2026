using System;
using System.Collections.Generic;
using FitMe.Grid;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Utils;
using R3;
using UnityEngine;
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

        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(false, false, false)]
        public void HintRequestCompletion_CleansUpHintAwaitEvenWhenSearchOwnershipChanged(
            bool isHintRequest, bool ownsActiveSearch, bool shouldComplete)
        {
            Assert.That(
                HintRequestCompletionPolicy.ShouldComplete(isHintRequest, ownsActiveSearch),
                Is.EqualTo(shouldComplete));
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
