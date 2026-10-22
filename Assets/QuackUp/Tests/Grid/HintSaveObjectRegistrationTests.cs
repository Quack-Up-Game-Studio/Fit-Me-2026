using FitMe.GameData;
using NUnit.Framework;
using QuackUp.Save;
using UnityEditor;

namespace FitMe.Grid.Tests
{
    public sealed class HintSaveObjectRegistrationTests
    {
        private const string SaveConfigPath = "Assets/QuackUp/Settings/Save/MessagePackSaveConfig.asset";

        [Test]
        public void MessagePackSaveConfig_ContainsHintSaveObjectInInitialSaveObjects()
        {
            var config = AssetDatabase.LoadAssetAtPath<MessagePackSaveConfig>(SaveConfigPath);

            Assert.That(config, Is.Not.Null);
            Assert.That(config.InitialSaveObjects.TryGetValue("Hint", out var saveObject), Is.True);
            Assert.That(saveObject, Is.TypeOf<HintManagerSaveObject>());
        }
    }
}
