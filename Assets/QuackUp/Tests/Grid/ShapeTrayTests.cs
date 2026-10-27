using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public class ShapeTrayTests
    {
        [Test]
        public void TrayBlockScaleHasIndependentSerializedPositiveInspectorDefault()
        {
            var config = ScriptableObject.CreateInstance<BlockManagerConfig>();
            try
            {
                var property = typeof(BlockManagerConfig).GetProperty("TrayBlockScale");
                Assert.That(property, Is.Not.Null);
                Assert.That(property.GetValue(config), Is.EqualTo(.5f));
                var field = typeof(BlockManagerConfig).GetField("<TrayBlockScale>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field.GetCustomAttribute<SerializeField>(), Is.Not.Null);
                Assert.That(field.GetCustomAttribute<MinAttribute>().min, Is.GreaterThan(0));
                property.SetValue(config, .8f);
                Assert.That(config.ObjectScale, Is.EqualTo(.5f));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void PieceKeepsLatestClampedTrayPositionAndGameplayRotationUntilPermanentlyPlaced()
        {
            var bounds = new Rect(-2f, -4.5f, 4f, 2f);
            var piece = new ShapeTrayPiece(17, new[,] { { 1, 1, 1 }, { 1, 0, 0 } }, bounds, .3f, 12f, Quaternion.Euler(0, 0, 90), new Vector2(0, -3.5f));
            Assert.That((int)piece.PieceId, Is.EqualTo(17));
            piece.DropInTray(new Vector2(50, 50));
            Vector2 saved = piece.Position;
            Rect footprint = piece.PresentationBounds;
            Assert.That(bounds.Contains(footprint.min), Is.True);
            Assert.That(footprint.xMax, Is.LessThanOrEqualTo(bounds.xMax + .0001f));
            Assert.That(footprint.yMax, Is.LessThanOrEqualTo(bounds.yMax + .0001f));
            piece.Rotate(-90f);
            Assert.That(Quaternion.Angle((Quaternion)piece.GameplayRotation, Quaternion.identity), Is.LessThan(.001f));
            Assert.That((float)piece.Tilt, Is.EqualTo(12f));
            // A turn may move the pivot inward to keep the whole new footprint
            // inside the tray; that clamped position becomes the return point.
            Assert.That(piece.PresentationBounds.xMin, Is.GreaterThanOrEqualTo(bounds.xMin - .0001f));
            Assert.That(piece.PresentationBounds.xMax, Is.LessThanOrEqualTo(bounds.xMax + .0001f));
            Assert.That(piece.PresentationBounds.yMin, Is.GreaterThanOrEqualTo(bounds.yMin - .0001f));
            Assert.That(piece.PresentationBounds.yMax, Is.LessThanOrEqualTo(bounds.yMax + .0001f));
            saved = piece.Position;
            piece.MarkPlaced();
            Assert.That((bool)piece.IsPlaced, Is.True);
            piece.DropInTray(Vector2.zero);
            Assert.That((Vector2)piece.Position, Is.EqualTo(saved));
        }

        [Test]
        public void TrayTiltDefaultsAreInspectorConfigurableMinus15To15()
        {
            var config = ScriptableObject.CreateInstance<BlockManagerConfig>();
            try
            {
                var min = typeof(BlockManagerConfig).GetProperty("TrayTiltMin");
                var max = typeof(BlockManagerConfig).GetProperty("TrayTiltMax");
                Assert.That(min, Is.Not.Null, "Shape tray must expose minimum visual tilt");
                Assert.That(max, Is.Not.Null, "Shape tray must expose maximum visual tilt");
                Assert.That(min.GetValue(config), Is.EqualTo(-15f));
                Assert.That(max.GetValue(config), Is.EqualTo(15f));
                foreach (var name in new[] { "TrayTiltMin", "TrayTiltMax" })
                    Assert.That(typeof(BlockManagerConfig).GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetCustomAttribute<SerializeField>(), Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
