using System.Security.Cryptography;
using System.Text;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Application.Authoring;

public sealed record SkillBattlePreviewContext(
    string EncounterContentId,
    string CasterUnitInstanceId,
    string? TargetUnitInstanceId,
    GridCellAuthoring TargetCell,
    ulong Seed,
    string? CasterUnitContentId = null);

public sealed record SkillBattlePreviewResult(
    bool Succeeded,
    string? RejectionReason,
    string BeforeFingerprint,
    string AfterFingerprint,
    IReadOnlyList<string> Events,
    IReadOnlyDictionary<string, string> Values,
    bool SourceStateUnchanged);

public sealed class SkillBattlePreviewService
{
    private readonly BattleTransitionService _transitions;

    public SkillBattlePreviewService(BattleTransitionService? transitions = null) =>
        _transitions = transitions ?? new BattleTransitionService();

    public SkillBattlePreviewResult Preview(
        BattleState state,
        SkillDefinition definition,
        SkillBattlePreviewContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);
        string before = Fingerprint(state);
        var actorId = new UnitInstanceId(context.CasterUnitInstanceId);
        UnitInstanceId? targetId = string.IsNullOrWhiteSpace(context.TargetUnitInstanceId)
            ? null
            : new UnitInstanceId(context.TargetUnitInstanceId);
        var targetCell = new GridPoint(context.TargetCell.X, context.TargetCell.Y);
        BattleTransition transition = _transitions.Apply(state,
            new UseSkillCommand(actorId, targetId, targetCell, definition));
        string after = Fingerprint(transition.State);
        string? rejection = transition.Events.OfType<CommandRejectedEvent>().Select(value => value.Reason).FirstOrDefault();
        int beforeHealth = state.Units.Values.Sum(value => value.CurrentHealth);
        int afterHealth = transition.State.Units.Values.Sum(value => value.CurrentHealth);
        int beforeMana = state.Units.TryGetValue(actorId, out BattleUnitState? actorBefore) ? actorBefore.CurrentMana : 0;
        int afterMana = transition.State.Units.TryGetValue(actorId, out BattleUnitState? actorAfter) ? actorAfter.CurrentMana : 0;
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["encounterContentId"] = context.EncounterContentId,
            ["seed"] = context.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["manaSpent"] = Math.Max(0, beforeMana - afterMana).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["healthDelta"] = (afterHealth - beforeHealth).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["unitCountDelta"] = (transition.State.Units.Count - state.Units.Count).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["corpseCount"] = transition.State.Corpses.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["droppedSpearCount"] = transition.State.DroppedSpears.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["statusCount"] = transition.State.Units.Values.Sum(value => value.Statuses.Count).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return new SkillBattlePreviewResult(
            transition.Succeeded,
            rejection,
            before,
            after,
            Array.AsReadOnly(transition.Events.Select(SerializeEvent).ToArray()),
            values,
            string.Equals(before, Fingerprint(state), StringComparison.Ordinal));
    }

    public static string Fingerprint(BattleState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var builder = new StringBuilder();
        builder.Append("round=").Append(state.Round).Append("|active=").Append(state.ActiveUnitId.Value)
            .Append("|rng=").Append(state.RandomState).Append("|units=");
        foreach (BattleUnitState unit in state.Units.Values.OrderBy(value => value.Unit.InstanceId.Value, StringComparer.Ordinal))
        {
            builder.Append(unit.Unit.InstanceId.Value).Append(':').Append(unit.Unit.Position.X).Append(',')
                .Append(unit.Unit.Position.Y).Append(':').Append(unit.CurrentHealth).Append(':').Append(unit.CurrentMana)
                .Append(':').Append(string.Join(',', unit.Statuses.OrderBy(value => value.Key.Value, StringComparer.Ordinal)
                    .Select(value => value.Key.Value + "=" + value.Value.RemainingTurns))).Append(';');
        }
        builder.Append("|corpses=").Append(string.Join(';', state.Corpses.OrderBy(value => value.X).ThenBy(value => value.Y)
            .Select(value => $"{value.X},{value.Y}")));
        builder.Append("|spears=").Append(string.Join(';', state.DroppedSpears.OrderBy(value => value.Key.Value, StringComparer.Ordinal)
            .Select(value => $"{value.Key.Value}:{value.Value.X},{value.Value.Y}")));
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static string SerializeEvent(BattleEvent value) =>
        value.GetType().Name + ":" + value;
}
