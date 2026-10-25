using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FitMe.Shared;
using NUnit.Framework;
using R3;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace FitMe.Grid.Tests
{
    public partial class GridManagerLifecycleTests
    {
        private const string ArtRoot = "Assets/QuackUp/";

        private void RenderShapeTrayEvidence(RectTransform tray)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            var camera = Keep(new GameObject("Tray evidence camera")).AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.6f;
            camera.aspect = 1.8f;
            camera.transform.position = tray.position + Vector3.back * 10;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .14f, .18f);
            const int layer = 30;
            foreach (var child in tray.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            camera.cullingMask = 1 << layer;
            var rt = Keep(new RenderTexture(1000, 560, 24));
            var image = Keep(new Texture2D(1000, 560, TextureFormat.RGBA32, false));
            var old = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1000, 560), 0, 0);
                image.Apply();
                var path = System.IO.Path.GetFullPath("../shape-tray-six-pieces.png");
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                Assert.That(image.GetPixels().Count(c => c.r > .7f && c.g > .6f), Is.GreaterThan(10000), "Actual tray artwork must render");
                TestContext.Out.WriteLine("Tray GPU evidence: " + path);
            }
            finally { camera.targetTexture = null; RenderTexture.active = old; rt.Release(); }
        }

        private sealed class TrayPointer : QuackUp.Input.IPointerHandler
        {
            public Vector2 MouseWorldPosition { get; set; }
            public Vector2 MouseCanvasPosition => MouseWorldPosition;
            public Vector2 WorldToLocalCanvasPosition(Vector3 position) => position;
            public Vector2 WorldToWorldCanvasPosition(Vector3 position) => position;
        }

        private BlockInstance CreateTrayDragBlock(out TrayPointer pointer, out BlockManagerConfig config, float trayScale = .35f)
        {
            SetUpOneCellPlacement();
            config = Keep(UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(ArtRoot + "Settings/Grid/Block/BlockManagerConfig.asset")));
            typeof(BlockManagerConfig).GetProperty("TrayBlockScale").SetValue(config, trayScale);
            foreach (var name in new[] { "UseBlockScaleTween", "UseBlockDragTween", "UseDynamicDragOffset" })
                typeof(BlockManagerConfig).GetProperty(name).SetValue(config, false);
            typeof(BlockManagerConfig).GetProperty("BlockDragInertia").SetValue(config, 0f);
            typeof(BlockManagerConfig).GetProperty("AllowPickUpAfterPlacement").SetValue(config, true);
            var gridConfig = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            pointer = new TrayPointer { MouseWorldPosition = new Vector2(0, -3) };
            var atom = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Prefabs/Atom/AtomView.prefab").GetComponent<AtomView>();
            var view = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Prefabs/Block/BlockView.prefab").GetComponent<BlockView>();
            var factory = new BlockFactory(view, config, gridConfig, new AtomFactory(config, atom), new BlockControllerFactory(config, _manager,
                new LevelManagerMock(GameState.PlaceBlock), new QuackUp.Audio.AudioManagerMock(), pointer));
            var parent = Keep(new GameObject("Tray parent")).transform;
            var block = factory.Create(BlockShape.OneByOne, pointer.MouseWorldPosition, Quaternion.Euler(0, 0, 90),
                new InstantiateParameters { parent = parent, worldSpace = true }, new[,] { { 1 } }, blockConfigOverride: config.ShapeModeBlockConfig);
            _objects.Add(block.GameObject);
            _additionalBlockModels.Add(block.Model);
            _additionalBlockControllers.Add(block.Controller);
            _additionalBlockViewModels.Add(block.ViewModel);
            block.Model.TrayPiece = new ShapeTrayPiece(31, new[,] { { 1 } }, new Rect(-2, -4, 4, 2), config.TrayBlockScale, 13f, Quaternion.Euler(0, 0, 90), pointer.MouseWorldPosition);
            block.GameObject.transform.rotation = block.Model.TrayPiece.TrayRotation;
            block.GameObject.transform.localScale = new Vector3(block.Model.TrayPiece.Scale, block.Model.TrayPiece.Scale, 1);
            return block;
        }

        [TestCase(.2f)]
        [TestCase(.7f)]
        public void ShapeTray_DragUsesGridScaleWithoutTiltAndTrayDropPersistsLatestReturnPoint(float trayScale)
        {
            var block = CreateTrayDragBlock(out var pointer, out var config, trayScale);
            var piece = block.Model.TrayPiece;
            var eventData = new UnityEngine.EventSystems.PointerEventData(null);
            block.Controller.BeingDragCommand.Execute(eventData);
            Assert.That(Quaternion.Angle(block.GameObject.transform.rotation, piece.GameplayRotation), Is.LessThan(.01f), "Pickup removes only visual tilt");
            var gridSize = Keep(ScriptableObject.CreateInstance<GridManagerConfig>()).CellSize;
            Assert.That(block.GameObject.transform.localScale.x, Is.EqualTo(gridSize.x).Within(.001f), "Pickup uses grid cell size, not either tray or ObjectScale");
            Assert.That(block.GameObject.transform.localScale.y, Is.EqualTo(gridSize.y).Within(.001f));
            pointer.MouseWorldPosition = new Vector2(1.9f, -2.1f);
            block.Controller.DragCommand.Execute(eventData);
            block.Controller.EndDragCommand.Execute(eventData);
            var saved = piece.Position;
            Assert.That(saved, Is.Not.EqualTo(new Vector2(0, -3)), "Tray drop must persist a new location");
            Assert.That(block.GameObject.transform.position.x, Is.EqualTo(saved.x).Within(.001f));
            Assert.That(block.GameObject.transform.localScale.x, Is.EqualTo(piece.Scale).Within(.001f));
            Assert.That(Quaternion.Angle(block.GameObject.transform.rotation, piece.TrayRotation), Is.LessThan(.01f));
            block.Controller.BeingDragCommand.Execute(eventData);
            pointer.MouseWorldPosition = new Vector2(50, 50);
            block.Controller.DragCommand.Execute(eventData);
            block.Controller.EndDragCommand.Execute(eventData);
            Assert.That(piece.Position, Is.EqualTo(saved));
            Assert.That((Vector2)block.GameObject.transform.position, Is.EqualTo(saved), "Outside drop bounces to latest tray position");
            Assert.That(_cell.CurrentAtom.Value, Is.Null);
        }

        [Test]
        public void ShapeTray_ValidGridDropOccupiesCellAndCannotBePickedUpEvenWhenLegacyConfigAllowsIt()
        {
            var block = CreateTrayDragBlock(out var pointer, out _);
            var eventData = new UnityEngine.EventSystems.PointerEventData(null);
            block.Controller.BeingDragCommand.Execute(eventData);
            pointer.MouseWorldPosition = _grid.GetCellCenterWorld(Vector3Int.zero);
            block.Controller.DragCommand.Execute(eventData);
            block.Controller.EndDragCommand.Execute(eventData);
            Assert.That(_cell.CurrentAtom.Value, Is.SameAs(block.Model.Atoms[0]));
            Assert.That(block.Model.TrayPiece.IsPlaced, Is.True);
            var position = block.GameObject.transform.position;
            block.Controller.BeingDragCommand.Execute(eventData);
            pointer.MouseWorldPosition = Vector2.one * 50;
            block.Controller.DragCommand.Execute(eventData);
            Assert.That(block.GameObject.transform.position, Is.EqualTo(position));
            Assert.That(_cell.CurrentAtom.Value, Is.SameAs(block.Model.Atoms[0]));
        }

        [Test]
        public void ShapeTray_DropOnArtworkPaddingClampsInsideUsableInteriorRatherThanBouncing()
        {
            var block = CreateTrayDragBlock(out var pointer, out _);
            typeof(ShapeTrayPiece).GetProperty("DropBounds")?.SetValue(block.Model.TrayPiece, new Rect(-2.2f, -4.2f, 4.4f, 2.4f));
            var previous = block.Model.TrayPiece.Position;
            var evt = new UnityEngine.EventSystems.PointerEventData(null);
            block.Controller.BeingDragCommand.Execute(evt);
            pointer.MouseWorldPosition = new Vector2(2.1f, -3);
            block.Controller.DragCommand.Execute(evt);
            block.Controller.EndDragCommand.Execute(evt);
            Assert.That(block.Model.TrayPiece.Position, Is.Not.EqualTo(previous));
            Assert.That(block.Model.TrayPiece.PresentationBounds.xMax, Is.LessThanOrEqualTo(2.0001f));
        }

        [Test]
        public void ShapeTray_RepeatedTapRotationUsesGameplayQuarterTurnsRatherThanVisualTilt()
        {
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            var entry = new ShapePlayerBlockRuntimeEntry(null, new[,] { { 1, 1 } });
            var block = CreateShapeTestBlock(config, new ShapeBlockSupplyQueue.Item(entry, Quaternion.Euler(0, 0, 90), BlockColor.Red), _grid.transform, -1, true);
            var piece = new ShapeTrayPiece(7, entry.Schema, new Rect(-2, -4, 4, 2), .3f, 13f, Quaternion.Euler(0, 0, 90), new Vector2(0, -3));
            block.Model.TrayPiece = piece;
            block.GameObject.transform.rotation = piece.TrayRotation;
            using var controller = new BlockController(config, _manager, new LevelManagerMock(GameState.PlaceBlock),
                new QuackUp.Audio.AudioManagerMock(), null);
            controller.Initialize(block);
            var requested = Quaternion.identity;
            using var sub = block.ViewModel.RotateCommand.Subscribe(data => { requested = data.Rotation; data.Promise.TrySetResult(Unit.Default); });
            var evt = new UnityEngine.EventSystems.PointerEventData(null);
            controller.PointerDownCommand.Execute(evt);
            controller.PointerUpCommand.Execute(evt);
            Assert.That(Quaternion.Angle(piece.GameplayRotation, Quaternion.identity), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(requested, Quaternion.Euler(0, 0, 13)), Is.LessThan(.01f));
            // No View applies the first request in this isolated controller test: the
            // second tap must still use gameplay state, not the stale visual transform.
            controller.PointerDownCommand.Execute(evt);
            controller.PointerUpCommand.Execute(evt);
            Assert.That(Quaternion.Angle(piece.GameplayRotation, Quaternion.Euler(0, 0, 270)), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(requested, Quaternion.Euler(0, 0, 283)), Is.LessThan(.01f));
            Assert.That(piece.Tilt, Is.EqualTo(13));
            Assert.That(controller.IsRotating, Is.False);
        }

        [Test]
        public void ShapeTray_DisabledRotationIgnoresTapAndKeepsGameplayOrientation()
        {
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            var entry = new ShapePlayerBlockRuntimeEntry(null, new[,] { { 1, 1 } });
            var block = CreateShapeTestBlock(config, new ShapeBlockSupplyQueue.Item(entry, Quaternion.identity, BlockColor.Red), _grid.transform, -1, false);
            var piece = new ShapeTrayPiece(7, entry.Schema, new Rect(-2, -4, 4, 2), .3f, 13f, Quaternion.identity, new Vector2(0, -3));
            block.Model.TrayPiece = piece;
            using var controller = new BlockController(config, _manager, new LevelManagerMock(GameState.PlaceBlock),
                new QuackUp.Audio.AudioManagerMock(), null);
            controller.Initialize(block);
            controller.AllowRotation = false;
            var rotateEvents = 0;
            using var sub = block.ViewModel.RotateCommand.Subscribe(_ => rotateEvents++);
            var evt = new UnityEngine.EventSystems.PointerEventData(null);
            controller.PointerDownCommand.Execute(evt);
            controller.PointerUpCommand.Execute(evt);

            Assert.That(rotateEvents, Is.Zero);
            Assert.That(Quaternion.Angle(piece.GameplayRotation, Quaternion.identity), Is.LessThan(.01f));
            Assert.That(piece.Tilt, Is.EqualTo(13f));
        }

        [Test]
        public void ShapeTray_RejectedOccupiedGridDropReturnsWithoutChangingOccupancy()
        {
            var block = CreateTrayDragBlock(out var pointer, out _);
            var existing = _blockModel.Atoms[0];
            _cell.CurrentAtom.Value = existing;
            block.Model.TrayPiece.DropInTray(new Vector2(.7f, -3));
            block.GameObject.transform.position = block.Model.TrayPiece.Position;
            pointer.MouseWorldPosition = block.Model.TrayPiece.Position;
            var eventData = new UnityEngine.EventSystems.PointerEventData(null);
            block.Controller.BeingDragCommand.Execute(eventData);
            pointer.MouseWorldPosition = _grid.GetCellCenterWorld(Vector3Int.zero);
            block.Controller.DragCommand.Execute(eventData);
            block.Controller.EndDragCommand.Execute(eventData);
            Assert.That(_cell.CurrentAtom.Value, Is.SameAs(existing));
            Assert.That(block.Model.TrayPiece.IsPlaced, Is.False);
            Assert.That((Vector2)block.GameObject.transform.position, Is.EqualTo(block.Model.TrayPiece.Position));
            Assert.That(Quaternion.Angle(block.GameObject.transform.rotation, block.Model.TrayPiece.TrayRotation), Is.LessThan(.01f));
        }

        [Test]
        public void ShapeTray_GameOverCheckIsDisabledForShapeLevels()
        {
            SetUpOneCellPlacement();
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            using var manager = new BlockManager(_manager, config, null, Array.Empty<BlockManager.SpawnPointData>(), null,
                Array.Empty<BlockManager.SpawnPointData>(), _hub);
            var entries = Enumerable.Range(0, 4).Select(i => new ShapePlayerBlockRuntimeEntry(null,
                i == 3 ? new[,] { { 1 } } : new[,] { { 1, 1 }, { 1, 1 } })).ToArray();
            _hub.Publish(new SpawnWithShapeLevelEvent(new ShapeLevelRuntimeData(null, new[,] { { 1 } }, Array.Empty<ShapeObstacleRuntimeData>(), entries)));
            var blocks = (List<BlockInstance>)typeof(BlockManager).GetField("_trayBlocks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            for (var i = 0; i < entries.Length; i++)
            {
                var block = CreateShapeTestBlock(config, new ShapeBlockSupplyQueue.Item(entries[i], Quaternion.identity, BlockColor.Red), _grid.transform, -1, true);
                block.Model.TrayPiece = new ShapeTrayPiece(i, entries[i].Schema, new Rect(-2, -4, 4, 2), .2f, 0, Quaternion.identity, new Vector2(0, -3));
                blocks.Add(block);
            }
            var gameOvers = 0;
            using var sub = _hub.Subscribe<GameOverEvent>(evt => { if (evt.IsOver) gameOvers++; });
            manager.GameOverCheck().GetAwaiter().GetResult();
            Assert.That(gameOvers, Is.Zero, "Shape Mode must not publish game over when a tray piece fits");
            blocks[3].Model.TrayPiece.MarkPlaced();
            manager.GameOverCheck().GetAwaiter().GetResult();
            Assert.That(gameOvers, Is.Zero, "Shape Mode must not publish game over even when no tray piece fits");
        }

        [Test]
        public void ShapeTray_HintSupplyContainsEveryUnplacedTrayPieceAsActiveInventory()
        {
            SetUpOneCellPlacement();
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            using var manager = new BlockManager(_manager, config, null, Array.Empty<BlockManager.SpawnPointData>(), null,
                Array.Empty<BlockManager.SpawnPointData>(), _hub);
            var entries = Enumerable.Range(0, 4).Select(i => new ShapePlayerBlockRuntimeEntry(null,
                i % 2 == 0 ? new[,] { { 1, 1 } } : new[,] { { 1 } })).ToArray();
            _hub.Publish(new SpawnWithShapeLevelEvent(new ShapeLevelRuntimeData(null, new[,] { { 1 } }, Array.Empty<ShapeObstacleRuntimeData>(), entries)));
            var blocks = (List<BlockInstance>)typeof(BlockManager).GetField("_trayBlocks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            for (var i = 0; i < entries.Length; i++)
            {
                var item = new ShapeBlockSupplyQueue.Item(entries[i], Quaternion.identity, BlockColor.Red);
                var block = CreateShapeTestBlock(config, item, _grid.transform, -1, true);
                block.Model.TrayPiece = new ShapeTrayPiece(i, entries[i].Schema, new Rect(-2, -4, 4, 2), .2f, 0, Quaternion.identity, new Vector2(0, -3));
                blocks.Add(block);
            }

            Assert.That(manager.TryGetShapeHintSupply(out var initialSupply, out var initialHandCount), Is.True);
            Assert.That(initialSupply, Has.Count.EqualTo(entries.Length));
            Assert.That(initialHandCount, Is.EqualTo(entries.Length));

            blocks[1].Model.TrayPiece.MarkPlaced();
            Assert.That(manager.TryGetShapeHintSupply(out var remainingSupply, out var remainingHandCount), Is.True);
            Assert.That(remainingSupply, Has.Count.EqualTo(entries.Length - 1));
            Assert.That(remainingHandCount, Is.EqualTo(entries.Length - 1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ShapeTray_VContainerResolvesGameplayBoundsAndPreservesLegacyScenesWithoutTray(bool hasTray)
        {
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            var rect = (RectTransform)Keep(new GameObject("Injected tray bounds", typeof(RectTransform))).transform;
            var builder = new VContainer.ContainerBuilder();
            builder.RegisterInstance(_manager);
            builder.RegisterInstance(config);
            builder.RegisterInstance(_grid.transform).Keyed(BlockManager.PreviewTransformKey);
            builder.RegisterInstance(Array.Empty<BlockManager.SpawnPointData>());
            builder.RegisterInstance(Array.Empty<BlockManager.SpawnPointData>()).Keyed(BlockManager.PreviewTransformKey);
            builder.RegisterInstance(new BlockFactory(null, config, null, null, null));
            builder.RegisterInstance<QuackUp.Utils.IMessageHub>(_hub).Keyed(BlockManagerMessageHub.MessageHubKey);
            builder.Register<RectTransform>(_ => hasTray ? rect : null, VContainer.Lifetime.Scoped).Keyed(BlockManager.ShapeTrayKey);
            builder.Register<BlockManager>(VContainer.Lifetime.Scoped);
            using var container = builder.Build();
            var resolved = (BlockManager)container.Resolve(typeof(BlockManager));
            var injected = typeof(BlockManager).GetField("_trayPanel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(resolved);
            if (hasTray) Assert.That(injected, Is.SameAs(rect));
            else Assert.That(injected, Is.Null);
        }

        [Test]
        public void ShapeTray_PanelOnlyVisibleForShapeModeAndRetryClearsOldSupply()
        {
            var rect = (RectTransform)Keep(new GameObject("Mode-specific Tray Panel", typeof(RectTransform))).transform;
            rect.sizeDelta = new Vector2(4, 2);
            var config = Keep(ScriptableObject.CreateInstance<BlockManagerConfig>());
            using var manager = new BlockManager(_manager, config, null, Array.Empty<BlockManager.SpawnPointData>(), null,
                Array.Empty<BlockManager.SpawnPointData>(), _hub, rect);
            Assert.That(rect.gameObject.activeSelf, Is.False, "Other modes must not show the tray artwork");
            var data = new ShapeLevelRuntimeData(null, new[,] { { 1 } }, Array.Empty<ShapeObstacleRuntimeData>(),
                new[] { new ShapePlayerBlockRuntimeEntry(null, new[,] { { 1 } }) });
            _hub.Publish(new SpawnWithShapeLevelEvent(data));
            Assert.That(rect.gameObject.activeSelf, Is.True);
            _hub.Publish(new SpawnWithShapeLevelEvent(null));
            Assert.That(rect.gameObject.activeSelf, Is.False);
            Assert.That(manager.BlockOnHand, Is.Empty);
        }

        [Test]
        public void ShapeTray_GameplaySceneWiresArtworkBoundsAndInspectorConfig()
        {
            // Acknowledge only pre-existing stale legacy button references; do not
            // silently swallow unrelated scene-load errors or repair unrelated UI here.
            var sceneText = System.IO.File.ReadAllText(ArtRoot + "Scenes/Gameplay.unity");
            var staleIds = new[] { 527656407, 1420291200, 1420291199, 527656406 }
                .Where(id => !System.Text.RegularExpressions.Regex.IsMatch(sceneText, @"&" + id + @"(?:\s|$)"))
                .ToArray();
            var stale = System.Text.RegularExpressions.Regex.Matches(sceneText, @"\{fileID: (\d+)\}")
                .Cast<System.Text.RegularExpressions.Match>().Select(match => long.Parse(match.Groups[1].Value))
                .Where(id => staleIds.Any(staleId => staleId == id)).ToArray();
            foreach (var id in stale)
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                    $"Broken text PPtr in file(Assets/QuackUp/Scenes/Gameplay.unity). Local file identifier ({id}) doesn't exist!");
            if (stale.Length > 0)
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                    "Problem detected while opening the Scene file: 'Assets/QuackUp/Scenes/Gameplay.unity'.\nCheck the following logs for more details.");
                foreach (var id in new[] { 1420291200, 527656407 }.Where(staleIds.Contains))
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Transform child can't be loaded");
            }
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ArtRoot + "Scenes/Gameplay.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var scope = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true))
                    .First(c => c && c.GetType().FullName == "FitMe.Scene.SceneLifetimeScope");
                var installers = (System.Collections.IEnumerable)scope.GetType().GetField("installers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scope);
                var installer = installers.Cast<object>().OfType<BlockManagerInstaller>().Single();
                var field = typeof(BlockManagerInstaller).GetField("shapeTrayPanel", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "Gameplay installer needs actual tray bounds");
                var tray = (RectTransform)field.GetValue(installer);
                Assert.That(tray, Is.Not.Null, "Gameplay must serialize the Tray Panel, not leave an optional field unassigned");
                var renderer = tray.GetComponentInChildren<SpriteRenderer>(true);
                Assert.That(renderer, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Does.EndWith("Shape Asset/Tray_Panel.png"));
                var config = (BlockManagerConfig)typeof(BlockManagerInstaller).GetField("blockManagerConfig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(installer);
                // Authored artwork dimensions may change; spawning needs finite,
                // positive world-space bounds after the configured interior padding.
                var corners = new Vector3[4];
                tray.GetWorldCorners(corners);
                var usableSize = new Vector2(
                    (corners[2].x - corners[0].x) * (1f - 2f * config.TrayInset.x),
                    (corners[2].y - corners[0].y) * (1f - 2f * config.TrayInset.y));
                Assert.That(float.IsNaN(usableSize.x) || float.IsInfinity(usableSize.x), Is.False);
                Assert.That(float.IsNaN(usableSize.y) || float.IsInfinity(usableSize.y), Is.False);
                Assert.That(usableSize.x, Is.GreaterThan(0), "Tray must have usable interior width");
                Assert.That(usableSize.y, Is.GreaterThan(0), "Tray must have usable interior height");
                Assert.That(config.TrayTiltMin, Is.EqualTo(-15f));
                Assert.That(config.TrayTiltMax, Is.EqualTo(15f));
                Assert.That(config.TrayInset.x, Is.GreaterThanOrEqualTo(.1f));
                Assert.That(config.TrayInset.y, Is.GreaterThanOrEqualTo(.15f));
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }

        [TestCase(1)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(20)]
        public void ShapeTray_AllAuthoredPiecesSpawnOnceAndRemainSelectableWithoutQueueRefill(int count)
        {
            var config = Keep(UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(ArtRoot + "Settings/Grid/Block/BlockManagerConfig.asset")));
            var gridConfig = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            var rect = (RectTransform)Keep(UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Prefabs/Block/ShapeTrayPanel.prefab"))).transform;
            var atomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Prefabs/Atom/AtomView.prefab").GetComponent<AtomView>();
            var blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "Prefabs/Block/BlockView.prefab").GetComponent<BlockView>();
            var factory = new BlockFactory(blockPrefab, config, gridConfig, new AtomFactory(config, atomPrefab),
                new BlockControllerFactory(config, _manager, null, null, null));
            var hand = CreateSpawnPoint(Keep(new GameObject("Legacy slot")).transform, null, true);
            using var manager = new BlockManager(_manager, config, null, new[] { hand }, factory,
                Array.Empty<BlockManager.SpawnPointData>(), _hub, rect);
            var entries = Enumerable.Range(0, count).Select(i => new ShapePlayerBlockRuntimeEntry(null,
                i % 2 == 0 ? new[,] { { 1, 1 }, { 1, 0 } } : new[,] { { 1, 1, 1 } })).ToArray();
            _hub.Publish(new SpawnWithShapeLevelEvent(new ShapeLevelRuntimeData(null, new[,] { { 1 } }, Array.Empty<ShapeObstacleRuntimeData>(), entries)));
            SetManagerField("<IsGameplay>k__BackingField", true);
            var cellsCreated = (Subject<Unit>)typeof(GridManager).GetField("_onCellsCreated", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_manager);
            cellsCreated.OnNext(Unit.Default);
            try
            {
                Assert.That(manager.BlockOnHand.Count, Is.EqualTo(count), "All authored entries, not only hand slots, must be available");
                Assert.That(manager.BlockOnPreview, Is.Empty);
                Assert.That(manager.CanSwap.CurrentValue, Is.False);
                var pieces = new List<ShapeTrayPiece>();
                foreach (var block in manager.BlockOnHand)
                {
                    _additionalBlockControllers.Add(block.Controller);
                    _additionalBlockViewModels.Add(block.ViewModel);
                    _additionalBlockModels.Add(block.Model);
                    _objects.Add(block.GameObject);
                    var piece = block.Model.TrayPiece;
                    Assert.That(piece, Is.Not.Null);
                    Assert.That(block.Model.SpawnIndex, Is.EqualTo(-1));
                    Assert.That(piece.Tilt, Is.InRange(config.TrayTiltMin, config.TrayTiltMax));
                    Assert.That(Quaternion.Angle(piece.GameplayRotation, Quaternion.identity), Is.LessThan(.01f));
                    Assert.That(block.Controller.AllowRotation, Is.False);
                    Assert.That(Quaternion.Angle(block.GameObject.transform.rotation, piece.TrayRotation), Is.LessThan(.01f));
                    pieces.Add(piece);
                    // Freeze spawn animation at its target for deterministic EditMode
                    // collider/render verification; gameplay still uses ScaleInCommand.
                    block.GameObject.transform.localScale = new Vector3(piece.Scale, piece.Scale, 1);
                }
                Physics2D.SyncTransforms();
                foreach (var block in manager.BlockOnHand)
                    Assert.That(Physics2D.OverlapPointAll(block.Model.Atoms[0].GameObject.transform.position)
                        .Any(hit => hit.transform.IsChildOf(block.GameObject.transform)), Is.True, "Each authored piece must have a live hittable collider");
                if (count == 6) RenderShapeTrayEvidence(rect);
                Assert.That(pieces.Select(p => p.PieceId).Distinct().Count(), Is.EqualTo(count));
                if (count <= 6)
                    for (int i = 0; i < pieces.Count; i++)
                    for (int j = i + 1; j < pieces.Count; j++)
                        Assert.That(pieces[i].PresentationBounds.Overlaps(pieces[j].PresentationBounds), Is.False, "Sparse initial supply must not overlap");
                var survivorPositions = pieces.Skip(1).Select(p => p.Position).ToArray();
                var survivorScales = pieces.Skip(1).Select(p => p.Scale).ToArray();
                pieces[0].MarkPlaced();
                Assert.That(manager.BlockOnHand.Count, Is.EqualTo(count - 1));
                cellsCreated.OnNext(Unit.Default);
                Assert.That(manager.BlockOnHand.Count, Is.EqualTo(count - 1), "Placement must not refill or duplicate");
                Assert.That(pieces.Skip(1).Select(p => p.Position).ToArray(), Is.EqualTo(survivorPositions));
                Assert.That(pieces.Skip(1).Select(p => p.Scale).ToArray(), Is.EqualTo(survivorScales));
            }
            finally
            {
                foreach (var view in rect.GetComponentsInChildren<BlockView>())
                    if (view) UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
