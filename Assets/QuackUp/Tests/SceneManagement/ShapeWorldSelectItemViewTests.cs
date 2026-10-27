using System;
using System.Collections.Generic;
using System.Reflection;
using FitMe.Panel;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuackUp.SceneManagement.Tests
{
    public sealed class ShapeWorldSelectItemViewTests
    {
        private readonly List<Object> _objects = new();
        private const string PrefabPath = "Assets/QuackUp/Prefabs/ShapeLevelSelect/ShapeWorldSelectItemView.prefab";

        [TearDown]
        public void TearDown()
        {
            for (var i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i]) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test]
        public void UnlockedWorldDisplaysItsOwnSpriteAndName()
        {
            var view = CreateView();
            var unlocked = CreateSprite();
            var locked = CreateSprite();
            var item = CreateItem("world-2", true, unlocked, locked);

            view.Construct(item, _ => { });

            Assert.That(GetField<Image>(view, "worldImage").sprite, Is.SameAs(unlocked));
            Assert.That(GetField<Image>(view, "worldImage").preserveAspect, Is.True);
            Assert.That(GetField<Component>(view, "worldNameText").gameObject.activeSelf, Is.True);
            Assert.That(GetField<Button>(view, "selectButton").interactable, Is.True);
        }

        private ShapeWorldSelectItemView CreateView()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            var instance = Object.Instantiate(prefab);
            _objects.Add(instance);
            return instance.GetComponent<ShapeWorldSelectItemView>();
        }

        [Test]
        public void LockedWorldDisplaysItsOwnSpriteWithoutDuplicateLockOrName()
        {
            var view = CreateView();
            var locked = CreateSprite();

            view.Construct(CreateItem("world-2", false, CreateSprite(), locked), _ => { });

            Assert.That(GetField<Image>(view, "worldImage").sprite, Is.SameAs(locked));
            Assert.That(GetField<Component>(view, "worldNameText").gameObject.activeSelf, Is.False);
            Assert.That(GetField<GameObject>(view, "lockedIndicator").activeSelf, Is.False);
            Assert.That(GetField<Button>(view, "selectButton").interactable, Is.False);
        }

        [Test]
        public void MissingUnlockedArtworkClearsPreviousLockedArtworkButKeepsNameAndButton()
        {
            var view = CreateView();
            view.Construct(CreateItem("world-2", false, null, CreateSprite()), _ => { });

            view.Construct(CreateItem("world-2", true, null, CreateSprite()), _ => { });

            Assert.That(GetField<Image>(view, "worldImage").enabled, Is.False);
            Assert.That(GetField<Image>(view, "worldImage").sprite, Is.Null);
            Assert.That(GetField<Component>(view, "worldNameText").gameObject.activeSelf, Is.True);
            Assert.That(GetField<Button>(view, "selectButton").interactable, Is.True);
            Assert.That(GetField<GameObject>(view, "lockedIndicator").activeSelf, Is.False);
        }

        [Test]
        public void MissingLockedArtworkUsesConfiguredFallbackWithoutChangingUnlockState()
        {
            var view = CreateView();
            var fallback = GetField<Sprite>(view, "fallbackLockedSprite");
            Assert.That(fallback, Is.Not.Null);

            view.Construct(CreateItem("world-1", false, CreateSprite(), null), _ => { });

            Assert.That(GetField<Image>(view, "worldImage").sprite, Is.SameAs(fallback));
            Assert.That(GetField<Button>(view, "selectButton").interactable, Is.False);
        }

        [Test]
        public void RebindingButtonSelectsOnlyTheLatestWorldKey()
        {
            var view = CreateView();
            var selected = new List<string>();
            view.Construct(CreateItem("world-1", true, CreateSprite(), null), selected.Add);
            view.Construct(CreateItem("world-2", true, CreateSprite(), null), selected.Add);

            GetField<Button>(view, "selectButton").onClick.Invoke();

            Assert.That(selected, Is.EqualTo(new[] { "world-2" }));
        }

        [Test]
        public void PrefabKeepsLockedArtworkFullColorAndCompletionOverlayHidden()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            var view = prefab.GetComponent<ShapeWorldSelectItemView>();
            Assert.That(GetField<Button>(view, "selectButton").colors.disabledColor, Is.EqualTo(Color.white));
            Assert.That(GetField<GameObject>(view, "completedIndicator"), Is.Null);
            Assert.That(prefab.transform.Find("Completed").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void LockedWorldNeverDispatchesSelectionEvenWhenClickIsInvokedFromCode()
        {
            var view = CreateView();
            var selected = new List<string>();
            view.Construct(CreateItem("world-3", false, null, CreateSprite()), selected.Add);

            GetField<Button>(view, "selectButton").onClick.Invoke();

            Assert.That(selected, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PointerOverArtworkOrTitleReachesButtonAndTintsArtwork(bool overTitle)
        {
            var canvasObject = new GameObject("Input test canvas", typeof(Canvas), typeof(GraphicRaycaster));
            _objects.Add(canvasObject);
            var cameraObject = new GameObject("Input test camera", typeof(Camera));
            _objects.Add(cameraObject);
            var camera = cameraObject.GetComponent<Camera>();
            var texture = new RenderTexture(800, 800, 24);
            _objects.Add(texture);
            camera.targetTexture = texture;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var eventObject = new GameObject("Input test events", typeof(EventSystem));
            _objects.Add(eventObject);
            var events = eventObject.GetComponent<EventSystem>();
            var view = CreateView();
            view.transform.SetParent(canvasObject.transform, false);
            ((RectTransform)view.transform).sizeDelta = new Vector2(400, 400);
            var button = GetField<Button>(view, "selectButton");
            var image = GetField<Image>(view, "worldImage");
            var colors = button.colors;
            colors.fadeDuration = 0;
            button.colors = colors;
            view.Construct(CreateItem("world-1", true, CreateSprite(), null), _ => { });
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var rect = overTitle
                ? (RectTransform)GetField<Component>(view, "worldNameText").transform
                : image.rectTransform;
            var pointer = new PointerEventData(events)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            canvasObject.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            var handler = ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject);
            Assert.That(handler, Is.EqualTo(button.gameObject), "Top raycast hit: " + hits[0].gameObject.name);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerDownHandler);
            Assert.That(image.canvasRenderer.GetColor(), Is.EqualTo(colors.pressedColor * colors.colorMultiplier));
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(image.canvasRenderer.GetColor(), Is.EqualTo(colors.highlightedColor * colors.colorMultiplier));
            button.interactable = false;
            Assert.That(image.canvasRenderer.GetColor(), Is.EqualTo(Color.white));
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            _objects.Add(texture);
            _objects.Add(sprite);
            return sprite;
        }

        private static ShapeWorldSelectItem CreateItem(string key, bool unlocked, Sprite unlockedSprite, Sprite lockedSprite)
        {
            return new ShapeWorldSelectItem(key, "World 2", unlocked, false, unlockedSprite, lockedSprite);
        }

        private static T GetField<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }
    }
}
