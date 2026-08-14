using Tactics.Application.Battle;
using Tactics.Application.Presentation;
using Tactics.Application.Runs;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Optional pre-tree dependency seam used by the isolated gameplay-spec runner.</summary>
public sealed record GodotPlayableRunTestContext(
    IRunSaveStore SaveStore,
    int FixedSeed,
    string CheckpointId,
    bool InterceptQuit = true,
    float InitialPlaybackSpeed = 1f);

/// <summary>Read-only runtime evidence exposed to the test-host assembly.</summary>
public sealed record GodotPlayableRunProbe(
    string PageTitle,
    PureRunSaveSnapshot? SaveSnapshot,
    BattleUiSnapshot? BattleSnapshot,
    bool BattleActive,
    bool PresentationLocked,
    bool PresentationPlaying,
    bool AutomaticFramesPending,
    int PresentationNumberCount,
    IReadOnlyList<BattlePresentationNumber> PresentationNumbers,
    bool PlaybackPaused,
    float PlaybackSpeed,
    bool PauseMenuVisible,
    bool CheatConsoleVisible,
    int RuntimeErrorCount,
    bool QuitRequested,
    string? StatusText);

public sealed record GodotBattleUnitProjection(UnitInstanceId UnitId, int MaxHealth, int MaxMana,
    float BaseSpeed, int PhysicalAttack, int MagicalAttack);
