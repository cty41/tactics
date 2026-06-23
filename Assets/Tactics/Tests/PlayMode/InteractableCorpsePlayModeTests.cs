using System.Collections;
using System.Linq;
using NUnit.Framework;
using Tactics.Common.Cells;
using Tactics.Common.Interactables;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class InteractableCorpsePlayModeTests
    {
        private GameObject _cellManagerRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            _cellManagerRoot = new GameObject("TestCellManager");
            var cellMgr = _cellManagerRoot.AddComponent<RegularCellManager>();
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    var cellGo = new GameObject($"Cell_{x}_{y}");
                    cellGo.transform.SetParent(_cellManagerRoot.transform);
                    var square = cellGo.AddComponent<Square>();
                    square.GridCoordinates = new Vector2IntImpl(x, y);
                    square.WorldPosition = new Vector3Impl(x, y, 0);
                    square.MovementCost = 1f;
                }
            }

            cellMgr.Initialize(null);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_cellManagerRoot != null)
            {
                Object.DestroyImmediate(_cellManagerRoot);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Corpse_OccupiesCell_CanBeConsumed()
        {
            var cell = FindCell(0, 0);
            Assert.IsNotNull(cell, "Cell should exist.");

            var corpseGo = new GameObject("TestCorpse");
            var corpse = corpseGo.AddComponent<Corpse>();
            cell.AddInteractable(corpse);

            Assert.IsTrue(cell.IsTaken, "Cell should be occupied by corpse.");
            Assert.IsTrue(cell.CurrentInteractables.Any(i => i is Corpse), "Cell should have a Corpse interactable.");
            Assert.IsTrue(corpse.OccupiesCell, "Corpse should occupy cell.");

            corpse.Consume();

            Assert.IsFalse(cell.CurrentInteractables.Any(i => i is Corpse), "Corpse should be consumed.");
            Assert.IsFalse(cell.IsTaken, "Cell should be free after corpse is consumed.");
            yield break;
        }

        [UnityTest]
        public IEnumerator Corpse_MultipleOnSameCell_AllTracked()
        {
            var cell = FindCell(1, 0);
            Assert.IsNotNull(cell, "Cell should exist.");

            var corpseGo1 = new GameObject("TestCorpse1");
            var corpse1 = corpseGo1.AddComponent<Corpse>();
            var corpseGo2 = new GameObject("TestCorpse2");
            var corpse2 = corpseGo2.AddComponent<Corpse>();
            cell.AddInteractable(corpse1);
            cell.AddInteractable(corpse2);

            Assert.IsTrue(cell.CurrentInteractables.Count(i => i is Corpse) == 2, "Both corpses should be tracked.");
            Assert.IsTrue(cell.IsTaken, "Cell should be occupied.");

            corpse1.Consume();

            Assert.IsTrue(cell.CurrentInteractables.Count(i => i is Corpse) == 1, "One corpse should remain.");
            Assert.IsTrue(cell.IsTaken, "Cell should still be occupied.");
            yield break;
        }

        private ICell FindCell(int x, int y)
        {
            return _cellManagerRoot.GetComponentsInChildren<Square>()
                .FirstOrDefault(c => c.GridCoordinates.x == x && c.GridCoordinates.y == y);
        }
    }
}
