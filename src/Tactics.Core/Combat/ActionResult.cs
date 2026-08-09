namespace Tactics.Core.Combat;

public readonly record struct ActionResult(
    bool Succeeded,
    int Damage,
    int PoisonTurns,
    string FailureReason = "")
{
    public static ActionResult Failed(string reason) => new(false, 0, 0, reason);
}
