using NUnit.Framework;
using System.Text.RegularExpressions;
using Tactics.RoguelikeMap;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.Editor
{
    /// <summary>
    /// RoguelikeNodeTransactionService.Abort 的回归测试。
    /// 覆盖：异常路径中止事务后节点回到未开始状态、不被消耗、可再次进入。
    /// </summary>
    public class RoguelikeNodeTransactionAbortTests
    {
        [Test]
        public void Abort_ClearsTransaction_AndKeepsNodeUnconsumed()
        {
            var node = new RoguelikeMapNode("mystery_01", RoguelikeNodeType.Mystery, "bp_mystery", Vector2.zero);

            var transaction = RoguelikeNodeTransactionService.Begin(node, null);
            Assert.IsNotNull(transaction, "Begin 应创建事务");
            Assert.AreEqual(RoguelikeNodeTransactionPhase.Entered, node.Transaction.Phase);

            LogAssert.Expect(LogType.Error,
                new Regex(@"\[NodeTransaction\] Abort node transaction: node=mystery_01, reason=测试中止原因"));
            RoguelikeNodeTransactionService.Abort(node, null, "测试中止原因");

            Assert.IsNull(node.Transaction, "Abort 后事务应被清除，节点回到未开始状态");
            Assert.IsFalse(node.IsConsumed, "Abort 不得消耗节点");
        }

        [Test]
        public void Abort_AllowsReenteringNode()
        {
            var node = new RoguelikeMapNode("mystery_02", RoguelikeNodeType.Mystery, "bp_mystery", Vector2.zero);

            RoguelikeNodeTransactionService.Begin(node, null);
            LogAssert.Expect(LogType.Error,
                new Regex(@"\[NodeTransaction\] Abort node transaction: node=mystery_02, reason=测试中止原因"));
            RoguelikeNodeTransactionService.Abort(node, null, "测试中止原因");

            var reentered = RoguelikeNodeTransactionService.Begin(node, null);
            Assert.IsNotNull(reentered, "Abort 后应能重新 Begin");
            Assert.AreEqual(RoguelikeNodeTransactionPhase.Entered, reentered.Phase);
        }

        [Test]
        public void Abort_NullNode_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RoguelikeNodeTransactionService.Abort(null, null, "空节点"));
        }
    }
}
