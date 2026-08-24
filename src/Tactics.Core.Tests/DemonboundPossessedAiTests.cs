using NUnit.Framework;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

[TestFixture]
public class DemonboundPossessedAiTests
{
    [Test]
    public void Definition_UsesStablePossessedIdentityAndChargerProfile()
    {
        AiDefinition definition = DemonboundPossessedAi.For( Array.Empty<SkillDefinition>());

        Assert.Multiple(() =>
        {
            Assert.That(definition.ContentId, Is.EqualTo(DemonboundPossessedAi.ContentId));
            Assert.That(definition.ContentId, Is.EqualTo(new ContentId("ai.demonbound.possessed")));
            Assert.That(definition.Archetype, Is.EqualTo(AiArchetype.Charger));
            Assert.That(definition.Profile, Is.EqualTo(DemonboundPossessedAi.Profile));
        });
    }

    [Test]
    public void For_ProjectsOnlyActiveNonMeditationSkillsIntoAiActions()
    {
        SkillDefinition bane = Skill("skill.demonbound.bane.lv3", SkillExecutionKind.Bane);
        SkillDefinition mindfulness = Skill("skill.demonbound.mindfulness.lv1", SkillExecutionKind.Mindfulness);
        SkillDefinition meditation = Skill("skill.demonbound.meditation", SkillExecutionKind.Meditation);

        AiDefinition definition = DemonboundPossessedAi.For( new[] { bane, mindfulness, meditation });

        Assert.That(definition.SkillIds, Is.EqualTo(new[] { bane.ContentId }));
    }

    [Test]
    public void For_EmptySkillListStillProducesAValidDefinition()
    {
        AiDefinition definition = DemonboundPossessedAi.For( Array.Empty<SkillDefinition>());

        Assert.That(definition.SkillIds, Is.Empty);
        Assert.That(definition.ContentId, Is.EqualTo(DemonboundPossessedAi.ContentId));
    }

    private static SkillDefinition Skill(string contentId, SkillExecutionKind execution) => new(
        new ContentId(contentId), "test." + contentId, SkillRole.Demonbound,
        execution switch
        {
            SkillExecutionKind.Mindfulness => SkillKind.Passive,
            SkillExecutionKind.Meditation => SkillKind.Utility,
            _ => SkillKind.Active
        },
        1, 0, 1, 1, execution, 5, SkillDamageKind.Magical);
}