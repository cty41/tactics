using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Pathfinding;

namespace Tactics.Core.Combat;

public readonly record struct PoisonSpearDefinition
{
    public PoisonSpearDefinition(
        ContentId skillId,
        int range,
        int damage,
        int poisonTurns,
        ContentId? poisonStatusId = null,
        int poisonDamagePerTurn = 2,
        int manaCost = 0,
        int dropSearchRadius = 3,
        int frozenPoisonTotalDamage = 0)
    {
        SkillId = skillId;
        Range = ValidatePositive(range, nameof(range));
        Damage = ValidateNonNegative(damage, nameof(damage));
        PoisonTurns = ValidateNonNegative(poisonTurns, nameof(poisonTurns));
        PoisonStatusId = poisonStatusId ?? new ContentId("buff.poison");
        PoisonDamagePerTurn = ValidateNonNegative(poisonDamagePerTurn, nameof(poisonDamagePerTurn));
        ManaCost = ValidateNonNegative(manaCost, nameof(manaCost));
        DropSearchRadius = ValidatePositive(dropSearchRadius, nameof(dropSearchRadius));
        FrozenPoisonTotalDamage = ValidateNonNegative(frozenPoisonTotalDamage, nameof(frozenPoisonTotalDamage));
    }

    public ContentId SkillId { get; init; }
    public int Range { get; init; }
    public int Damage { get; init; }
    public int PoisonTurns { get; init; }
    public ContentId PoisonStatusId { get; init; }
    public int PoisonDamagePerTurn { get; init; }
    public int ManaCost { get; init; }
    public int DropSearchRadius { get; init; }
    public int FrozenPoisonTotalDamage { get; init; }

    private static int ValidatePositive(int value, string name) =>
        value <= 0 ? throw new ArgumentOutOfRangeException(name) : value;

    private static int ValidateNonNegative(int value, string name) =>
        value < 0 ? throw new ArgumentOutOfRangeException(name) : value;
}

/// <summary>
/// Minimal deterministic Poison Spear semantic slice. Presentation is deliberately outside this resolver.
/// </summary>
public sealed class PoisonSpearResolver
{
    private readonly ILineOfSightService _lineOfSight;

    public PoisonSpearResolver(ILineOfSightService? lineOfSight = null)
    {
        _lineOfSight = lineOfSight ?? new ShadowConeLineOfSight();
    }

    public ActionResult Resolve(
        BoardSnapshot board,
        UnitState caster,
        UnitState target,
        PoisonSpearDefinition definition,
        IReadOnlySet<GridPoint>? dynamicBlockers = null)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (!caster.IsAlive || !target.IsAlive)
            return ActionResult.Failed("dead_unit");

        int distance = Math.Abs(caster.Position.X - target.Position.X) +
                      Math.Abs(caster.Position.Y - target.Position.Y);
        if (distance > definition.Range)
            return ActionResult.Failed("out_of_range");
        if (!_lineOfSight.HasLineOfSight(board, caster.Position, target.Position, dynamicBlockers))
            return ActionResult.Failed("line_of_sight_blocked");

        return new ActionResult(true, definition.Damage, definition.PoisonTurns);
    }
}
