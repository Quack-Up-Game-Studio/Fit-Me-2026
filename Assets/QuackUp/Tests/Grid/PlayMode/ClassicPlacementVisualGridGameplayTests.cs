using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FitMe.Grid;
using FitMe.Scene;
using NUnit.Framework;
using QuackUp.Core;
using QuackUp.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using UnityScene = UnityEngine.SceneManagement.Scene;

namespace FitMe.Grid.PlayModeTests
{
    public sealed class ClassicPlacementVisualGridGameplayTests
    {
        private const string GameplayScenePath = "Assets/QuackUp/Scenes/Gameplay.unity";
        private UnityScene _gameplayScene;
        private UnityScene _keeperScene;
        private GridPreset _activePreset;
        private bool _originalOverrideBag;
        private bool _restorePreset;
        private UnityEngine.Random.State _randomState;
        private bool _restoreRandomState;

        [UnityTest]
        public IEnumerator NormalBagRefillAndSeveralGameplayPlacements_KeepVisualFootprintAlignedWithOccupiedCells()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                UnityEngine.Object.DontDestroyOnLoad(root);
            _keeperScene = SceneManager.CreateScene("ClassicPlacementVisualGridTestKeeper");
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
            Assert.That(unexpectedErrors, Is.Empty,
                "Gameplay startup produced unexpected errors: " + string.Join(" | ", unexpectedErrors));

            var scope = _gameplayScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SceneLifetimeScope>(true))
                .SingleOrDefault();
            Assert.That(scope, Is.Not.Null, "The loaded Gameplay scene must own its SceneLifetimeScope.");
            var blockManager = scope.Container.Resolve<BlockManager>();
            var gridManager = scope.Container.Resolve<GridManager>();
            var blockManagerConfig = scope.Container.Resolve<BlockManagerConfig>();
            var gridManagerConfig = scope.Container.Resolve<GridManagerConfig>();
            yield return WaitUntil(
                () => gridManager.CurrentGridPreset != null && blockManager.BlockOnHand.Count == 1,
                "Classic grid and first hand block startup");

            _activePreset = gridManager.CurrentGridPreset;
            _originalOverrideBag = _activePreset.OverrideBag;
            _restorePreset = true;
            SetAutoProperty(_activePreset, nameof(GridPreset.OverrideBag), false);
            _randomState = UnityEngine.Random.state;
            _restoreRandomState = true;
            UnityEngine.Random.InitState(424242);
            blockManager.ResetBag(shouldRespawn: true);
            yield return WaitUntil(
                () => blockManager.BlockOnHand.Count == 1 &&
                      blockManager.BlockOnPreview.Count == blockManagerConfig.PreviewCount,
                "Production random-bag refill and preview creation");

            const int placementCount = 3;
            var placedCount = 0;
            for (var placementIndex = 0; placementIndex < placementCount; placementIndex++)
            {
                var block = blockManager.BlockOnHand.Single();
                Assert.That(block.Model.BlockPreset, Is.Not.Null,
                    "A normal Classic gameplay spawn must retain its configured block preset.");
                block.ViewModel.BlockInteractionState.Value = BlockInteractionState.PickUp;
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(TryPositionAtFirstValidCell(block, gridManager, gridManagerConfig), Is.True,
                    $"Could not find a valid board placement for gameplay block {placementIndex}.");
                Assert.That(gridManager.TryPlaceBlock(block), Is.True,
                    $"Gameplay placement {placementIndex} should be accepted by GridManager.");

                yield return new WaitForSecondsRealtime(0.35f);
                AssertOccupiedCellsMatchVisualFootprint(block, block.Model.BlockPreset.BlockSchema.schema,
                    $"Gameplay placement {placementIndex} ({block.Model.BlockShape}, rotation {block.GameObject.transform.eulerAngles.z}).");
                placedCount++;

                if (placementIndex + 1 < placementCount)
                {
                    yield return WaitUntil(
                        () => blockManager.BlockOnHand.Count == 1 && !ReferenceEquals(blockManager.BlockOnHand[0], block),
                        $"normal refill after gameplay placement {placementIndex}");
                }
            }

            Assert.That(placedCount, Is.EqualTo(placementCount),
                "The regression must exercise several consecutive placements through the real Gameplay manager.");
        }

        [UnityTearDown]
        public IEnumerator UnloadGameplayScene()
        {
            if (_restorePreset && _activePreset)
            {
                SetAutoProperty(_activePreset, nameof(GridPreset.OverrideBag), _originalOverrideBag);
                _restorePreset = false;
            }
            if (_restoreRandomState)
            {
                UnityEngine.Random.state = _randomState;
                _restoreRandomState = false;
            }
            if (_keeperScene.IsValid() && _keeperScene.isLoaded)
                SceneManager.SetActiveScene(_keeperScene);
            if (!_gameplayScene.IsValid() || !_gameplayScene.isLoaded)
                yield break;
            var unload = SceneManager.UnloadSceneAsync(_gameplayScene);
            if (unload != null)
                yield return WaitUntil(() => unload.isDone, "Gameplay scene unload");
        }

        private static bool TryPositionAtFirstValidCell(
            BlockInstance block,
            GridManager gridManager,
            GridManagerConfig gridManagerConfig)
        {
            var transform = block.GameObject.transform;
            transform.localScale = Vector3.one * gridManagerConfig.CellSize.x;
            var firstAtom = block.Model.Atoms[0].GameObject.transform;
            var firstAtomOffset = firstAtom.position - transform.position;
            var gridSize = gridManager.CurrentGridSize;

            for (var row = 0; row < gridSize.y; row++)
            for (var column = 0; column < gridSize.x; column++)
            {
                var cell = gridManager.GetCellByArrayIndex(row, column);
                if (cell == null) continue;
                transform.position = cell.GameObject.transform.position - firstAtomOffset;
                if (gridManager.ValidatePlacement(block.Model)) return true;
            }

            return false;
        }

        private static void AssertOccupiedCellsMatchVisualFootprint(
            BlockInstance block,
            int[,] visualSchema,
            string context)
        {
            var expectedPositions = new List<Vector3>();
            for (var row = 0; row < visualSchema.GetLength(0); row++)
            for (var column = 0; column < visualSchema.GetLength(1); column++)
            {
                if (visualSchema[row, column] == 0) continue;
                var localPosition = new Vector3(
                    -visualSchema.GetLength(1) / 2f + 0.5f + column,
                    visualSchema.GetLength(0) / 2f - 0.5f - row,
                    0f);
                expectedPositions.Add(block.GameObject.transform.TransformPoint(localPosition));
            }

            var unmatchedCells = block.Model.BlockCells
                .Select(cell => cell.GameObject.transform.position)
                .ToList();
            Assert.That(unmatchedCells.Count, Is.EqualTo(expectedPositions.Count), context + " cell count");
            foreach (var expectedPosition in expectedPositions)
            {
                var closestIndex = -1;
                var closestDistance = float.MaxValue;
                for (var index = 0; index < unmatchedCells.Count; index++)
                {
                    var distance = Vector3.Distance(expectedPosition, unmatchedCells[index]);
                    if (distance >= closestDistance) continue;
                    closestDistance = distance;
                    closestIndex = index;
                }

                Assert.That(closestDistance, Is.LessThan(0.05f),
                    $"{context} visible footprint cell at {expectedPosition} has no corresponding occupied grid cell.");
                unmatchedCells.RemoveAt(closestIndex);
            }
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            var field = target.GetType().GetField($"<{propertyName}>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized backing field for {propertyName}.");
            field.SetValue(target, value);
        }

        private static async UniTask ObserveSceneLoad(
            LoadSceneManager sceneLoader,
            Action completed,
            Action<Exception> failed)
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
    }
}
