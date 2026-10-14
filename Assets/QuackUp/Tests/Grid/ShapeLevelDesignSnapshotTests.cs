using System.Collections.Generic;
using System.Reflection;
using FitMe.GameData;
using FitMe.GameData.Editor;
using FitMe.Grid;
using NUnit.Framework;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public sealed class ShapeLevelDesignSnapshotTests
    {
        private readonly List<ScriptableObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
                if (createdObject) Object.DestroyImmediate(createdObject);
            _createdObjects.Clear();
        }

        [Test]
        public void ManualBoardObstacleWithoutCellDoesNotReducePlayableMetrics()
        {
            var board = CreateObject<GridPreset>();
            board.PresetGridType = GridType.Rectangle;
            board.GridSize = new Vector2Int(2, 1);
            SetProperty(board, nameof(GridPreset.ObstacleMode), ObstacleMode.Custom);
            board.CustomObstacleData = new ObstacleData[1, 2];
            board.CustomObstacleData[0, 0] = new ObstacleData
            {
                hasCell = false,
                hasObstacle = true
            };

            var preset = CreateObject<BlockPreset>();
            SetProperty(preset, nameof(BlockPreset.BlockSchema), new BlockSchema(new[,] { { 1 } }, 0));

            var level = CreateObject<ShapeLevelDefinition>();
            SetPrivateField(level, "_board", board);
            SetPrivateField(level, "_playerBlocks", new List<ShapePlayerBlockEntry>
            {
                new(preset, 2)
            });

            var snapshot = ShapeLevelDesignSnapshot.Create(level);

            Assert.That(snapshot.BoardCellCount, Is.EqualTo(2));
            Assert.That(snapshot.ObstacleCellCount, Is.Zero);
            Assert.That(snapshot.PlayableCellCount, Is.EqualTo(2));
            Assert.That(snapshot.CreatePlayableMask(), Is.EqualTo(new[,] { { 1, 1 } }));
            Assert.That(snapshot.Errors, Does.Contain(ShapeLevelValidationError.ObstacleOnVoidCell));
        }

        private T CreateObject<T>() where T : ScriptableObject
        {
            var createdObject = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            var setter = target.GetType().GetProperty(propertyName)
                .GetSetMethod(nonPublic: true);
            Assert.That(setter, Is.Not.Null, $"Expected a setter for {propertyName}.");
            setter.Invoke(target, new[] { value });
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected private field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
