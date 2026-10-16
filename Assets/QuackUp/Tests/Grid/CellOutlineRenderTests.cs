using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Utils;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FitMe.Grid.Tests
{
    public class CellOutlineRenderTests
    {
        const string Root = "Assets/QuackUp/";
        readonly List<Object> objects = new();
        readonly List<IDisposable> disposables = new();
        T Keep<T>(T item) where T : Object { objects.Add(item); return item; }
        T Own<T>(T item) where T : IDisposable { disposables.Add(item); return item; }

        [TearDown] public void Cleanup()
        {
            foreach (var item in disposables) item.Dispose();
            disposables.Clear();
            foreach (var item in objects) if (item) Object.DestroyImmediate(item);
            objects.Clear();
        }

        CellView Cell(Vector2 position, out CellModel model, bool checkAdjacent = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Cell/CellView.prefab");
            var go = Keep(Object.Instantiate(prefab));
            go.transform.position = position;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            var view = go.GetComponent<CellView>();
            model = Own(new CellModel());
            var vm = Own(new CellViewModel(model));
            var config = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<CellConfig>(Root + "Settings/Grid/CellConfig.asset")));
            PrepareConfig(config);
            ConfigValue(config, "CheckAdjacentCellsForOutline", checkAdjacent);
            view.Construct(config, vm);
            return view;
        }

        static void ConfigValue(CellConfig config, string name, object value)
        {
            var field = typeof(CellConfig).GetField("<" + name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing configurable floor outline setting: " + name);
            field.SetValue(config, value);
        }

        static void PrepareConfig(CellConfig config)
        {
            // Isolate pixel classification from the user's selected outline colour.
            ConfigValue(config, "BaseColor", Color.red);
            // The current art selection has one pattern; duplicate only in this test clone
            // because the legacy checkerboard selector indexes patterns [0] and [1].
            foreach (var name in new[] { "WhitePatterns", "BlackPatterns" })
            {
                var patterns = (Sprite[])typeof(CellConfig).GetProperty(name).GetValue(config);
                if (patterns.Length == 1) ConfigValue(config, name, new[] { patterns[0], patterns[0] });
            }
        }

        static void Scallop(CellView[,] views, float depth, float width)
        {
            foreach (var view in views)
            {
                if (!view) continue;
                var config = (CellConfig)typeof(CellView).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                ConfigValue(config, "BaseSeamInsetDepth", depth);
                ConfigValue(config, "BaseSeamInsetWidth", width);
                view.SetBaseScale(config.BaseScale);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdjacentRim_CheckedModeStaysSmoothDespiteLegacySeamSettings(bool vertical)
        {
            Board(vertical ? new[,] {{1},{1}} : new[,] {{1,1}}, out var views, out _);
            var b = views[0,0].SpriteRenderer.bounds;
            var unit = b.size.x;
            var center = vertical ? new Vector2(0, -b.size.y * .5f) : new Vector2(unit * .5f, 0);
            var size = unit * 1.25f;
            Scallop(views, 0, .16f);
            var before = Render(center, size, vertical ? "scallop-vertical-zero" : "scallop-horizontal-zero");
            Scallop(views, .025f, .16f);
            var after = Render(center, size, vertical ? "scallop-vertical-inset" : "scallop-horizontal-inset");
            var normal = vertical ? Vector2.right : Vector2.up;
            var tangent = vertical ? Vector2.up : Vector2.right;
            foreach (var sign in new[] {-1, 1})
            {
                var join = center + normal * sign * unit * .5f;
                var changed = 0;
                for (var i = 2; i <= 18; i++)
                {
                    var point = join + normal * sign * unit * (i * .005f);
                    if (At(before, point, center, size).r - At(after, point, center, size).r > .4f) changed++;
                }
                Assert.That(changed, Is.Zero, "Checked mode must keep a smooth straight row rim, ignoring legacy seam settings");
                // Not a gap through the ring, nor a deformation of the rest of the rim.
                Assert.That(Red(At(after, join + normal * sign * unit * .025f, center, size)), Is.True);
                for (var i = 1; i <= 15; i++)
                {
                    var away = join + tangent * unit * .3f + normal * sign * unit * i * .005f;
                    Assert.That(At(after, away, center, size).r, Is.EqualTo(At(before, away, center, size).r).Within(.02f), "Rim away from join changed");
                }
            }
            for (var i = -35; i <= 35; i++)
                Assert.That(Red(At(after, center + normal * unit * i * .01f, center, size)), Is.False, "Internal shared outline");
            Assert.That(after.GetRawTextureData<byte>().ToArray(), Is.EqualTo(before.GetRawTextureData<byte>().ToArray()),
                "Nonzero legacy seam depth must not deform the checked-mode contour");
            Scallop(views, 0, .16f);
            var restored = Render(center, size, vertical ? "scallop-vertical-restored" : "scallop-horizontal-restored");
            Assert.That(restored.GetRawTextureData<byte>().ToArray(), Is.EqualTo(before.GetRawTextureData<byte>().ToArray()), "Zero depth must exactly restore smooth rim");
            Scallop(views, .025f, 0);
            var zeroWidth = Render(center, size, vertical ? "scallop-vertical-zero-width" : "scallop-horizontal-zero-width");
            Assert.That(zeroWidth.GetRawTextureData<byte>().ToArray(), Is.EqualTo(before.GetRawTextureData<byte>().ToArray()), "Zero width must disable scallops");
            // Additional evidence in the user's actual outline colour; diagnostic images
            // above intentionally use red to make unwanted shared-edge pixels detectable.
            var artColor = AssetDatabase.LoadAssetAtPath<CellConfig>(Root + "Settings/Grid/CellConfig.asset").BaseColor;
            foreach (var view in views) if (view) view.SetBaseColor(artColor);
            Scallop(views, 0, .16f);
            Render(center, size, vertical ? "scallop-art-vertical-before" : "scallop-art-horizontal-before");
            Scallop(views, .025f, .16f);
            Render(center, size, vertical ? "scallop-art-vertical-after" : "scallop-art-horizontal-after");
        }

        [Test] public void DiagonalOnlyCells_ScallopsDoNotChangeContour()
        {
            Board(new[,] {{1,0},{0,1}}, out var views, out _);
            var b = views[0,0].SpriteRenderer.bounds;
            var center = new Vector2(b.size.x * .5f, -b.size.y * .5f);
            var size = b.size.x * 1.25f;
            Scallop(views, 0, .16f);
            var before = Render(center, size, "diagonal-zero");
            Scallop(views, .025f, .16f);
            var after = Render(center, size, "diagonal-inset");
            Assert.That(after.GetRawTextureData<byte>().ToArray(), Is.EqualTo(before.GetRawTextureData<byte>().ToArray()), "Diagonal contact must not count as a straight-rim neighbour");
        }

        Texture2D Render(Vector2 center, float size, string name)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "GPU required");
            var camera = Keep(new GameObject("Outline evidence camera")).AddComponent<Camera>();
            camera.transform.position = new Vector3(center.x, center.y, -10);
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 31;
            var rt = Keep(new RenderTexture(512, 512, 24));
            camera.targetTexture = rt;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = Keep(new Texture2D(512, 512, TextureFormat.RGBA32, false));
            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "OutlineEvidence");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Debug.Log("Outline render evidence: " + Path.Combine(folder, name + ".png") + " GPU=" + SystemInfo.graphicsDeviceType);
            return image;
        }

        static bool Red(Color c) => c.r > .5f && c.g < .3f && c.b < .4f;
        static Color At(Texture2D image, Vector2 world, Vector2 center, float size)
        {
            var p = (world - center) / (2 * size) + Vector2.one * .5f;
            return image.GetPixel(Mathf.RoundToInt(p.x * 511), Mathf.RoundToInt(p.y * 511));
        }

        GridManager Board(int[,] shape, out CellView[,] views, out CellModel[,] models, bool checkAdjacent = true)
        {
            views = new CellView[shape.GetLength(0), shape.GetLength(1)];
            models = new CellModel[shape.GetLength(0), shape.GetLength(1)];
            var cells = new CellInstance[shape.GetLength(0), shape.GetLength(1)];
            float width = 0, height = 0;
            for (var r = 0; r < shape.GetLength(0); r++)
            for (var c = 0; c < shape.GetLength(1); c++)
            {
                if (shape[r,c] == 0) continue;
                var view = Cell(Vector2.zero, out var model, checkAdjacent);
                if (width == 0) { width = view.SpriteRenderer.bounds.size.x; height = view.SpriteRenderer.bounds.size.y; }
                view.transform.position = new Vector2(c * width, -r * height);
                model.ArrayIndex.Value = new Vector2Int(r,c);
                views[r,c] = view;
                models[r,c] = model;
                cells[r,c] = new CellInstance(model, null, view.gameObject);
            }
            var grid = Keep(new GameObject("Floor grid")).AddComponent<UnityEngine.Grid>();
            var config = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            var manager = Own(new GridManager(grid, config, null, null, new Hub()));
            typeof(GridManager).GetField("_cellArray", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, cells);
            var method = typeof(GridManager).GetMethod("RefreshCellOutlines", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Board cell adjacency path must exist independently of occupied atoms.");
            method.Invoke(manager, null);
            return manager;
        }

        sealed class Hub : IMessageHub
        {
            public void Publish<T>(T message) { }
            public IDisposable Subscribe<T>(Action<T> action) => Disposable.Create(() => { });
            public Observable<T> GetObservable<T>() => Observable.Empty<T>();
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void AdjacencyToggle_PreservesEveryOpaqueFillWithOutlinesBehindAllCells(bool checkAdjacent, bool vertical)
        {
            Board(vertical ? new[,] {{1},{1}} : new[,] {{1,1}}, out var views, out var models, checkAdjacent);
            var b = views[0,0].SpriteRenderer.bounds;
            var normal = vertical ? Vector2.up : Vector2.right;
            var tangent = vertical ? Vector2.right : Vector2.up;
            var center = vertical ? new Vector2(0, -b.size.y * .5f) : new Vector2(b.size.x * .5f, 0);
            var size = b.size.x * 1.25f;
            var name = "adjacency-" + (checkAdjacent ? "on" : "off") + (vertical ? "-vertical" : "-horizontal");
            var image = Render(center, size, name);
            foreach (var view in views) view.BaseRenderer.enabled = false;
            var fillsOnly = Render(center, size, name + "-fills-only");
            foreach (var view in views) view.BaseRenderer.enabled = true;
            // Also save the exact user artwork/colour, not just diagnostic red.
            var artColor = AssetDatabase.LoadAssetAtPath<CellConfig>(Root + "Settings/Grid/CellConfig.asset").BaseColor;
            foreach (var view in views) view.SetBaseColor(artColor);
            Render(center, size, name + "-art");
            foreach (var sign in new[] {-1, 1})
            for (var i = -3; i <= 3; i++)
            {
                var point = center + normal * sign * b.size.x * .035f + tangent * i * b.size.x * .1f;
                Assert.That(At(image, point, center, size), Is.EqualTo(At(fillsOnly, point, center, size)),
                    "Every opaque neighbour fill must remain unchanged; hidden shared outlines must never overlay it");
            }
            foreach (var view in views)
            {
                Assert.That(view.BaseRenderer.sortingLayerID, Is.EqualTo(view.SpriteRenderer.sortingLayerID));
                foreach (var other in views)
                    Assert.That(view.BaseRenderer.sortingOrder, Is.LessThan(other.SpriteRenderer.sortingOrder));
            }
            Assert.That(models[0,0].CurrentAtom.Value, Is.Null);
            Assert.That(Red(At(image, new Vector2(b.min.x - b.size.x * .035f, b.center.y), center, size)), Is.True);
        }

        [Test] public void AdjacencyToggle_DefaultPreservesBoardSilhouette()
        {
            var fresh = Keep(ScriptableObject.CreateInstance<CellConfig>());
            Assert.That(fresh.CheckAdjacentCellsForOutline, Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<CellConfig>(Root + "Settings/Grid/CellConfig.asset").CheckAdjacentCellsForOutline, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdjacencyToggle_FourCellJunctionUsesIndividualOrBoardCorners(bool checkAdjacent)
        {
            Board(new[,] {{1,1},{1,1}}, out var views, out _, checkAdjacent);
            var b = views[0,0].SpriteRenderer.bounds;
            var center = new Vector2(b.size.x * .5f, -b.size.y * .5f);
            var size = b.size.x * 1.4f;
            var name = "adjacency-" + (checkAdjacent ? "on" : "off") + "-four-cells";
            var image = Render(center, size, name);
            foreach (var x in new[] {-1,1})
            foreach (var y in new[] {-1,1})
            {
                var point = center + new Vector2(x,y) * b.size.x * .035f;
                Assert.That(Red(At(image, point, center, size)), Is.EqualTo(!checkAdjacent),
                    "Individual convex corners must not be clipped by existing diagonal cells");
            }
            var artColor = AssetDatabase.LoadAssetAtPath<CellConfig>(Root + "Settings/Grid/CellConfig.asset").BaseColor;
            foreach (var view in views) view.SetBaseColor(artColor);
            Render(center, size, name + "-art");
            if (!checkAdjacent)
            {
                foreach (var view in views) view.SetBaseColor(Color.red);
                Scallop(views, 0, .16f);
                var smooth = Render(center, size, name + "-zero-inset");
                Assert.That(smooth.GetRawTextureData<byte>().ToArray(), Is.EqualTo(image.GetRawTextureData<byte>().ToArray()),
                    "Ignoring neighbours must disable adjacency scallops entirely");
            }
        }

        [Test] public void EmptyAdjacentFloorCells_HaveNoSharedBaseOutline()
        {
            Board(new[,] {{1,1}}, out var views, out var models);
            Assert.That(models[0,0].CurrentAtom.Value, Is.Null);
            Assert.That(models[0,0].BaseOutlineEdges.Value, Is.EqualTo(new Vector4(1,0,1,1)));
            Assert.That(models[0,1].BaseOutlineEdges.Value, Is.EqualTo(new Vector4(0,1,1,1)));
            var b = views[0,0].SpriteRenderer.bounds;
            var center = new Vector2(b.center.x + b.size.x * .5f, b.center.y);
            var size = b.size.x * 1.25f;
            var image = Render(center, size, "two-empty-cells");
            for (int i = -40; i <= 40; i++)
                Assert.That(Red(At(image, center + new Vector2(0, i * b.size.y / 100), center, size)), Is.False, "Red shared seam");
            Assert.That(Red(At(image, new Vector2(b.min.x - b.size.x * .035f, b.center.y), center, size)), Is.True);
        }

        [Test] public void IrregularEmptyFloor_ExposesNotchButNotInternalCorners()
        {
            Board(new[,] {{1,1,0},{1,1,1}}, out var views, out var models);
            Assert.That(models[0,1].BaseOutlineEdges.Value, Is.EqualTo(new Vector4(0,1,1,0)));
            Assert.That(models[1,1].BaseOutlineEdges.Value, Is.EqualTo(new Vector4(0,0,0,1)));
            var b = views[0,0].SpriteRenderer.bounds;
            var center = new Vector2(b.size.x, -b.size.y * .5f);
            var size = b.size.x * 1.9f;
            var image = Render(center, size, "irregular-empty-grid");
            var junction = new Vector2(b.max.x, b.min.y);
            for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
                Assert.That(Red(At(image, junction + new Vector2(x,y) * b.size.x * .01f, center, size)), Is.False, "Red internal corner artifact");
            var topJoin = new Vector2(b.max.x, b.max.y + b.size.y * .035f);
            for (var x = -2; x <= 2; x++)
                Assert.That(At(image, topJoin + Vector2.right * x * b.size.x * .005f, center, size).r,
                    Is.GreaterThan(.9f), "Outer silhouette must not have a dark gap at joined cell corners");
            var notch = new Vector2(b.center.x + 2*b.size.x, b.min.y + .035f*b.size.y);
            Assert.That(Red(At(image, notch, center, size)), Is.True, "Missing outline inside notch");
        }

        [Test] public void CreateCells_UsesRealFactoryAndRefreshesBeforeNotification()
        {
            var grid = Keep(new GameObject("Factory grid")).AddComponent<UnityEngine.Grid>();
            var config = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
            var cellConfig = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<CellConfig>(Root + "Settings/Grid/CellConfig.asset")));
            PrepareConfig(cellConfig);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Cell/CellView.prefab").GetComponent<CellView>();
            var factory = new CellFactory(cellConfig, prefab, grid.transform);
            var manager = Own(new GridManager(grid, config, factory, null, new Hub()));
            var preset = Keep(ScriptableObject.CreateInstance<GridPreset>());
            preset.GridSize = new Vector2Int(3,2);
            preset.PresetGridType = GridType.Custom;
            preset.customGrid = new[,] {{1,1,0},{1,1,1}};
            typeof(GridManager).GetField("<CurrentGridPreset>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, preset);
            var notified = false;
            Own(manager.OnCellsCreated.Subscribe(_ =>
            {
                notified = true;
                var cells = (CellInstance[,])typeof(GridManager).GetField("_cellArray", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
                Assert.That(cells[0,1].Model.BaseOutlineEdges.Value, Is.EqualTo(new Vector4(0,1,1,0)));
            }));
            typeof(GridManager).GetMethod("CreateCells", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager,null);
            Assert.That(notified, Is.True);
            var created = (CellInstance[,])typeof(GridManager).GetField("_cellArray", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
            var bounds = new Bounds();
            var first = true;
            foreach (var cell in created)
            {
                if (cell == null) continue;
                Own(cell.Model); Own(cell.ViewModel);
                foreach (var t in cell.GameObject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                var view = cell.GameObject.GetComponent<CellView>();
                Assert.That(view.BaseRenderer.sortingOrder, Is.LessThan(view.SpriteRenderer.sortingOrder));
                Assert.That(view.BaseRenderer.sortingLayerID, Is.EqualTo(view.SpriteRenderer.sortingLayerID));
                if (first) { bounds = view.SpriteRenderer.bounds; first = false; }
                else bounds.Encapsulate(view.SpriteRenderer.bounds);
            }
            var renderSize = Mathf.Max(bounds.size.x,bounds.size.y) * .65f;
            var image = Render(bounds.center, renderSize, "factory-created-grid");
            var firstBounds = created[0,0].GameObject.GetComponent<CellView>().SpriteRenderer.bounds;
            Assert.That(Red(At(image, new Vector2(firstBounds.min.x - firstBounds.size.x * .035f,
                firstBounds.center.y), bounds.center, renderSize)), Is.True, "Factory path must render outside cell bounds");
            Assert.That(Red(At(image, new Vector2(firstBounds.max.x, firstBounds.min.y), bounds.center, renderSize)),
                Is.False, "Factory path must not render an internal corner outline");
        }

        [Test] public void HoleInBoard_HasExposedInnerBoundary()
        {
            Board(new[,] {{1,1,1},{1,0,1},{1,1,1}}, out var views, out var models);
            Assert.That(models[0,1].BaseOutlineEdges.Value.w, Is.EqualTo(1));
            Assert.That(models[1,0].BaseOutlineEdges.Value.y, Is.EqualTo(1));
            var b = views[0,0].SpriteRenderer.bounds;
            var center = new Vector2(b.size.x, -b.size.y);
            var size = b.size.x * 1.9f;
            var image = Render(center, size, "grid-with-hole");
            Assert.That(Red(At(image, center, center, size)), Is.False);
            Assert.That(Red(At(image, center + Vector2.up * b.size.y * .465f, center, size)), Is.True);
            Assert.That(Red(At(image, center + Vector2.left * b.size.x * .465f, center, size)), Is.True);
            foreach (var x in new[] {-1,1})
            foreach (var y in new[] {-1,1})
            foreach (var offset in new[] { new Vector2(.53f,.48f), new Vector2(.48f,.53f), new Vector2(.53f,.53f) })
                Assert.That(Red(At(image, center + new Vector2(x * b.size.x * offset.x, y * b.size.y * offset.y), center, size)),
                    Is.False, "Concave joins must not bleed red into an occupied diagonal cell's transparent corner");
        }

        [Test] public void RealCellPrefab_DrawsOutlineOutsideOriginalSpriteBounds()
        {
            var view = Cell(Vector2.zero, out _);
            var bounds = view.SpriteRenderer.bounds;
            var size = Mathf.Max(bounds.size.x, bounds.size.y) * .8f;
            var image = Render(bounds.center, size, "single-cell");
            var point = new Vector2(bounds.max.x + bounds.size.x * .035f, bounds.center.y);
            Assert.That(Red(At(image, point, bounds.center, size)), Is.True,
                "The actual CellView must draw the red base OUTSIDE the original sprite bounds.");
        }
    }
}
