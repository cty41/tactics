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
    string? StatusText,
    GodotAdventureRuntimeProbe? Adventure);

public sealed record GodotAdventureRuntimeProbe(
    string BoardContentId,
    string? LeaderId,
    IReadOnlyDictionary<string, string> ActorCells,
    IReadOnlyDictionary<string, string> ObjectStates,
    IReadOnlyList<string> RouteCandidateNodeIds,
    string? NodeLifecycle,
    string? EventResolution,
    string? PendingBattleContextKind,
    string? EscortState,
    bool? ProtectedNpcAlive,
    int StoreOfferCount,
    int StoreSoldOfferCount,
    int LeaderRevision,
    int InteractionRevision,
    int RouteRevision,
    int SceneRevision);

public sealed record GodotBattleUnitProjection(UnitInstanceId UnitId, int MaxHealth, int MaxMana,
    float BaseSpeed, int PhysicalAttack, int MagicalAttack, int MoveRange, float Initiative,
    int ManaRecoveryPerTurn);

public sealed record GodotInventoryBattleProjectionEvidence(string CharacterId, int EquipmentCount,
    int BaseMaxHealth, int ProjectedMaxHealth, int BattleMaxHealth, int BaseMaxMana, int ProjectedMaxMana,
    int BattleMaxMana, int ProjectedMoveRange, int BattleMoveRange, float ProjectedInitiative,
    float BattleInitiative, int ProjectedManaRecovery, int BattleManaRecovery, bool Matches);
