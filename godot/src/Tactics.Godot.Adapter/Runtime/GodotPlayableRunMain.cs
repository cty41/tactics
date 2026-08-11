using Godot;
using Tactics.Application.Battle;
using Tactics.Application.Runs;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Runs;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Native 1600x900 Home -> N1/N2/N3 -> Summary playable flow.</summary>
public partial class GodotPlayableRunMain : Control
{
    public const int CanvasWidth = 1600;
    public const int CanvasHeight = 900;
    private const float CellSize = 68f;
    private static readonly Vector2 BoardOrigin = new(90, 115);
    private readonly Dictionary<ContentId, UnitDefinition> _units = new();
    private readonly Dictionary<ContentId, UnitDefinitionResource> _unitResources = new();
    private readonly Dictionary<ContentId, SkillDefinition> _skills = new();
    private readonly Dictionary<ContentId, AiDefinition> _ai = new();
    private readonly Dictionary<ContentId, BattleLayoutDefinition> _layouts = new();
    private readonly Dictionary<ContentId, EncounterDefinition> _encounters = new();
    private readonly Dictionary<UnitInstanceId, GodotUnitActor> _actors = new();
    private PureRunSessionService? _run;
    private PlayableBattleSessionService? _battle;
    private Control? _page;
    private Label? _status;
    private VBoxContainer? _skillPanel;
    private Control? _board;

    public bool IsReadyForInput => _run is not null && _page is not null && _units.Count == 12 &&
        _skills.Count >= 16 && _ai.Count == 6 && _layouts.Count == 2 && _encounters.Count == 3;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        LoadCatalogs();
        ShowHome();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_battle is null) return;
        if (inputEvent is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Escape) ApplyIntent(new CancelTargetingIntent());
            else if (key.Keycode is Key.Enter or Key.KpEnter) ApplyIntent(new EndTurnIntent());
        }
        else if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
            ApplyIntent(new CancelTargetingIntent());
    }

    private void LoadCatalogs()
    {
        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres")
            ?? throw new InvalidOperationException("Canonical Catalog is missing.");
        PureRunDefinitionResource? runResource = null;
        foreach (GodotResourceEntry entry in catalog.Entries)
        {
            Resource resource = ResourceLoader.Load(entry.ResourceLocator)
                ?? throw new InvalidOperationException($"Missing canonical resource: {entry.ContentIdValue}");
            var id = new ContentId(entry.ContentIdValue);
            switch (resource)
            {
                case UnitDefinitionResource unit:
                    _unitResources[id] = unit; _units[id] = unit.ToCoreDefinition(); break;
                case SkillDefinitionResource skill:
                    _skills[id] = skill.ToCoreDefinition(); break;
                case PoisonSpearSkillResource poison:
                    _skills[id] = new SkillDefinition(id, "amazon.poison_spear", SkillRole.Amazon, SkillKind.Active, 1,
                        poison.ManaCost, 1, poison.Range, SkillExecutionKind.PoisonSpear, poison.Damage,
                        SkillDamageKind.Physical, new ContentId("buff.poison"), poison.PoisonTurns, externalDependency: true); break;
                case AiDefinitionResource ai: _ai[id] = ai.ToCoreDefinition(); break;
                case BattleLayoutResource layout: _layouts[id] = layout.ToCoreDefinition(); break;
                case EncounterDefinitionResource encounter:
                    _encounters[id] = new EncounterDefinition(id, new ContentId(encounter.LayoutContentId),
                        Enumerable.Range(0, encounter.MonsterUnitContentIds.Length).Select(index =>
                            new EncounterMonsterDefinition(new ContentId(encounter.MonsterUnitContentIds[index]),
                                new ContentId(encounter.MonsterAiContentIds[index]),
                                _ai.TryGetValue(new ContentId(encounter.MonsterAiContentIds[index]), out AiDefinition? definition)
                                    ? definition.SkillIds : Array.Empty<ContentId>())).ToArray()); break;
                case PureRunDefinitionResource run: runResource = run; break;
            }
        }
        // Encounter resources can sort before AI entries in the canonical catalog; rebuild their skill bindings now.
        foreach (GodotResourceEntry entry in catalog.Entries.Where(value =>
                     value.ContentIdValue.StartsWith("encounter.pure-run.", StringComparison.Ordinal)))
        {
            var resource = ResourceLoader.Load<EncounterDefinitionResource>(entry.ResourceLocator)!;
            var id = new ContentId(entry.ContentIdValue);
            _encounters[id] = new EncounterDefinition(id, new ContentId(resource.LayoutContentId),
                Enumerable.Range(0, resource.MonsterUnitContentIds.Length).Select(index =>
                {
                    var aiId = new ContentId(resource.MonsterAiContentIds[index]);
                    return new EncounterMonsterDefinition(new ContentId(resource.MonsterUnitContentIds[index]), aiId, _ai[aiId].SkillIds);
                }).ToArray());
        }
        _run = new PureRunSessionService((runResource ?? throw new InvalidOperationException("Run definition is missing.")).ToCoreDefinition(), new GodotRunSaveStore());
    }

    private void ShowHome()
    {
        _battle = null;
        Control root = NewPage("PURE RUN", "Three-encounter Godot vertical slice");
        VBoxContainer menu = new() { Position = new Vector2(620, 310), Size = new Vector2(360, 320) };
        root.AddChild(menu);
        Button newRun = Button("New Run", () => StartNewRun()); menu.AddChild(newRun);
        RunStoreResult loaded = new GodotRunSaveStore().Load();
        Button continueRun = Button("Continue", ContinueRun); continueRun.Disabled = !loaded.Succeeded || loaded.Snapshot?.ActiveRun is null; menu.AddChild(continueRun);
        menu.AddChild(Button("Quit", () => GetTree().Quit()));
        _status = LabelAt(root, loaded.Snapshot?.ActiveRun is null ? "No active run" : $"Active run: {loaded.Snapshot.ActiveRun.EncounterContentId.Value}", new Vector2(620, 560), 22);
    }

    private void StartNewRun()
    {
        RunStoreResult loaded = new GodotRunSaveStore().Load();
        if (loaded.Snapshot?.ActiveRun is not null)
        {
            var confirm = new ConfirmationDialog { DialogText = "Overwrite the active Pure Run?", Title = "New Run" };
            AddChild(confirm); confirm.Confirmed += () => { confirm.QueueFree(); StartNewRunConfirmed(); }; confirm.Canceled += confirm.QueueFree; confirm.PopupCentered();
            return;
        }
        StartNewRunConfirmed();
    }

    private void StartNewRunConfirmed()
    {
        RunSessionResult started = _run!.StartNewRun(7);
        if (!started.Succeeded) { SetStatus(started.ErrorCode); return; }
        BeginReadyEncounter();
    }

    private void ContinueRun()
    {
        RunSessionResult resumed = _run!.ResumeRun();
        if (!resumed.Succeeded) { SetStatus(resumed.ErrorCode); return; }
        if (resumed.EncounterRequest is EncounterRequest request) StartBattle(request);
        else BeginReadyEncounter();
    }

    private void BeginReadyEncounter()
    {
        RunSessionResult begun = _run!.BeginEncounter();
        if (!begun.Succeeded || begun.EncounterRequest is null) { SetStatus(begun.ErrorCode); return; }
        StartBattle(begun.EncounterRequest);
    }

    private void StartBattle(EncounterRequest request)
    {
        EncounterDefinition encounter = _encounters[request.EncounterContentId];
        _battle = new PlayableBattleSessionFactory().Create(request, encounter, _layouts[encounter.LayoutId], _units, _skills, _ai);
        BuildBattlePage();
    }

    private void BuildBattlePage()
    {
        Control root = NewPage("PURE RUN BATTLE", "Left click: select/confirm   Right click or Esc: cancel   Enter: end turn");
        _board = new Control { Position = BoardOrigin, Size = new Vector2(CellSize * 10, CellSize * 10) }; root.AddChild(_board);
        for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++)
        {
            GridPoint cell = new(x, y);
            var button = new Button { Position = new Vector2(x * CellSize, y * CellSize), Size = new Vector2(CellSize, CellSize), Text = $"{x},{y}", Modulate = new Color(0.75f, 0.82f, 0.86f, 0.75f) };
            button.AddThemeFontSizeOverride("font_size", 12); button.Pressed += () => ApplyIntent(new ConfirmCellIntent(cell)); _board.AddChild(button);
        }
        _skillPanel = new VBoxContainer { Position = new Vector2(840, 145), Size = new Vector2(660, 610) }; root.AddChild(_skillPanel);
        _status = LabelAt(root, string.Empty, new Vector2(840, 760), 18); _status.Size = new Vector2(680, 110); _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        RefreshBattle();
    }

    private void RefreshBattle()
    {
        if (_battle is null || _board is null || _skillPanel is null) return;
        BattleUiSnapshot snapshot = _battle.CaptureSnapshot();
        foreach (GodotUnitActor actor in _actors.Values) actor.QueueFree(); _actors.Clear();
        foreach (BattleUiUnitSnapshot unit in snapshot.Units)
        {
            GodotUnitActor actor = GodotUnitFactory.InstantiateActor(_unitResources[unit.DefinitionId]);
            actor.Position = new Vector2((unit.Cell.X + .5f) * CellSize, (unit.Cell.Y + .72f) * CellSize);
            actor.Scale = Vector2.One * .34f; actor.SetDeathVisual(!unit.IsAlive); _board.AddChild(actor); _actors[unit.UnitId] = actor;
        }
        foreach (Node child in _skillPanel.GetChildren()) child.QueueFree();
        _skillPanel.AddChild(Label($"Round {snapshot.Round} | Active {snapshot.ActiveUnitId.Value}\nMode {snapshot.TargetingMode} | {snapshot.Phase}", 25));
        _skillPanel.AddChild(Button("Move", () => ApplyIntent(new BeginMoveIntent())));
        foreach (SkillDefinition skill in snapshot.ActiveSkills.Where(skill => !skill.Hidden))
            _skillPanel.AddChild(Button($"{skill.ContentId.Value}  MP {skill.ManaCost}", () => ApplyIntent(new SelectSkillIntent(skill.ContentId))));
        _skillPanel.AddChild(Button("End Turn (Enter)", () => ApplyIntent(new EndTurnIntent())));
        _skillPanel.AddChild(Button("Abandon Run", AbandonRun));
        string units = string.Join('\n', snapshot.Units.Select(unit => $"{unit.UnitId.Value}: HP {unit.CurrentHealth}/{unit.MaxHealth} MP {unit.CurrentMana}/{unit.MaxMana} {string.Join(',', unit.StatusIds.Select(id => id.Value))}"));
        string events = string.Join(" -> ", snapshot.RecentEvents.TakeLast(8).Select(value => value.GetType().Name));
        SetStatus($"{units}\nEvents: {events}\n{snapshot.FailureCode}");
    }

    private void ApplyIntent(BattleUiIntent intent)
    {
        if (_battle is null) return;
        BattleUiIntentResult result = _battle.Submit(intent);
        if (result.BattleResult is PureRunBattleResult battleResult)
        {
            RunSessionResult settled = _run!.ApplyBattleResult(battleResult);
            if (!settled.Succeeded) { SetStatus(settled.ErrorCode); return; }
            ShowSettlement(settled.Snapshot!); return;
        }
        RefreshBattle();
        if (!result.Succeeded) SetStatus(result.FailureCode);
    }

    private void ShowSettlement(PureRunSaveSnapshot snapshot)
    {
        _battle = null;
        if (snapshot.TerminalSummary is PureRunSummary summary) { ShowSummary(summary); return; }
        PureRunState run = snapshot.ActiveRun!;
        Control root = NewPage("BATTLE SETTLEMENT", $"Completed {run.BattlesCompleted}/3 battles");
        LabelAt(root, $"Gold: {run.Gold}\nItems: {string.Join(", ", run.AcquiredItems.Select(id => id.Value))}\nPending Progression: {run.PendingProgression.LastOrDefault()?.CharacterId ?? "none"}\nDead: {string.Join(", ", run.Party.Where(value => value.IsDead).Select(value => value.CharacterId))}", new Vector2(480, 260), 28);
        Button next = Button("Continue", BeginReadyEncounter); next.Position = new Vector2(650, 610); next.Size = new Vector2(300, 70); root.AddChild(next);
    }

    private void ShowSummary(PureRunSummary summary)
    {
        Control root = NewPage(summary.Outcome.ToString(), "Three-encounter slice complete");
        LabelAt(root, $"Battles: {summary.BattlesCompleted}\nKills: {summary.EnemiesDefeated}\nGold: {summary.TotalGoldEarned}\nItems: {string.Join(", ", summary.AcquiredItems.Select(id => id.Value))}\nDead: {string.Join(", ", summary.DeadCharacters)}", new Vector2(520, 260), 30);
        Button home = Button("Return Home", () => { _run!.ConsumeCompletedSummary(); ShowHome(); }); home.Position = new Vector2(650, 620); home.Size = new Vector2(300, 70); root.AddChild(home);
    }

    private void AbandonRun()
    {
        RunSessionResult result = _run!.AbandonRun();
        if (result.Snapshot?.TerminalSummary is PureRunSummary summary) ShowSummary(summary); else SetStatus(result.ErrorCode);
    }

    private Control NewPage(string title, string subtitle)
    {
        _page?.QueueFree(); _actors.Clear();
        var root = new Control(); root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(root); _page = root;
        var background = new ColorRect { Color = new Color("657784") }; background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); root.AddChild(background);
        LabelAt(root, title, new Vector2(70, 35), 40); LabelAt(root, subtitle, new Vector2(70, 82), 20); return root;
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(300, 56) }; button.AddThemeFontSizeOverride("font_size", 21); button.Pressed += action; return button;
    }
    private static Label Label(string text, int size) { var label = new Label { Text = text }; label.AddThemeFontSizeOverride("font_size", size); return label; }
    private static Label LabelAt(Control parent, string text, Vector2 position, int size) { Label label = Label(text, size); label.Position = position; parent.AddChild(label); return label; }
    private void SetStatus(string? text) { if (_status is not null) _status.Text = text ?? string.Empty; }
}
