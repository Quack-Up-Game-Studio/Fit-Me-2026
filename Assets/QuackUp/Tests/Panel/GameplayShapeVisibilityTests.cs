using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FitMe.Grid;
using QuackUp.Utils;
using R3;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using FitMe.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FitMe.Panel.Tests
{
    public sealed class GameplayShapeVisibilityTests
    {
        [Test]
        public void ShapeMode_HidesConfiguredTargetsAndRestoresAuthoredStatesAcrossRepeatedNotifications()
        {
            var root = new GameObject("Gameplay", typeof(RectTransform), typeof(CanvasGroup));
            var active = new GameObject("Active legacy UI");
            var inactive = new GameObject("Authored inactive UI");
            var untouched = new GameObject("Score");
            inactive.SetActive(false);
            var view = root.AddComponent<GameplayPanelView>();
            try
            {
                var field = typeof(GameplayPanelView).GetField("hideInShapeMode", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "GameplayPanelView needs an Inspector-configurable hideInShapeMode list");
                var serialized = new SerializedObject(view);
                Assert.That(serialized.FindProperty("hideInShapeMode"), Is.Not.Null, "Targets must be Unity serialized");
                field.SetValue(view, new[] { active, inactive, null, active });
                Apply(view, GameMode.Classic);
                Assert.That(active.activeSelf, Is.True);
                Assert.That(inactive.activeSelf, Is.False);
                for (var cycle = 0; cycle < 3; cycle++)
                {
                    Apply(view, GameMode.LevelShape);
                    Apply(view, GameMode.LevelShape);
                    Assert.That(active.activeSelf, Is.False);
                    Assert.That(inactive.activeSelf, Is.False);
                    Assert.That(untouched.activeSelf, Is.True);
                    Apply(view, GameMode.Classic);
                    Apply(view, GameMode.Classic);
                    Assert.That(active.activeSelf, Is.True);
                    Assert.That(inactive.activeSelf, Is.False, "Do not blindly enable authored inactive targets");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(active);
                Object.DestroyImmediate(inactive);
                Object.DestroyImmediate(untouched);
            }
        }

        [TestCase(GameMode.Classic)]
        [TestCase(GameMode.LevelShape)]
        public void Construct_BindsInitialModeAndLiveSessionChangesThroughViewModel(GameMode initialMode)
        {
            Assert.That(typeof(GameplayPanelViewModel).GetProperty("CurrentGameMode"), Is.Not.Null,
                "ViewModel must expose current mode reactively, sourced from ILevelManager");
            var root = new GameObject("Gameplay", typeof(RectTransform), typeof(CanvasGroup));
            var target = new GameObject("Legacy UI");
            var button = new GameObject("Pause", typeof(RectTransform), typeof(Button));
            var level = new LevelManagerMock(GameState.PlaceBlock) { CurrentGameMode = initialMode };
            using var hub = new TestHub();
            var vm = new GameplayPanelViewModel(null, level, level, hub);
            var view = root.AddComponent<GameplayPanelView>();
            typeof(PanelView).GetField("canvasGroup", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, root.GetComponent<CanvasGroup>());
            typeof(GameplayPanelView).GetField("pauseButton", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, button.GetComponent<Button>());
            typeof(GameplayPanelView).GetField("hideInShapeMode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, new[] { target });
            try
            {
                view.Construct(vm);
                Assert.That(target.activeSelf, Is.EqualTo(initialMode != GameMode.LevelShape));
                foreach (var mode in new[] { GameMode.LevelShape, GameMode.LevelShape, GameMode.Classic, GameMode.LevelShape, GameMode.Classic })
                {
                    level.CurrentGameMode = mode;
                    // Runtime data can be null for a legacy shape session: authoritative mode is the level manager.
                    hub.Publish(new SpawnWithShapeLevelEvent(null));
                    Assert.That(target.activeSelf, Is.EqualTo(mode != GameMode.LevelShape));
                }
                view.Dispose();
                level.CurrentGameMode = GameMode.LevelShape;
                hub.Publish(new SpawnWithShapeLevelEvent(null));
                Assert.That(target.activeSelf, Is.True, "Disposed view must not receive future mode changes");
                vm.Dispose();
                Assert.That(hub.SubscriberCount, Is.Zero, "ViewModel must dispose its hub subscription");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(button);
            }
        }

        [Test]
        public void SavedGameplayScene_WiresOnlyTheFiveHighlightedLegacyObjects()
        {
            const string scenePath = "Assets/QuackUp/Scenes/Gameplay.unity";
            // Scope expectations to the known pre-existing stale legacy references.
            var text = System.IO.File.ReadAllText(scenePath);
            var staleIds = new[] { 527656407, 1420291200, 1420291199, 527656406 }
                .Where(id => !System.Text.RegularExpressions.Regex.IsMatch(text, @"&" + id + @"(?:\s|$)")).ToArray();
            var stale = System.Text.RegularExpressions.Regex.Matches(text, @"\{fileID: (\d+)\}")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => long.Parse(m.Groups[1].Value))
                .Where(id => staleIds.Any(staleId => staleId == id)).ToArray();
            foreach (var id in stale)
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                    $"Broken text PPtr in file({scenePath}). Local file identifier ({id}) doesn't exist!");
            if (stale.Length > 0)
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                    $"Problem detected while opening the Scene file: '{scenePath}'.\nCheck the following logs for more details.");
                foreach (var id in new[] { 1420291200, 527656407 }.Where(staleIds.Contains))
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Transform child can't be loaded");
            }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var canvas = scene.GetRootGameObjects().Single(go => go.name == "OverlayCanvas");
                var panel = canvas.transform.Find("GameplayPanel");
                var view = panel.GetComponent<GameplayPanelView>();
                var targets = (GameObject[])typeof(GameplayPanelView).GetField("hideInShapeMode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                var paths = new[] { "Back/NextQueue", "Back/Hands", "Back/Swap Icon", "Back/Next Queue", "SwapButton" };
                var expected = paths.Select(path => panel.Find(path).gameObject).ToArray();
                Assert.That(targets, Is.EquivalentTo(expected), "Saved list must contain exactly the five selected identities, including both queue objects");
                var untouched = new[] { "Back/Fit BG", "Back/Score", "Back/BG", "Back/Banner", "Score" }
                    .Select(path => panel.Find(path).gameObject).ToArray();
                var authored = expected.Concat(untouched).ToDictionary(go => go, go => go.activeSelf);
                Apply(view, GameMode.LevelShape);
                Assert.That(targets.All(go => !go.activeSelf), Is.True);
                foreach (var go in untouched) Assert.That(go.activeSelf, Is.EqualTo(authored[go]), go.name);
                Apply(view, GameMode.Classic);
                foreach (var go in expected.Concat(untouched)) Assert.That(go.activeSelf, Is.EqualTo(authored[go]), go.name);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingOrEmptyTargetList_IsSafe(bool useNull)
        {
            var root = new GameObject("Gameplay");
            var view = root.AddComponent<GameplayPanelView>();
            try
            {
                typeof(GameplayPanelView).GetField("hideInShapeMode", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(view, useNull ? null : Array.Empty<GameObject>());
                Assert.DoesNotThrow(() => { Apply(view, GameMode.LevelShape); Apply(view, GameMode.Classic); });
            }
            finally { Object.DestroyImmediate(root); }
        }

        private sealed class TestHub : IMessageHub, IDisposable
        {
            private readonly Subject<SpawnWithShapeLevelEvent> _changes = new();
            public int SubscriberCount { get; private set; }
            public void Publish<T>(T message) => _changes.OnNext((SpawnWithShapeLevelEvent)(object)message);
            public IDisposable Subscribe<T>(Action<T> action)
            {
                SubscriberCount++;
                var binding = _changes.Subscribe(evt => action((T)(object)evt));
                return Disposable.Create(() => { binding.Dispose(); SubscriberCount--; });
            }
            public Observable<T> GetObservable<T>() => throw new NotSupportedException();
            public void Dispose() => _changes.Dispose();
        }

        private static void Apply(GameplayPanelView view, GameMode mode)
        {
            var method = typeof(GameplayPanelView).GetMethod("OnGameModeChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(view, new object[] { mode });
        }
    }
}
