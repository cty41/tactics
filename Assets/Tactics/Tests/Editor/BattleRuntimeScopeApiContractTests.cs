using NUnit.Framework;
using Tactics.Common.Battle;

namespace Tactics.Tests.Editor
{
    public sealed class BattleRuntimeScopeApiContractTests
    {
        [Test]
        public void BattleController_RuntimeScopeHasNoPublicSetter()
        {
            var property = typeof(BattleController).GetProperty(nameof(BattleController.RuntimeScope));

            Assert.That(property, Is.Not.Null);
            Assert.That(property.GetMethod?.IsPublic, Is.True,
                "RuntimeScope must remain publicly observable for diagnostics and consumers.");
            Assert.That(property.SetMethod?.IsPublic, Is.False,
                "BattleController exclusively owns scope replacement; callers must use explicit test seams.");
        }
    }
}
