using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using FitMe.Panel;
using FitMe.Scene;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Core;
using QuackUp.SceneManagement;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace FitMe.Grid.PlayModeTests
{
    public sealed class ClassicSwapGameplayTests
    {
        private const string GameplayScenePath = "Assets/QuackUp/Scenes/Gameplay.unity";
        private UnityScene _gameplayScene;
        private UnityScene _keeperScene;

        [UnityTest]
        public IEnumerator GameplayScene_ClassicSwapExchangesHandAndQueueFrontAcrossRepeatedClicks()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                UnityEngine.Object.DontDestroyOnLoad(root);
            _keeperScene = SceneManager.CreateScene("ClassicSwapGameplayTestKeeper");
            SceneManager.SetActiveScene(_keeperScene);
            var projectScope = UnityEngine.Object.FindObjectsByType<ProjectLifetimeScope>(FindObjectsSortMode.None)
                .SingleOrDefault();
            Assert.That(projectScope, Is.Not.Null, "The real project lifetime scope must be present.");
            var sceneLoader = projectScope.Container.Resolve<LoadSceneManager>();
            var unexpectedErrors = new List<string>();
            Application.LogCallback logHandler = (condition, _, type) =>
            {
                if (type == LogType.Error && condition != "GameAnalytics: REMEMBER THE SDK NEEDS TO BE MANUALLY INITIALIZED NOW")
                    unexpectedErrors.Add(condition);
            };
            var previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            Application.logMessageReceived += logHandler;
            LogAssert.ignoreFailingMessages = true;
            Exception loadError = null;
            var loaded = false;
            try
            {
                ObserveSceneLoad(sceneLoader, () => loaded = true, error => loadError = error).Forget();
                yield return WaitUntil(() => loaded || loadError != null, "Gameplay scene load through LoadSceneManager");
                Assert.That(loadError, Is.Null, loadError?.ToString());
                yield return WaitUntil(
                    () => sceneLoader.CurrentSceneType == SceneType.Gameplay &&
                          SceneManager.GetSceneByPath(GameplayScenePath).isLoaded,
                    "Gameplay scene activation and scene-type publication");
                _gameplayScene = SceneManager.GetSceneByPath(GameplayScenePath);
                Assert.That(_gameplayScene.IsValid() && _gameplayScene.isLoaded, Is.True);
            }
            finally
            {
                Application.logMessageReceived -= logHandler;
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
            Assert.That(unexpectedErrors, Is.Empty, "Gameplay startup produced unexpected errors: " + string.Join(" | ", unexpectedErrors));

            var scope = _gameplayScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SceneLifetimeScope>(true))
                .SingleOrDefault();
            Assert.That(scope, Is.Not.Null, "The loaded Gameplay scene must own its SceneLifetimeScope.");
            var blockManager = scope.Container.Resolve<BlockManager>();
            var gridManager = scope.Container.Resolve<GridManager>();
            var blockConfig = scope.Container.Resolve<BlockManagerConfig>();
            yield return WaitUntil(
                () => gridManager.CurrentGridPreset != null && blockManager.BlockOnHand.Count == 1,
                "Classic grid and first hand block startup");

            // Set a deterministic authored bag on the real runtime grid preset, then
            // ask the real BlockManager to reset/refill/spawn it through gameplay code.
            var presets = blockConfig.BlockPresetDictionary.OrderBy(x => x.Key).ToList();
            Assert.That(presets, Has.Count.GreaterThanOrEqualTo(2));
            var colors = ((BlockColor[])Enum.GetValues(typeof(BlockColor))).Distinct().ToArray();
            Assert.That(colors, Has.Length.GreaterThanOrEqualTo(2));
            var bag = new List<SpawnBlockData>();
            for (var i = 0; i < 8; i++)
            {
                var preset = presets[i % presets.Count];
                bag.Add(new SpawnBlockData(
                    preset.Key,
                    Quaternion.identity,
                    preset.Value.BlockSchema,
                    colors[i % colors.Length]));
            }

            SetAutoProperty(gridManager.CurrentGridPreset, nameof(GridPreset.OverrideBag), true);
            SetAutoProperty(gridManager.CurrentGridPreset, nameof(GridPreset.ShuffleBag), false);
            SetAutoProperty(gridManager.CurrentGridPreset, nameof(GridPreset.SpawnBlockData), bag);
            blockManager.ResetBag(shouldRespawn: true);
            yield return WaitUntil(
                () => blockManager.BlockOnHand.Count == 1 && blockManager.BlockOnPreview.Count == blockConfig.PreviewCount,
                "Deterministic gameplay bag refill and preview creation");

            var swapView = _gameplayScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SwapBlockView>(true))
                .SingleOrDefault();
            Assert.That(swapView, Is.Not.Null, "Use the SwapBlockView serialized in Gameplay.unity.");
            var buttonField = typeof(SwapBlockView).GetField("swapButton",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buttonField, Is.Not.Null);
            var button = (Button)buttonField.GetValue(swapView);
            Assert.That(button, Is.Not.Null);
            var viewModelField = typeof(SwapBlockView).GetField("_viewModel", BindingFlags.Instance | BindingFlags.NonPublic);
            var viewModel = viewModelField.GetValue(swapView);
            var viewModelManagerField = viewModel.GetType().GetField("_blockManager", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(viewModelManagerField.GetValue(viewModel), Is.SameAs(blockManager),
                "The actual Gameplay swap view must target the scene's BlockManager.");
            Assert.That(button.interactable, Is.True);

            var firstHandBefore = blockManager.BlockOnHand[0];
            var previewsBefore = blockManager.BlockOnPreview.ToArray();
            Assert.That(previewsBefore.Length, Is.EqualTo(blockConfig.PreviewCount));
            Assert.That(firstHandBefore.Model.BlockShape, Is.Not.EqualTo(previewsBefore[0].Model.BlockShape),
                "The deterministic bag must make the expected exchange observable.");
            var queueField = typeof(BlockManager).GetField("_spawnBag", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueField, Is.Not.Null);
            var spawnBag = (Queue<SpawnBlockData>)queueField.GetValue(blockManager);
            Assert.That(spawnBag, Is.Not.Empty);
            TestContext.Out.WriteLine($"CLASSIC_SWAP_BEFORE hand={firstHandBefore.Model.BlockShape} preview0={previewsBefore[0].Model.BlockShape} bagHead={spawnBag.Peek().blockShape} previews={previewsBefore.Length}");
            Assert.That(spawnBag.Peek().blockShape, Is.EqualTo(previewsBefore[0].Model.BlockShape),
                "Production preview zero must represent the real queue front.");
            var oldHandData = CaptureBlockData(firstHandBefore);
            var incomingBlockData = CaptureBlockData(previewsBefore[0]);
            var unchangedPreviewTail = previewsBefore.Skip(1).ToArray();
            var swapSignals = 0;
            using var swapSignalSubscription = blockManager.OnSwap.Subscribe(_ => swapSignals++);

            button.onClick.Invoke();
            yield return null;
            Assert.That(swapSignals, Is.EqualTo(1), "One physical button click must execute exactly one queue swap.");
            var firstHandAfter = blockManager.BlockOnHand[0];
            var previewsAfter = blockManager.BlockOnPreview.ToArray();
            Assert.That(firstHandAfter, Is.Not.SameAs(firstHandBefore));
            AssertEquivalentBlockData(incomingBlockData, firstHandAfter, "First swap: queue front must enter hand.");
            Assert.That(previewsAfter[0], Is.Not.SameAs(previewsBefore[0]));
            AssertEquivalentBlockData(oldHandData, previewsAfter[0], "First swap: displaced hand block must become preview zero.");
            Assert.That(previewsAfter.Skip(1).ToArray(), Is.EqualTo(unchangedPreviewTail),
                "Swapping the queue front must preserve the remaining preview order.");
            Assert.That(blockManager.CanSwap.CurrentValue, Is.True);

            button.onClick.Invoke();
            yield return null;
            Assert.That(swapSignals, Is.EqualTo(2), "The second physical click must execute exactly one additional queue swap.");
            var secondHandAfter = blockManager.BlockOnHand[0];
            var previewsAfterSecondSwap = blockManager.BlockOnPreview.ToArray();
            Assert.That(secondHandAfter, Is.Not.SameAs(firstHandAfter));
            AssertEquivalentBlockData(oldHandData, secondHandAfter, "Second swap must bring the returned hand block back.");
            AssertEquivalentBlockData(incomingBlockData, previewsAfterSecondSwap[0], "Second swap must return the first queue item to preview zero.");
            Assert.That(previewsAfterSecondSwap.Skip(1).ToArray(), Is.EqualTo(unchangedPreviewTail));
        }

        [UnityTearDown]
        public IEnumerator UnloadGameplayScene()
        {
            if (_keeperScene.IsValid() && _keeperScene.isLoaded)
                SceneManager.SetActiveScene(_keeperScene);
            if (!_gameplayScene.IsValid() || !_gameplayScene.isLoaded)
                yield break;
            var unload = SceneManager.UnloadSceneAsync(_gameplayScene);
            if (unload == null)
                yield break;
            yield return WaitUntil(() => unload.isDone, "Gameplay scene unload");
        }

        private static async UniTask ObserveSceneLoad(LoadSceneManager sceneLoader, Action completed, Action<Exception> failed)
        {
            try
            {
                await sceneLoader.LoadScene(SceneType.Gameplay, LoadSceneMode.Additive, useLoadingScene: false);
                completed();
            }
            catch (Exception error)
            {
                failed(error);
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string operation)
        {
            var deadline = Time.realtimeSinceStartup + 90f;
            while (!condition())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), $"Timed out waiting for {operation}.");
                yield return null;
            }
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            var field = target.GetType().GetField($"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized backing field for {propertyName}.");
            field.SetValue(target, value);
        }

        private sealed class BlockSnapshot
        {
            public object Shape;
            public object Color;
            public Quaternion Rotation;
            public int[,] Schema;
        }

        private static BlockSnapshot CaptureBlockData(BlockInstance block)
        {
            var schema = block.Model.BlockSchemas.FirstOrDefault()?.schema;
            Assert.That(schema, Is.Not.Null, "Gameplay block must have a schema.");
            return new BlockSnapshot
            {
                Shape = block.Model.BlockShape,
                Color = block.Model.BlockColor.CurrentValue,
                Rotation = block.GameObject.transform.rotation,
                Schema = (int[,])schema.Clone()
            };
        }

        private static void AssertEquivalentBlockData(BlockSnapshot expected, BlockInstance actual, string message)
        {
            Assert.That(actual.Model.BlockShape, Is.EqualTo(expected.Shape), message + " shape");
            Assert.That(actual.Model.BlockColor.CurrentValue, Is.EqualTo(expected.Color), message + " color");
            Assert.That(Quaternion.Angle(actual.GameObject.transform.rotation, expected.Rotation),
                Is.LessThan(0.01f), message + " rotation");
            var actualSchema = actual.Model.BlockSchemas.FirstOrDefault()?.schema;
            Assert.That(actualSchema, Is.Not.Null, message + " actual schema");
            Assert.That(actualSchema.GetLength(0), Is.EqualTo(expected.Schema.GetLength(0)), message + " schema rows");
            Assert.That(actualSchema.GetLength(1), Is.EqualTo(expected.Schema.GetLength(1)), message + " schema columns");
            for (var row = 0; row < expected.Schema.GetLength(0); row++)
            for (var column = 0; column < expected.Schema.GetLength(1); column++)
                Assert.That(actualSchema[row, column], Is.EqualTo(expected.Schema[row, column]),
                    $"{message} schema[{row},{column}]");
        }
    }
}
