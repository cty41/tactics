using Tactics.Core.Board;
using Tactics.Core.Content;

namespace Tactics.Core.Encounters;

public sealed record BattleLayoutDefinition(ContentId ContentId, IReadOnlyList<GridPoint> PartySpawns, IReadOnlyList<GridPoint> EnemySpawns, IReadOnlyList<GridPoint> BlockedCells);
public sealed record EncounterMonsterDefinition(ContentId UnitId, ContentId AiId, IReadOnlyList<ContentId> SkillIds);
public enum EncounterClass { Normal, Elite, Boss }
public sealed record EncounterDefinition(ContentId ContentId, ContentId LayoutId,
    IReadOnlyList<EncounterMonsterDefinition> Monsters, float HealthMultiplier = 1f,
    float OutputMultiplier = 1f, int MinimumStartingMana = 0,
    EncounterClass Class = EncounterClass.Normal);
public sealed record ResolvedEncounter(EncounterDefinition Encounter, BattleLayoutDefinition Layout, IReadOnlyList<(EncounterMonsterDefinition Monster, GridPoint Cell)> Enemies);

public sealed class EncounterResolver
{
    public ResolvedEncounter Resolve(EncounterDefinition encounter, BattleLayoutDefinition layout)
    {
        if(encounter.LayoutId!=layout.ContentId) throw new ArgumentException("Encounter layout reference mismatch.");
        if(encounter.Monsters.Count>layout.EnemySpawns.Count) throw new ArgumentException("Encounter exceeds layout enemy spawns.");
        GridPoint[] all=layout.PartySpawns.Concat(layout.EnemySpawns).Concat(layout.BlockedCells).ToArray();
        if(all.Any(cell=>cell.X<0||cell.X>=10||cell.Y<0||cell.Y>=10)||all.Distinct().Count()!=all.Length) throw new ArgumentException("Layout contains invalid or overlapping cells.");
        return new ResolvedEncounter(encounter,layout,encounter.Monsters.Select((monster,index)=>(monster,layout.EnemySpawns[index])).ToArray());
    }
}
