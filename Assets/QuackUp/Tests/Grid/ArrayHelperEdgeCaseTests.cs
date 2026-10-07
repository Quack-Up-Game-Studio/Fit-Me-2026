using System.Collections.Generic;
using FitMe.Grid;
using NUnit.Framework;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public sealed class ArrayHelperEdgeCaseTests
    {
        [Test]
        public void IsTheSameShape_EmptyShapesAreEqual()
        {
            Assert.That(ArrayHelper.IsTheSameShape(
                new List<Vector2Int>(), new List<Vector2Int>()), Is.True);
        }

        [Test]
        public void IsTheSameShape_NullShapesAreEqualOnlyTogether()
        {
            Assert.That(ArrayHelper.IsTheSameShape(null, null), Is.True);
            Assert.That(ArrayHelper.IsTheSameShape(null, new List<Vector2Int>()), Is.False);
        }

        [Test]
        public void GenerateWindow_NegativeOriginTreatsOutsideCellsAsEmpty()
        {
            var source = new[,] { { 1, 2 }, { 3, 4 } };

            var window = ArrayHelper.GenerateWindow(
                source, new Vector2Int(-1, -1), new Vector2Int(2, 2));

            Assert.That(window[0, 0], Is.EqualTo(0));
            Assert.That(window[0, 1], Is.EqualTo(0));
            Assert.That(window[1, 0], Is.EqualTo(0));
            Assert.That(window[1, 1], Is.EqualTo(1));
        }
    }
}
