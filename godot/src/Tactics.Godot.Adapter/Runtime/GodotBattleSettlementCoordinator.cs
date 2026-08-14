using Tactics.Application.Runs;
using Tactics.Core.Content;
using Tactics.Core.Runs;

namespace Tactics.Godot.Adapter.Runtime;

internal enum BattleSettlementStage
{
    Idle,
    Submitting,
    Rejected,
    Saved,
    NavigationCompleted
}

internal sealed record BattleSettlementDiagnostic(
    long AttemptId,
    BattleSettlementStage Stage,
    ContentId EncounterContentId,
    long CheckpointRevision,
    long? SavedRevision,
    string Marker,
    string? ErrorCode);

/// <summary>Tracks one terminal settlement without participating in run resolution.</summary>
internal sealed class GodotBattleSettlementCoordinator
{
    private long _nextAttemptId;

    public BattleSettlementDiagnostic? Current { get; private set; }

    public bool TryBegin(PureRunBattleResult result, string marker, out BattleSettlementDiagnostic diagnostic)
    {
        if (Current is not null)
        {
            diagnostic = Current with { Marker = $"duplicate:{marker}" };
            return false;
        }

        diagnostic = new BattleSettlementDiagnostic(++_nextAttemptId, BattleSettlementStage.Submitting,
            result.EncounterContentId, result.CheckpointRevision, null, marker, null);
        Current = diagnostic;
        return true;
    }

    public BattleSettlementDiagnostic Reject(string errorCode)
    {
        Current = RequireCurrent() with { Stage = BattleSettlementStage.Rejected, ErrorCode = errorCode };
        return Current;
    }

    public BattleSettlementDiagnostic MarkSaved(long revision)
    {
        Current = RequireCurrent() with { Stage = BattleSettlementStage.Saved, SavedRevision = revision };
        return Current;
    }

    public BattleSettlementDiagnostic MarkNavigationCompleted()
    {
        Current = RequireCurrent() with { Stage = BattleSettlementStage.NavigationCompleted };
        return Current;
    }

    public BattleSettlementDiagnostic MarkNavigationFailure(string errorCode)
    {
        Current = RequireCurrent() with { ErrorCode = errorCode };
        return Current;
    }

    public void Reset() => Current = null;

    private BattleSettlementDiagnostic RequireCurrent() =>
        Current ?? throw new InvalidOperationException("Settlement has not started.");
}
