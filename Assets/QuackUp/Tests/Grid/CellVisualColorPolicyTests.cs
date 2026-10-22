using NUnit.Framework;
using UnityEngine;

namespace FitMe.Grid.Tests
{
    public sealed class CellVisualColorPolicyTests
    {
        [Test]
        public void Resolve_HoverTintTakesPrecedenceOverHintFlash()
        {
            var hover = Color.green;
            var result = CellVisualColorPolicy.Resolve(
                Color.white, CellState.CanBePlaced, true, true, hover, Color.red, Color.yellow);

            Assert.That(result, Is.EqualTo(hover));
        }

        [Test]
        public void Resolve_HoverRemainsVisibleWhenHintFlashEnds()
        {
            var hover = Color.red;
            var result = CellVisualColorPolicy.Resolve(
                Color.white, CellState.CannotBePlaced, true, false, Color.green, hover, Color.yellow);

            Assert.That(result, Is.EqualTo(hover));
        }
    }
}
