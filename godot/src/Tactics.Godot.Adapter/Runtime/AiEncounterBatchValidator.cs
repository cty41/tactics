using Godot;

namespace Tactics.Godot.Adapter.Runtime;

public sealed record AiEncounterBatchValidation(int BatchCount,int GlobalCount,int Skills,int Ai,int Layouts,int Encounters);

public static class AiEncounterBatchValidator
{
    public static AiEncounterBatchValidation Validate(GodotResourceCatalog batch,GodotResourceCatalog global)
    {
        batch.Validate();global.Validate();if(batch.Entries.Length!=15||global.Entries.Length is not (74 or 101 or 108 or 114 or 115 or 116 or 119))throw new InvalidOperationException("AI/Encounter or canonical Catalog count is invalid.");
        int skills=0,ai=0,layouts=0,encounters=0;foreach(GodotResourceEntry entry in batch.Entries){Resource value=ResourceLoader.Load(entry.DiagnosticPathValue,string.Empty,ResourceLoader.CacheMode.Ignore)??throw new InvalidOperationException($"Missing AI/Encounter Resource: {entry.ContentIdValue}");switch(value){case SkillDefinitionResource skill: _=skill.ToCoreDefinition();skills++;break;case AiDefinitionResource definition when definition.ContentIdValue==entry.ContentIdValue:ai++;break;case BattleLayoutResource layout when layout.ContentIdValue==entry.ContentIdValue:layouts++;break;case EncounterDefinitionResource encounter when encounter.ContentIdValue==entry.ContentIdValue:encounters++;break;default:throw new InvalidOperationException($"Wrong Resource type: {entry.ContentIdValue}");}}
        if(skills!=4||ai!=6||layouts!=2||encounters!=3)throw new InvalidOperationException("AI/Encounter Resource category count is invalid.");return new(15,global.Entries.Length,skills,ai,layouts,encounters);
    }
}
