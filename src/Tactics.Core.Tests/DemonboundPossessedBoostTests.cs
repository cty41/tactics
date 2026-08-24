using NUnit.Framework;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

[TestFixture]
public class DemonboundPossessedBoostTests
{
    private static readonly ContentId DemonboundUnit = new("unit.pure-run.demonbound");

    [Test]
    public void Apply_BoostsAllSixAttributesByFiveAndRecalculatesDerivedValues()
    {
        // 基线:体质 6 → MaxHP 24;魅力 6 → MaxMP 18;移动 2+floor(6/2)=5;先攻 敏捷 4×2=8
        BattleUnitState unit = Unit(new UnitAttributes(6, 4, 6, 6, 6, 2), health: 24, maxHealth: 24,
            maxMana: 18, currentMana: 6, moveRange: 5, initiative: 8,
            possessed: new DemonboundBattleState(10, 1, isPossessed: true));

        BattleUnitState boosted = DemonboundPossessedBoostService.Apply(unit);

        UnitAttributes attributes = boosted.Unit.EffectiveAttributes;
        Assert.Multiple(() =>
        {
            Assert.That(attributes.Strength, Is.EqualTo(11));
            Assert.That(attributes.Agility, Is.EqualTo(9));
            Assert.That(attributes.Constitution, Is.EqualTo(11));
            Assert.That(attributes.Intelligence, Is.EqualTo(11));
            Assert.That(attributes.Charisma, Is.EqualTo(11));
            Assert.That(attributes.Luck, Is.EqualTo(7));
            // 体质 11 ×4 = 44
            Assert.That(boosted.MaxHealth, Is.EqualTo(44));
            // 魅力 11 ×3 = 33
            Assert.That(boosted.MaxMana, Is.EqualTo(33));
            // 移动 2 + floor(11/2)=7 但上限 5
            Assert.That(boosted.Unit.MoveRange, Is.EqualTo(5));
            // 先攻 敏捷 9 ×2 = 18
            Assert.That(boosted.Unit.Initiative, Is.EqualTo(18f));
            // 命中 = 100 + (9-5)*5 = 120;闪避 = 5 + (7-5)*5 = 15;暴击 = 10 + (7-5)*3 = 16
            Assert.That(UnitCombatStatRules.Accuracy(boosted.Unit.EffectiveAttributes), Is.EqualTo(120));
            Assert.That(UnitCombatStatRules.Dodge(boosted.Unit.EffectiveAttributes), Is.EqualTo(15));
            Assert.That(UnitCombatStatRules.CriticalChance(boosted.Unit.EffectiveAttributes), Is.EqualTo(16));
        });
    }

    [Test]
    public void Apply_CurrentHealthAndManaScaleWithNewMaximums()
    {
        // 16/24 HP → 44 max:期望 floor(16*44/24)=29;6/18 MP → 33 max:floor(6*33/18)=11
        BattleUnitState unit = Unit(new UnitAttributes(6, 4, 6, 6, 6, 2), health: 16, maxHealth: 24,
            maxMana: 18, currentMana: 6, moveRange: 5, initiative: 8,
            possessed: new DemonboundBattleState(10, 1, isPossessed: true));

        BattleUnitState boosted = DemonboundPossessedBoostService.Apply(unit);

        Assert.Multiple(() =>
        {
            Assert.That(boosted.CurrentHealth, Is.EqualTo(29));
            Assert.That(boosted.CurrentMana, Is.EqualTo(11));
        });
    }

    [Test]
    public void Apply_IsIdempotent_RepeatedCallsNeverStackTheBoost()
    {
        BattleUnitState unit = Unit(new UnitAttributes(6, 4, 6, 6, 6, 2), health: 24, maxHealth: 24,
            maxMana: 18, currentMana: 6, moveRange: 5, initiative: 8,
            possessed: new DemonboundBattleState(10, 1, isPossessed: true));

        BattleUnitState once = DemonboundPossessedBoostService.Apply(unit);
        BattleUnitState twice = DemonboundPossessedBoostService.Apply(once);

        Assert.Multiple(() =>
        {
            Assert.That(once.DemonboundState!.PossessedBoostApplied, Is.True);
            Assert.That(twice.Unit.EffectiveAttributes, Is.EqualTo(once.Unit.EffectiveAttributes));
            Assert.That(twice.MaxHealth, Is.EqualTo(once.MaxHealth));
            Assert.That(twice.MaxMana, Is.EqualTo(once.MaxMana));
            Assert.That(twice.CurrentHealth, Is.EqualTo(once.CurrentHealth));
        });
    }

    [Test]
    public void Apply_WithoutPossessedFormOrAlreadyApplied_ReturnsTheUnitUnchanged()
    {
        BattleUnitState sane = Unit(new UnitAttributes(6, 4, 6, 6, 6, 2), health: 24, maxHealth: 24,
            maxMana: 18, currentMana: 6, moveRange: 5, initiative: 8,
            possessed: new DemonboundBattleState(7, 1));
        BattleUnitState untouched = DemonboundPossessedBoostService.Apply(sane);
        Assert.That(untouched.DemonboundState!.PossessedBoostApplied, Is.False);
        Assert.That(untouched.Unit.EffectiveAttributes, Is.EqualTo(sane.Unit.EffectiveAttributes));
    }

    private static BattleUnitState Unit(UnitAttributes attributes, int health, int maxHealth,
        int maxMana, int currentMana, int moveRange, float initiative, DemonboundBattleState? possessed = null) =>
        new(
            new UnitState(
                new UnitInstanceId("party-demonbound"),
                DemonboundUnit,
                new GridPoint(1, 1),
                moveRange,
                initiative,
                0,
                0,
                effectiveAttributes: attributes),
            maxHealth,
            health,
            maxMana: maxMana,
            currentMana: currentMana,
            physicalAttack: 1,
            magicalAttack: 1,
            demonboundState: possessed ?? new DemonboundBattleState());
}