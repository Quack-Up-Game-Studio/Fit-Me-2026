using System;
using FitMe.GameData;
using NUnit.Framework;

namespace FitMe.Grid.Tests
{
    public class DailyHintRewardPolicyTests
    {
        [Test]
        public void NewDayBelowCapGrantsConfiguredAmountAndRecordsLocalDate()
        {
            var now = new DateTime(2026, 10, 20, 0, 1, 0, DateTimeKind.Local);

            var resolved = DailyHintRewardPolicy.TryResolve(now, 2, 5, 2, "2026-10-19",
                out var hints, out var checkedDate);

            Assert.That(resolved, Is.True);
            Assert.That(hints, Is.EqualTo(4));
            Assert.That(checkedDate, Is.EqualTo("2026-10-20"));
        }

        [Test]
        public void DailyRewardClampsToFreeCap()
        {
            var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Local);

            DailyHintRewardPolicy.TryResolve(now, 4, 5, 3, "2026-10-19", out var hints, out _);

            Assert.That(hints, Is.EqualTo(5));
        }

        [Test]
        public void AtOrAboveCapResolvesDayWithoutGranting()
        {
            var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Local);

            var resolved = DailyHintRewardPolicy.TryResolve(now, 7, 5, 1, "2026-10-19",
                out var hints, out var checkedDate);

            Assert.That(resolved, Is.True);
            Assert.That(hints, Is.EqualTo(7));
            Assert.That(checkedDate, Is.EqualTo("2026-10-20"));
        }

        [Test]
        public void SameDayDoesNotRetryAfterBalanceFallsBelowCap()
        {
            var now = new DateTime(2026, 10, 20, 23, 59, 0, DateTimeKind.Local);

            var resolved = DailyHintRewardPolicy.TryResolve(now, 2, 5, 1, "2026-10-20",
                out var hints, out var checkedDate);

            Assert.That(resolved, Is.False);
            Assert.That(hints, Is.EqualTo(2));
            Assert.That(checkedDate, Is.EqualTo("2026-10-20"));
        }

        [Test]
        public void MissedDaysGrantOnlyOneCurrentDayReward()
        {
            var now = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Local);

            DailyHintRewardPolicy.TryResolve(now, 0, 5, 1, "2026-10-10", out var hints, out var checkedDate);

            Assert.That(hints, Is.EqualTo(1));
            Assert.That(checkedDate, Is.EqualTo("2026-10-20"));
        }
    }
}
