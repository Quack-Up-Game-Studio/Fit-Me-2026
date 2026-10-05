using System.IO;
using System.IO.Compression;
using MessagePack;
using NUnit.Framework;
using UnityEngine;
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
