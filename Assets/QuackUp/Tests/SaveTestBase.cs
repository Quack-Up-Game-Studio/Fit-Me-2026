using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using QuackUp.Save;

namespace QuackUp.Save.Tests
{
    public abstract class SaveTestBase
    {
        protected MessagePackSaveManager Manager { get; private set; }
        protected TestMessagePackSaveObject SaveObject { get; private set; }
        protected string SaveDir { get; private set; }
        protected string ZipPath => Path.Combine(Application.persistentDataPath, SaveDir, "testSave.sav");

        [SetUp]
        public virtual void SetUp()
        {
            SaveDir = $"QUSaveTests_{Guid.NewGuid():N}";
            var settings = new SaveSettings
            {
                saveLocation = SaveLocation.PersistentDataPath,
                saveDirectory = SaveDir,
                saveFileName = "testSave"
            };
            var config = MessagePackSaveConfig.CreateForTests(settings);
            SaveObject = ScriptableObject.CreateInstance<TestMessagePackSaveObject>();
            SaveObject.TestData = new TestMessagePackSaveData { Version = Application.version, testInt = 0 };
            Manager = new MessagePackSaveManager(config);
            Manager.RegisterSaveObject("testEntry", SaveObject);
        }

        [TearDown]
        public virtual void TearDown()
        {
            var dir = Path.Combine(Application.persistentDataPath, SaveDir);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
