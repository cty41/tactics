using NUnit.Framework;
using Tactics.Application.Battle;
using Tactics.Application.Presentation;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Application.Tests.Battle;

public sealed class BattlePresentationFrameCompilerTests
{
    [Test]
    public void CompilesOrderedMoveActionHitAndDefeatWithoutChangingSnapshots()
    {
        UnitInstanceId actor = new("actor"); UnitInstanceId target = new("target"); ContentId skillId = new("skill.basic.melee");
        BattleUiSnapshot before = Snapshot(actor,target,new GridPoint(1,1),new GridPoint(3,1),true);
        BattleUiSnapshot after = Snapshot(actor,target,new GridPoint(2,1),new GridPoint(3,1),false);
        SkillDefinition skill = new(skillId,"basic",SkillRole.Any,SkillKind.Basic,1,0,1,1,SkillExecutionKind.MeleeAttack,5,SkillDamageKind.Physical);
        BattleEvent[] events =
        [
            new UnitMovedEvent(actor,new GridPoint(1,1),new GridPoint(2,1),[new GridPoint(2,1)]),
            new SkillUsedEvent(actor,target,skillId),
            new DamageAppliedEvent(actor,target,skillId,5,0),
            new UnitDefeatedEvent(target)
        ];
        BattlePresentationFrame frame=BattlePresentationFrameCompiler.Compile("test",before,after,events,new Dictionary<ContentId,SkillDefinition>{{skillId,skill}});
        Assert.That(frame.Cues.Select(cue=>cue.Kind),Is.EqualTo(new[]{PresentationCueKind.Move,PresentationCueKind.Melee,PresentationCueKind.Hit,PresentationCueKind.Defeat}));
        Assert.That(frame.Cues[0].Path,Is.EqualTo(new[]{new GridPoint(2,1)}));
        Assert.That(frame.Cues.All(cue=>cue.Markers.Count==5),Is.True);
        Assert.That(before.Units.Single(value=>value.UnitId==actor).Cell,Is.EqualTo(new GridPoint(1,1)));
    }

    private static BattleUiSnapshot Snapshot(UnitInstanceId actor,UnitInstanceId target,GridPoint actorCell,GridPoint targetCell,bool targetAlive)
    {
        BattleUiUnitSnapshot Unit(UnitInstanceId id,GridPoint cell,bool alive)=>new(id,new ContentId("unit.test"),cell,id==actor?0:1,alive,alive?10:0,10,5,5,false,[],new Dictionary<ContentId,int>());
        return new BattleUiSnapshot(PlayableBattlePhase.PlayerTurn,1,actor,BattleTargetingMode.None,null,[Unit(actor,actorCell,true),Unit(target,targetCell,targetAlive)],[],[],[],null,[],new Dictionary<UnitInstanceId,GridPoint>(),[],[actor,target],0,null,[]);
    }
}
