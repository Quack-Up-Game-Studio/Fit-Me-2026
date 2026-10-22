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


    }
}
