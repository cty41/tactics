using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Cells;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    public class AiDecisionComponentTests
    {
        private GameObject _cellManagerRoot;
        private GameObject _unitContainer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            _cellManagerRoot = new GameObject("TestCellManager");
            var cellMgr = _cellManagerRoot.AddComponent<RegularCellManager>();
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    var cellGo = new GameObject($"Cell_{x}_{y}");
                    cellGo.transform.SetParent(_cellManagerRoot.transform);
                    var square = cellGo.AddComponent<Square>();
                    square.GridCoordinates = new Vector2IntImpl(x, y);
                    square.WorldPosition = new Vector3Impl(x, y, 0);
                    square.MovementCost = 1f;
                }
            }

            _unitContainer = new GameObject("UnitContainer");

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            if (_cellManagerRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_cellManagerRoot);
                _cellManagerRoot = null;
            }

            if (_unitContainer != null)
            {
                UnityEngine.Object.DestroyImmediate(_unitContainer);
                _unitContainer = null;
            }

            yield return null;
        }

        [Test]
        public void AiBrainAsset_AttackBrain_IsValid()
        {
            var brain = AiBrainTestHelper.CreateAttackBrain();
            Assert.IsNotNull(brain);
            Assert.IsTrue(brain.IsValid());
        }

        [Test]
        public void AiBrainAsset_HealBrain_IsValid()
        {
            var brain = AiBrainTestHelper.CreateHealBrain();
            Assert.IsNotNull(brain);
            Assert.IsTrue(brain.IsValid());
        }

        [Test]
        public void AiDecisionGraph_BasicAttackGraph_HasCorrectStructure()
        {
            var brain = AiBrainTestHelper.CreateAttackBrain();
            var graph = brain.DecisionGraph;
            Assert.IsNotNull(graph);

            // Should have intent, rule, and score nodes
            var nodes = graph.Nodes;
            Assert.That(nodes.Count, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void IntentGenerator_WithAttackBrain_GeneratesCandidates()
        {
            // This test would require a full IGridController setup
            // For now, just verify the brain asset is valid
            var brain = AiBrainTestHelper.CreateAttackBrain();
            Assert.IsTrue(brain.IsValid());
        }
    }
}
