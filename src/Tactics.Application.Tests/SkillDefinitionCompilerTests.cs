using NUnit.Framework;
using Tactics.Application.Skills;

namespace Tactics.Application.Tests;

public sealed class SkillDefinitionCompilerTests
{
    [Test]
    public void CompleteBatchCompilesTwelveDefinitionsAndExternalPoison()
    {
        SkillDefinitionCompileResult result = new SkillDefinitionCompiler().Compile(Drafts());
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Definitions, Has.Count.EqualTo(12));
            Assert.That(result.Definitions!.Single(item => item.Key.Value == "skill.poison-spear.lv1").Value.ExternalDependency, Is.True);
            Assert.That(result.Definitions![new Tactics.Core.Content.ContentId("skill.basic.magic")]
                .ExecutionProfile.EffectScaling, Is.EqualTo(Tactics.Core.Skills.SkillEffectScalingKind.Magical));
            Assert.That(result.Definitions![new Tactics.Core.Content.ContentId("skill.basic.magic")]
                .ExecutionProfile.AccuracyFactor, Is.EqualTo(0.75m));
        });
    }

    [Test]
    public void InvalidStatusPairAndDuplicateIdentityAreRejected()
    {
        SkillDefinitionDraft first = Drafts()[0];
        SkillDefinitionCompileResult result = new SkillDefinitionCompiler().Compile(new[] { first, first with { StatusDuration = 2 } }, requireCompleteBatch: false);
        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics.Select(item => item.Code), Does.Contain("skill.duplicate_id"));
        });
    }

    private static SkillDefinitionDraft[] Drafts()
    {
        string[] ids = { "skill.basic.magic", "skill.basic.melee", "skill.mage.fireball.lv1", "skill.mage.ice-bolt.lv1", "skill.mage.lightning.lv1", "skill.necromancer.summon-skeleton.lv1", "skill.necromancer.amplify-damage.lv1", "skill.necromancer.bone-spear.lv1", "skill.amazon.thrust.lv1", "skill.poison-spear.lv1", "skill.amazon.combat-techniques.lv1", "skill.amazon.pickup-spear.lv1" };
        return ids.Select(id => new SkillDefinitionDraft
        {
            ContentId = id, SourceId = id, Role = "Any", Kind = id.Contains("combat-techniques") ? "Passive" : id.Contains("pickup") ? "Utility" : "Active", Level = 1,
            ManaCost = 0, MinRange = 0, MaxRange = 4, ExecutionKind = id switch { "skill.basic.magic" => "MagicAttack", "skill.basic.melee" => "MeleeAttack", "skill.mage.fireball.lv1" => "Fireball", "skill.mage.ice-bolt.lv1" => "IceBolt", "skill.mage.lightning.lv1" => "Lightning", "skill.necromancer.summon-skeleton.lv1" => "SummonSkeleton", "skill.necromancer.amplify-damage.lv1" => "AmplifyDamage", "skill.necromancer.bone-spear.lv1" => "BoneSpear", "skill.amazon.thrust.lv1" => "Thrust", "skill.poison-spear.lv1" => "PoisonSpear", "skill.amazon.combat-techniques.lv1" => "CombatTechniques", _ => "PickupSpear" },
            Damage = 0, DamageKind = "None", ExternalDependency = id == "skill.poison-spear.lv1",
            EffectScaling = id == "skill.basic.magic" ? "Magical" : "None",
            AccuracyFactor = id == "skill.basic.magic" ? 0.75m : 1m
        }).ToArray();
    }
}
