using Godot;
using Tactics.Application.Battle;
using Tactics.Application.Runs;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Items;
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
    private readonly Dictionary<ContentId, EquipmentDefinition> _equipment = new();
    private PlayableBattleBalanceProfile? _balance;
    private readonly Dictionary<UnitInstanceId, GodotUnitActor> _actors = new();
    private readonly Dictionary<UnitInstanceId, Control> _unitMeters = new();
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
    private ContentId? _currentEncounterId;
    private bool _settlementCommitted;

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
        _balance = (ResourceLoader.Load<PlayableLv1BalanceProfileResource>("res://content/ui/PlayableLv1BalanceProfile.tres")
            ?? throw new InvalidOperationException("Playable Lv1 balance profile is missing.")).ToCoreProfile();
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
                case EquipmentDefinitionResource equipment: _equipment[id] = equipment.ToCoreDefinition(); break;
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
        if (loaded.Snapshot?.ActiveRun is not null) menu.AddChild(Button("Inventory", () => ShowInventory(loaded.Snapshot.ActiveRun)));
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
        AddLog(new BattleUiLogEntry(BattleUiLogCategory.Gameplay,"Settlement Continue requested the next encounter","EncounterNavigationEvent"));
        RunSessionResult begun = _run!.BeginEncounter();
        if (!begun.Succeeded || begun.EncounterRequest is null) { SetStatus(begun.ErrorCode); return; }
        StartBattle(begun.EncounterRequest);
    }

    private void StartBattle(EncounterRequest request)
    {
        _playbackTimer?.Stop();
        _settlementCommitted=false;
        _currentEncounterId=request.EncounterContentId;
        EncounterDefinition encounter = _encounters[request.EncounterContentId];
        _battle = new PlayableBattleSessionFactory().Create(request, encounter, _layouts[encounter.LayoutId], _units, _skills, _ai, _balance);
        BuildBattlePage();
        AddLog(new BattleUiLogEntry(BattleUiLogCategory.Gameplay,$"Entered {EncounterLabel(request.EncounterContentId)} ({request.EncounterContentId.Value})","EncounterNavigationEvent"));
        RefreshLog();
    }

    private void BuildBattlePage()
    {
        ContentId encounterId=_currentEncounterId??throw new InvalidOperationException("Battle encounter identity is missing.");
        Control root = NewPage($"PURE RUN BATTLE — {EncounterLabel(encounterId)}", $"{encounterId.Value}   |   Left click: select/confirm   Right click or Esc: cancel   Enter: end turn");
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
        foreach (GodotUnitActor actor in _actors.Values)
            if (GodotObject.IsInstanceValid(actor)) actor.QueueFree();
        _actors.Clear();
        foreach (Control meter in _unitMeters.Values)
            if (GodotObject.IsInstanceValid(meter)) meter.QueueFree();
        _unitMeters.Clear();
        foreach (BattleUiUnitSnapshot unit in snapshot.Units.Where(unit=>unit.IsAlive||snapshot.Corpses.Contains(unit.Cell)))
        {
            GodotUnitActor actor = GodotUnitFactory.InstantiateActor(_unitResources[unit.DefinitionId]);
            actor.Position = new Vector2((unit.Cell.X + .5f) * CellSize, (unit.Cell.Y + .72f) * CellSize);
            actor.Scale = Vector2.One * .34f; actor.SetDeathVisual(!unit.IsAlive); _board.AddChild(actor); _actors[unit.UnitId] = actor;
            Control meter=CreateUnitMeters(unit);_board.AddChild(meter);_unitMeters[unit.UnitId]=meter;
        }
        foreach (Node child in _skillPanel.GetChildren()) child.QueueFree();
        _skillPanel.AddChild(Label($"Round {snapshot.Round} | Active {snapshot.ActiveUnitId.Value}\nMode {snapshot.TargetingMode} | {snapshot.Phase}", 25));
        bool aiPlayback=_battle.HasPendingAutomaticFrames;
        Button moveButton=Button("Move", () => ApplyIntent(new BeginMoveIntent()));moveButton.Disabled=aiPlayback||snapshot.Phase!=PlayableBattlePhase.PlayerTurn;_skillPanel.AddChild(moveButton);
        bool spearDropped=snapshot.DroppedSpears.ContainsKey(snapshot.ActiveUnitId);
        _skillPanel.AddChild(Label($"Spear: {(spearDropped?"Dropped":"Held")}",18));
        foreach (SkillDefinition skill in snapshot.ActiveSkills.Where(skill => !skill.IsPassive&&(!skill.Hidden||skill.ExecutionKind==SkillExecutionKind.PickupSpear&&spearDropped)))
        {
            BattleUiUnitSnapshot activeUnit=snapshot.Units.Single(unit=>unit.UnitId==snapshot.ActiveUnitId);
            int uses=activeUnit.SuccessfulSkillUses.TryGetValue(skill.ContentId,out int count)?count:0;
            string? usageFailure=skill.IsBasicAbility&&uses>=1?"basic_ability_already_used":!skill.IsBasicAbility&&skill.MaxUsesPerTurn>0&&uses>=skill.MaxUsesPerTurn?"ability_use_limit_reached":null;
            Button skillButton=Button($"{skill.ContentId.Value}  MP {skill.ManaCost}{(usageFailure is null?string.Empty:"  [USED]")}", () => ApplyIntent(new SelectSkillIntent(skill.ContentId)));skillButton.Disabled=aiPlayback||usageFailure is not null;skillButton.TooltipText=usageFailure??SkillTooltip(skill);_skillPanel.AddChild(skillButton);
        }
        foreach(SkillDefinition passive in snapshot.ActiveSkills.Where(skill=>skill.IsPassive))_skillPanel.AddChild(Label($"Passive: {passive.ContentId.Value}",16));
        Button endTurn=Button("End Turn (Enter)", () => ApplyIntent(new EndTurnIntent()));endTurn.Disabled=aiPlayback||snapshot.Phase!=PlayableBattlePhase.PlayerTurn;_skillPanel.AddChild(endTurn);
        _skillPanel.AddChild(Button("Abandon Run", AbandonRun));
        ApplyHighlights(snapshot);
        if(_turnOrder is not null)_turnOrder.Text="Turn: "+string.Join(" → ",snapshot.TurnOrder.Select((id,index)=>$"{(index==snapshot.ActiveTurnIndex?"▶":"")}{id.Value}{(snapshot.Units.First(unit=>unit.UnitId==id).IsAlive?string.Empty:"✝")}"));
        RefreshLog();
    }

    private static Control CreateUnitMeters(BattleUiUnitSnapshot unit)
    {
        var root=new Control{Position=new Vector2((unit.Cell.X+.5f)*CellSize-42,(unit.Cell.Y+.14f)*CellSize),Size=new Vector2(84,34),ZIndex=20,MouseFilter=MouseFilterEnum.Ignore};
        var hp=new ProgressBar{Position=Vector2.Zero,Size=new Vector2(84,15),MinValue=0,MaxValue=unit.MaxHealth,Value=unit.CurrentHealth,ShowPercentage=false,MouseFilter=MouseFilterEnum.Ignore};hp.Modulate=new Color(.35f,1f,.4f);root.AddChild(hp);
        var hpText=Label($"HP {unit.CurrentHealth}/{unit.MaxHealth}",11);hpText.Position=new Vector2(3,-2);hpText.MouseFilter=MouseFilterEnum.Ignore;root.AddChild(hpText);
        var mp=new ProgressBar{Position=new Vector2(0,17),Size=new Vector2(84,15),MinValue=0,MaxValue=Math.Max(1,unit.MaxMana),Value=unit.CurrentMana,ShowPercentage=false,MouseFilter=MouseFilterEnum.Ignore};mp.Modulate=new Color(.35f,.65f,1f);root.AddChild(mp);
        var mpText=Label($"MP {unit.CurrentMana}/{unit.MaxMana}",11);mpText.Position=new Vector2(3,15);mpText.MouseFilter=MouseFilterEnum.Ignore;root.AddChild(mpText);
        return root;
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
        {
            if(snapshot.SkillPreview is BattleUiSkillPreview skillPreview)
                foreach(GridPoint cell in skillPreview.RangeCells)colors[cell]=new Color(.58f,.22f,.2f,.48f);
            foreach(BattleUiTarget target in snapshot.LegalTargets.Where(target=>target.SkillId==skillId))
                colors[target.Cell]=_skills[skillId].ExecutionKind==SkillExecutionKind.PickupSpear?new Color(.25f,.9f,.35f,.82f):new Color(1f,.3f,.2f,.78f);
        }
        if(_hoveredCell is GridPoint hovered)
        {
            if(snapshot.TargetingMode==BattleTargetingMode.Move&&snapshot.LegalMoveCells.Contains(hovered))
            {IReadOnlyList<GridPoint> path=_battle?.PreviewMovePath(hovered)??Array.Empty<GridPoint>();foreach(GridPoint cell in path)colors[cell]=new Color(1f,.85f,.2f,.85f);colors[hovered]=new Color(1f,.5f,0,.9f);}
            if(snapshot.TargetingMode==BattleTargetingMode.Skill&&_battle?.PreviewSkillTarget(hovered) is BattleUiImpactPreview impact)
            {
                if(impact.IsLegal)
                {
                    foreach(GridPoint cell in impact.PathCells)colors[cell]=new Color(1f,.85f,.2f,.85f);
                    foreach(GridPoint cell in impact.ImpactCells)colors[cell]=new Color(1f,.5f,0,.72f);
                    if(impact.PrimaryImpactCell is GridPoint primary)colors[primary]=new Color(1f,.5f,0,.9f);
                }
            }
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
        {
            BattleUiImpactPreview? preview=_battle?.PreviewSkillTarget(cell);
            if(preview is not null)
            {
                detail+=preview.IsInRange?" | In range":" | Out of range";
                detail+=preview.IsLegal?" | Legal target":$" | Blocked: {preview.FailureCode??"invalid_target"}";
                if(preview.PrimaryImpactUnitId is UnitInstanceId primary)detail+=$" | First hit: {primary.Value}";
                if(preview.ImpactUnitIds.Count>1||_skills[skillId].ExecutionKind==SkillExecutionKind.AreaBlast)detail+=$" | AOE targets {preview.ImpactUnitIds.Count}";
            }
        }
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

    private void CompleteBattle(PureRunBattleResult battleResult)
    {
        if(_settlementCommitted)return;
        _settlementCommitted=true;_playbackTimer?.Stop();
        AddLog(new BattleUiLogEntry(BattleUiLogCategory.Gameplay,$"Submitting {EncounterLabel(battleResult.EncounterContentId)} BattleResult","EncounterNavigationEvent"));
        RunSessionResult settled=_run!.ApplyBattleResult(battleResult);if(!settled.Succeeded){_settlementCommitted=false;SetStatus(settled.ErrorCode);return;}ShowSettlement(settled.Snapshot!);
    }

    private void OnPlaybackTimer(){if(!_playbackPaused)PlaybackStep(false);}
    private void PlaybackStep(bool forced)
    {
        if(_battle is null||(_playbackPaused&&!forced))return;
        BattleUiFrame? frame=_battle.DequeueAutomaticFrame();
        if(frame is not null){if(frame.Decision is { } decision)AddLog(new BattleUiLogEntry(BattleUiLogCategory.Ai,$"{decision.ActorId.Value} selected {decision.Intent}{(decision.SkillId is null?string.Empty:" + "+decision.SkillId.Value)} to {decision.Destination}; target {decision.TargetId?.Value??"none"} ({decision.TargetDefinitionId?.Value??"none"}); score {decision.Score:0.##} [distance {decision.DistanceScore:0.##}, damage {decision.DamageScore:0.##}, target {decision.TargetScore:0.##}, status {decision.StatusScore:0.##}]; candidates {decision.CandidateCount}",nameof(AiDecisionEvent)));AddEvents(frame.Events);RefreshBattle(frame.Snapshot);return;}
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
        CorpseConsumedEvent e=>new(BattleUiLogCategory.Gameplay,$"Corpse consumed at {e.Cell}",nameof(CorpseConsumedEvent)),
        UnitSummonedEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.OwnerId.Value} summoned {e.SummonId.Value} at {e.Cell}",nameof(UnitSummonedEvent)),
        ManaRestoredEvent e=>new(BattleUiLogCategory.Gameplay,$"{e.TargetId.Value} restored {e.Amount} MP; MP {e.CurrentMana}",nameof(ManaRestoredEvent)),
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
        string completed=_currentEncounterId is ContentId completedId?EncounterLabel(completedId):$"Battle {run.BattlesCompleted}";
        string next=EncounterLabel(run.EncounterContentId);
        Control root = NewPage("BATTLE SETTLEMENT", $"{completed} completed → Next: {next}");
        LabelAt(root, $"Gold: {run.Gold}\nItems: {string.Join(", ", run.AcquiredItems.Select(id => id.Value))}\nPending Progression: {run.PendingProgression.LastOrDefault()?.CharacterId ?? "none"}\nDead: {string.Join(", ", run.Party.Where(value => value.IsDead).Select(value => value.CharacterId))}", new Vector2(480, 260), 28);
        root.AddChild(PlaceControl(Button("Inventory",()=>ShowInventory(run)),new Vector2(480,520),new Vector2(260,60)));
        PendingProgression? pending=run.PendingProgression.FirstOrDefault();
        if(pending is not null)root.AddChild(PlaceControl(Button("Complete Progression",()=>ShowProgression(run,pending)),new Vector2(780,520),new Vector2(320,60)));
        bool continueRequested=false;
        Button nextButton = Button($"Continue to {next}",()=>{if(continueRequested)return;continueRequested=true;BeginReadyEncounter();}); nextButton.Position = new Vector2(650, 610); nextButton.Size = new Vector2(300, 70); root.AddChild(nextButton);
        nextButton.Disabled=pending is not null;
    }

    private void ShowInventory(PureRunState run)
    {
        Control root=NewPage("INVENTORY","Functional placeholder — equipment, carried consumable, attributes and skill levels");
        var columns=new HBoxContainer{Position=new Vector2(90,150),Size=new Vector2(1420,610)};root.AddChild(columns);
        foreach(RunCharacterState character in run.Party)
        {
            var panel=new VBoxContainer{CustomMinimumSize=new Vector2(440,580)};columns.AddChild(panel);
            panel.AddChild(Label($"{character.CharacterId}  Lv{character.Level}\nHP {character.CurrentHealth}/{character.MaxHealth}  MP {character.CurrentMana}/{character.MaxMana}\nSTR {character.Attributes.Strength} AGI {character.Attributes.Agility} CON {character.Attributes.Constitution}\nINT {character.Attributes.Intelligence} CHA {character.Attributes.Charisma} LUCK {character.Attributes.Luck}",20));
            panel.AddChild(Label("Skills:\n"+string.Join('\n',character.LearnedSkillStates.Select(value=>$"{value.BranchId} Lv{value.Level}")),17));
            panel.AddChild(Label("Equipment:\n"+string.Join('\n',character.Equipment.Select(value=>$"{value.Slot}: {value.DefinitionId.Value}")),17));
            panel.AddChild(Label("Carried: "+(character.CarriedConsumables.FirstOrDefault()?.DefinitionId.Value??"none"),17));
            foreach(RunEquipmentState equipped in character.Equipment)
                panel.AddChild(Button($"Unequip {equipped.Slot}",()=>CommitMutation(state=>new RunInventoryProgressionService().Unequip(state,state.Revision,character.CharacterId,equipped.Slot))));
            foreach(RunEquipmentState item in run.BackpackEquipment)
                panel.AddChild(Button($"Equip {_equipment.GetValueOrDefault(item.DefinitionId)?.DisplayName ?? item.DefinitionId.Value}",()=>CommitMutation(state=>new RunInventoryProgressionService().Equip(state,state.Revision,character.CharacterId,item.InstanceId,_equipment,_units[character.UnitContentId].Speed))));
            foreach(BattleConsumableState item in run.BackpackConsumables)
                panel.AddChild(Button($"Carry {item.DefinitionId.Value}",()=>CommitMutation(state=>new RunInventoryProgressionService().Carry(state,state.Revision,character.CharacterId,item.InstanceId))));
            if(character.CarriedConsumables.Count>0)panel.AddChild(Button("Unload Consumable",()=>CommitMutation(state=>new RunInventoryProgressionService().Unload(state,state.Revision,character.CharacterId))));
        }
        root.AddChild(PlaceControl(Button("Back",()=>ShowSettlement(new PureRunSaveSnapshot(run.Revision,run,null))),new Vector2(650,800),new Vector2(300,55)));
    }

    private void ShowProgression(PureRunState run, PendingProgression pending)
    {
        RunCharacterState character=run.Party.Single(value=>value.CharacterId==pending.CharacterId);
        Control root=NewPage("PROGRESSION",$"{character.CharacterId}: allocate 1 point, then learn/upgrade one skill");
        var menu=new VBoxContainer{Position=new Vector2(430,160),Size=new Vector2(740,650)};root.AddChild(menu);
        menu.AddChild(Label($"Current Lv{character.Level} | INT {character.Attributes.Intelligence} AGI {character.Attributes.Agility} CHA {character.Attributes.Charisma} LUCK {character.Attributes.Luck}",22));
        var progressionService=new RunInventoryProgressionService();
        SkillDefinition[] candidates=progressionService.GrowthCandidates(character,_skills).Where(skill=>progressionService.CanUnlockWithAttributePoints(character,skill,pending.AttributePoints)).ToArray();
        foreach(SkillDefinition skill in candidates)
        {
            menu.AddChild(Button($"+1 {skill.RequiredAttribute} → {skill.BranchId} Lv{skill.Level} (requires {skill.MinimumAttribute})",()=>
            {
                UnitAttributes raised=Raise(character.Attributes,skill.RequiredAttribute);
                CommitMutation(state=>new RunInventoryProgressionService().CompleteProgression(state,state.Revision,pending.TransactionKey,raised,skill.ContentId,_skills));
            }));
        }
        if(candidates.Length==0)
        {
            menu.AddChild(Label("No skill candidate can be unlocked with this progression point. Attribute-only confirmation is allowed.",18));
            menu.AddChild(Button("+1 primary attribute and confirm",()=>CommitMutation(state=>progressionService.CompleteProgression(state,state.Revision,pending.TransactionKey,Raise(character.Attributes,PrimaryAttribute(character.UnitContentId)),null,_skills))));
        }
        root.AddChild(PlaceControl(Button("Back",()=>ShowSettlement(new PureRunSaveSnapshot(run.Revision,run,null))),new Vector2(650,820),new Vector2(300,55)));
    }

    private static string PrimaryAttribute(ContentId unitId)=>unitId.Value switch{"unit.pure-run.mage"=>"Intelligence","unit.pure-run.necromancer"=>"Charisma","unit.pure-run.amazon"=>"Agility",_=>throw new InvalidOperationException($"Unknown progression unit '{unitId.Value}'.")};
    private static UnitAttributes Raise(UnitAttributes a,string name)=>name switch{"Strength"=>new(a.Strength+1,a.Agility,a.Constitution,a.Intelligence,a.Charisma,a.Luck),"Agility"=>new(a.Strength,a.Agility+1,a.Constitution,a.Intelligence,a.Charisma,a.Luck),"Constitution"=>new(a.Strength,a.Agility,a.Constitution+1,a.Intelligence,a.Charisma,a.Luck),"Intelligence"=>new(a.Strength,a.Agility,a.Constitution,a.Intelligence+1,a.Charisma,a.Luck),"Charisma"=>new(a.Strength,a.Agility,a.Constitution,a.Intelligence,a.Charisma+1,a.Luck),"Luck"=>new(a.Strength,a.Agility,a.Constitution,a.Intelligence,a.Charisma,a.Luck+1),_=>a};
    private void CommitMutation(Func<PureRunState,RunMutationResult> mutation){RunSessionResult result=_run!.ApplyMutation(mutation);if(!result.Succeeded){SetStatus(result.ErrorCode);return;}ShowSettlement(result.Snapshot!);}
    private static Control PlaceControl(Control control,Vector2 position,Vector2 size){control.Position=position;control.Size=size;return control;}

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
        // The old page owns every actor and meter. Queueing the page frees those
        // children, so page navigation must forget their managed references rather
        // than attempting to QueueFree the disposed children during the next refresh.
        _page?.QueueFree();
        _actors.Clear();
        _unitMeters.Clear();
        _visibleSnapshot=null;
        _hoveredCell=null;
        _board=null;
        _skillPanel=null;
        _turnOrder=null;
        _hoverInfo=null;
        _eventLog=null;
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
    private static string SkillTooltip(SkillDefinition skill)=>skill.ExecutionKind==SkillExecutionKind.Fireball
        ? "Lv1: single target; hits the first enemy on the selected ray. Splash begins at Lv2."
        : $"Range {skill.MinRange}-{skill.MaxRange}; damage {skill.Damage}.";
    private static string EncounterLabel(ContentId id)=>id.Value.EndsWith(".n1",StringComparison.Ordinal)?"N1":id.Value.EndsWith(".n2",StringComparison.Ordinal)?"N2":id.Value.EndsWith(".n3",StringComparison.Ordinal)?"N3":id.Value;
}
