using System;
using System.Collections.Generic;
using FitMe.Grid;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Utils;
using R3;

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

        [Test]
        public void Retry_PublishesContinueThenClearsPlacedBlocksWithoutDestroyingObstacles()
        {
            var gridHub = new RecordingHub();
            var blockHub = new RecordingHub();
            using var viewModel = new GameplayPanelViewModel(null, null, null, gridHub, blockHub);

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

            public IDisposable Subscribe<TMessage>(Action<TMessage> action) =>
                throw new NotSupportedException("This fixture only records published messages.");

            public Observable<TMessage> GetObservable<TMessage>() =>
                throw new NotSupportedException("This fixture only records published messages.");
        }
    }
}
