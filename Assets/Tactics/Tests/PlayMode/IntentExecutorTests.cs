using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Cells;
using Tactics.Common.Testing.Gameplay;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tactics.Tests.PlayMode
{
    /// <summary>
    /// IntentExecutor 白盒测试。
    /// 覆盖 FindAttackAbility 名称匹配逻辑，防止 "Melee Attack" vs "MeleeAttack" 空格不匹配 bug 回归。
    /// </summary>
    public class IntentExecutorTests
    {
        [Test]
        public void FindAttackAbility_RecognizesMeleeAttack_WithSpace()
        {
            var context = CreateContextWithAbility("Melee Attack");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNotNull(result, "FindAttackAbility should recognize 'Melee Attack' (with space).");
            Assert.AreEqual("Melee Attack", result.Name);
        }

        [Test]
        public void FindAttackAbility_RecognizesRangedAttack_WithSpace()
        {
            var context = CreateContextWithAbility("Ranged Attack");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNotNull(result, "FindAttackAbility should recognize 'Ranged Attack' (with space).");
            Assert.AreEqual("Ranged Attack", result.Name);
        }

        [Test]
        public void FindAttackAbility_RecognizesMagicAttack_WithSpace()
        {
            var context = CreateContextWithAbility("Magic Attack");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNotNull(result, "FindAttackAbility should recognize 'Magic Attack' (with space).");
            Assert.AreEqual("Magic Attack", result.Name);
        }

        [Test]
        public void FindAttackAbility_RecognizesMeleeAttack_WithoutSpace()
        {
            var context = CreateContextWithAbility("MeleeAttack");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNotNull(result, "FindAttackAbility should recognize 'MeleeAttack' (without space).");
            Assert.AreEqual("MeleeAttack", result.Name);
        }

        [Test]
        public void FindAttackAbility_RecognizesRangedAttack_WithoutSpace()
        {
            var context = CreateContextWithAbility("RangedAttack");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNotNull(result, "FindAttackAbility should recognize 'RangedAttack' (without space).");
            Assert.AreEqual("RangedAttack", result.Name);
        }

        [Test]
        public void FindAttackAbility_ReturnsNull_ForMoveOnly()
        {
            var context = CreateContextWithAbility("Move");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNull(result, "FindAttackAbility should return null for 'Move' ability.");
        }

        [Test]
        public void FindAttackAbility_ReturnsNull_ForHeal()
        {
            var context = CreateContextWithAbility("Heal");
            var result = InvokeFindAttackAbility(context);
            Assert.IsNull(result, "FindAttackAbility should return null for 'Heal' ability.");
        }

        [Test]
        public void FindAttackAbility_ReturnsNull_ForEmpty()
        {
            var context = CreateContextWithNoAbilities();
            var result = InvokeFindAttackAbility(context);
            Assert.IsNull(result, "FindAttackAbility should return null when no abilities exist.");
        }

        [Test]
        public void FindMoveAbility_RecognizesMove()
        {
            var context = CreateContextWithAbility("Move");
            var result = InvokeFindMoveAbility(context);
            Assert.IsNotNull(result, "FindMoveAbility should recognize 'Move'.");
            Assert.AreEqual("Move", result.Name);
        }

        [Test]
        public void FindMoveAbility_ReturnsNull_ForMeleeAttack()
        {
            var context = CreateContextWithAbility("Melee Attack");
            var result = InvokeFindMoveAbility(context);
            Assert.IsNull(result, "FindMoveAbility should return null for 'Melee Attack'.");
        }

        #region Helpers

        /// <summary>
        /// Creates an AiContext with a single ability of the given name.
        /// Uses reflection to build the context since AiContext has no public constructor for testing.
        /// </summary>
        private static AiContext CreateContextWithAbility(string abilityName)
        {
            // Create a minimal AiContext with AvailableAbilities containing one entry
            var abilityInfo = new AbilityInfo(
                abilityName,
                1,      // range
                true,   // isReady
                null,   // ability (not needed for name matching)
                AbilityAiTags.Damage,
                5f,     // baseDamage
                0f,     // healAmount
                0f,     // controlValue
                0f      // utilityValue
            );

            return CreateContextWithAbilities(new[] { abilityInfo });
        }

        private static AiContext CreateContextWithNoAbilities()
        {
            return CreateContextWithAbilities(new AbilityInfo[0]);
        }

        private static AiContext CreateContextWithAbilities(AbilityInfo[] abilities)
        {
            // AiContext constructor: (self, gridController, enemies, allies, reachableCells, candidateTargets, availableAbilities, brainAsset, decisionLog)
            var context = new AiContext(
                null,   // self
                null,   // gridController
                null,   // enemies
                null,   // allies
                null,   // reachableCells
                null,   // candidateTargets
                abilities.ToList(),
                null,   // brainAsset
                new AiDecisionLog(false)
            );
            return context;
        }

        /// <summary>
        /// Invokes the private static FindAttackAbility method via reflection.
        /// </summary>
        private static AbilityInfo InvokeFindAttackAbility(AiContext context)
        {
            var method = typeof(IntentExecutor).GetMethod("FindAttackAbility", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "FindAttackAbility method should exist.");
            return (AbilityInfo)method.Invoke(null, new object[] { context });
        }

        /// <summary>
        /// Invokes the private static FindMoveAbility method via reflection.
        /// </summary>
        private static AbilityInfo InvokeFindMoveAbility(AiContext context)
        {
            var method = typeof(IntentExecutor).GetMethod("FindMoveAbility", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "FindMoveAbility method should exist.");
            return (AbilityInfo)method.Invoke(null, new object[] { context });
        }

        #endregion
    }
}
