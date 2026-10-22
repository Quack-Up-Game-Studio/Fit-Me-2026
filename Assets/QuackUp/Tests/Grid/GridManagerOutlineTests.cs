using System;
using System.Collections.Generic;
using System.Reflection;
using FitMe.Grid;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Utils;
using R3;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FitMe.Grid.Tests
{
    public class GridManagerOutlineTests
    {
        private readonly List<Object> _objects = new();
        private GridManager _manager;
        private CellModel _firstCellModel;
        private CellModel _secondCellModel;
        private AtomModel _firstAtomModel;
        private AtomModel _secondAtomModel;
        private readonly List<AtomViewModel> _atomViewModels = new();

        [SetUp]
        public void SetUp()
        {
            var gridObject = Keep(new GameObject("Outline test grid"));
            var grid = gridObject.AddComponent<UnityEngine.Grid>();
            var config = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            _manager = new GridManager(grid, config, null, null, new TestMessageHub());

            var preset = Keep(ScriptableObject.CreateInstance<GridPreset>());
            preset.GridSize = new Vector2Int(2, 1);
            SetManagerField("<CurrentGridPreset>k__BackingField", preset);

            _firstCellModel = new CellModel();
            _secondCellModel = new CellModel();
            _firstCellModel.ArrayIndex.Value = Vector2Int.zero;
            _secondCellModel.ArrayIndex.Value = new Vector2Int(0, 1);
            var firstCell = new CellInstance(_firstCellModel, null, Keep(new GameObject("First cell")));
            var secondCell = new CellInstance(_secondCellModel, null, Keep(new GameObject("Second cell")));

            CreateOccupiedCell(firstCell, out _firstAtomModel);
            CreateOccupiedCell(secondCell, out _secondAtomModel);
            SetManagerField("_cellArray", new[,] { { firstCell, secondCell } });
        }

        [TearDown]
        public void TearDown()
        {
            _manager?.Dispose();
            _firstCellModel?.Dispose();
            _secondCellModel?.Dispose();
            _firstAtomModel?.ParentBlock.Dispose();
            _firstAtomModel?.ArrayIndex.Dispose();
            _secondAtomModel?.ParentBlock.Dispose();
            _secondAtomModel?.ArrayIndex.Dispose();
            foreach (var viewModel in _atomViewModels)
                viewModel.SetOutlineCommand.Dispose();
            foreach (var item in _objects)
                if (item) Object.DestroyImmediate(item);
            _objects.Clear();
        }

        [Test]
        public void RefreshGridOutlines_HidesEdgesBetweenOccupiedAdjacentCells()
        {
            var firstViewModel = _atomViewModels[0];
            var secondViewModel = _atomViewModels[1];
            SpriteOutlineSettings firstSettings = null;
            SpriteOutlineSettings secondSettings = null;
            using var firstSubscription = firstViewModel.SetOutlineCommand.Subscribe(x => firstSettings = x);
            using var secondSubscription = secondViewModel.SetOutlineCommand.Subscribe(x => secondSettings = x);

            var refresh = typeof(GridManager).GetMethod("RefreshGridOutlines",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refresh, Is.Not.Null);
            refresh.Invoke(_manager, null);

            Assert.That(firstSettings, Is.Not.Null);
            Assert.That(secondSettings, Is.Not.Null);
            Assert.That(firstSettings.OutlineRight, Is.False);
            Assert.That(secondSettings.OutlineLeft, Is.False);
            Assert.That(firstSettings.OutlineLeft, Is.True);
            Assert.That(secondSettings.OutlineRight, Is.True);
        }

        private BlockInstance CreateOccupiedCell(CellInstance cell, out AtomModel atomModel)
        {
            var blockModel = new BlockModel(null, null) { BlockCells = new List<CellInstance> { cell } };
            var blockObject = Keep(new GameObject("Cell block"));
            var block = new BlockInstance(blockModel, null, null, blockObject);
            atomModel = new AtomModel();
            atomModel.ParentBlock.Value = block;
            var atomViewModel = new AtomViewModel(atomModel);
            _atomViewModels.Add(atomViewModel);
            var atomObject = Keep(new GameObject("Cell atom"));
            var atom = new AtomInstance(atomModel, atomViewModel, atomObject);
            blockModel.Atoms.Add(atom);
            cell.Model.CurrentAtom.Value = atom;
            return block;
        }

        private T Keep<T>(T value) where T : Object
        {
            _objects.Add(value);
            return value;
        }

        private void SetManagerField(string name, object value)
        {
            typeof(GridManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_manager, value);
        }

        private sealed class TestMessageHub : IMessageHub
        {
            public void Publish<TMessage>(TMessage message) { }
            public IDisposable Subscribe<TMessage>(Action<TMessage> action) => Disposable.Create(() => { });
            public Observable<TMessage> GetObservable<TMessage>() => Observable.Empty<TMessage>();
        }
    }
}
