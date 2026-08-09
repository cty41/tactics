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
        int dropSearchRadius = 3)
    {
        SkillId = skillId;
        Range = ValidatePositive(range, nameof(range));
        Damage = ValidateNonNegative(damage, nameof(damage));
        PoisonTurns = ValidateNonNegative(poisonTurns, nameof(poisonTurns));
        PoisonStatusId = poisonStatusId ?? new ContentId("buff.poison");
        PoisonDamagePerTurn = ValidateNonNegative(poisonDamagePerTurn, nameof(poisonDamagePerTurn));
        ManaCost = ValidateNonNegative(manaCost, nameof(manaCost));
        DropSearchRadius = ValidatePositive(dropSearchRadius, nameof(dropSearchRadius));
    }

    public ContentId SkillId { get; init; }
    public int Range { get; init; }
    public int Damage { get; init; }
    public int PoisonTurns { get; init; }
    public ContentId PoisonStatusId { get; init; }
    public int PoisonDamagePerTurn { get; init; }
    public int ManaCost { get; init; }
    public int DropSearchRadius { get; init; }

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
        _lineOfSight = lineOfSight ?? new SupercoverLineOfSight();
    }

    public ActionResult Resolve(
        BoardSnapshot board,
        UnitState caster,
        UnitState target,
        PoisonSpearDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (!caster.IsAlive || !target.IsAlive)
            return ActionResult.Failed("dead_unit");

        int distance = Math.Abs(caster.Position.X - target.Position.X) +
                      Math.Abs(caster.Position.Y - target.Position.Y);
        if (distance > definition.Range)
            return ActionResult.Failed("out_of_range");
        if (!_lineOfSight.HasLineOfSight(board, caster.Position, target.Position))
            return ActionResult.Failed("blocked_line_of_sight");

        return new ActionResult(true, definition.Damage, definition.PoisonTurns);
    }
}
