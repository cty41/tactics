using NUnit.Framework;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class UnitCombatStatRulesTests
{
    [Test]
    public void SecondaryStatsUseConfirmedCapsAndCoefficients()
    {
        var attributes = new UnitAttributes(6, 7, 5, 5, 5, 20);
        Assert.Multiple(() =>
        {
            Assert.That(UnitCombatStatRules.Accuracy(attributes), Is.EqualTo(110));
            Assert.That(UnitCombatStatRules.Dodge(attributes), Is.EqualTo(50));
            Assert.That(UnitCombatStatRules.CriticalChance(attributes), Is.EqualTo(55));
            Assert.That(UnitCombatStatRules.CriticalMultiplier(attributes), Is.EqualTo(1.55m));
        });
    }

    [Test]
    public void PrimaryRolesAndUniversalRoleUseEffectiveAttributes()
    {
        var mage = new UnitAttributes(4, 5, 3, 8, 6, 6);
        var amazonWithEquipment = new UnitAttributes(5, 7, 5, 5, 5, 5);
        Assert.Multiple(() =>
        {
            Assert.That(UnitCombatStatRules.AttributeContribution(mage, SkillRole.Mage,
                SkillEffectScalingKind.Magical), Is.EqualTo(4));
            Assert.That(UnitCombatStatRules.AttributeContribution(amazonWithEquipment, SkillRole.Amazon,
                SkillEffectScalingKind.MeleePhysical), Is.EqualTo(4));
            Assert.That(UnitCombatStatRules.AttributeContribution(amazonWithEquipment, SkillRole.Amazon,
                SkillEffectScalingKind.RangedPhysical), Is.EqualTo(3));
        });
    }

    [Test]
    public void UniversalUnlockReadsPermanentTotalOnly()
    {
        Assert.That(UnitCombatStatRules.MeetsUniversalTier(new UnitAttributes(5, 6, 5, 5, 6, 5), 2), Is.True);
        Assert.That(UnitCombatStatRules.MeetsUniversalTier(new UnitAttributes(5, 6, 5, 5, 6, 5), 3), Is.False);
    }

    [Test]
    public void DerivedStatsUseConstitutionAndAgility()
    {
        UnitDerivedStats stats = UnitDerivedStatRules.Calculate(new UnitAttributes(4, 5, 3, 6, 6, 6));
        Assert.That(stats, Is.EqualTo(new UnitDerivedStats(12, 18, 6, 3, 10)));
    }
}
