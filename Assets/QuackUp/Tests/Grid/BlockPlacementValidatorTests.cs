using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public class BlockPlacementValidatorTests
    {
        [Test]
        public void TryValidate_AcceptsLegalFootprintAndPreservesResolvedOrder()
        {
            var target = new[] { new Vector2Int(0, 1), new Vector2Int(1, 1) };
            var valid = BlockPlacementValidator.TryValidate(2, 2, new[,] { { 1, 1 }, { 1, 1 } }, target, _ => false, out var resolved);

            Assert.That(valid, Is.True);
            Assert.That(resolved, Is.EqualTo(target));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 2)]
        public void TryValidate_RejectsFootprintOutsideBoard(int row, int column)
        {
            var valid = BlockPlacementValidator.TryValidate(2, 2, null, new[] { new Vector2Int(row, column) }, _ => false, out var resolved);

            Assert.That(valid, Is.False);
            Assert.That(resolved, Is.Empty);
        }

        [Test]
        public void TryValidate_RejectsStageHole()
        {
            var valid = BlockPlacementValidator.TryValidate(1, 2, new[,] { { 1, 0 } }, new[] { Vector2Int.right }, _ => false, out _);

            Assert.That(valid, Is.False);
        }

        [Test]
        public void TryValidate_RejectsOccupiedCell()
        {
            var valid = BlockPlacementValidator.TryValidate(1, 2, null, new[] { Vector2Int.right }, index => index == Vector2Int.right, out _);

            Assert.That(valid, Is.False);
        }

        [Test]
        public void TryValidate_RejectsDuplicateCoordinate()
        {
            var duplicate = new[] { Vector2Int.zero, Vector2Int.zero };
            var valid = BlockPlacementValidator.TryValidate(1, 1, null, duplicate, _ => false, out _);

            Assert.That(valid, Is.False);
        }

        [Test]
        public void TryValidate_AcceptsRotatedNonSquareFootprintWhenWithinBoard()
        {
            // Original 2x1 horizontal footprint rotated clockwise around its anchor.
            var original = new[,] { { 1, 1 } };
            var rotatedSchema = FitMe.Grid.ArrayHelper.Rotate90(original);
            var rotated = new List<Vector2Int>();
            for (var row = 0; row < rotatedSchema.GetLength(0); row++)
            for (var column = 0; column < rotatedSchema.GetLength(1); column++)
            {
                if (rotatedSchema[row, column] != 0)
                    rotated.Add(new Vector2Int(row, column));
            }

            var valid = BlockPlacementValidator.TryValidate(2, 2, null, rotated, _ => false, out var resolved);

            Assert.That(valid, Is.True);
            Assert.That(resolved, Is.EqualTo(rotated));
            Assert.That(rotated, Is.EqualTo(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }));
        }
    }
}
