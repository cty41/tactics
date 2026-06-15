using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tactics.Common.Battle;
using Tactics.Roster;

namespace Tactics.Tests.Editor
{
    public class BattleSettlementSaveTimingTests
    {
        [Test]
        public void ProcessRewards_DoesNotCallSave()
        {
            var type = typeof(BattleSettlementCoordinator);
            var processRewards = type.GetMethod("ProcessRewards", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(processRewards, Is.Not.Null, "ProcessRewards method should exist");

            var methodBody = processRewards.GetMethodBody();
            Assert.That(methodBody, Is.Not.Null);

            string il = string.Join(" ", methodBody.GetILAsByteArray().Select(b => b.ToString("X2")));

            var saveMethods = typeof(PlayerAdventureStateStore)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(m => m.Name == "Save")
                .ToList();
            Assert.That(saveMethods.Count, Is.GreaterThan(0), "PlayerAdventureStateStore.Save should exist");

            foreach (var saveMethod in saveMethods)
            {
                var saveToken = saveMethod.MetadataToken.ToString("X8");
                Assert.That(il, Does.Not.Contain(saveToken),
                    $"ProcessRewards should NOT call PlayerAdventureStateStore.Save({string.Join(", ", saveMethod.GetParameters().Select(p => p.ParameterType.Name))})");
            }
        }

        [Test]
        public void StartSettlement_AcceptsOnCompleteCallback()
        {
            var type = typeof(BattleSettlementCoordinator);
            var method = type.GetMethod("StartSettlement", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "StartSettlement should exist");

            var parameters = method.GetParameters();
            var onCompleteParam = parameters.FirstOrDefault(p => p.Name == "onComplete");
            Assert.That(onCompleteParam, Is.Not.Null, "StartSettlement should have onComplete parameter");
            Assert.That(onCompleteParam.ParameterType, Is.EqualTo(typeof(Action)));
        }

        [Test]
        public void AdvancePhase_FiresOnSettlementComplete_AtCompletePhase()
        {
            var type = typeof(BattleSettlementCoordinator);
            var advancePhase = type.GetMethod("AdvancePhase", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(advancePhase, Is.Not.Null, "AdvancePhase should exist");

            var methodBody = advancePhase.GetMethodBody();
            Assert.That(methodBody, Is.Not.Null);

            var onSettlementCompleteField = type.GetEvent("OnSettlementComplete", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(onSettlementCompleteField, Is.Not.Null, "OnSettlementComplete event should exist");
        }
    }
}
