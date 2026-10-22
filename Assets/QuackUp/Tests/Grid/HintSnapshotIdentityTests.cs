using NUnit.Framework;

namespace FitMe.Grid.Tests
{
    public sealed class HintSnapshotIdentityTests
    {
        [Test]
        public void TryCreate_TreatsActiveBlockAndPreviewAsAnEquivalentSwapPair()
        {
            var playable = new[,] { { 1, 1 }, { 1, 1 } };
            var occupied = new[,] { { 1, 0 }, { 1, 1 } };
            var obstacles = new[,] { { 0, 0 }, { 0, 0 } };
            var beforeSwap = new[]
            {
                new[,] { { 1, 1 } },
                new[,] { { 1 } },
                new[,] { { 1, 1 }, { 1, 0 } }
            };
            var afterSwap = new[]
            {
                new[,] { { 1, 1 }, { 1, 0 } },
                new[,] { { 1 } },
                new[,] { { 1, 1 } }
            };

            Assert.That(HintSnapshotIdentity.TryCreate(playable, occupied, obstacles, beforeSwap, 2, out var before), Is.True);
            Assert.That(HintSnapshotIdentity.TryCreate(playable, occupied, obstacles, afterSwap, 2, out var after), Is.True);
            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void TryCreate_TracksBoardOccupancyObstaclesAndQueueOrder()
        {
            var playable = new[,] { { 1, 1 }, { 1, 1 } };
            var occupied = new[,] { { 1, 0 }, { 1, 1 } };
            var obstacles = new[,] { { 0, 0 }, { 0, 0 } };
            var supply = new[]
            {
                new[,] { { 1 } },
                new[,] { { 1, 1 } },
                new[,] { { 1, 1 }, { 1, 0 } },
                new[,] { { 1 }, { 1 } }
            };
            var reorderedQueue = new[]
            {
                new[,] { { 1 } },
                new[,] { { 1, 1 } },
                new[,] { { 1 }, { 1 } },
                new[,] { { 1, 1 }, { 1, 0 } }
            };

            Assert.That(HintSnapshotIdentity.TryCreate(playable, occupied, obstacles, supply, 2, out var original), Is.True);
            Assert.That(HintSnapshotIdentity.TryCreate(new[,] { { 1, 0 }, { 1, 1 } }, occupied, obstacles, supply, 2, out var changedBoard), Is.True);
            Assert.That(HintSnapshotIdentity.TryCreate(playable, new[,] { { 1, 1 }, { 1, 1 } }, obstacles, supply, 2, out var changedOccupancy), Is.True);
            Assert.That(HintSnapshotIdentity.TryCreate(playable, occupied, new[,] { { 1, 0 }, { 0, 0 } }, supply, 2, out var changedObstacles), Is.True);
            Assert.That(HintSnapshotIdentity.TryCreate(playable, occupied, obstacles, reorderedQueue, 2, out var changedQueue), Is.True);
            Assert.That(changedBoard, Is.Not.EqualTo(original));
            Assert.That(changedOccupancy, Is.Not.EqualTo(original));
            Assert.That(changedObstacles, Is.Not.EqualTo(original));
            Assert.That(changedQueue, Is.Not.EqualTo(original));
        }

        [Test]
        public void TryCreate_TreatsFullTrayInventoryAsUnorderedAndAllowsMoreThanTwoPieces()
        {
            var playable = new[,] { { 1, 1, 1 } };
            var occupied = new[,] { { 0, 0, 0 } };
            var obstacles = new[,] { { 0, 0, 0 } };
            var supply = new[]
            {
                new[,] { { 1, 1 } },
                new[,] { { 1 } },
                new[,] { { 1 } }
            };
            var reorderedSupply = new[]
            {
                new[,] { { 1 } },
                new[,] { { 1, 1 } },
                new[,] { { 1 } }
            };

            Assert.That(HintSnapshotIdentity.TryCreate(playable, occupied, obstacles, supply, 3, out var original), Is.True);
            Assert.That(HintSnapshotIdentity.TryCreate(playable, occupied, obstacles, reorderedSupply, 3, out var reordered), Is.True);
            Assert.That(reordered, Is.EqualTo(original));
        }
    }
}
