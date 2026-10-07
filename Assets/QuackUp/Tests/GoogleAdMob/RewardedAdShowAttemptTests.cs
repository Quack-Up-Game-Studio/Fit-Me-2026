using NUnit.Framework;
using R3;
using System;
using QuackUp.GoogleAdMob;
using UnityEngine;

namespace QuackUp.GoogleAdMob.Tests
{
    public class RewardedAdShowAttemptTests
    {
        [Test]
        public void FailedShow_DoesNotReceiveLaterReward()
        {
            var rewards = new Subject<Unit>();
            var closed = new Subject<Unit>();
            var failed = new Subject<Unit>();
            var rewardCount = 0;

            using (var attempt = new RewardedAdShowAttempt(
                       rewards,
                       closed,
                       failed,
                       () => rewardCount++))
            {
                Assert.That(attempt.TryShow(() => false), Is.False);
                rewards.OnNext(Unit.Default);
            }

            Assert.That(rewardCount, Is.Zero);
        }

        [Test]
        public void Close_ReleasesAttemptBeforeLaterReward()
        {
            var rewards = new Subject<Unit>();
            var closed = new Subject<Unit>();
            var failed = new Subject<Unit>();
            var rewardCount = 0;

            using (var attempt = new RewardedAdShowAttempt(
                       rewards,
                       closed,
                       failed,
                       () => rewardCount++))
            {
                Assert.That(attempt.TryShow(() => true), Is.True);
                closed.OnNext(Unit.Default);
                rewards.OnNext(Unit.Default);
            }

            Assert.That(rewardCount, Is.Zero);
        }

        [Test]
        public void Reward_IsDeliveredAtMostOnce()
        {
            var rewards = new Subject<Unit>();
            var closed = new Subject<Unit>();
            var failed = new Subject<Unit>();
            var rewardCount = 0;

            using (var attempt = new RewardedAdShowAttempt(
                       rewards,
                       closed,
                       failed,
                       () => rewardCount++))
            {
                Assert.That(attempt.TryShow(() => true), Is.True);
                rewards.OnNext(Unit.Default);
                rewards.OnNext(Unit.Default);
            }

            Assert.That(rewardCount, Is.EqualTo(1));
        }
    }
}
