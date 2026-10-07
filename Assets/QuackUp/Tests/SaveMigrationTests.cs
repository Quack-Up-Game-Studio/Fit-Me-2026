using System.IO;
using System.IO.Compression;
using MessagePack;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using QuackUp.Save;

namespace QuackUp.Save.Tests
{
    public class SaveMigrationTests : SaveTestBase
    {
        /// <summary>
        /// Writes a zip whose entry holds data stamped with an OLD version —
        /// simulating a save produced by a previous app release (the public
        /// Save() API always stamps Application.version, so the zip must be
        /// crafted directly).
        /// </summary>
        protected void WriteZipWithEntry(TestMessagePackSaveData data)
        {
            var bytes = MessagePackSerializer.Serialize(data, SaveObject.DefaultSerializerOptions);
            Directory.CreateDirectory(Path.GetDirectoryName(ZipPath)!);
            using var fs = new FileStream(ZipPath, FileMode.Create);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
            var entry = zip.CreateEntry("testEntry.bin");
            using var s = entry.Open();
            using var w = new BinaryWriter(s);
            w.Write(bytes);
        }

        [Test]
        public void ReadinessGate_AllowsRemoteApplyBeforeReadyRelease()
        {
            var gate = new SaveReadinessGate();

            Assert.IsTrue(gate.TryBeginRemoteApply());
            gate.CompleteReadiness();
            Assert.IsTrue(gate.IsReady);
        }

        [Test]
        public void ReadinessGate_RejectsLateRemoteApplyAfterTimeout()
        {
            var gate = new SaveReadinessGate();
            gate.CompleteReadiness();

            Assert.IsFalse(gate.TryBeginRemoteApply());
            Assert.IsTrue(gate.IsReady);
        }

        [Test]
        public void ReadinessGate_AllowsUserSelectedLoadAfterStartupReadiness()
        {
            var gate = new SaveReadinessGate();
            gate.CompleteReadiness();

            var generation = gate.BeginExplicitRemoteApply();

            Assert.IsTrue(gate.CompleteExplicitRemoteApply(generation));
            Assert.IsTrue(gate.IsCurrent(generation));
            Assert.IsFalse(gate.TryBeginRemoteApply());
            Assert.IsTrue(gate.IsReady);
        }

        [Test]
        public void ReadinessGate_RejectsInFlightStartupResultAfterUserSelectedLoad()
        {
            var gate = new SaveReadinessGate();
            Assert.IsTrue(gate.TryBeginRemoteApply(out var startupGeneration));

            var explicitGeneration = gate.BeginExplicitRemoteApply();
            Assert.IsTrue(gate.CompleteExplicitRemoteApply(explicitGeneration));

            Assert.IsFalse(gate.IsCurrent(startupGeneration));
            Assert.IsFalse(gate.TryBeginRemoteApply());
        }

        [Test]
        public void ReadinessGate_RejectsRepeatedCompletion()
        {
            var gate = new SaveReadinessGate();

            Assert.IsTrue(gate.CompleteReadiness());
            Assert.IsFalse(gate.CompleteReadiness());
        }

        [Test]
        public void ReadinessGate_ApplyLeaseMustBeAcquiredBeforeCompletion()
        {
            var gate = new SaveReadinessGate();
            gate.CompleteReadiness();

            Assert.IsFalse(gate.TryBeginRemoteApply());
        }


        [Test]
        public void Migration_AfterStartupReset_TargetsApplicationVersion()
        {
            WriteZipWithEntry(new TestMessagePackSaveData { Version = "0.9.0", testInt = 7 });
            SaveObject.SetResolvers(new TestMessagePackMigrationResolver("0.9.0", Application.version, 3));

            Manager.Initialize();

            Assert.AreEqual(10, SaveObject.TestData.testInt, "startup reset must not bypass migration");
            Assert.AreEqual(Application.version, SaveObject.TestData.Version);
        }

        [Test]
        public void Migration_EquivalentGraphVersions_ConnectAcrossSteps()
        {
            WriteZipWithEntry(new TestMessagePackSaveData { Version = "0.8", testInt = 7 });
            SaveObject.SetResolvers(
                new TestMessagePackMigrationResolver("0.8.0", "0.9+first", 3),
                new TestMessagePackMigrationResolver("0.9.0+second", Application.version, 5));

            Manager.Load("testEntry");

            Assert.AreEqual(15, SaveObject.TestData.testInt, "equivalent graph nodes must connect");
            Assert.AreEqual(Application.version, SaveObject.TestData.Version);
        }

        [Test]
        public void Migration_CloudZipRead_AfterReset_UsesApplicationTarget()
        {
            WriteZipWithEntry(new TestMessagePackSaveData { Version = "0.9.0", testInt = 7 });
            SaveObject.SetResolvers(new TestMessagePackMigrationResolver("0.9.0", Application.version, 3));
            SaveObject.Reset();

            var candidate = Manager.DeserializeSaveDataFromZipBytes(File.ReadAllBytes(ZipPath));
            Assert.AreEqual(0, SaveObject.TestData.testInt, "candidate evaluation must not apply cloud data");
            Manager.LoadFromDeserializedData(candidate);

            Assert.AreEqual(10, SaveObject.TestData.testInt);
            Assert.AreEqual(Application.version, SaveObject.TestData.Version);
        }

        [Test]
        public void SelectedCloudSave_WithValidZipButNoRegisteredEntries_IsNotApplicable()
        {
            using (var stream = new MemoryStream())
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                using (var entry = archive.CreateEntry("unregistered.bin").Open())
                    entry.WriteByte(1);

                LogAssert.Expect(LogType.Error, "Save object with key unregistered not found.");
                var candidate = Manager.DeserializeSaveDataFromZipBytes(stream.ToArray());

                Assert.That(candidate, Is.Not.Null);
                Assert.That(candidate.SaveData, Is.Empty);
                Assert.That(Manager.HasApplicableSaveData(candidate), Is.False);
            }
        }

        [Test]
        public void Migration_StaleInMemoryVersion_DoesNotChangeTarget()
        {
            WriteZipWithEntry(new TestMessagePackSaveData { Version = "0.9.0", testInt = 7 });
            SaveObject.SetResolvers(new TestMessagePackMigrationResolver("0.9.0", Application.version, 3));
            SaveObject.TestData = new TestMessagePackSaveData { Version = "0.8.0", testInt = 99 };

            Manager.Load("testEntry");

            Assert.AreEqual(10, SaveObject.TestData.testInt);
            Assert.AreEqual(Application.version, SaveObject.TestData.Version);
        }

        [Test]
        public void Migration_ShortestPath_WinsOverEarlierLongerPath()
        {
            WriteZipWithEntry(new TestMessagePackSaveData { Version = "0.8", testInt = 7 });
            SaveObject.SetResolvers(
                new TestMessagePackMigrationResolver("0.8.0", "0.9", 100),
                new TestMessagePackMigrationResolver("0.9.0", Application.version, 100),
                new TestMessagePackMigrationResolver("0.8", Application.version, 3));

            Manager.Load("testEntry");

            Assert.AreEqual(10, SaveObject.TestData.testInt);
        }

        [Test]
        public void Migration_InvalidResolverEndpoints_DoNotBlockValidPath()
        {
            WriteZipWithEntry(new TestMessagePackSaveData { Version = "0.9", testInt = 7 });
            SaveObject.SetResolvers(
                null,
                new TestMessagePackMigrationResolver(null, Application.version, 100),
                new TestMessagePackMigrationResolver("0.9", "garbage", 100),
                new TestMessagePackMigrationResolver("0.9.0", Application.version, 3));

            Assert.DoesNotThrow(() => Manager.Load("testEntry"));
            Assert.AreEqual(10, SaveObject.TestData.testInt);
        }

        [Test]
        public void Migration_NoPath_DoesNotClaimCurrentVersion()
        {
            WriteZipWithEntry(new TestMessagePackSaveData { Version = "0.9.0", testInt = 7 });
            SaveObject.SetResolvers();
            SaveObject.Reset();

            Manager.Load("testEntry");

            Assert.AreEqual(7, SaveObject.TestData.testInt);
            Assert.AreEqual("0.9.0", SaveObject.TestData.Version, "fallback is not a completed migration");
        }

        [Test]
        public void Migration_WithMatchingResolver_TransformsData()
        {
            WriteZipWithEntry(new TestMessagePackSaveData { Version = "0.9.0", testInt = 7 });
            SaveObject.SetResolvers(new TestMessagePackMigrationResolver("0.9.0", Application.version, 3));
            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 0 };

            Manager.Load("testEntry");

            Assert.AreEqual(7 + 3, SaveObject.TestData.testInt, "resolver applied (+3)");
        }

        [Test]
        public void Migration_NoMatchingResolver_FallsBackToDeserializedData_Characterization()
        {
            // Characterization: locks today's fallback behavior — unmatched
            // migration loads the deserialized data anyway, silently.
            WriteZipWithEntry(new TestMessagePackSaveData { Version = "0.9.0", testInt = 7 });
            SaveObject.SetResolvers(); // empty: no migration path exists
            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 0 };

            Assert.DoesNotThrow(() => Manager.Load("testEntry"), "fallback must not throw");
            Assert.AreEqual(7, SaveObject.TestData.testInt, "deserialized data preserved by fallback");
        }

        [Test]
        public void Migration_GarbageVersionString_DoesNotThrow_Characterization()
        {
            WriteZipWithEntry(new TestMessagePackSaveData { Version = "garbage", testInt = 5 });
            SaveObject.SetResolvers();
            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 0 };

            Assert.DoesNotThrow(() => Manager.Load("testEntry"), "unparsable stored version must not throw");
            Assert.AreEqual(5, SaveObject.TestData.testInt, "data preserved");
        }
    }
}
