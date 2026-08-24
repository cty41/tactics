using NUnit.Framework;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

/// <summary>
/// Deterministic diagnostic proxy for the Demonbound balance loop. It exercises canonical movement,
/// skill, corruption, possession and RNG transitions, but is not evidence for human play quality.
/// </summary>
public sealed class DemonboundFixedSeedBattleProbeTests
{
    private static readonly string[][] PartyCombinations =
    [
        ["mage", "amazon"],
        ["mage", "necromancer"],
        ["necromancer", "amazon"]
    ];

    [Test]
    public void ThirtyFixedSeedProxyBattlesReachATerminalStateAndExposeMetrics()
    {
        BattleProbeResult[] results = PartyCombinations
            .SelectMany(party => Enumerable.Range(0, 10).Select(seed => Run(party, (ulong)seed)))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Length.EqualTo(30));
            Assert.That(results.Select(result => (result.Party, result.Seed)).Distinct().ToArray(), Has.Length.EqualTo(30));
            Assert.That(results, Has.All.Matches<BattleProbeResult>(result => result.Completed));
            Assert.That(results, Has.All.Matches<BattleProbeResult>(result => result.Commands <= 500));
            Assert.That(results, Has.All.Matches<BattleProbeResult>(result => result.CorruptionPeak is >= 0 and <= 10));
            Assert.That(results.Sum(result => result.SkillUses), Is.GreaterThan(0));
        });

        TestContext.Progress.WriteLine("party,seed,result,rounds,commands,corruption_peak,meditations,first_possession_round,friendly_damage,downs,permanent_deaths,skill_uses");
        foreach (BattleProbeResult result in results)
            TestContext.Progress.WriteLine(result.ToCsv());
    }

    [Test]
    public void FixedSeedProxyResultsAreReplayStable()
    {
        BattleProbeResult[] first = PartyCombinations
            .SelectMany(party => Enumerable.Range(0, 10).Select(seed => Run(party, (ulong)seed)))
            .ToArray();
        BattleProbeResult[] replay = PartyCombinations
            .SelectMany(party => Enumerable.Range(0, 10).Select(seed => Run(party, (ulong)seed)))
            .ToArray();

        Assert.That(replay, Is.EqualTo(first));
    }

    private static BattleProbeResult Run(IReadOnlyList<string> allies, ulong seed)
    {
        ProbeFixture fixture = CreateFixture(allies, seed);
        BattleState state = fixture.State;
        var decisions = new AiDecisionService();
        var turns = new AiTurnService();
        var transitions = new BattleTransitionService();
        var patternIndices = fixture.Definitions.Keys.ToDictionary(id => id, _ => 0);
        var events = new List<BattleEvent>();
        int commands = 0;
        int corruptionPeak = 0;
        int? firstPossessionRound = null;

        while (!IsTerminal(state) && commands < 500)
        {
            BattleUnitState actor = state.Units[state.ActiveUnitId];
            if (!actor.IsAlive)
            {
                BattleTransition skipped = transitions.Apply(state, new EndTurnCommand(actor.Unit.InstanceId));
                state = skipped.State;
                events.AddRange(skipped.Events);
                commands++;
                continue;
            }

            DemonboundBattleState? demonbound = actor.DemonboundState;
            if (demonbound is { IsPossessed: false, Corruption: >= 5 })
            {
                BattleTransition meditation = transitions.Apply(state, new MeditateCommand(actor.Unit.InstanceId));
                if (meditation.Succeeded)
                {
                    state = meditation.State;
                    events.AddRange(meditation.Events);
                    commands++;
                    corruptionPeak = Math.Max(corruptionPeak,
                        state.Units[fixture.DemonboundId].DemonboundState?.Corruption ?? 0);
                    continue;
                }
            }

            AiDefinition definition = fixture.Definitions[actor.Unit.InstanceId];
            TargetRelationshipStrategy strategy = demonbound?.IsPossessed == true
                ? TargetRelationshipStrategy.UnifiedAll
                : TargetRelationshipStrategy.StandardHostile;
            AiTurnPlan plan = decisions.Decide(state, definition, fixture.Skills,
                patternIndices[actor.Unit.InstanceId], strategy);
            AiPlanExecutionResult executed = turns.Execute(state, plan, fixture.Skills);
            if (firstPossessionRound is null && executed.Events.OfType<DemonboundPossessedEvent>().Any())
                firstPossessionRound = state.Round;
            state = executed.State;
            patternIndices[actor.Unit.InstanceId] = executed.NextPatternIndex;
            events.AddRange(executed.Events);
            commands++;
            corruptionPeak = Math.Max(corruptionPeak,
                state.Units[fixture.DemonboundId].DemonboundState?.Corruption ?? 0);
        }

        bool enemiesAlive = state.Units.Values.Any(unit => unit.IsAlive && unit.Unit.PlayerNumber == 1);
        bool playersAlive = state.Units.Values.Any(unit => unit.IsAlive && unit.Unit.PlayerNumber == 0);
        string result = !enemiesAlive ? "player_victory" : !playersAlive ? "player_defeat" : "command_cap";
        Dictionary<UnitInstanceId, int> factions = fixture.State.Units.Values
            .ToDictionary(unit => unit.Unit.InstanceId, unit => unit.Unit.PlayerNumber);
        int friendlyDamage = events.OfType<DamageAppliedEvent>()
            .Where(value => factions[value.SourceId] == factions[value.TargetId])
            .Sum(value => value.Amount);

        return new BattleProbeResult(
            string.Join("+", allies), seed, result != "command_cap", result, state.Round, commands,
            corruptionPeak, events.OfType<MeditationUsedEvent>().Count(),
            firstPossessionRound,
            friendlyDamage, events.OfType<UnitDefeatedEvent>().Count(value => factions[value.UnitId] == 0),
            events.OfType<RunPermanentDeathRolledEvent>().Count(value => value.PermanentDeath),
            events.OfType<SkillUsedEvent>().Count());
    }

    private static bool IsTerminal(BattleState state) =>
        !state.Units.Values.Any(unit => unit.IsAlive && unit.Unit.PlayerNumber == 0) ||
        !state.Units.Values.Any(unit => unit.IsAlive && unit.Unit.PlayerNumber == 1);

    private static ProbeFixture CreateFixture(IReadOnlyList<string> allies, ulong seed)
    {
        Dictionary<GridPoint, CellState> cells = Enumerable.Range(0, BoardSpec.Width)
            .SelectMany(x => Enumerable.Range(0, BoardSpec.Height)
                .Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState())))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        SkillDefinition basicMelee = Skill("skill.basic.melee", SkillExecutionKind.MeleeAttack, 3, SkillDamageKind.Physical, 1, 1);
        SkillDefinition basicMagic = Skill("skill.basic.magic", SkillExecutionKind.MagicAttack, 3, SkillDamageKind.Magical, 1, 4);
        SkillDefinition infernal = new(new ContentId("skill.demonbound.infernal-blast.lv1"), "probe.infernal",
            SkillRole.Demonbound, SkillKind.Active, 1, 2, 1, 3, SkillExecutionKind.InfernalBlast, 4,
            SkillDamageKind.Magical, executionProfile: new SkillExecutionProfile(IgnoreLineOfSight: true, CorruptionCost: 3));
        SkillDefinition bane = new(new ContentId("skill.demonbound.bane.lv1"), "probe.bane",
            SkillRole.Demonbound, SkillKind.Active, 1, 2, 0, 0, SkillExecutionKind.Bane, 0,
            SkillDamageKind.None, new ContentId("buff.demonbound.bane-weapon"), 2,
            executionProfile: new SkillExecutionProfile(CorruptionCost: 2));
        var skills = new[] { basicMelee, basicMagic, infernal, bane }.ToDictionary(skill => skill.ContentId);

        var demonboundId = new UnitInstanceId("player.demonbound");
        int branch = (int)(seed % 3);
        ContentId[] demonboundSkills = branch switch
        {
            0 => [basicMelee.ContentId, bane.ContentId],
            1 => [basicMelee.ContentId, infernal.ContentId],
            _ => [basicMelee.ContentId]
        };
        var units = new List<BattleUnitState>
        {
            Unit(demonboundId, "unit.demonbound", new GridPoint(1, 3), 0, 0, 12, 28, 12, 4,
                demonbound: new DemonboundBattleState(mindfulnessLevel: branch == 2 ? 1 : 0))
        };
        var definitions = new Dictionary<UnitInstanceId, AiDefinition>
        {
            [demonboundId] = Ai(demonboundId, demonboundSkills)
        };

        GridPoint[] allyCells = [new GridPoint(1, 2), new GridPoint(1, 4)];
        for (int index = 0; index < allies.Count; index++)
        {
            string role = allies[index];
            var id = new UnitInstanceId($"player.{role}");
            bool magic = role is "mage" or "necromancer";
            units.Add(Unit(id, $"unit.{role}", allyCells[index], 0, index + 1, 10 - index, 24, 10,
                magic ? 4 : 3));
            definitions[id] = Ai(id, [magic ? basicMagic.ContentId : basicMelee.ContentId]);
        }

        GridPoint[] enemyCells = [new GridPoint(8, 2), new GridPoint(8, 3), new GridPoint(8, 4)];
        for (int index = 0; index < enemyCells.Length; index++)
        {
            var id = new UnitInstanceId($"enemy.{index}");
            units.Add(Unit(id, "unit.enemy", enemyCells[index], 1, index + 3, 9 - index, 30, 0, 4));
            definitions[id] = Ai(id, [basicMelee.ContentId]);
        }

        UnitInstanceId[] order = units.OrderByDescending(unit => unit.Unit.Initiative)
            .ThenBy(unit => unit.Unit.SpawnOrdinal).Select(unit => unit.Unit.InstanceId).ToArray();
        return new ProbeFixture(new BattleState(new BoardSnapshot(cells), units, order, randomState: seed),
            definitions, skills, demonboundId);
    }

    private static BattleUnitState Unit(UnitInstanceId id, string definition, GridPoint cell, int faction,
        int ordinal, float initiative, int health, int mana, int attack, DemonboundBattleState? demonbound = null) =>
        new(new UnitState(id, new ContentId(definition), cell, 3, initiative, faction, ordinal),
            health, health, maxMana: mana, currentMana: mana, physicalAttack: attack,
            magicalAttack: attack, canProduceCorpse: false, manaRecoveryPerTurn: 1,
            demonboundState: demonbound);

    private static SkillDefinition Skill(string id, SkillExecutionKind kind, int damage,
        SkillDamageKind damageKind, int minimumRange, int maximumRange) =>
        new(new ContentId(id), $"probe.{id}", SkillRole.Any, SkillKind.Basic, 1, 0,
            minimumRange, maximumRange, kind, damage, damageKind);

    private static AiDefinition Ai(UnitInstanceId id, IReadOnlyList<ContentId> skillIds) =>
        new(new ContentId($"ai.{id.Value}"), AiArchetype.Charger,
            new AiProfileDefinition(1, 2, 1, 1), skillIds, Array.Empty<ContentId>());

    private sealed record ProbeFixture(BattleState State,
        IReadOnlyDictionary<UnitInstanceId, AiDefinition> Definitions,
        IReadOnlyDictionary<ContentId, SkillDefinition> Skills,
        UnitInstanceId DemonboundId);

    private sealed record BattleProbeResult(string Party, ulong Seed, bool Completed, string Result,
        int Rounds, int Commands, int CorruptionPeak, int Meditations, int? FirstPossessionRound,
        int FriendlyDamage, int Downs, int PermanentDeaths, int SkillUses)
    {
        public string ToCsv() => string.Join(',', Party, Seed, Result, Rounds, Commands, CorruptionPeak,
            Meditations, FirstPossessionRound?.ToString() ?? string.Empty, FriendlyDamage, Downs,
            PermanentDeaths, SkillUses);
    }
}
