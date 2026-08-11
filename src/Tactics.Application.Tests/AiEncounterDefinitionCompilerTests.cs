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
}
