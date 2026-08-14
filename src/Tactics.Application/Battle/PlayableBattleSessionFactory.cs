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
    private static readonly ContentId SkeletonUnitId = new("unit.pure-run.skeleton-warrior");
    private static readonly ContentId SkeletonMageUnitId = new("unit.pure-run.skeleton-mage");
    private static readonly ContentId FireDemonUnitId = new("unit.pure-run.fire-demon");
    private readonly EncounterResolver _encounters = new();

    public PlayableBattleSessionService Create(
        EncounterRequest request,
        EncounterDefinition encounter,
        BattleLayoutDefinition layout,
        IReadOnlyDictionary<ContentId, UnitDefinition> units,
        IReadOnlyDictionary<ContentId, SkillDefinition> skills,
        IReadOnlyDictionary<ContentId, AiDefinition> aiDefinitions,
        PlayableBattleBalanceProfile? balance = null,
        PlayableEnemySpeedProfile? enemySpeed = null,
        IReadOnlyDictionary<ContentId, EquipmentDefinition>? equipment = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EncounterContentId != encounter.ContentId)
            throw new ArgumentException("Encounter request does not match the supplied definition.", nameof(encounter));
        ResolvedEncounter resolved = _encounters.Resolve(encounter, layout);
        if (request.Party.Count > layout.PartySpawns.Count)
            throw new ArgumentException("Party exceeds layout spawn capacity.", nameof(request));
        IReadOnlyDictionary<ContentId, SkillDefinition> playableSkills = skills.ToDictionary(
            item => item.Key, item => balance?.Apply(item.Value) ?? item.Value);

        var states = new List<BattleUnitState>();
        var skillsByUnit = new Dictionary<UnitInstanceId, IReadOnlyList<SkillDefinition>>();
        var aiByUnit = new Dictionary<UnitInstanceId, AiDefinition>();
        var characterIds = new Dictionary<UnitInstanceId, string>();
        for (int index = 0; index < request.Party.Count; index++)
        {
            RunCharacterState character = request.Party[index];
            UnitDefinition definition = units[character.UnitContentId];
            var instanceId = new UnitInstanceId($"party-{character.CharacterId}");
            BattleUnitState state = CreatePartyState(definition, character, instanceId, layout.PartySpawns[index], index,
                balance, equipment);
            states.Add(state);
            characterIds.Add(instanceId, character.CharacterId);
            ContentId basicId = definition.RoleId.Contains("amazon", StringComparison.OrdinalIgnoreCase)
                ? MeleeAttackId
                : MagicAttackId;
            IEnumerable<ContentId> learned=new[] { basicId }.Concat(character.LearnedSkills);
            if(definition.RoleId.Contains("amazon",StringComparison.OrdinalIgnoreCase))learned=learned.Append(PickupSpearId);
            skillsByUnit.Add(instanceId, learned.Distinct()
                .Select(id => playableSkills[id]).ToArray());
        }

        for (int index = 0; index < resolved.Enemies.Count; index++)
        {
            (EncounterMonsterDefinition monster, GridPoint cell) = resolved.Enemies[index];
            var instanceId = new UnitInstanceId($"enemy-{index:D2}");
            UnitDefinition enemyDefinition = units[monster.UnitId];
            BattleUnitState enemy = enemyDefinition.CreateBattleState(instanceId, cell, 1, request.Party.Count + index);
            if (enemySpeed is not null)
                enemy = enemy.WithBaseSpeed(enemySpeed.Speed(monster.UnitId, enemyDefinition.Speed));
            int scaledHealth = (int)Math.Ceiling(enemy.MaxHealth * encounter.HealthMultiplier);
            enemy = enemy.WithHealthAndMana(scaledHealth, scaledHealth,
                enemy.MaxMana, Math.Min(enemy.MaxMana, Math.Max(enemy.CurrentMana, encounter.MinimumStartingMana)))
                .WithDamageOutputMultiplier(encounter.OutputMultiplier);
            states.Add(enemy);
            skillsByUnit.Add(instanceId, monster.SkillIds.Select(id => playableSkills[id]).ToArray());
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
        var summonControllers = new Dictionary<ContentId, SummonControllerDefinition>
        {
            [SkeletonUnitId] = new(aiDefinitions[new ContentId("ai.summon.basic-melee")],
                Levels(playableSkills, "skill.summon.skeleton-attack"), SkillExecutionKind.SummonSkeleton),
            [SkeletonMageUnitId] = new(aiDefinitions[new ContentId("ai.summon.fire-demon")],
                Levels(playableSkills, "skill.summon.skeleton-mage-fireball"), SkillExecutionKind.SummonSkeletonMage),
            [FireDemonUnitId] = new(aiDefinitions[new ContentId("ai.summon.fire-demon")],
                new Dictionary<int, SkillDefinition>
                {
                    [1] = playableSkills[new ContentId("skill.summon.fire-demon-attack")],
                    [2] = playableSkills[new ContentId("skill.summon.fire-demon-attack")]
                }, SkillExecutionKind.SummonFireDemon)
        };
        return new PlayableBattleSessionService(new PlayableBattleSessionContext(
            battle, 0, skillsByUnit, aiByUnit, playableSkills, request, characterIds, layout.BlockedCells,
            summonControllers));
    }

    private static IReadOnlyDictionary<int, SkillDefinition> Levels(
        IReadOnlyDictionary<ContentId, SkillDefinition> skills, string prefix) => new Dictionary<int, SkillDefinition>
    {
        [1] = skills[new ContentId(prefix + ".lv1")],
        [2] = skills[new ContentId(prefix + ".lv2")]
    };

    private static BattleUnitState CreatePartyState(
        UnitDefinition definition,
        RunCharacterState character,
        UnitInstanceId instanceId,
        GridPoint cell,
        int spawnOrdinal,
        PlayableBattleBalanceProfile? balance,
        IReadOnlyDictionary<ContentId, EquipmentDefinition>? equipment)
    {
        EquipmentDefinition[] loadout = character.Equipment.Select(item =>
            equipment?.GetValueOrDefault(item.DefinitionId) ??
            throw new ArgumentException($"Equipment definition '{item.DefinitionId}' is unavailable.", nameof(equipment)))
            .ToArray();
        EquipmentStatProjection projection = EquipmentStatProjector.Project(character.Attributes, definition.Speed, loadout);
        var facts = new UnitState(
            instanceId, definition.ContentId, cell, projection.DerivedStats.MoveRange,
            projection.DerivedStats.Initiative, 0, spawnOrdinal, !character.IsDead);
        IReadOnlyDictionary<ItemInstanceId, BattleConsumableState> consumables = character.CarriedConsumables
            .ToDictionary(item => item.InstanceId);
        (int physical, int magical) = balance?.Attacks(definition.ContentId) ?? (2, 2);
        BattleUnitState state=new BattleUnitState(
            facts, character.MaxHealth, character.CurrentHealth,
            maxMana: character.MaxMana, currentMana: character.CurrentMana,
            baseSpeed: definition.Speed, consumables: consumables,
            physicalAttack: physical, magicalAttack: magical,
            canProduceCorpse: definition.CanProduceCorpse,
            manaRecoveryPerTurn: projection.Attributes.Intelligence);
        int combatTechniquesLevel = character.LearnedSkills
            .Where(id => id.Value.StartsWith("skill.amazon.combat-techniques.lv", StringComparison.Ordinal))
            .Select(id => id.Value.EndsWith("lv2", StringComparison.Ordinal) ? 2 : 1)
            .DefaultIfEmpty(0).Max();
        return combatTechniquesLevel > 0 ? state.WithCombatTechniquesLevel(combatTechniquesLevel) : state;
    }
}
