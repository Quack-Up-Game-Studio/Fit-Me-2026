using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FitMe.Shared;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FitMe.Grid.Tests
{
    public sealed class FreeformPresentationTests
    {
        private readonly List<Object> _objects = new();
        private readonly List<AtomInstance> _generatedAtoms = new();
        private BlockPreset _preset;

        [SetUp]
        public void SetUp()
        {
            _preset = Keep(ScriptableObject.CreateInstance<BlockPreset>());
            _preset.BlockSchema.schema = new[,] { { 1, 0, 1 }, { 0, 1, 0 } };
            _preset.GenerateSchema();
        }

        [TearDown]
        public void TearDown()
        {
            var viewModelField = typeof(AtomView).GetField("_viewModel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var item in _objects)
                if (item is GameObject gameObject)
                    foreach (var atomView in gameObject.GetComponentsInChildren<AtomView>())
                        if (viewModelField?.GetValue(atomView) is AtomViewModel viewModel)
                            viewModel.SetOutlineCommand.Dispose();
            foreach (var atom in _generatedAtoms)
            {
                atom.Model.ParentBlock.Dispose();
                atom.Model.ArrayIndex.Dispose();
            }
            _generatedAtoms.Clear();
            foreach (var item in _objects)
                if (item)
                    Object.DestroyImmediate(item);
            _objects.Clear();
        }

        [Test]
        public void SparseSchema_GeneratesEachOccupiedRotationOnce()
        {
            var schema = _preset.BlockSchema.schema;
            var rotationCount = _preset.DistinctBlockSchemas.Count;
            var originalAtomCount = CountOccupied(schema);

            Assert.That(originalAtomCount, Is.EqualTo(3));
            Assert.That(rotationCount, Is.EqualTo(4),
                "The diagonal sparse footprint occupies one cell in each row and column and has four distinct rotations.");
            foreach (var rotated in _preset.DistinctBlockSchemas)
                Assert.That(CountOccupied(rotated.schema), Is.EqualTo(originalAtomCount));
        }

        [Test]
        public void BlockModel_GeneratesExactlyOneAtomPerOccupiedSparseSchemaCell()
        {
            var blockManagerConfig = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            SetField(blockManagerConfig, "_atomColorDict", new Dictionary<BlockColor, Color>
            {
                [BlockColor.Red] = Color.red
            });
            var atomFactory = new AtomFactory(blockManagerConfig, CreateAtomViewPrefab(blockManagerConfig));
            var blockConfig = Keep(ScriptableObject.CreateInstance<BlockConfig>());
            SetField(blockConfig, "<UseAtomSprite>k__BackingField", true);
            var blockModel = new BlockModel(blockConfig, atomFactory);
            var blockViewModel = new BlockViewModel(blockModel);
            var block = new BlockInstance(blockModel, blockViewModel, null, Keep(new GameObject("Sparse block")));
            blockModel.ChangeColor(BlockColor.Red, false);
            var schema = new[,] { { 1, 0, 1 }, { 0, 1, 0 } };
            var expected = new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 2),
                new Vector2Int(1, 1)
            };
            Assert.DoesNotThrow(() => blockModel.GenerateAtom(default, _preset, block, schema),
                "Every nonzero schema cell must create an atom through AtomFactory.");

            _generatedAtoms.AddRange(blockModel.Atoms);
            Assert.That(blockModel.Atoms, Has.Count.EqualTo(expected.Length));
            CollectionAssert.AreEqual(expected, blockModel.Atoms.Select(atom => atom.Model.ArrayIndex.Value));
            Assert.That(blockModel.Atoms.Select(atom => atom.Model.ArrayIndex.Value).Distinct().Count(),
                Is.EqualTo(expected.Length), "No occupied schema coordinate should generate duplicate atoms.");
            Assert.That(blockModel.Atoms.Select(atom => atom.Model.ParentBlock.Value),
                Is.All.SameAs(block), "Generated atoms must each be linked to their parent block instance.");
        }

        [Test]
        public void BlockModel_GeneratesOutlineEdgesForSparseFootprint()
        {
            var blockManagerConfig = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            SetField(blockManagerConfig, "_atomColorDict", new Dictionary<BlockColor, Color>
            {
                [BlockColor.Red] = Color.red
            });
            var atomFactory = new AtomFactory(blockManagerConfig, CreateAtomViewPrefab(blockManagerConfig));
            var blockConfig = Keep(ScriptableObject.CreateInstance<BlockConfig>());
            SetField(blockConfig, "<UseAtomSprite>k__BackingField", true);
            var blockModel = new BlockModel(blockConfig, atomFactory);
            var blockViewModel = new BlockViewModel(blockModel);
            var block = new BlockInstance(blockModel, blockViewModel, null, Keep(new GameObject("Sparse block")));
            blockModel.ChangeColor(BlockColor.Red, false);
            var schema = new[,] { { 1, 0, 1 }, { 0, 1, 0 } };
            var expected = new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 2),
                new Vector2Int(1, 1)
            };

            Assert.DoesNotThrow(() => blockModel.GenerateAtom(default, _preset, block, schema));

            _generatedAtoms.AddRange(blockModel.Atoms);
            Assert.That(blockModel.Atoms, Has.Count.EqualTo(expected.Length));
            CollectionAssert.AreEqual(expected, blockModel.Atoms.Select(atom => atom.Model.ArrayIndex.Value));
            var atomViewModelField = typeof(AtomView).GetField("_viewModel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(atomViewModelField, Is.Not.Null);
            var outlineField = typeof(SpriteOutlineController).GetField("outlineSettings",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(outlineField, Is.Not.Null);
            var outlineSettings = blockModel.Atoms.Select(atom =>
            {
                var atomView = atom.GameObject.GetComponent<AtomView>();
                var viewModel = (AtomViewModel)atomViewModelField.GetValue(atomView);
                return (SpriteOutlineSettings)outlineField.GetValue(atomView.GetComponent<SpriteOutlineController>());
            }).ToArray();

            Assert.That(outlineSettings, Has.Length.EqualTo(expected.Length));
            CollectionAssert.AreEqual(expected.Select(index => ExpectedOutline(schema, index)), outlineSettings);
        }

        private T Keep<T>(T item) where T : Object
        {
            _objects.Add(item);
            return item;
        }

        private static int CountOccupied(int[,] schema)
        {
            var count = 0;
            foreach (var cell in schema)
                if (cell != 0)
                    count++;
            return count;
        }

        private static SpriteOutlineSettings ExpectedOutline(int[,] schema, Vector2Int index) => new()
        {
            OutlineTop = !HasElement(schema, index.x - 1, index.y),
            OutlineBottom = !HasElement(schema, index.x + 1, index.y),
            OutlineLeft = !HasElement(schema, index.x, index.y - 1),
            OutlineRight = !HasElement(schema, index.x, index.y + 1)
        };

        private static bool HasElement(int[,] schema, int row, int column) =>
            row >= 0 && row < schema.GetLength(0) &&
            column >= 0 && column < schema.GetLength(1) &&
            schema[row, column] != 0;

        private AtomView CreateAtomViewPrefab(BlockManagerConfig blockManagerConfig)
        {
            var root = Keep(new GameObject("Sparse atom prefab"));
            var spriteRenderer = root.AddComponent<SpriteRenderer>();
            var outlineShader = Shader.Find("Sprites/Default");
            Assert.That(outlineShader, Is.Not.Null, "The fixture requires Unity's built-in sprite shader.");
            spriteRenderer.sharedMaterial = new Material(outlineShader);
            var outlineController = root.AddComponent<SpriteOutlineController>();
            SetField(outlineController, "_spriteRenderer", spriteRenderer);
            var atomView = root.AddComponent<AtomView>();
            typeof(AtomView).GetField("spriteRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(atomView, spriteRenderer);
            typeof(AtomView).GetField("spriteOutlineController", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(atomView, outlineController);
            var viewModelField = typeof(AtomView).GetField("_viewModel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(viewModelField, Is.Not.Null);
            var viewModel = new AtomViewModel(new AtomModel());
            viewModelField.SetValue(atomView, viewModel);
            typeof(AtomView).GetField("_blockManagerConfig", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(atomView, blockManagerConfig);
            atomView.gameObject.SetActive(false);
            return atomView;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing serialized fixture field {name}.");
            field.SetValue(target, value);
        }
    }
}
