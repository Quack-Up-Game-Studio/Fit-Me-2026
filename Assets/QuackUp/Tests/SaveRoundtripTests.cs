using System;
using System.IO;
using MessagePack;
using NUnit.Framework;
using UnityEngine;
using QuackUp.Save;

namespace QuackUp.Save.Tests
{
    public class SaveRoundtripTests : SaveTestBase
    {
        [Test]
        public void DirectSerialize_KeyedType_WithDefaultOptions_Succeeds()
        {
            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 42 };

            byte[] raw = null;
            try
            {
                raw = MessagePackSerializer.Serialize(SaveObject.TestData, SaveObject.DefaultSerializerOptions);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Direct MessagePack serialize threw: {ex.GetType().Name}: {ex.Message}");
            }
            Assert.That(raw, Is.Not.Null.And.Not.Empty, "direct serialize produced bytes");
        }

        [Test]
        public void TrySerializeSaveData_AcceptsTwoPartApplicationVersion()
        {
            // bundleVersion is "1.0" (2-part). Strict SemVer parse rejects it; the save
            // gate must not. RED until MessagePackSaveObject parses with SemVersionStyles.Any.
            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 42 };

            var ok = SaveObject.TrySerializeSaveData(out var bytes);

            Assert.That(ok, Is.True, $"TrySerializeSaveData succeeded for Application.version='{Application.version}'");
            Assert.That(bytes, Is.Not.Null.And.Not.Empty, "bytes produced");
        }

        [Test]
        public void Roundtrip_PreservesData_AcrossSaveLoad()
        {
            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 42 };

            Manager.Save("testEntry");
            Assert.IsTrue(File.Exists(ZipPath), $"zip written at {ZipPath}");
            Assert.IsFalse(File.Exists(ZipPath + ".copy.tmp"), "temp copy cleaned up");

            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 0 };
            Manager.Load("testEntry");

            Assert.AreEqual(42, SaveObject.TestData.testInt);
        }
    }
}
