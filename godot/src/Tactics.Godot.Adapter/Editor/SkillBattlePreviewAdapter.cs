#if TOOLS
using Godot;
using Tactics.Application.Authoring;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Skills;
using Tactics.Core.Units;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

internal sealed class SkillBattlePreviewAdapter
{
    public SkillBattlePreviewResult Preview(
        GodotResourceCatalog catalog,
        SkillAuthoringDocument skill,
        SkillBattlePreviewContext context)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(context);
        Dictionary<string, GodotResourceEntry> entries = catalog.Entries
            .ToDictionary(value => value.ContentIdValue, StringComparer.Ordinal);
        EncounterDefinitionResource encounterResource = Load<EncounterDefinitionResource>(entries, context.EncounterContentId);
        EncounterDefinition encounter = EncounterAuthoringEditorService.Read(encounterResource).ToCoreDefinition();
        BattleLayoutResource layoutResource = Load<BattleLayoutResource>(entries, encounter.LayoutId.Value);
        BattleLayoutDefinition layout = layoutResource.ToCoreDefinition();
        string casterContentId = context.CasterUnitContentId ?? "unit.pure-run.amazon";
        UnitDefinitionResource casterResource = Load<UnitDefinitionResource>(entries, casterContentId);
        var actorId = new UnitInstanceId(context.CasterUnitInstanceId);
        var units = new List<BattleUnitState>
        {
            EnsurePreviewMana(GodotUnitFactory.CreateBattleState(casterResource, actorId, layout.PartySpawns[0], 0, 0))
        };
        for (int index = 0; index < encounter.Monsters.Count; index++)
        {
            EncounterMonsterDefinition monster = encounter.Monsters[index];
            UnitDefinitionResource enemyResource = Load<UnitDefinitionResource>(entries, monster.UnitId.Value);
            units.Add(GodotUnitFactory.CreateBattleState(enemyResource, new UnitInstanceId($"preview.enemy.{index}"),
                layout.EnemySpawns[index], 1, index));
        }
        Dictionary<GridPoint, CellState> cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height).Select(y =>
            {
                var cell = new GridPoint(x, y);
                return new KeyValuePair<GridPoint, CellState>(cell,
                    new CellState(blocksMovement: layout.BlockedCells.Contains(cell)));
            })).ToDictionary();
        BattleState state = new(new BoardSnapshot(cells), units,
            units.Select(value => value.Unit.InstanceId).ToArray(), randomState: context.Seed);
        GridPoint targetCell = new(context.TargetCell.X, context.TargetCell.Y);
        SkillDefinition definition = skill.Definition;
        if (definition.ExecutionKind is SkillExecutionKind.SummonSkeleton or SkillExecutionKind.SummonSkeletonMage)
            state = state.WithCorpse(targetCell);
        if (definition.ExecutionKind == SkillExecutionKind.PickupSpear)
            state = state.WithDroppedSpear(actorId, targetCell);
        return new SkillBattlePreviewService().Preview(state, definition, context);
    }

    private static TResource Load<TResource>(IReadOnlyDictionary<string, GodotResourceEntry> entries, string contentId)
        where TResource : Resource
    {
        if (!entries.TryGetValue(contentId, out GodotResourceEntry? entry))
            throw new InvalidOperationException($"Preview ContentId '{contentId}' is missing from the Catalog.");
        return ResourceLoader.Load<TResource>(entry.DiagnosticPathValue, string.Empty, ResourceLoader.CacheMode.Ignore)
            ?? throw new InvalidOperationException($"Preview Resource '{contentId}' is not a {typeof(TResource).Name}.");
    }

    private static BattleUnitState EnsurePreviewMana(BattleUnitState source)
    {
        if (source.MaxMana >= 30 && source.CurrentMana >= 30) return source;
        return new BattleUnitState(source.Unit, source.MaxHealth, source.CurrentHealth, source.HasMovedThisTurn,
            maxMana: Math.Max(30, source.MaxMana), currentMana: Math.Max(30, source.CurrentMana),
            statuses: source.Statuses, baseSpeed: source.BaseSpeed, physicalAttack: source.PhysicalAttack,
            magicalAttack: source.MagicalAttack, canReceiveStandardHealing: source.CanReceiveStandardHealing,
            hasCombatTechniquesLevelOne: source.HasCombatTechniquesLevelOne,
            canProduceCorpse: source.CanProduceCorpse, successfulSkillUses: source.SuccessfulSkillUses,
            manaRecoveryPerTurn: source.ManaRecoveryPerTurn, summonCategory: source.SummonCategory,
            combatTechniquesLevel: source.CombatTechniquesLevel, damageShield: source.DamageShield,
            movementCellsThisTurn: source.MovementCellsThisTurn);
    }
}
#endif
