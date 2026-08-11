using Tactics.Application.Runs;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Skills;
using Tactics.Core.Turns;
using Tactics.Core.Units;

namespace Tactics.Application.Battle;

/// <summary>Composes migrated catalogs into a deterministic playable battle session.</summary>
public sealed class PlayableBattleSessionFactory
{
    private static readonly ContentId MagicAttackId = new("skill.basic.magic");
    private static readonly ContentId MeleeAttackId = new("skill.basic.melee");
    private static readonly ContentId PickupSpearId = new("skill.amazon.pickup-spear.lv1");
    private static readonly ContentId CombatTechniquesId = new("skill.amazon.combat-techniques.lv1");
    private readonly EncounterResolver _encounters = new();

    public PlayableBattleSessionService Create(
        EncounterRequest request,
        EncounterDefinition encounter,
        BattleLayoutDefinition layout,
        IReadOnlyDictionary<ContentId, UnitDefinition> units,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills,
        IReadOnlyDictionary<ContentId, AiDefinition> aiDefinitions,
        PlayableBattleBalanceProfile? balance = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EncounterContentId != encounter.ContentId)
            throw new ArgumentException("Encounter request does not match the supplied definition.", nameof(encounter));
        ResolvedEncounter resolved = _encounters.Resolve(encounter, layout);
        if (request.Party.Count > layout.PartySpawns.Count)
            throw new ArgumentException("Party exceeds layout spawn capacity.", nameof(request));

        var states = new List<BattleUnitState>();
        var skillsByUnit = new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>();
        var aiByUnit = new Dictionary<UnitInstanceId, AiDefinition>();
        var characterIds = new Dictionary<UnitInstanceId, string>();
        for (int index = 0; index < request.Party.Count; index++)
        {
            RunCharacterState character = request.Party[index];
            UnitDefinition definition = units[character.UnitContentId];
            var instanceId = new UnitInstanceId($"party-{character.CharacterId}");
            BattleUnitState state = CreatePartyState(definition, character, instanceId, layout.PartySpawns[index], index, balance);
            states.Add(state);
            characterIds.Add(instanceId, character.CharacterId);
            ContentId basicId = definition.RoleId.Contains("amazon", StringComparison.OrdinalIgnoreCase)
                ? MeleeAttackId
                : MagicAttackId;
            IEnumerable<ContentId> learned=new[] { basicId }.Concat(character.LearnedSkills);
            if(definition.RoleId.Contains("amazon",StringComparison.OrdinalIgnoreCase))learned=learned.Append(PickupSpearId);
            skillsByUnit.Add(instanceId, learned.Distinct()
                .Select(id => balance?.Apply(skills[id]) ?? skills[id]).ToArray());
        }

        for (int index = 0; index < resolved.Enemies.Count; index++)
        {
            (EncounterMonsterDefinition monster, GridPoint cell) = resolved.Enemies[index];
            var instanceId = new UnitInstanceId($"enemy-{index:D2}");
            states.Add(units[monster.UnitId].CreateBattleState(instanceId, cell, 1, request.Party.Count + index));
            skillsByUnit.Add(instanceId, monster.SkillIds.Select(id => skills[id]).ToArray());
            aiByUnit.Add(instanceId, aiDefinitions[monster.AiId]);
        }

        var cells = new Dictionary<GridPoint, CellState>();
        for (int x = 0; x < 10; x++)
        for (int y = 0; y < 10; y++)
        {
            var cell = new GridPoint(x, y);
            bool blocked = layout.BlockedCells.Contains(cell);
            cells[cell] = new CellState(blocksMovement: blocked, blocksLineOfSight: blocked);
        }
        UnitInstanceId[] order = InitiativeOrder.Sort(states.Select(state => new InitiativeEntry(
            state.Unit.InstanceId, state.Unit.Initiative, state.Unit.PlayerNumber, state.Unit.SpawnOrdinal)))
            .Select(entry => entry.UnitId).ToArray();
        var battle = new BattleState(new BoardSnapshot(cells), states, order,
            randomState: unchecked((ulong)request.CheckpointRevision));
        return new PlayableBattleSessionService(new PlayableBattleSessionContext(
            battle, 0, skillsByUnit, aiByUnit, skills, request, characterIds));
    }

    private static BattleUnitState CreatePartyState(
        UnitDefinition definition,
        RunCharacterState character,
        UnitInstanceId instanceId,
        GridPoint cell,
        int spawnOrdinal,
        PlayableBattleBalanceProfile? balance)
    {
        var facts = new UnitState(
            instanceId, definition.ContentId, cell, definition.DerivedStats.MoveRange,
            definition.DerivedStats.Initiative, 0, spawnOrdinal, !character.IsDead);
        IReadOnlyDictionary<ItemInstanceId, BattleConsumableState> consumables = character.CarriedConsumables
            .ToDictionary(item => item.InstanceId);
        (int physical, int magical) = balance?.Attacks(definition.ContentId) ?? (2, 2);
        BattleUnitState state=new BattleUnitState(
            facts, character.MaxHealth, character.CurrentHealth,
            maxMana: character.MaxMana, currentMana: character.CurrentMana,
            baseSpeed: definition.Speed, consumables: consumables,
            physicalAttack: physical, magicalAttack: magical,
            canProduceCorpse: definition.CanProduceCorpse,
            manaRecoveryPerTurn: character.Attributes.Intelligence);
        return character.LearnedSkills.Contains(CombatTechniquesId)?state.WithCombatTechniquesLevelOne(true):state;
    }
}
