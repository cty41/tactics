using NUnit.Framework;
using Tactics.Core.AI;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Skills;
using Tactics.Core.Battle;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

[TestFixture]
public sealed class AiEncounterRuntimeTests
{
    [Test]
    public void Resolver_BindsN1ToFrozenOpenSpawns()
    {
        var layout=new BattleLayoutDefinition(new ContentId("battle-layout.pure-run.open"),new[]{new GridPoint(1,4),new GridPoint(1,5),new GridPoint(2,4)},new[]{new GridPoint(6,4),new GridPoint(7,3),new GridPoint(7,5),new GridPoint(8,4)},Array.Empty<GridPoint>());
        var monster=new EncounterMonsterDefinition(new ContentId("unit.pure-run.charger"),new ContentId("ai.pure-run.charger"),new[]{new ContentId("skill.enemy.charge-strike.lv1")});
        var encounter=new EncounterDefinition(new ContentId("encounter.pure-run.n1"),layout.ContentId,new[]{monster,monster,monster});
        ResolvedEncounter resolved=new EncounterResolver().Resolve(encounter,layout);
        Assert.That(resolved.Enemies.Select(item=>item.Cell),Is.EqualTo(layout.EnemySpawns.Take(3)));
    }

    [Test]
    public void AiDefinition_PreservesElitePatternOrder()
    {
        var definition=new AiDefinition(new ContentId("ai.pure-run.elite-charger"),AiArchetype.EliteCharger,new AiProfileDefinition(1,1,0,0),new[]{new ContentId("skill.basic.melee"),new ContentId("skill.enemy.charge-strike.lv1")},new[]{new ContentId("skill.enemy.charge-strike.lv1"),new ContentId("skill.basic.melee")});
        Assert.That(definition.PatternSkillIds.Select(value=>value.Value),Is.EqualTo(new[]{"skill.enemy.charge-strike.lv1","skill.basic.melee"}));
    }

    [TestCase(SkillExecutionKind.RangedAttack)]
    [TestCase(SkillExecutionKind.ChargeStrike)]
    [TestCase(SkillExecutionKind.HeavyShot)]
    [TestCase(SkillExecutionKind.AreaBlast)]
    public void EnemySkillKinds_ArePartOfGenericSkillContract(SkillExecutionKind kind)=>Assert.That(Enum.IsDefined(kind),Is.True);

    [Test]
    public void Decision_GeneratesMoveAlongsideCurrentAttackAndCanSelectMoveThenSkill()
    {
        var cells=new Dictionary<GridPoint,CellState>();for(int x=0;x<5;x++)for(int y=0;y<3;y++)cells[new GridPoint(x,y)]=new CellState();
        var actorId=new UnitInstanceId("enemy");var targetId=new UnitInstanceId("party");
        var actor=new BattleUnitState(new UnitState(actorId,new ContentId("unit.enemy"),new GridPoint(0,1),3,5,1,0),20,20);
        var target=new BattleUnitState(new UnitState(targetId,new ContentId("unit.party"),new GridPoint(3,1),3,4,0,1),20,20);
        var state=new BattleState(new BoardSnapshot(cells),new[]{actor,target},new[]{actorId,targetId});
        var skill=new SkillDefinition(new ContentId("skill.basic.melee"),"melee",SkillRole.Any,SkillKind.Active,1,0,1,1,SkillExecutionKind.MeleeAttack,2,SkillDamageKind.Physical);
        var definition=new AiDefinition(new ContentId("ai.test"),AiArchetype.Charger,new AiProfileDefinition(1,1,0,0),new[]{skill.ContentId},Array.Empty<ContentId>());
        AiTurnPlan plan=new AiDecisionService().Decide(state,definition,new Dictionary<ContentId,SkillDefinition>{{skill.ContentId,skill}});
        Assert.Multiple(()=>
        {
            Assert.That(plan.Candidates.Any(item=>item.Intent==AiIntentKind.Engage),Is.True);
            Assert.That(plan.Candidates.Any(item=>item.SkillId==skill.ContentId&&item.MoveBeforeSkill),Is.True);
        });
    }

    [Test]
    public void BasicAttack_TargetHealthCurveCanSelectLowHealthSummon()
    {
        var cells=new Dictionary<GridPoint,CellState>();for(int x=0;x<4;x++)for(int y=0;y<4;y++)cells[new GridPoint(x,y)]=new CellState();
        var actorId=new UnitInstanceId("enemy");var heroId=new UnitInstanceId("party.hero");var skeletonId=new UnitInstanceId("party.skeleton.0");
        var actor=new BattleUnitState(new UnitState(actorId,new ContentId("unit.enemy"),new GridPoint(1,1),0,5,1,0),20,20);
        var hero=new BattleUnitState(new UnitState(heroId,new ContentId("unit.hero"),new GridPoint(2,1),0,4,0,1),20,20);
        var skeleton=new BattleUnitState(new UnitState(skeletonId,new ContentId("unit.pure-run.skeleton-warrior"),new GridPoint(1,2),0,4,0,2),12,2,summonOwnerId:heroId,canProduceCorpse:false);
        var state=new BattleState(new BoardSnapshot(cells),new[]{actor,hero,skeleton},new[]{actorId,heroId,skeletonId});
        var skill=new SkillDefinition(new ContentId("skill.basic.melee"),"melee",SkillRole.Any,SkillKind.Basic,1,0,1,1,SkillExecutionKind.MeleeAttack,2,SkillDamageKind.Physical);
        var graph=new AiDecisionGraphDefinition(
            new[]{new AiIntentDefinition("intent","BasicAttack",25,true)},Array.Empty<AiRuleDefinition>(),
            new[]{new AiScoreDefinition("health","TargetHealth",10,true,new[]{new AiCurveKey(0,1,0,-1),new AiCurveKey(1,0,-1,0)})},
            new[]{new AiDecisionEdge("intent","health")},"sha256:test");
        var definition=new AiDefinition(new ContentId("ai.test"),AiArchetype.Charger,new AiProfileDefinition(0,0,0,0),new[]{skill.ContentId},Array.Empty<ContentId>(),graph);
        AiTurnPlan plan=new AiDecisionService().Decide(state,definition,new Dictionary<ContentId,SkillDefinition>{{skill.ContentId,skill}});
        Assert.That(plan.Selected.TargetId,Is.EqualTo(skeletonId));
    }
}
