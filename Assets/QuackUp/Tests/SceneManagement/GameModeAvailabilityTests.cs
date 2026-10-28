using TMPro;
using FitMe.Shared;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class GameModeAvailabilityTests
    {
        [TestCase(GameMode.Classic, true)]
        [TestCase(GameMode.LevelShape, false)]
        public void OnlyQueueModeIsAvailable(GameMode mode, bool expected)
        {
            Assert.That(GameModeAvailability.IsAvailable(mode), Is.EqualTo(expected));
        }

        [Test]
        public void ModeSelectScene_DisablesShapeModeAndShowsSoonBadge()
        {
            EditorSceneManager.OpenScene("Assets/QuackUp/Scenes/ModeSelect.unity", OpenSceneMode.Single);

            var shapeMode = GameObject.Find("LevelShapeMode");
            Assert.That(shapeMode, Is.Not.Null);
            Assert.That(shapeMode.GetComponent<Button>().interactable, Is.False);
            Assert.That(shapeMode.transform.Find("UnavailableOverlay"), Is.Not.Null);
            Assert.That(shapeMode.transform.Find("SoonBadge"), Is.Not.Null);
            var textValues = System.Array.ConvertAll(shapeMode.GetComponentsInChildren<TMP_Text>(true), text => text.text);
            Assert.That(textValues, Does.Contain("JIGSAW MODE"));
            Assert.That(textValues, Does.Contain("SOON!"));
        }
    }
}
