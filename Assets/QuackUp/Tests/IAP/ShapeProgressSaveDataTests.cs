using System.Collections.Generic;
using FitMe.GameData;
using MessagePack;
using NUnit.Framework;

namespace QuackUp.IAP.Tests
{
    public sealed class ShapeProgressSaveDataTests
    {
        private static readonly MessagePackSerializerOptions SerializerOptions =
            MessagePack.Resolvers.ContractlessStandardResolverAllowPrivate.Options;

        [Test]
        public void LegacyPlayerRecordPayload_UsesShapeProgressDefaultsAndPreservesExistingFields()
        {
            var legacyFields = new Dictionary<string, object>
            {
                ["Version"] = "1.0.2",
                ["PlayerID"] = "legacy-player",
                ["IsFirstTimePlayer"] = false,
                ["CompletedTutorial"] = true,
                ["CurrentRemainingAd"] = 4,
                ["HasPurchasedAtLeastOnce"] = true,
                ["RecordedPurchaseTransactionIds"] = new List<string> { "tx-recorded" },
                ["CompletedPurchaseTransactionIds"] = new List<string> { "tx-completed" },
                ["CumulativeScore"] = 125f,
                ["CumulativeFitMe"] = 3
            };
            var bytes = MessagePackSerializer.Serialize(legacyFields, SerializerOptions);
            var restored = MessagePackSerializer.Deserialize<PlayerRecordSaveData>(bytes, SerializerOptions);

            Assert.That(restored.PlayerID, Is.EqualTo("legacy-player"));
            Assert.That(restored.IsFirstTimePlayer, Is.False);
            Assert.That(restored.CompletedTutorial, Is.True);
            Assert.That(restored.CurrentRemainingAd, Is.EqualTo(4));
            Assert.That(restored.HasPurchasedAtLeastOnce, Is.True);
            Assert.That(restored.RecordedPurchaseTransactionIds, Is.EqualTo(new[] { "tx-recorded" }));
            Assert.That(restored.CompletedPurchaseTransactionIds, Is.EqualTo(new[] { "tx-completed" }));
            Assert.That(restored.cumulativeScore, Is.EqualTo(125f));
            Assert.That(restored.cumulativeFitMe, Is.EqualTo(3));
            Assert.That(restored.HighestUnlockedShapeOrder, Is.EqualTo(1));
            Assert.That(restored.ShapeLevelHighScores, Is.Empty);
        }

        [Test]
        public void FreshPlayerRecord_HasFirstLevelUnlockedAndNoShapeHighScores()
        {
            var saveData = new PlayerRecordSaveData();
            Assert.That(saveData.HighestUnlockedShapeOrder, Is.EqualTo(1));
            Assert.That(saveData.ShapeLevelHighScores, Is.Empty);
            Assert.That(saveData.IsShapeLevelUnlocked(1), Is.True);
            Assert.That(saveData.IsShapeLevelUnlocked(2), Is.False);
        }

        [Test]
        public void RecordShapeLevelPass_UnlocksSequentiallyAndPreservesProgressOnReplay()
        {
            var saveData = new PlayerRecordSaveData();
            Assert.That(saveData.RecordShapeLevelPass(1, "shape-001", 250), Is.True);
            Assert.That(saveData.HighestUnlockedShapeOrder, Is.EqualTo(2));
            Assert.That(saveData.IsShapeLevelUnlocked(2), Is.True);
            Assert.That(saveData.IsShapeLevelUnlocked(3), Is.False);
            Assert.That(saveData.RecordShapeLevelPass(1, "shape-001", 200), Is.False);
            Assert.That(saveData.HighestUnlockedShapeOrder, Is.EqualTo(2));
            Assert.That(saveData.ShapeLevelHighScores["shape-001"], Is.EqualTo(250));
            Assert.That(saveData.RecordShapeLevelPass(2, "shape-002", 100), Is.True);
            Assert.That(saveData.HighestUnlockedShapeOrder, Is.EqualTo(3));
        }

        [Test]
        public void RecordShapeLevelPass_OnlyRaisesPerIdHighScoreAndRoundTrips()
        {
            var saveData = new PlayerRecordSaveData();
            saveData.RecordShapeLevelPass(1, "shape-001", 250);
            saveData.RecordShapeLevelPass(1, "shape-001", 300);
            saveData.RecordShapeLevelPass(1, "retired-shape", 75);
            var restored = MessagePackSerializer.Deserialize<PlayerRecordSaveData>(
                MessagePackSerializer.Serialize(saveData, SerializerOptions), SerializerOptions);
            Assert.That(restored.HighestUnlockedShapeOrder, Is.EqualTo(2));
            Assert.That(restored.ShapeLevelHighScores["shape-001"], Is.EqualTo(300));
            Assert.That(restored.ShapeLevelHighScores["retired-shape"], Is.EqualTo(75));
            Assert.That(restored.PlayerID, Is.EqualTo(saveData.PlayerID));
            Assert.That(restored.cumulativeScore, Is.EqualTo(saveData.cumulativeScore));
        }

        [TestCase(0, "shape-001", 100)]
        [TestCase(2, "shape-002", 100)]
        [TestCase(1, "", 100)]
        [TestCase(1, "shape-001", -1)]
        public void RecordShapeLevelPass_RejectsInvalidInputWithoutChangingProgress(int order, string levelId, int score)
        {
            var saveData = new PlayerRecordSaveData();
            Assert.That(saveData.RecordShapeLevelPass(order, levelId, score), Is.False);
            Assert.That(saveData.HighestUnlockedShapeOrder, Is.EqualTo(1));
            Assert.That(saveData.ShapeLevelHighScores, Is.Empty);
        }
    }
}