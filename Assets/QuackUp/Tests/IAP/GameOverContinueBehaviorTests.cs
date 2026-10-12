using System;
using System.Collections.Generic;
using System.Reflection;
using FitMe.Panel;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.GoogleAdMob;
using QuackUp.IAP;
using QuackUp.Utils;
using R3;

namespace QuackUp.IAP.Tests
{
    public sealed class GameOverContinueBehaviorTests
    {
        [Test]
        public void AcceptedRewardedContinue_PublishesContinueAndClearsCellsButKeepsObstacles()
        {
            using var ads = new AdsService(null, null, null);
            using var purchases = new InAppPurchaseManager(null, null);
            var rewardedAd = new TestRewardedAd();
            var instances = (Dictionary<Type, AdsInstance>)typeof(AdsService)
                .GetField("_adsInstances", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ads);
            instances.Add(typeof(RewardedAdInstance), rewardedAd);
            var gridHub = new RecordingHub();
            var blockHub = new RecordingHub();
            using var viewModel = new GameOverPanelViewModel(null, ads, purchases, 1, 10f, gridHub, blockHub);
            var returnedToGameplay = 0;
            using var returnSubscription = viewModel.OnReturnToGameplay.Subscribe(_ => returnedToGameplay++);
            viewModel.AdsContinueCommand.Execute(Unit.Default);
            rewardedAd.EarnReward();

            Assert.That(returnedToGameplay, Is.EqualTo(1));
            Assert.That(viewModel.RemainingContinueCount.CurrentValue, Is.Zero);
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

        private sealed class TestRewardedAd : RewardedAdInstance
        {
            private bool _showing;
            public TestRewardedAd() : base(null, null, null) { }
            public override bool CanShowAd() => Enabled && !_showing;
            public override void Load() { }
            public override bool TryShow()
            {
                _showing = true;
                return true;
            }

            public void EarnReward() => ((Subject<Unit>)typeof(RewardedAdInstance)
                .GetField("_onUserEarnedReward", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(this)).OnNext(Unit.Default);
        }
    }
}