using System;
using System.Collections.Generic;
using System.Reflection;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Utils;
using R3;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FitMe.Grid.Tests
{
    public class GridManagerOutlineTests
    {
        private readonly List<Object> _objects = new();
        private readonly List<BlockInstance> _blocks = new();
        private readonly List<CellModel> _cells = new();
        private readonly List<IDisposable> _subscriptions = new();
        private GridManager _manager;
        private UnityEngine.Grid _grid;
        private BlockFactory _factory;
        private BlockManagerConfig _config;

        [SetUp]
        public void SetUp()
        {
            const string root = "Assets/QuackUp/";
            _grid = Keep(new GameObject("Outline test grid")).AddComponent<UnityEngine.Grid>();
            var gridConfig = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            _manager = new GridManager(_grid, gridConfig, null, null, new TestMessageHub());
            var preset = Keep(ScriptableObject.CreateInstance<GridPreset>());
            preset.GridSize = new Vector2Int(6, 6);
            SetManagerField("<CurrentGridPreset>k__BackingField", preset);
            var cells = new CellInstance[6, 6];
            for (int row = 0; row < 6; row++)
            for (int column = 0; column < 6; column++)
            {
                var model = new CellModel();
                _cells.Add(model);
                model.ArrayIndex.Value = new Vector2Int(row, column);
                model.GridIndex.Value = new Vector2Int(column, -row);
                var obj = Keep(new GameObject("Outline test cell"));
                obj.transform.position = _grid.GetCellCenterWorld(new Vector3Int(column, -row, 0));
                cells[row, column] = new CellInstance(model, null, obj);
            }
            SetManagerField("_cellArray", cells);
            _config = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(root + "Settings/Grid/Block/BlockManagerConfig.asset")));
            _factory = new BlockFactory(
                AssetDatabase.LoadAssetAtPath<GameObject>(root + "Prefabs/Block/BlockView.prefab").GetComponent<BlockView>(), _config, gridConfig,
                new AtomFactory(_config, AssetDatabase.LoadAssetAtPath<GameObject>(root + "Prefabs/Atom/AtomView.prefab").GetComponent<AtomView>()),
                new BlockControllerFactory(_config, _manager, null, null, null));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
            _manager?.Dispose();
            foreach (var block in _blocks)
            {
                block.GameObject.GetComponent<BlockView>().Dispose();
                block.Controller.Dispose();
                block.ViewModel.Dispose();
            }
            _blocks.Clear();
            foreach (var cell in _cells) cell.Dispose();
            _cells.Clear();
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i]) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator PlacementAndRemoval_PreserveGeneratedLocalOutlines()
        {
            TearDown();
            _manager = null;
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            yield return new UnityEngine.TestTools.EnterPlayMode();
            SetUp();
            foreach (int angle in new[] { 0, 90, 180, 270 })
            foreach (bool obstacle in new[] { false, true })
            {
                VerifyPlacementAndRemoval(angle, obstacle);
                TestContext.Out.WriteLine($"Preserved local edges: rotation={angle}, obstacle={obstacle}");
            }
            TearDown();
            _manager = null;
            yield return new UnityEngine.TestTools.ExitPlayMode();
        }

        private void VerifyPlacementAndRemoval(int angle, bool obstacle)
        {
            var incoming = CreateBlock(new[,] { { 1, 0 }, { 1, 1 } }, new Vector3(2, -2, 0), angle);
            // Pick an empty cardinal neighbour of this rotated L, not a separate remote block.
            var occupied = new HashSet<Vector2Int>();
            foreach (var atom in incoming.Model.Atoms)
                occupied.Add(_manager.GetCellByPosition(atom.GameObject.transform.position).Model.ArrayIndex.Value);
            CellInstance neighbour = null;
            foreach (var index in occupied)
            {
                foreach (var delta in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    var candidate = index + delta;
                    if (!occupied.Contains(candidate)) neighbour = _manager.GetCellByArrayIndex(candidate);
                    if (neighbour != null) break;
                }
                if (neighbour != null) break;
            }
            Assert.That(neighbour, Is.Not.Null);
            var existing = CreateBlock(new[,] { { 1 } }, neighbour.GameObject.transform.position, 0);
            if (obstacle) existing.Model.BlockState.Value = BlockState.Obstacle;

            var initial = new Dictionary<AtomInstance, Vector4>();
            var updates = new Dictionary<AtomInstance, int>();
            foreach (var block in new[] { incoming, existing })
            foreach (var atom in block.Model.Atoms)
            {
                int row = atom.Model.ArrayIndex.Value.x, column = atom.Model.ArrayIndex.Value.y;
                bool Exists(int r, int c) => block == existing ? r == 0 && c == 0 :
                    r >= 0 && r < 2 && c >= 0 && c < 2 && !(r == 0 && c == 1);
                var expected = new Vector4(Exists(row - 1, column) ? 0 : 1, Exists(row + 1, column) ? 0 : 1,
                    Exists(row, column - 1) ? 0 : 1, Exists(row, column + 1) ? 0 : 1);
                Assert.That(ReadEdges(atom), Is.EqualTo(expected), "GenerateAtom must start with local schema edges");
                initial.Add(atom, expected);
                updates.Add(atom, 0);
                _subscriptions.Add(atom.ViewModel.SetOutlineCommand.Subscribe(_ => updates[atom]++));
            }

            Assert.That(_manager.TryPlaceBlock(existing, true), Is.True);
            Assert.That(_manager.TryPlaceBlock(incoming, true), Is.True);
            var violations = new List<string>();
            AssertUnchanged("placement");
            _manager.RemoveBlock(incoming, FitType.None).GetAwaiter().GetResult();
            AssertUnchanged("incoming removal");
            _manager.RemoveBlock(existing, FitType.None).GetAwaiter().GetResult();
            AssertUnchanged("existing removal");
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
            foreach (var cell in _cells) Assert.That(cell.CurrentAtom.Value, Is.Null);
            Assert.That(incoming.GameObject.transform.eulerAngles.z, Is.EqualTo(angle).Within(.001f));

            void AssertUnchanged(string stage)
            {
                foreach (var pair in initial)
                {
                    if (updates[pair.Key] != 0) violations.Add(stage + " must not issue SetOutlineCommand: " + updates[pair.Key]);
                    if (ReadEdges(pair.Key) != pair.Value) violations.Add(stage + " must retain local edges: " + pair.Key.Model.ArrayIndex.Value);
                }
            }
        }

        private BlockInstance CreateBlock(int[,] schema, Vector3 position, int angle)
        {
            var block = _factory.Create(BlockShape.OneByOne, position, Quaternion.Euler(0, 0, angle),
                schemaOverride: schema, blockConfigOverride: _config.ShapeModeBlockConfig);
            _blocks.Add(block);
            Keep(block.GameObject);
            typeof(BlockView).GetField("_originalScale", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(block.GameObject.GetComponent<BlockView>(), Vector3.one);
            block.GameObject.transform.localScale = Vector3.one * _grid.cellSize.x;
            return block;
        }

        private static Vector4 ReadEdges(AtomInstance atom)
        {
            var properties = new MaterialPropertyBlock();
            atom.GameObject.GetComponent<SpriteRenderer>().GetPropertyBlock(properties);
            return new Vector4(properties.GetFloat("_OutlineTop"), properties.GetFloat("_OutlineBottom"),
                properties.GetFloat("_OutlineLeft"), properties.GetFloat("_OutlineRight"));
        }

        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private void SetManagerField(string name, object value) => typeof(GridManager)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_manager, value);

        private sealed class TestMessageHub : IMessageHub
        {
            public void Publish<TMessage>(TMessage message) { }
            public IDisposable Subscribe<TMessage>(Action<TMessage> action) => Disposable.Create(() => { });
            public Observable<TMessage> GetObservable<TMessage>() => Observable.Empty<TMessage>();
        }
    }
}
