using System.IO;
using NUnit.Framework;
using UnityEngine;
using QuackUp.Save;

namespace QuackUp.Save.Tests
{
    public class SaveCorruptionTests : SaveTestBase
    {
        private void WriteGarbageZip()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ZipPath)!);
            File.WriteAllBytes(ZipPath, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xFF, 0x00, 0x13, 0x37 });
        }

        [Test]
        public void Load_MissingZipFile_DoesNotThrow_Characterization()
        {
            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 9 };

            Assert.DoesNotThrow(() => Manager.Load("testEntry"), "missing archive is not an error");
            Assert.AreEqual(9, SaveObject.TestData.testInt, "in-memory data kept");
        }

        [Test]
        public void Load_CorruptZip_DoesNotThrow_AndKeepsInMemoryData()
        {
            WriteGarbageZip();
            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 9 };

            Assert.DoesNotThrow(() => Manager.Load("testEntry"), "corrupt archive must not throw (policy: keep data + LogError)");
            Assert.AreEqual(9, SaveObject.TestData.testInt, "in-memory data kept");
        }

        [Test]
        public void Save_OverCorruptZip_RebuildsArchive_AndBecomesLoadable()
        {
            WriteGarbageZip();
            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 55 };

            Assert.DoesNotThrow(() => Manager.Save("testEntry"), "save over corrupt archive must self-heal");

            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 0 };
            Manager.Load("testEntry");
            Assert.AreEqual(55, SaveObject.TestData.testInt, "rebuilt archive loads");
        }

        [Test]
        public void Save_LeavesNoTmpOrBak_Artifacts()
        {
            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 1 };

            Manager.Save("testEntry");

            Assert.IsFalse(File.Exists(ZipPath + ".copy.tmp"), "temp archive cleaned up");
            Assert.IsFalse(File.Exists(ZipPath + ".bak"), "backup cleaned up");
            Assert.IsTrue(File.Exists(ZipPath), "archive written");
        }
    }
}
