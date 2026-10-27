using System;
using System.Linq;
using System.Reflection;
using FitMe.Shared;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public partial class GridManagerLifecycleTests
    {
        private BlockInstance[] SpawnScaleTray(int count, float scale, int[,] schema = null, float objectScale = .03f, Vector2? traySize = null, bool mixed = false)
        {
            var config = Keep(UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(ArtRoot + "Settings/Grid/Block/BlockManagerConfig.asset")));
            var property = typeof(BlockManagerConfig).GetProperty("TrayBlockScale");
            Assert.That(property, Is.Not.Null, "Shape Tray needs its own Inspector Tray Block Scale");
            property.SetValue(config, scale);
            typeof(BlockManagerConfig).GetProperty("ObjectScale").SetValue(config, objectScale);
            var rect = (RectTransform)Keep(new GameObject("Scale test tray", typeof(RectTransform))).transform;
            rect.sizeDelta = traySize ?? new Vector2(5, 3);
            rect.position = new Vector3(0, -4, 0);
            var gridConfig = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            var factory = new BlockFactory(
                AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Prefabs/Block/BlockView.prefab").GetComponent<BlockView>(), config, gridConfig,
                new AtomFactory(config, AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Prefabs/Atom/AtomView.prefab").GetComponent<AtomView>()),
                new BlockControllerFactory(config, _manager, null, null, null));
            using var manager = new BlockManager(_manager, config, null, Array.Empty<BlockManager.SpawnPointData>(), factory,
                Array.Empty<BlockManager.SpawnPointData>(), _hub, rect);
            var mixedSchemas = new[] { new[,] { { 1 } }, new[,] { { 1, 1 } }, new[,] { { 1, 1, 1, 1 } } };
            var entries = Enumerable.Range(0, count).Select(i => new ShapePlayerBlockRuntimeEntry(null,
                mixed ? mixedSchemas[i % mixedSchemas.Length] : schema ?? new[,] { { 1, 1 }, { 1, 0 } })).ToArray();
            UnityEngine.Random.InitState(719);
            _hub.Publish(new SpawnWithShapeLevelEvent(new ShapeLevelRuntimeData(null, new[,] { { 1 } }, Array.Empty<ShapeObstacleRuntimeData>(), entries)));
            SetManagerField("<IsGameplay>k__BackingField", true);
            ((Subject<Unit>)typeof(GridManager).GetField("_onCellsCreated", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_manager)).OnNext(Unit.Default);
            var blocks = manager.BlockOnHand.ToArray();
            foreach (var block in blocks)
            {
                _objects.Add(block.GameObject);
                _additionalBlockModels.Add(block.Model);
                _additionalBlockControllers.Add(block.Controller);
                _additionalBlockViewModels.Add(block.ViewModel);
                block.GameObject.transform.localScale = new Vector3(block.Model.TrayPiece.Scale, block.Model.TrayPiece.Scale, 1);
            }
            return blocks;
        }


        [TestCase(3, .5f)]
        [TestCase(3, 10f)]
        [TestCase(6, .5f)]
        [TestCase(7, .5f)]
        [TestCase(20, 10f)]
        public void ShapeTray_MixedFootprintsShareOneEffectiveRestingScale(int count, float requestedScale)
        {
            var schemas = new[] { new[,] { { 1 } }, new[,] { { 1, 1 } }, new[,] { { 1, 1, 1, 1 } } };
            var items = Enumerable.Range(0, count).Select(i => new ShapeBlockSupplyQueue.Item(
                new ShapePlayerBlockRuntimeEntry(null, schemas[i % schemas.Length]),
                Quaternion.Euler(0, 0, i * 90), BlockColor.Red)).ToArray();
            UnityEngine.Random.InitState(719);
            var tilts = Enumerable.Range(0, count).Select(_ => UnityEngine.Random.Range(-15f, 15f)).ToArray();
            UnityEngine.Random.InitState(719);
            var bounds = new Rect(0, 0, 5, 3);
            var pieces = ShapeTrayLayout.Create(items, bounds, requestedScale, -15, 15);
            Assert.That(pieces[0].Scale, Is.GreaterThan(0));
            Assert.That(pieces[0].Scale, Is.LessThanOrEqualTo(requestedScale));
            foreach (var piece in pieces)
            {
                Assert.That(piece.Scale, Is.EqualTo(pieces[0].Scale),
                    $"Piece {piece.PieceId}: every atom must use the same initial resting scale, including fit/visibility reductions");
                Assert.That(piece.Tilt, Is.EqualTo(tilts[piece.PieceId]), "Common-scale retries must not reroll tilt");
                Assert.That(Quaternion.Angle(piece.GameplayRotation, items[piece.PieceId].Rotation), Is.LessThan(.001f));
            }
        }

        [TestCase(7)]
        [TestCase(20)]
        public void ShapeTray_SeparatedFallbackKeepsMixedFootprintsAtOneScale(int count)
        {
            var shapes = new[] { new[,] { { 1 } }, new[,] { { 1, 1 } }, new[,] { { 1, 1, 1, 1 } } };
            var schemas = Enumerable.Range(0, count).Select(i => shapes[i % shapes.Length]).ToArray();
            var bounds = new Rect(0, 0, 8, 5);
            var pieces = Enumerable.Range(0, count).Select(i => new ShapeTrayPiece(i, schemas[i], bounds,
                .5f, i % 2 == 0 ? -13 : 13, Quaternion.Euler(0, 0, i * 90), bounds.center)).ToArray();
            // Exercise the deterministic safety path without relying on random
            // candidate exhaustion (which is not a stable public repro trigger).
            typeof(ShapeTrayLayout).GetMethod("PackSeparated", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { pieces, schemas, bounds });
            typeof(ShapeTrayLayout).GetMethod("ValidatePickablePatches", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { pieces, schemas, bounds });
            foreach (var piece in pieces)
            {
                Assert.That(piece.Scale, Is.EqualTo(pieces[0].Scale));
                Assert.That(piece.Scale, Is.InRange(float.Epsilon, .5f));
                Assert.That(piece.PresentationBounds.xMin, Is.GreaterThanOrEqualTo(-.0001f));
                Assert.That(piece.PresentationBounds.yMin, Is.GreaterThanOrEqualTo(-.0001f));
                Assert.That(piece.PresentationBounds.xMax, Is.LessThanOrEqualTo(8.0001f));
                Assert.That(piece.PresentationBounds.yMax, Is.LessThanOrEqualTo(5.0001f));
            }
        }

        [Test]
        public void ShapeTray_QuarterTurnClampsWholeFootprintWithoutChangingRestingScale()
        {
            var piece = new ShapeTrayPiece(0, new[,] { { 1, 1, 1, 1 } }, new Rect(0, 0, 5, 3),
                .5f, 13f, Quaternion.identity, new Vector2(100, 100));
            var scale = piece.Scale;
            for (var turn = 0; turn < 4; turn++)
            {
                piece.Rotate(90);
                Assert.That(piece.Scale, Is.EqualTo(scale));
                Assert.That(piece.PresentationBounds.xMax, Is.LessThanOrEqualTo(5.0001f));
                Assert.That(piece.PresentationBounds.yMax, Is.LessThanOrEqualTo(3.0001f));
                Assert.That(piece.PresentationBounds.xMin, Is.GreaterThanOrEqualTo(-.0001f));
                Assert.That(piece.PresentationBounds.yMin, Is.GreaterThanOrEqualTo(-.0001f));
            }
        }

        [Test]
        public void ShapeTray_TinyDenseFallbackRejectsUnpickableConfiguration()
        {
            var items = Enumerable.Range(0, 7).Select(_ => new ShapeBlockSupplyQueue.Item(
                new ShapePlayerBlockRuntimeEntry(null, new[,] { { 1 } }), Quaternion.identity, BlockColor.Red)).ToArray();
            UnityEngine.Random.InitState(719);
            var error = Assert.Throws<InvalidOperationException>(() =>
                ShapeTrayLayout.Create(items, new Rect(0, 0, .1f, .1f), .5f, 0, 0),
                "The separated fallback must not return pieces hidden by the fixed world-space collider radius");
            StringAssert.Contains("Shape Tray", error.Message);
            StringAssert.Contains("usable bounds", error.Message);
            StringAssert.Contains("enlarge", error.Message);
        }

        [Test]
        public void ShapeTray_RealAtomPrefabRadiusStillCoversTinyTrayAtFallbackScale()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Prefabs/Atom/AtomView.prefab");
            var atom = Keep(UnityEngine.Object.Instantiate(prefab));
            atom.transform.position = new Vector3(.05f, .05f);
            atom.transform.localScale = new Vector3(.02f, .02f, 1);
            var collider = atom.GetComponent<BoxCollider2D>();
            Assert.That(collider.edgeRadius, Is.EqualTo(.25f));
            Physics2D.SyncTransforms();
            // Real Physics2D confirms the radius does not shrink with the artwork.
            foreach (var point in new[] { Vector2.zero, new Vector2(.1f, 0), new Vector2(0, .1f), new Vector2(.1f, .1f) })
                Assert.That(collider.OverlapPoint(point), Is.True, $"Tiny-tray corner {point} is covered by one atom collider");
            Assert.That(collider.bounds.size.x, Is.LessThan(.1f), "Reported bounds omit the edge radius");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ShapeTray_InvalidSerializedScaleUsesFinitePositiveFallback(float scale)
        {
            var blocks = SpawnScaleTray(7, scale);
            foreach (var block in blocks)
            {
                var piece = block.Model.TrayPiece;
                Assert.That(float.IsNaN(piece.Scale) || float.IsInfinity(piece.Scale), Is.False);
                Assert.That(piece.Scale, Is.GreaterThan(0));
                Assert.That(piece.Scale, Is.LessThanOrEqualTo(.5f));
            }
            AssertTrayBoundsAndSeparation(blocks, false);
        }

        [Test]
        public void ShapeTray_ClassicSpawnStillUsesObjectScaleNotTrayScale()
        {
            var config = Keep(UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(ArtRoot + "Settings/Grid/Block/BlockManagerConfig.asset")));
            typeof(BlockManagerConfig).GetProperty("TrayBlockScale").SetValue(config, .12f);
            typeof(BlockManagerConfig).GetProperty("ObjectScale").SetValue(config, .61f);
            var gridConfig = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            var factory = new BlockFactory(
                AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Prefabs/Block/BlockView.prefab").GetComponent<BlockView>(), config, gridConfig,
                new AtomFactory(config, AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Prefabs/Atom/AtomView.prefab").GetComponent<AtomView>()),
                new BlockControllerFactory(config, _manager, null, null, null));
            var point = CreateSpawnPoint(Keep(new GameObject("Classic hand")).transform, null, true);
            using var manager = new BlockManager(_manager, config, null, new[] { point }, factory, Array.Empty<BlockManager.SpawnPointData>(), _hub);
            var queue = (System.Collections.Generic.Queue<SpawnBlockData>)typeof(BlockManager).GetField("_spawnBag", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            queue.Enqueue(new SpawnBlockData(BlockShape.OneByOne, Quaternion.identity, null, BlockColor.Red));
            manager.SpawnBlocksFromBag(false);
            var block = manager.BlockOnHand.Single();
            _objects.Add(block.GameObject);
            _additionalBlockModels.Add(block.Model);
            _additionalBlockControllers.Add(block.Controller);
            _additionalBlockViewModels.Add(block.ViewModel);
            Assert.That(block.Model.TrayPiece, Is.Null);
            var target = (Vector3)typeof(BlockView).GetField("_originalScale", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(block.GameObject.GetComponent<BlockView>());
            Assert.That(target.x, Is.EqualTo(.61f).Within(.0001f));
            Assert.That(config.TrayBlockScale, Is.EqualTo(.12f));
        }

        [TestCase(7, .9f)]
        [TestCase(20, .9f)]
        [TestCase(20, 10f)]
        public void ShapeTray_IdenticalDenseShapesHaveExposedHittableAtomArea(int count, float scale)
        {
            var blocks = SpawnScaleTray(count, scale, new[,] { { 1, 1 }, { 1, 1 } });
            AssertExposedAtomPatches(blocks);
        }

        [TestCase(6, .5f)]
        [TestCase(7, .5f)]
        [TestCase(20, 10f)]
        public void ShapeTray_MixedRealPrefabsShareScaleBoundsAndExposedHitPatches(int count, float scale)
        {
            var blocks = SpawnScaleTray(count, scale, mixed: true);
            foreach (var block in blocks)
            {
                var piece = block.Model.TrayPiece;
                Assert.That(piece.Scale, Is.EqualTo(blocks[0].Model.TrayPiece.Scale));
                Assert.That(piece.Scale, Is.LessThanOrEqualTo(scale));
                var target = (Vector3)typeof(BlockView).GetField("_originalScale", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(block.GameObject.GetComponent<BlockView>());
                Assert.That(target.x, Is.EqualTo(piece.Scale), "Actual view resting target must match the shared layout scale");
            }
            AssertTrayBoundsAndSeparation(blocks, count <= 6);
            AssertExposedAtomPatches(blocks);
            foreach (var block in blocks)
                for (var turn = 0; turn < 4; turn++)
                {
                    block.Model.TrayPiece.Rotate(90);
                    AssertTrayBoundsAndSeparation(new[] { block }, false);
                }
        }

        private static void AssertExposedAtomPatches(BlockInstance[] blocks)
        {
            Physics2D.SyncTransforms();
            for (var i = 0; i < blocks.Length; i++)
            {
                var exposed = false;
                foreach (var atom in blocks[i].Model.Atoms)
                for (var x = -1; x <= 1; x++)
                for (var y = -1; y <= 1; y++)
                {
                    var point = atom.GameObject.transform.TransformPoint(new Vector3(x * .25f, y * .25f));
                    var hits = Physics2D.OverlapPointAll(point);
                    if (!hits.Any(h => h.transform.IsChildOf(blocks[i].GameObject.transform))) continue;
                    if (blocks.Skip(i + 1).Any(later => hits.Any(h => h.transform.IsChildOf(later.GameObject.transform)))) continue;
                    exposed = true;
                }

                Assert.That(exposed, Is.True, $"Piece {i} must expose actual atom/collider area above all later pieces, not just a unique pivot");
            }
        }

        [TestCase(1, .8f)]
        [TestCase(6, .3f)]
        public void ShapeTray_SparseSupplyKeepsRequestedScaleWhenRotatedShapesFit(int count, float scale)
        {
            var blocks = SpawnScaleTray(count, scale);
            foreach (var block in blocks)
                Assert.That(block.Model.TrayPiece.Scale, Is.EqualTo(scale).Within(.0001f));
            AssertTrayBoundsAndSeparation(blocks, true);
        }

        [TestCase(1)]
        [TestCase(6)]
        [TestCase(20)]
        public void ShapeTray_OversizedPiecesFitCompleteRotatedBounds(int count)
        {
            var blocks = SpawnScaleTray(count, 100f, new[,] { { 1, 1, 1, 1, 1, 1, 1, 1 } });
            AssertTrayBoundsAndSeparation(blocks, count <= 6);
            foreach (var block in blocks)
            {
                var piece = block.Model.TrayPiece;
                Assert.That(piece.Scale, Is.GreaterThan(0));
                for (var turn = 0; turn < 4; turn++)
                {
                    piece.Rotate(90);
                    piece.DropInTray(piece.Position);
                    var b = piece.PresentationBounds;
                    Assert.That(b.xMin, Is.GreaterThanOrEqualTo(piece.TrayBounds.xMin - .0001f));
                    Assert.That(b.xMax, Is.LessThanOrEqualTo(piece.TrayBounds.xMax + .0001f));
                    Assert.That(b.yMin, Is.GreaterThanOrEqualTo(piece.TrayBounds.yMin - .0001f));
                    Assert.That(b.yMax, Is.LessThanOrEqualTo(piece.TrayBounds.yMax + .0001f));
                }
            }
        }

        private static void AssertTrayBoundsAndSeparation(BlockInstance[] blocks, bool separate)
        {
            for (var i = 0; i < blocks.Length; i++)
            {
                var piece = blocks[i].Model.TrayPiece;
                var b = piece.PresentationBounds;
                Assert.That(b.xMin, Is.GreaterThanOrEqualTo(piece.TrayBounds.xMin - .0001f));
                Assert.That(b.xMax, Is.LessThanOrEqualTo(piece.TrayBounds.xMax + .0001f));
                Assert.That(b.yMin, Is.GreaterThanOrEqualTo(piece.TrayBounds.yMin - .0001f));
                Assert.That(b.yMax, Is.LessThanOrEqualTo(piece.TrayBounds.yMax + .0001f));
                if (separate)
                    for (var j = i + 1; j < blocks.Length; j++)
                        Assert.That(b.Overlaps(blocks[j].Model.TrayPiece.PresentationBounds), Is.False);
            }
        }

        [TestCase(7)]
        [TestCase(20)]
        public void ShapeTray_InspectorScaleEnlargesDenseSupplyIndependentlyOfObjectScale(int count)
        {
            // Leave enough physical room for unoccluded hit patches at both sizes;
            // an overfilled tray may legitimately shrink all pieces for visibility.
            var size = count == 20 ? new Vector2(8, 5) : new Vector2(5, 3);
            var small = SpawnScaleTray(count, .12f, traySize: size);
            var large = SpawnScaleTray(count, .45f, traySize: size);
            Assert.That(small, Has.Length.EqualTo(count));
            Assert.That(large, Has.Length.EqualTo(count));
            for (var i = 0; i < count; i++)
            {
                Assert.That(small[i].Model.TrayPiece.Scale, Is.EqualTo(.12f).Within(.0001f));
                Assert.That(large[i].Model.TrayPiece.Scale, Is.EqualTo(.45f).Within(.0001f));
                Assert.That(large[i].Model.TrayPiece.PresentationBounds.size.magnitude,
                    Is.GreaterThan(small[i].Model.TrayPiece.PresentationBounds.size.magnitude * 3));
            }
        }
    }
}
