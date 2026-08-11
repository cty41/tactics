using Tactics.Application.Content;
using Tactics.Core.AI;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;

namespace Tactics.Application.AI;

public sealed record AiDefinitionDraft(string ContentId,string Archetype,float DistanceWeight,float DamageWeight,float TargetCountWeight,float HarmfulStatusWeight,IReadOnlyList<string> SkillIds,IReadOnlyList<string> PatternSkillIds);
public sealed record BattleLayoutDraft(string ContentId,IReadOnlyList<GridPoint> PartySpawns,IReadOnlyList<GridPoint> EnemySpawns,IReadOnlyList<GridPoint> BlockedCells);
public sealed record EncounterMonsterDraft(string UnitId,string AiId,IReadOnlyList<string> SkillIds);
public sealed record EncounterDefinitionDraft(string ContentId,string LayoutId,IReadOnlyList<EncounterMonsterDraft> Monsters);
public sealed record AiEncounterCompileResult(IReadOnlyDictionary<ContentId,AiDefinition> Ai,IReadOnlyDictionary<ContentId,BattleLayoutDefinition> Layouts,IReadOnlyDictionary<ContentId,EncounterDefinition> Encounters,IReadOnlyList<ContentDraft> ContentDrafts,IReadOnlyList<ContentDiagnostic> Diagnostics){public bool Succeeded=>Diagnostics.All(item=>item.Severity!=ContentDiagnosticSeverity.Error);}

public sealed class AiEncounterDefinitionCompiler
{
    public AiEncounterCompileResult Compile(IEnumerable<AiDefinitionDraft> aiDrafts,IEnumerable<BattleLayoutDraft> layoutDrafts,IEnumerable<EncounterDefinitionDraft> encounterDrafts)
    {
        var diagnostics=new List<ContentDiagnostic>(); var ai=new Dictionary<ContentId,AiDefinition>(); var layouts=new Dictionary<ContentId,BattleLayoutDefinition>(); var encounters=new Dictionary<ContentId,EncounterDefinition>(); var content=new List<ContentDraft>();
        try
        {
            foreach(AiDefinitionDraft draft in aiDrafts){var id=new ContentId(draft.ContentId); if(!Enum.TryParse(draft.Archetype,true,out AiArchetype archetype)||!Enum.IsDefined(archetype))throw new ArgumentException($"Unknown AI archetype '{draft.Archetype}'."); var skills=draft.SkillIds.Select(value=>new ContentId(value)).ToArray(); var pattern=draft.PatternSkillIds.Select(value=>new ContentId(value)).ToArray(); if(!ai.TryAdd(id,new AiDefinition(id,archetype,new AiProfileDefinition(draft.DistanceWeight,draft.DamageWeight,draft.TargetCountWeight,draft.HarmfulStatusWeight),skills,pattern)))throw new ArgumentException($"Duplicate AI '{id}'."); content.Add(new ContentDraft(id,"ai",1,skills));}
            foreach(BattleLayoutDraft draft in layoutDrafts){var id=new ContentId(draft.ContentId); var value=new BattleLayoutDefinition(id,draft.PartySpawns,draft.EnemySpawns,draft.BlockedCells); _=new EncounterResolver().Resolve(new EncounterDefinition(new ContentId("encounter.validation"),id,Array.Empty<EncounterMonsterDefinition>()),value); if(!layouts.TryAdd(id,value))throw new ArgumentException($"Duplicate layout '{id}'."); content.Add(new ContentDraft(id,"battle-layout",1));}
            foreach(EncounterDefinitionDraft draft in encounterDrafts){var id=new ContentId(draft.ContentId); var layoutId=new ContentId(draft.LayoutId); if(!layouts.ContainsKey(layoutId))throw new ArgumentException($"Missing layout '{layoutId}'."); var monsters=draft.Monsters.Select(item=>new EncounterMonsterDefinition(new ContentId(item.UnitId),new ContentId(item.AiId),item.SkillIds.Select(value=>new ContentId(value)).ToArray())).ToArray(); if(monsters.Any(item=>!ai.ContainsKey(item.AiId)))throw new ArgumentException("Encounter references missing AI."); var value=new EncounterDefinition(id,layoutId,monsters); _=new EncounterResolver().Resolve(value,layouts[layoutId]); if(!encounters.TryAdd(id,value))throw new ArgumentException($"Duplicate encounter '{id}'."); content.Add(new ContentDraft(id,"encounter",1,new[]{layoutId}.Concat(monsters.SelectMany(item=>new[]{item.UnitId,item.AiId}.Concat(item.SkillIds)))));}
        }
        catch(ArgumentException error){diagnostics.Add(new ContentDiagnostic("ai-encounter.invalid_contract",ContentDiagnosticSeverity.Error,error.Message));}
        return new AiEncounterCompileResult(ai,layouts,encounters,content,diagnostics);
    }
}
