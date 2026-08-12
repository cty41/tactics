using Tactics.Application.Battle;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Application.Presentation;

public enum PresentationCueKind { Move, Melee, Ranged, Cast, Hit, Defeat, CorpseRemoved }
public enum PresentationMarkerKind { Begin, Release, Impact, Recover, Complete }

public sealed record BattlePresentationMarker(PresentationMarkerKind Kind, int Order);
public sealed record BattlePresentationCue(
    PresentationCueKind Kind,
    UnitInstanceId ActorId,
    UnitInstanceId? TargetId,
    ContentId? SkillId,
    GridPoint Origin,
    GridPoint Destination,
    IReadOnlyList<GridPoint> Path,
    IReadOnlyList<UnitInstanceId> AffectedUnitIds,
    IReadOnlyList<BattlePresentationMarker> Markers);

public sealed record BattlePresentationFrame(
    string Stage,
    BattleUiSnapshot Before,
    BattleUiSnapshot After,
    IReadOnlyList<BattlePresentationCue> Cues);

/// <summary>Compiles immutable gameplay events into deterministic presentation instructions.</summary>
public static class BattlePresentationFrameCompiler
{
    public static BattlePresentationFrame Compile(
        string stage,
        BattleUiSnapshot before,
        BattleUiSnapshot after,
        IReadOnlyList<BattleEvent> events,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(skills);
        var cues = new List<BattlePresentationCue>();
        foreach (BattleEvent item in events)
        {
            switch (item)
            {
                case UnitMovedEvent moved:
                    cues.Add(Cue(PresentationCueKind.Move, moved.UnitId, null, null, moved.Origin, moved.Destination, moved.Path, []));
                    break;
                case SkillUsedEvent used:
                    BattleUiUnitSnapshot actor = Find(before, after, used.ActorId);
                    BattleUiUnitSnapshot target = Find(before, after, used.TargetId);
                    PresentationCueKind actionKind = ResolveAction(skills[used.SkillId]);
                    cues.Add(Cue(actionKind, used.ActorId, used.TargetId, used.SkillId, actor.Cell, target.Cell, [], [used.TargetId]));
                    break;
                case DamageAppliedEvent damage:
                    BattleUiUnitSnapshot source = Find(before, after, damage.SourceId);
                    BattleUiUnitSnapshot damaged = Find(before, after, damage.TargetId);
                    cues.Add(Cue(PresentationCueKind.Hit, damage.SourceId, damage.TargetId, damage.SkillId, source.Cell, damaged.Cell, [], [damage.TargetId]));
                    break;
                case UnitDefeatedEvent defeated:
                    BattleUiUnitSnapshot unit = Find(before, after, defeated.UnitId);
                    cues.Add(Cue(PresentationCueKind.Defeat, defeated.UnitId, null, null, unit.Cell, unit.Cell, [], [defeated.UnitId]));
                    break;
                case CorpseConsumedEvent consumed:
                    BattleUiUnitSnapshot? corpse = before.Units.FirstOrDefault(value => !value.IsAlive && value.Cell == consumed.Cell);
                    if (corpse is not null)
                        cues.Add(Cue(PresentationCueKind.CorpseRemoved, corpse.UnitId, null, null, consumed.Cell, consumed.Cell, [], [corpse.UnitId]));
                    break;
            }
        }
        var affectedBySkill = events.OfType<DamageAppliedEvent>()
            .GroupBy(value => (value.SourceId, value.SkillId))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<UnitInstanceId>)group.Select(value => value.TargetId).Distinct().ToArray());
        for (int index = 0; index < cues.Count; index++)
        {
            BattlePresentationCue cue = cues[index];
            if (cue.SkillId is not ContentId skillId || cue.Kind is not (PresentationCueKind.Melee or PresentationCueKind.Ranged or PresentationCueKind.Cast)) continue;
            IReadOnlyList<UnitInstanceId> affected = affectedBySkill.GetValueOrDefault((cue.ActorId, skillId), cue.AffectedUnitIds);
            cues[index] = cue with { Path = Ray(cue.Origin, cue.Destination), AffectedUnitIds = affected };
        }
        return new BattlePresentationFrame(stage, before, after, cues);
    }

    private static PresentationCueKind ResolveAction(SkillDefinition skill) => skill.ExecutionKind switch
    {
        SkillExecutionKind.MeleeAttack or SkillExecutionKind.Thrust or SkillExecutionKind.MultiStab => PresentationCueKind.Melee,
        SkillExecutionKind.RangedAttack or SkillExecutionKind.HeavyShot or SkillExecutionKind.PoisonSpear => PresentationCueKind.Ranged,
        _ => PresentationCueKind.Cast
    };

    private static BattlePresentationCue Cue(PresentationCueKind kind, UnitInstanceId actor, UnitInstanceId? target,
        ContentId? skill, GridPoint origin, GridPoint destination, IReadOnlyList<GridPoint> path,
        IReadOnlyList<UnitInstanceId> affected) => new(kind, actor, target, skill, origin, destination, path, affected,
        [new(PresentationMarkerKind.Begin, 0), new(PresentationMarkerKind.Release, 1), new(PresentationMarkerKind.Impact, 2), new(PresentationMarkerKind.Recover, 3), new(PresentationMarkerKind.Complete, 4)]);

    private static BattleUiUnitSnapshot Find(BattleUiSnapshot before, BattleUiSnapshot after, UnitInstanceId id) =>
        before.Units.Concat(after.Units).First(value => value.UnitId == id);

    private static IReadOnlyList<GridPoint> Ray(GridPoint origin, GridPoint target)
    {
        int dx=target.X-origin.X,dy=target.Y-origin.Y,steps=GreatestCommonDivisor(Math.Abs(dx),Math.Abs(dy));
        if(steps==0)return [];
        int sx=dx/steps,sy=dy/steps;
        return Enumerable.Range(1,steps).Select(index=>new GridPoint(origin.X+sx*index,origin.Y+sy*index)).ToArray();
    }
    private static int GreatestCommonDivisor(int left,int right){while(right!=0)(left,right)=(right,left%right);return left;}
}
