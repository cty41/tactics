#if TOOLS
using Tactics.Core.Skills;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

internal static class AttributeSkillBalanceOverrides
{
    public static void Apply(SkillDefinitionResource resource)
    {
        string id = resource.ContentIdValue;
        resource.Damage = id switch
        {
            var value when value.StartsWith("skill.mage.fireball.", StringComparison.Ordinal) => 3,
            var value when value.StartsWith("skill.mage.lightning.", StringComparison.Ordinal) => 2,
            var value when value.StartsWith("skill.mage.ice-bolt.", StringComparison.Ordinal) => 1,
            var value when value.StartsWith("skill.necromancer.bone-spear.", StringComparison.Ordinal) => 2,
            var value when value.StartsWith("skill.amazon.thrust.", StringComparison.Ordinal) => 3,
            var value when value.StartsWith("skill.amazon.multi-stab.", StringComparison.Ordinal) => 1,
            var value when value is "skill.poison-spear.lv1" ||
                value.StartsWith("skill.amazon.poison-spear.", StringComparison.Ordinal) => 4,
            var value when value.StartsWith("skill.demonbound.bane.", StringComparison.Ordinal) ||
                value.StartsWith("skill.demonbound.cleave.", StringComparison.Ordinal) ||
                value.StartsWith("skill.demonbound.infernal-blast.", StringComparison.Ordinal) ||
                value.StartsWith("skill.demonbound.hellfire.", StringComparison.Ordinal) => 4,
            _ => resource.Damage
        };
        resource.EffectScalingValue = resource.ExecutionKindValue switch
        {
            nameof(SkillExecutionKind.Thrust) or nameof(SkillExecutionKind.MultiStab) =>
                nameof(SkillEffectScalingKind.MeleePhysical),
            nameof(SkillExecutionKind.RangedAttack) or nameof(SkillExecutionKind.HeavyShot) or
                nameof(SkillExecutionKind.PoisonSpear) => nameof(SkillEffectScalingKind.RangedPhysical),
            nameof(SkillExecutionKind.MagicAttack) or nameof(SkillExecutionKind.Fireball) or
                nameof(SkillExecutionKind.Lightning) or nameof(SkillExecutionKind.IceBolt) or
                nameof(SkillExecutionKind.BoneSpear) or nameof(SkillExecutionKind.Bane) or
                nameof(SkillExecutionKind.Cleave) or nameof(SkillExecutionKind.InfernalBlast) or
                nameof(SkillExecutionKind.Hellfire) => nameof(SkillEffectScalingKind.Magical),
            nameof(SkillExecutionKind.RecoverSpear) or nameof(SkillExecutionKind.DemonicRegeneration) =>
                nameof(SkillEffectScalingKind.Healing),
            nameof(SkillExecutionKind.IceArmor) or nameof(SkillExecutionKind.BoneShield) =>
                nameof(SkillEffectScalingKind.Shield),
            _ => nameof(SkillEffectScalingKind.None)
        };
        resource.AccuracyFactor = 1d;
        if (id.StartsWith("skill.demonbound.infernal-blast.", StringComparison.Ordinal))
        {
            resource.DisplayName = "魔炎斩";
            resource.Description = "魔炎斩";
        }
    }
}
#endif
