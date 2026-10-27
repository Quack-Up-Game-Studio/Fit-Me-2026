using System;
using System.Collections.Generic;
using System.Reflection;
using FitMe.Panel;
using FitMe.Shared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class MainMenuStartViewTests
    {
        private readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _objects) Object.DestroyImmediate(item);
            _objects.Clear();
        }

        [Test]
        public void SwitchingToPressHidesDragPresentationAndSwitchingBackRestoresIt()
        {
            var type = typeof(MainMenuStartView);
            var root = Create("StartControl");
            var view = root.AddComponent(type);
            var drag = Create("ExistingDragPresentation");
            drag.transform.localPosition = new Vector3(12, 34, 0);
            var press = Create("PressStart", typeof(RectTransform), typeof(Image), typeof(Button));
            SetField(view, "dragStartObjects", new[] { drag });
            SetField(view, "pressStartButton", press.GetComponent<Button>());
            var mode = type.GetField("startMode", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(mode, Is.Not.Null);

            mode.SetValue(view, Enum.ToObject(mode.FieldType, 1));
            type.GetMethod("ApplyMode").Invoke(view, null);
            Assert.That(press.activeSelf, Is.True);
            Assert.That(drag.activeSelf, Is.False);

            mode.SetValue(view, Enum.ToObject(mode.FieldType, 0));
            type.GetMethod("ApplyMode").Invoke(view, null);
            Assert.That(press.activeSelf, Is.False);
            Assert.That(drag.activeSelf, Is.True);
            Assert.That(drag.transform.localPosition, Is.EqualTo(new Vector3(12, 34, 0)));
        }

        [Test]
        public void StartingWaitsForSaveReadiness()
        {
            Assert.That(MainMenuStartRules.Resolve(false, true), Is.EqualTo(MainMenuStartAction.WaitForSave));
        }

        [Test]
        public void StartingWithEnergyGoesDirectlyToModeSelectWhileTutorialIsDisabled()
        {
            Assert.That(MainMenuStartRules.Resolve(true, true), Is.EqualTo(MainMenuStartAction.ModeSelect));
        }

        [Test]
        public void StartingWithoutEnergyGetsTheExistingEnergyPrompt()
        {
            Assert.That(MainMenuStartRules.Resolve(true, false), Is.EqualTo(MainMenuStartAction.OutOfEnergy));
        }

        [Test]
        public void UnreadySaveTakesPriorityOverInsufficientEnergy()
        {
            Assert.That(MainMenuStartRules.Resolve(false, false), Is.EqualTo(MainMenuStartAction.WaitForSave));
        }

        [Test]
        public void AuthoredButtonUsesBothSuppliedSpritesAndANonBlockingStartLabel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/QuackUp/Prefabs/MainMenuPressStart.prefab");
            Assert.That(prefab, Is.Not.Null);
            var button = prefab.GetComponent<Button>();
            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
            Assert.That(prefab.GetComponent<Image>().sprite.name, Is.EqualTo("BTTN_Start_Still_0"));
            Assert.That(button.spriteState.pressedSprite.name, Is.EqualTo("BTTN_Start_Click_0"));
            var label = prefab.GetComponentInChildren<TMP_Text>();
            Assert.That(label.text, Is.EqualTo("START"));
            Assert.That(label.raycastTarget, Is.False);
        }

        [Test]
        public void NativePointerPressSwapsArtworkAndReleaseClicksOnce()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/QuackUp/Prefabs/MainMenuPressStart.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            _objects.Add(instance);
            var button = instance.GetComponent<Button>();
            var events = Create("Events", typeof(EventSystem));
            var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { button = PointerEventData.InputButton.Left };
            var clicks = 0;
            button.onClick.AddListener(() => clicks++);

            button.OnPointerDown(pointer);
            Assert.That(instance.GetComponent<Image>().overrideSprite, Is.SameAs(button.spriteState.pressedSprite));
            Assert.That(clicks, Is.Zero);
            button.OnPointerUp(pointer);
            button.OnPointerClick(pointer);
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(instance.GetComponent<Image>().overrideSprite, Is.SameAs(instance.GetComponent<Image>().sprite));
        }

        private GameObject Create(string name, params Type[] components)
        {
            var item = new GameObject(name, components);
            _objects.Add(item);
            return item;
        }

        private static void SetField(Component view, string name, object value)
        {
            view.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, value);
        }
    }
}
