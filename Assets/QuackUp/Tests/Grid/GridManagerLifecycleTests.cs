using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using FitMe.Grid;
using NUnit.Framework;
using ObservableCollections;
using QuackUp.Utils;
using R3;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FitMe.Grid.Tests
{
    public class GridManagerLifecycleTests
    {
        private readonly List<Object> _objects = new();
        private GridManager _manager;
        private CountingHub _hub;
        private UnityEngine.Grid _grid;
        private CellModel _cell;
        private AtomModel _atom;
        private BlockModel _blockModel;
        private BlockViewModel _blockViewModel;
        private BlockInstance _block;

        [SetUp]
        public void SetUp()
        {
            _cell = null;
            _atom = null;
            _blockModel = null;
            _blockViewModel = null;
            _block = null;
            var gridObject = Keep(new GameObject("Lifecycle test grid"));
            _grid = gridObject.AddComponent<UnityEngine.Grid>();
            var config = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            _hub = new CountingHub();
            // Factory and audio are deliberately null: these tests never create
            // cells through the factory, play audio, or run async grid effects.
            _manager = new GridManager(_grid, config, null, null, _hub);
        }

        [TearDown]
        public void TearDown()
        {
            // Avoid an extra manager Dispose obscuring failures on old code.
            // A fresh fixture is created for each test.
            _hub?.Dispose();
            _cell?.Dispose();
            _atom?.ParentBlock.Dispose();
            _atom?.ArrayIndex.Dispose();
            _blockModel?.UpdateGridCommand.Dispose();
            _blockModel?.BlockState.Dispose();
            if (_blockViewModel != null)
            {
                _blockViewModel.Dispose();
                _blockViewModel.BlockInteractionState.Dispose();
                _blockViewModel.SetSortingLayerCommand.Dispose();
                _blockViewModel.SetSortingOrderCommand.Dispose();
                _blockViewModel.ExplodeCommand.Dispose();
                _blockViewModel.ScaleInCommand.Dispose();
                _blockViewModel.RotateCommand.Dispose();
                _blockViewModel.DestroyCommand.Dispose();
            }
            // Release Subjects even in RED runs without calling manager Dispose
            // a second time. This is fixture cleanup, not the behavior under test.
            foreach (var field in typeof(GridManager).GetFields(
                         BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.FieldType.IsGenericType &&
                    field.FieldType.GetGenericTypeDefinition() == typeof(Subject<>))
                    ((IDisposable)field.GetValue(_manager)).Dispose();
            }
            foreach (var item in _objects)
                if (item) Object.DestroyImmediate(item);
            _objects.Clear();
        }

        private T Keep<T>(T obj) where T : Object
        {
            _objects.Add(obj);
            return obj;
        }

        private void SetManagerField(string name, object value)
        {
            var field = typeof(GridManager).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Fixture anchor missing: {name}");
            field.SetValue(_manager, value);
        }

        private void SetUpOneCellPlacement(bool withViewModel = true)
        {
            var preset = Keep(ScriptableObject.CreateInstance<GridPreset>());
            preset.GridSize = Vector2Int.one;
            SetManagerField("<CurrentGridPreset>k__BackingField", preset);
            _cell = new CellModel();
            var cellObject = Keep(new GameObject("Lifecycle test cell"));
            cellObject.transform.position = _grid.GetCellCenterWorld(Vector3Int.zero);
            SetManagerField("_cellArray", new[,]
            {
                { new CellInstance(_cell, null, cellObject) }
            });
            var blockObject = Keep(new GameObject("Lifecycle test block"));
            var atomObject = Keep(new GameObject("Lifecycle test atom"));
            atomObject.transform.position = cellObject.transform.position;
            _blockModel = new BlockModel(null, null);
            if (withViewModel) _blockViewModel = new BlockViewModel(_blockModel);
            _block = new BlockInstance(_blockModel, _blockViewModel, null, blockObject);
            _atom = new AtomModel();
            _atom.ParentBlock.Value = _block;
            _blockModel.Atoms.Add(new AtomInstance(_atom, null, atomObject));
        }

        private static void AssertDisposed(CancellationTokenSource source)
        {
            Assert.That(source, Is.Not.Null, "veto event must supply a source");
            Assert.Throws<ObjectDisposedException>(() => source.Cancel());
        }

        [Test]
        public void Dispose_UnsubscribesIncomingHubHandlers()
        {
            Assert.That(_hub.ActiveHandlers, Is.GreaterThan(0));
            _manager.Dispose();
            Assert.That(_hub.ActiveHandlers, Is.Zero);
        }

        [Test]
        public void Placement_VetoSourceIsDisposed_WhenCancelled()
        {
            SetUpOneCellPlacement();
            CancellationTokenSource captured = null;
            using var subscription = _manager.OnAboutToPlaceBlock.Subscribe(x =>
            {
                captured = x.placeCancellation;
                x.placeCancellation.Cancel();
            });
            Assert.That(_manager.TryPlaceBlock(_block, updateGrid: false), Is.False);
            Assert.That(_cell.CurrentAtom.Value, Is.Null, "veto prevents occupancy");
            Assert.That(_manager.BlocksOnGrid.Count, Is.Zero);
            AssertDisposed(captured);
            _manager.Dispose();
        }

        [Test]
        public void Placement_VetoSourceIsDisposed_AfterSuccessfulPlacement()
        {
            SetUpOneCellPlacement();
            CancellationTokenSource captured = null;
            var placedEvents = 0;
            using var veto = _manager.OnAboutToPlaceBlock.Subscribe(x =>
                captured = x.placeCancellation);
            using var placed = _manager.OnBlockPlaced.Subscribe(_ => placedEvents++);
            Assert.That(_manager.TryPlaceBlock(_block, updateGrid: false), Is.True);
            Assert.That(_cell.CurrentAtom.Value, Is.SameAs(_blockModel.Atoms[0]));
            Assert.That(_manager.BlocksOnGrid.Count, Is.EqualTo(1));
            Assert.That(_blockViewModel.BlockInteractionState.Value,
                Is.EqualTo(BlockInteractionState.PlacedOnGrid));
            Assert.That(_block.GameObject.transform.parent, Is.SameAs(_grid.transform));
            Assert.That(placedEvents, Is.EqualTo(1));
            AssertDisposed(captured);
            _manager.Dispose();
        }

        [Test]
        public void Placement_VetoSourceIsDisposed_IfDownstreamPlacementThrows()
        {
            // Deliberate invalid fixture to exercise exceptional-exit disposal.
            // This test does not claim partially committed placement is rolled back.
            SetUpOneCellPlacement(withViewModel: false);
            CancellationTokenSource captured = null;
            using var subscription = _manager.OnAboutToPlaceBlock.Subscribe(x =>
                captured = x.placeCancellation);
            Assert.Throws<NullReferenceException>(() =>
                _manager.TryPlaceBlock(_block, updateGrid: false));
            AssertDisposed(captured);
            _manager.Dispose();
        }

        [TestCase("OnCellsCreated")]
        [TestCase("OnAboutToPlaceBlock")]
        [TestCase("OnBlockPlaced")]
        [TestCase("OnScoreAdded")]
        [TestCase("OnFitCheck")]
        [TestCase("OnAboutToClearGrid")]
        [TestCase("OnClearGrid")]
        public void Dispose_ReleasesEveryOwnedSubject(string propertyName)
        {
            var property = typeof(GridManager).GetProperty(propertyName);
            Assert.That(property, Is.Not.Null);
            var subject = property.GetValue(_manager);
            var isDisposed = subject.GetType().GetProperty("IsDisposed");
            Assert.That(isDisposed, Is.Not.Null, "R3 Subject exposes disposal state");
            _manager.Dispose();
            Assert.That((bool)isDisposed.GetValue(subject), Is.True, propertyName);
        }

        [Test]
        public void Dispose_ReleasesPerBlockSubscriptionExactlyOnce()
        {
            var counter = new CountingDisposable();
            // This isolates cleanup; Dispose does not access BlockInstance.
            ((ObservableList<GridBlockData>)_manager.BlocksOnGrid)
                .Add(new GridBlockData(null, counter));
            _manager.Dispose();
            _manager.Dispose();
            Assert.That(counter.Count, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_ReentrantCleanupDoesNotDisposeResourceTwice()
        {
            var counter = new CountingDisposable();
            counter.OnDispose = () =>
            {
                // Bound reentry so a regression fails rather than overflowing.
                counter.OnDispose = null;
                _manager.Dispose();
            };
            ((ObservableList<GridBlockData>)_manager.BlocksOnGrid)
                .Add(new GridBlockData(null, counter));
            _manager.Dispose();
            Assert.That(counter.Count, Is.EqualTo(1));
        }

        private sealed class CountingDisposable : IDisposable
        {
            public int Count { get; private set; }
            public Action OnDispose { get; set; }
            public void Dispose()
            {
                Count++;
                OnDispose?.Invoke();
            }
        }

        private sealed class CountingHub : IMessageHub, IDisposable
        {
            private readonly List<IDisposable> _streams = new();
            public int ActiveHandlers { get; private set; }
            public void Publish<TMessage>(TMessage message) { }
            public IDisposable Subscribe<TMessage>(Action<TMessage> action)
            {
                ActiveHandlers++;
                return Disposable.Create(() => ActiveHandlers--);
            }
            public Observable<TMessage> GetObservable<TMessage>()
            {
                var subject = new Subject<TMessage>();
                _streams.Add(subject);
                return subject;
            }
            public void Dispose()
            {
                foreach (var stream in _streams) stream.Dispose();
            }
        }
    }
}
