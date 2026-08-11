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
    private readonly Dictionary<GridPoint, Button> _cells = new();
    private readonly List<BattleUiLogEntry> _logs = new();
    private PureRunSessionService? _run;
    private PlayableBattleSessionService? _battle;
    private Control? _page;
    private Label? _status;
    private VBoxContainer? _skillPanel;
    private Control? _board;
    private RichTextLabel? _eventLog;
    private Label? _hoverInfo;
    private Label? _turnOrder;
    private global::Godot.Timer? _playbackTimer;
    private bool _playbackPaused;
    private int _logFilter;
    private BattleUiSnapshot? _visibleSnapshot;
    private GridPoint? _hoveredCell;

    public bool IsReadyForInput => _run is not null && _page is not null && _units.Count == 12 &&
        _skills.Count >= 16 && _ai.Count == 6 && _layouts.Count == 2 && _encounters.Count == 3;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        LoadCatalogs();
        _playbackTimer=new global::Godot.Timer{WaitTime=.45,OneShot=false};_playbackTimer.Timeout+=OnPlaybackTimer;AddChild(_playbackTimer);
        ShowHome();
    }

    public override void _ExitTree(){if(_playbackTimer is not null)_playbackTimer.Timeout-=OnPlaybackTimer;}

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
        _playbackTimer?.Stop();_logs.Clear();_visibleSnapshot=null;
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
        _cells.Clear();_logs.Clear();_playbackPaused=false;
        _board = new Control { Position = BoardOrigin, Size = new Vector2(CellSize * 10, CellSize * 10) }; root.AddChild(_board);
        for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++)
        {
            GridPoint cell = new(x, y);
            var button = new Button { Position = new Vector2(x * CellSize, y * CellSize), Size = new Vector2(CellSize, CellSize), Text = $"{x},{y}", Modulate = new Color(0.75f, 0.82f, 0.86f, 0.75f) };
            button.AddThemeFontSizeOverride("font_size", 12); button.Pressed += () => ApplyIntent(new ConfirmCellIntent(cell));button.MouseEntered+=()=>HoverCell(cell);button.MouseExited+=ClearHover; _board.AddChild(button);_cells[cell]=button;
        }
        _skillPanel = new VBoxContainer { Position = new Vector2(800, 125), Size = new Vector2(330, 650) }; root.AddChild(_skillPanel);
        _turnOrder=LabelAt(root,string.Empty,new Vector2(800,88),18);_turnOrder.Size=new Vector2(720,32);
        _hoverInfo=LabelAt(root,"Hover a cell",new Vector2(800,780),16);_hoverInfo.Size=new Vector2(720,80);_hoverInfo.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        var logPanel=new VBoxContainer{Position=new Vector2(1145,125),Size=new Vector2(390,650)};root.AddChild(logPanel);
        var controls=new HBoxContainer();logPanel.AddChild(controls);
        controls.AddChild(SmallButton("Pause/Resume",TogglePause));controls.AddChild(SmallButton("Step",()=>PlaybackStep(true)));controls.AddChild(SmallButton("1x/2x",ToggleSpeed));
        var filters=new OptionButton();foreach(string name in new[]{"All","Gameplay","AI","Rejected"})filters.AddItem(name);filters.ItemSelected+=index=>{_logFilter=(int)index;RefreshLog();};logPanel.AddChild(filters);
        logPanel.AddChild(Button("Clear Log",()=>{_logs.Clear();RefreshLog();}));
        var scroll=new ScrollContainer{CustomMinimumSize=new Vector2(390,500)};logPanel.AddChild(scroll);
        _eventLog=new RichTextLabel{FitContent=false,CustomMinimumSize=new Vector2(370,500),ScrollActive=true};_eventLog.AddThemeFontSizeOverride("normal_font_size",15);scroll.AddChild(_eventLog);
        _status = _hoverInfo;
        RefreshBattle();
        if(_battle!.HasPendingAutomaticFrames){PlaybackStep(true);_playbackTimer!.Start();}
    }

    private void RefreshBattle(BattleUiSnapshot? presented=null)
    {
        if (_battle is null || _board is null || _skillPanel is null) return;
        BattleUiSnapshot snapshot = presented??_battle.CaptureSnapshot();_visibleSnapshot=snapshot;
        foreach (GodotUnitActor actor in _actors.Values) actor.QueueFree(); _actors.Clear();
        foreach (BattleUiUnitSnapshot unit in snapshot.Units)
        {
            GodotUnitActor actor = GodotUnitFactory.InstantiateActor(_unitResources[unit.DefinitionId]);
            actor.Position = new Vector2((unit.Cell.X + .5f) * CellSize, (unit.Cell.Y + .72f) * CellSize);
            actor.Scale = Vector2.One * .34f; actor.SetDeathVisual(!unit.IsAlive); _board.AddChild(actor); _actors[unit.UnitId] = actor;
        }
        foreach (Node child in _skillPanel.GetChildren()) child.QueueFree();
        _skillPanel.AddChild(Label($"Round {snapshot.Round} | Active {snapshot.ActiveUnitId.Value}\nMode {snapshot.TargetingMode} | {snapshot.Phase}", 25));
        bool aiPlayback=_battle.HasPendingAutomaticFrames;
        Button moveButton=Button("Move", () => ApplyIntent(new BeginMoveIntent()));moveButton.Disabled=aiPlayback||snapshot.Phase!=PlayableBattlePhase.PlayerTurn;_skillPanel.AddChild(moveButton);
        bool spearDropped=snapshot.DroppedSpears.ContainsKey(snapshot.ActiveUnitId);
        _skillPanel.AddChild(Label($"Spear: {(spearDropped?"Dropped":"Held")}",18));
        foreach (SkillDefinition skill in snapshot.ActiveSkills.Where(skill => !skill.IsPassive&&(!skill.Hidden||skill.ExecutionKind==SkillExecutionKind.PickupSpear&&spearDropped)))
        {Button skillButton=Button($"{skill.ContentId.Value}  MP {skill.ManaCost}", () => ApplyIntent(new SelectSkillIntent(skill.ContentId)));skillButton.Disabled=aiPlayback;_skillPanel.AddChild(skillButton);}
        foreach(SkillDefinition passive in snapshot.ActiveSkills.Where(skill=>skill.IsPassive))_skillPanel.AddChild(Label($"Passive: {passive.ContentId.Value}",16));
        Button endTurn=Button("End Turn (Enter)", () => ApplyIntent(new EndTurnIntent()));endTurn.Disabled=aiPlayback||snapshot.Phase!=PlayableBattlePhase.PlayerTurn;_skillPanel.AddChild(endTurn);
        _skillPanel.AddChild(Button("Abandon Run", AbandonRun));
        ApplyHighlights(snapshot);
        if(_turnOrder is not null)_turnOrder.Text="Turn: "+string.Join(" → ",snapshot.TurnOrder.Select((id,index)=>$"{(index==snapshot.ActiveTurnIndex?"▶":"")}{id.Value}{(snapshot.Units.First(unit=>unit.UnitId==id).IsAlive?string.Empty:"✝")}"));
        RefreshLog();
    }

    private void ApplyHighlights(BattleUiSnapshot snapshot)
    {
        var colors=new Dictionary<GridPoint,Color>();
        foreach(GridPoint cell in _cells.Keys)colors[cell]=new Color(.75f,.82f,.86f,.75f);
        foreach(BattleUiUnitSnapshot unit in snapshot.Units.Where(unit=>unit.IsAlive))
            colors[unit.Cell]=unit.UnitId==snapshot.ActiveUnitId?new Color(.95f,.66f,.24f,.75f):unit.PlayerNumber==0?new Color(.34f,.52f,.62f,.6f):colors[unit.Cell];
        foreach(BattleUiUnitSnapshot unit in snapshot.Units.Where(unit=>unit.IsAlive&&unit.HasMovedThisTurn&&unit.UnitId!=snapshot.ActiveUnitId))colors[unit.Cell]=new Color(.36f,.42f,.48f,.55f);
        foreach(GridPoint corpse in snapshot.Corpses)colors[corpse]=new Color(.38f,.18f,.48f,.8f);
        foreach(GridPoint spear in snapshot.DroppedSpears.Values)colors[spear]=new Color(1f,.55f,.15f,.85f);
        if(snapshot.TargetingMode==BattleTargetingMode.Move)foreach(GridPoint cell in snapshot.LegalMoveCells)colors[cell]=new Color(.2f,.8f,1f,.75f);
        if(snapshot.TargetingMode==BattleTargetingMode.Skill&&snapshot.SelectedSkillId is ContentId skillId)
            foreach(BattleUiTarget target in snapshot.LegalTargets.Where(target=>target.SkillId==skillId))
                colors[target.Cell]=_skills[skillId].ExecutionKind==SkillExecutionKind.PickupSpear?new Color(.25f,.9f,.35f,.82f):new Color(1f,.3f,.2f,.78f);
        if(_hoveredCell is GridPoint hovered)
        {
            if(snapshot.TargetingMode==BattleTargetingMode.Move&&snapshot.LegalMoveCells.Contains(hovered))
            {IReadOnlyList<GridPoint> path=_battle?.PreviewMovePath(hovered)??Array.Empty<GridPoint>();foreach(GridPoint cell in path)colors[cell]=new Color(1f,.85f,.2f,.85f);colors[hovered]=new Color(1f,.5f,0,.9f);}
            if(snapshot.TargetingMode==BattleTargetingMode.Skill&&snapshot.SelectedSkillId is ContentId selected&&_skills[selected].ExecutionKind==SkillExecutionKind.AreaBlast)
                foreach(GridPoint cell in _cells.Keys.Where(cell=>Math.Abs(cell.X-hovered.X)+Math.Abs(cell.Y-hovered.Y)<=2))colors[cell]=new Color(1f,.5f,0,.72f);
            colors[hovered]=colors[hovered].Lightened(.22f);
        }
        foreach((GridPoint cell,Button button) in _cells)button.Modulate=colors[cell];
    }

    private void HoverCell(GridPoint cell)
    {
        _hoveredCell=cell;if(_visibleSnapshot is not BattleUiSnapshot snapshot)return;
        BattleUiUnitSnapshot? unit=snapshot.Units.FirstOrDefault(value=>value.Cell==cell);
        string detail=unit is null?$"Cell {cell}":$"Cell {cell} | {unit.UnitId.Value} | HP {unit.CurrentHealth}/{unit.MaxHealth} MP {unit.CurrentMana}/{unit.MaxMana} | {string.Join(',',unit.StatusIds.Select(id=>id.Value))}";
        if(snapshot.Corpses.Contains(cell))detail+=" | Corpse";
        if(snapshot.TargetingMode==BattleTargetingMode.Move)detail+=snapshot.LegalMoveCells.Contains(cell)?$" | Legal move, path {_battle?.PreviewMovePath(cell).Count??0}":" | Illegal move";
        if(snapshot.TargetingMode==BattleTargetingMode.Skill&&snapshot.SelectedSkillId is ContentId skillId)
        {bool legal=snapshot.LegalTargets.Any(target=>target.SkillId==skillId&&target.Cell==cell);detail+=legal?" | Legal target":" | Illegal target";if(legal&&_skills[skillId].ExecutionKind==SkillExecutionKind.AreaBlast)detail+=$" | AOE targets {snapshot.Units.Count(value=>value.IsAlive&&value.PlayerNumber!=0&&Math.Abs(value.Cell.X-cell.X)+Math.Abs(value.Cell.Y-cell.Y)<=2)}";}
        if(_hoverInfo is not null)_hoverInfo.Text=detail;ApplyHighlights(snapshot);
    }
    private void ClearHover(){_hoveredCell=null;if(_hoverInfo is not null)_hoverInfo.Text="Hover a cell";if(_visibleSnapshot is not null)ApplyHighlights(_visibleSnapshot);}

    private void ApplyIntent(BattleUiIntent intent)
    {
        if (_battle is null) return;
        BattleUiIntentResult result = _battle.Submit(intent);
        AddEvents(result.Events);
        if(!result.Succeeded&&result.Events.Count==0&&result.FailureCode is not null)AddLog(new BattleUiLogEntry(BattleUiLogCategory.Rejected,result.FailureCode,"CommandRejectedEvent"));
        if(_battle.HasPendingAutomaticFrames){PlaybackStep(true);_playbackTimer!.Start();return;}
        if (result.BattleResult is PureRunBattleResult battleResult)
        {
            CompleteBattle(battleResult);return;
        }
        RefreshBattle();
        if (!result.Succeeded) SetStatus(result.FailureCode);
    }

    private void CompleteBattle(PureRunBattleResult battleResult){RunSessionResult settled=_run!.ApplyBattleResult(battleResult);if(!settled.Succeeded){SetStatus(settled.ErrorCode);return;}ShowSettlement(settled.Snapshot!);}

    private void OnPlaybackTimer(){if(!_playbackPaused)PlaybackStep(false);}
    private void PlaybackStep(bool forced)
    {
        if(_battle is null||(_playbackPaused&&!forced))return;
        BattleUiFrame? frame=_battle.DequeueAutomaticFrame();
        if(frame is not null){if(frame.Decision is { } decision)AddLog(new BattleUiLogEntry(BattleUiLogCategory.Ai,$"{decision.ActorId.Value} selected {decision.Intent}{(decision.SkillId is null?string.Empty:" + "+decision.SkillId.Value)} to {decision.Destination}; score {decision.Score:0.##}; candidates {decision.CandidateCount}",nameof(AiDecisionEvent)));AddEvents(frame.Events);RefreshBattle(frame.Snapshot);return;}
        _playbackTimer?.Stop();RefreshBattle();if(_battle.BattleResult is { } result)CompleteBattle(result);
    }
    private void TogglePause(){_playbackPaused=!_playbackPaused;AddLog(new BattleUiLogEntry(BattleUiLogCategory.Ai,_playbackPaused?"AI playback paused":"AI playback resumed","Playback"));RefreshLog();}
    private void ToggleSpeed(){if(_playbackTimer is null)return;_playbackTimer.WaitTime=_playbackTimer.WaitTime>.3?.225:.45;AddLog(new BattleUiLogEntry(BattleUiLogCategory.Ai,$"Playback {(_playbackTimer.WaitTime<.3?"2x":"1x")}","Playback"));RefreshLog();}

    private void AddEvents(IEnumerable<BattleEvent> events){foreach(BattleEvent item in events)AddLog(FormatEvent(item));}
    private void AddLog(BattleUiLogEntry entry){if(_logs.Count>=100)_logs.RemoveAt(0);_logs.Add(entry);}
    private static BattleUiLogEntry FormatEvent(BattleEvent item)=>item switch
    {
        UnitMovedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.UnitId.Value} moved {e.Origin} → {e.Destination}",nameof(UnitMovedEvent)),
        DamageAppliedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.TargetId.Value} took {e.Amount} damage; HP {e.RemainingHealth}",nameof(DamageAppliedEvent)),
        UnitDefeatedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.UnitId.Value} was defeated",nameof(UnitDefeatedEvent)),
        CorpseCreatedEvent e=>new(BattleUiLogCategory.Gameplay,$"Corpse created at {e.Cell} from {e.UnitId.Value}",nameof(CorpseCreatedEvent)),
        SkillUsedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.ActorId.Value} used {e.SkillId.Value} on {e.TargetId.Value}",nameof(SkillUsedEvent)),
        SpearDroppedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.OwnerId.Value} dropped spear at {e.Cell}",nameof(SpearDroppedEvent)),
        SpearRecoveredEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.OwnerId.Value} recovered spear at {e.Cell}",nameof(SpearRecoveredEvent)),
        CommandRejectedEvent e=>new(BattleUiLogCategory.Rejected,$"{e.ActorId.Value}: {e.Reason}",nameof(CommandRejectedEvent)),
        _=>new(BattleUiLogCategory.Gameplay,item.ToString()??item.GetType().Name,item.GetType().Name)
    };
    private void RefreshLog(){if(_eventLog is null)return;IEnumerable<BattleUiLogEntry> shown=_logs;if(_logFilter>0)shown=shown.Where(item=>(int)item.Category==_logFilter-1);_eventLog.Text=string.Join('\n',shown.Select(item=>$"[{item.EventType}] {item.Message}"));_eventLog.ScrollToLine(Math.Max(0,_eventLog.GetLineCount()-1));}

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
    private static Button SmallButton(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(118, 44) }; button.AddThemeFontSizeOverride("font_size", 15); button.Pressed += action; return button;
    }
    private static Label Label(string text, int size) { var label = new Label { Text = text }; label.AddThemeFontSizeOverride("font_size", size); return label; }
    private static Label LabelAt(Control parent, string text, Vector2 position, int size) { Label label = Label(text, size); label.Position = position; parent.AddChild(label); return label; }
    private void SetStatus(string? text) { if (_status is not null) _status.Text = text ?? string.Empty; }
}
