using NUnit.Framework;
using Tactics.Application.AI;
using Tactics.Core.Board;

namespace Tactics.Application.Tests;

[TestFixture]
public sealed class AiEncounterDefinitionCompilerTests
{
    [Test]
    public void Compile_ProducesAiLayoutEncounterContentDrafts()
    {
        var ai=new AiDefinitionDraft("ai.pure-run.charger","Charger",1,1,0,0,new[]{"skill.basic.melee"},Array.Empty<string>());
        var layout=new BattleLayoutDraft("battle-layout.pure-run.open",new[]{new GridPoint(1,4)},new[]{new GridPoint(6,4)},Array.Empty<GridPoint>());
        var encounter=new EncounterDefinitionDraft("encounter.pure-run.n1",layout.ContentId,new[]{new EncounterMonsterDraft("unit.pure-run.charger",ai.ContentId,new[]{"skill.basic.melee"})});
        AiEncounterCompileResult result=new AiEncounterDefinitionCompiler().Compile(new[]{ai},new[]{layout},new[]{encounter});
        Assert.That(result.Succeeded,Is.True); Assert.That(result.ContentDrafts.Select(item=>item.ResourceTypeId),Is.EquivalentTo(new[]{"ai","battle-layout","encounter"}));
    }

    [Test]
    public void Compile_RejectsMissingAiReference()
    {
        var layout=new BattleLayoutDraft("battle-layout.pure-run.open",new[]{new GridPoint(1,4)},new[]{new GridPoint(6,4)},Array.Empty<GridPoint>());
        var encounter=new EncounterDefinitionDraft("encounter.pure-run.n1",layout.ContentId,new[]{new EncounterMonsterDraft("unit.pure-run.charger","ai.pure-run.missing",Array.Empty<string>())});
        Assert.That(new AiEncounterDefinitionCompiler().Compile(Array.Empty<AiDefinitionDraft>(),new[]{layout},new[]{encounter}).Succeeded,Is.False);
    }

    [Test]
    public void Compile_PreservesFrozenDecisionGraph()
    {
        var graph=new Tactics.Core.AI.AiDecisionGraphDefinition(
            new[]{new Tactics.Core.AI.AiIntentDefinition("1","Engage",15,true)},
            new[]{new Tactics.Core.AI.AiRuleDefinition("10","TargetInMoveAttackRange",0,true)},
            new[]{new Tactics.Core.AI.AiScoreDefinition("20","DistanceToTarget",5,true,Array.Empty<Tactics.Core.AI.AiCurveKey>())},
            new[]{new Tactics.Core.AI.AiDecisionEdge("1","10"),new Tactics.Core.AI.AiDecisionEdge("1","20")},"sha256:test");
        var draft=new AiDefinitionDraft("ai.pure-run.charger","Charger",1,1,0,0,new[]{"skill.basic.melee"},Array.Empty<string>(),graph);
        var result=new AiEncounterDefinitionCompiler().Compile(new[]{draft},Array.Empty<BattleLayoutDraft>(),Array.Empty<EncounterDefinitionDraft>());
        Assert.That(result.Ai.Values.Single().DecisionGraph,Is.SameAs(graph));
    }
}
