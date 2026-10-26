using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FitMe.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FitMe.Grid.Tests
{
    public class ShapeAtomColorRenderTests
    {
        [TestCase(BlockColor.Yellow)]
        [TestCase(BlockColor.Green)]
        [TestCase(BlockColor.Purple)]
        [TestCase(BlockColor.Blue)]
        public void RuntimeOutline_PreservesThePaletteAtEveryAtomCenter(BlockColor color)
        {
            const string root = "Assets/QuackUp/";
            var owned = new List<Object>();
            T Keep<T>(T value) where T : Object { owned.Add(value); return value; }
            BlockInstance block = null;
            RenderTexture target = null;
            var previous = RenderTexture.active;
            try
            {
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
                var config = Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<BlockManagerConfig>(root + "Settings/Grid/Block/BlockManagerConfig.asset")));
                var grid = Keep(ScriptableObject.CreateInstance<GridManagerConfig>());
                var parent = Keep(new GameObject("Shape colour regression"));
                var factory = new BlockFactory(AssetDatabase.LoadAssetAtPath<GameObject>(root + "Prefabs/Block/BlockView.prefab").GetComponent<BlockView>(), config, grid,
                    new AtomFactory(config, AssetDatabase.LoadAssetAtPath<GameObject>(root + "Prefabs/Atom/AtomView.prefab").GetComponent<AtomView>()),
                    new BlockControllerFactory(config, null, null, null, null));
                block = factory.Create(BlockShape.OneByOne, Vector3.zero, Quaternion.identity,
                    new InstantiateParameters { parent = parent.transform, worldSpace = true }, new[,] { { 1, 1 }, { 1, 0 } }, blockConfigOverride: config.ArbitraryBlockConfig);
                block.Model.ChangeColor(color, false);
                foreach (var atom in block.Model.Atoms)
                {
                    // EditMode does not execute this MonoBehaviour's runtime Awake.
                    // Exercise the actual prefab's outline colour/width, not a normalized test palette.
                    var outline = atom.GameObject.GetComponent<SpriteOutlineController>();
                    typeof(SpriteOutlineController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(outline, null);
                    int x = atom.Model.ArrayIndex.Value.x, y = atom.Model.ArrayIndex.Value.y;
                    bool Exists(int row, int column) => row >= 0 && row < 2 && column >= 0 && column < 2 && !(row == 1 && column == 1);
                    atom.ViewModel.SetOutlineCommand.Execute(new SpriteOutlineSettings
                    {
                        OutlineTop = !Exists(x - 1, y), OutlineBottom = !Exists(x + 1, y),
                        OutlineLeft = !Exists(x, y - 1), OutlineRight = !Exists(x, y + 1)
                    });
                }
                foreach (var child in parent.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                var camera = Keep(new GameObject("Shape colour GPU camera")).AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 1.4f; camera.aspect = 1;
                camera.transform.position = Vector3.back * 10; camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
                target = Keep(new RenderTexture(560, 560, 24));
                var image = Keep(new Texture2D(560, 560, TextureFormat.RGBA32, false));
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 560, 560), 0, 0); image.Apply();
                var path = Path.GetFullPath("../shape-atom-runtime-" + color + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG()); TestContext.Out.WriteLine(path);
                var expected = config.AtomColorDict[color];
                foreach (var atom in block.Model.Atoms)
                {
                    var point = camera.WorldToViewportPoint(atom.GameObject.transform.position);
                    var actual = image.GetPixel(Mathf.RoundToInt(point.x * 560), Mathf.RoundToInt(point.y * 560));
                    Assert.That(actual.r, Is.EqualTo(expected.r).Within(.025f), "Atom " + atom.Model.ArrayIndex.Value + " red channel is overwritten by outline");
                    Assert.That(actual.g, Is.EqualTo(expected.g).Within(.025f));
                    Assert.That(actual.b, Is.EqualTo(expected.b).Within(.025f));
                }
                camera.targetTexture = null;
            }
            finally
            {
                RenderTexture.active = previous;
                block?.Controller.Dispose(); block?.ViewModel.Dispose();
                if (target) target.Release();
                for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]);
            }
        }
    }
}
