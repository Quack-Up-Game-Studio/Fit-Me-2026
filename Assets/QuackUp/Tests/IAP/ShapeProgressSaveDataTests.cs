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
            Assert.That(restored.ShapeWorldProgress, Is.Empty);
        }

        [Test]
        public void FreshPlayerRecord_HasNoWorldProgressUntilInitialized()
        {
            var saveData = new PlayerRecordSaveData();
            Assert.That(saveData.ShapeWorldProgress, Is.Empty);
        }


    }
}