using NUnit.Framework;
using Tactics.Common.Battle;
using UnityEngine;

namespace Tactics.Tests.Editor
{
    public sealed class BattleBoardSpecTests
    {
        [Test]
        public void DimensionsAndBounds_AreFixedToTenByTen()
        {
            Assert.That(BattleBoardSpec.Width, Is.EqualTo(10));
            Assert.That(BattleBoardSpec.Height, Is.EqualTo(10));
            Assert.That(BattleBoardSpec.CellCount, Is.EqualTo(100));
            Assert.That(BattleBoardSpec.Minimum, Is.EqualTo(Vector2Int.zero));
            Assert.That(BattleBoardSpec.Maximum, Is.EqualTo(new Vector2Int(9, 9)));
        }

        [TestCase(0, 0)]
        [TestCase(9, 9)]
        public void Contains_ReturnsTrue_ForBoundaryCells(int x, int y)
        {
            Assert.That(BattleBoardSpec.Contains(x, y), Is.True);
            Assert.That(BattleBoardSpec.Contains(new Vector2Int(x, y)), Is.True);
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        [TestCase(10, 9)]
        [TestCase(9, 10)]
        public void Contains_ReturnsFalse_ForCellsOutsideBounds(int x, int y)
        {
            Assert.That(BattleBoardSpec.Contains(x, y), Is.False);
            Assert.That(BattleBoardSpec.Contains(new Vector2Int(x, y)), Is.False);
        }
    }
}
