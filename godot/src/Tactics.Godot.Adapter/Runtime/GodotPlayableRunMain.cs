using Godot;
using Tactics.Application.Battle;
using Tactics.Application.Runs;
using Tactics.Application.Presentation;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Items;
using Tactics.Core.Runs;
using Tactics.Core.Skills;
using Tactics.Core.Units;
using System.Text.Json;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Native 1600x900 Home -> N1/N2/N3 -> Summary playable flow.</summary>
public partial class GodotPlayableRunMain : Control
{
    public const int CanvasWidth = 1600;
    public const int CanvasHeight = 900;
    public static readonly Vector2 UnitMeterSize = new(60, 18);
    public const int UnitMeterBarHeight = 7;
    private readonly Dictionary<ContentId, UnitDefinition> _units = new();
    private readonly Dictionary<ContentId, UnitDefinitionResource> _unitResources = new();
    private readonly Dictionary<ContentId, SkillDefinition> _skills = new();
    private readonly Dictionary<ContentId, AiDefinition> _ai = new();
    private readonly Dictionary<ContentId, BattleLayoutDefinition> _layouts = new();
    private readonly Dictionary<ContentId, EncounterDefinition> _encounters = new();
    private readonly Dictionary<ContentId, EquipmentDefinition> _equipment = new();
    private readonly Dictionary<ContentId, ConsumableDefinition> _consumables = new();
    private readonly Dictionary<string, string> _layerFourEventPayloads = new(StringComparer.Ordinal);
    private PlayableBattleBalanceProfile? _balance;
    private PureRunDefinition? _runDefinition;
    private readonly Dictionary<UnitInstanceId, GodotUnitActor> _actors = new();
    private readonly Dictionary<UnitInstanceId, Control> _unitMeters = new();
    private readonly List<BattleUiLogEntry> _logs = new();
    private PureRunSessionService? _run;
    private PlayableBattleSessionService? _battle;
    private Control? _page;
    private Label? _status;
    private VBoxContainer? _skillPanel;
    private GodotIsometricBattleBoard? _board;
    private RichTextLabel? _eventLog;
    private Label? _hoverInfo;
    private Label? _turnOrder;
    private Button? _speedButton;
    private bool _playbackPaused;
    private float _playbackSpeed = 1f;
    private int _logFilter;
    private BattleUiSnapshot? _visibleSnapshot;
    private GridPoint? _hoveredCell;
    private (UnitInstanceId UnitId, GodotUnitFacing Facing)? _targetingFacingPreview;
    private ContentId? _currentEncounterId;
    private bool _settlementCommitted;
    private GodotBattlePresentationPlayer? _presentationPlayer;
    private BattleUiSnapshot? _presentationAfter;
    private PureRunBattleResult? _battleResultAfterPresentation;
    private bool _continueAutomaticAfterPresentation;
    private bool _pauseAfterCurrentFrame;
    private bool _presentationInputLocked;
    private StandardUnitPresentationResource? _presentationProfile;
    private readonly List<SkillPresentationResource> _skillPresentationProfiles=new();

    public bool IsReadyForInput => _run is not null && _page is not null && _units.Count == 12 &&
        _skills.Count >= 16 && _ai.Count == 6 && _layouts.Count >= 2 && _encounters.Count >= 3;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        LoadCatalogs();
        ShowHome();
    }

    public override void _ExitTree()=>DisposePresentationPlayer();
    public override void _Process(double delta)
    {
        foreach((UnitInstanceId id,Control meter) in _unitMeters)
            if(_actors.TryGetValue(id,out GodotUnitActor? actor)&&GodotObject.IsInstanceValid(actor)&&GodotObject.IsInstanceValid(meter))meter.Position=actor.Position+new Vector2(-30,-62);
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
        _balance = (ResourceLoader.Load<PlayableLv1BalanceProfileResource>("res://content/ui/PlayableLv1BalanceProfile.tres")
            ?? throw new InvalidOperationException("Playable Lv1 balance profile is missing.")).ToCoreProfile();
        _presentationProfile = ResourceLoader.Load<StandardUnitPresentationResource>("res://content/presentation/StandardUnitPresentationV1.tres");
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
                                    ? definition.SkillIds : Array.Empty<ContentId>())).ToArray(), encounter.HealthMultiplier,
                        encounter.OutputMultiplier, encounter.MinimumStartingMana,
                        Enum.Parse<EncounterClass>(encounter.EncounterClassValue)); break;
                case PureRunDefinitionResource run: runResource = run; break;
                case SkillPresentationResource presentation: _skillPresentationProfiles.Add(presentation); break;
                case EquipmentDefinitionResource equipment: _equipment[id] = equipment.ToCoreDefinition(); break;
                case ConsumableDefinitionResource consumable: _consumables[id] = consumable.ToCoreDefinition(); break;
                case PureRunLayerFourResource layerFour when layerFour.KindValue == "encounter":
                    using (JsonDocument payload = JsonDocument.Parse(layerFour.PayloadJson))
                    {
                        string[] units=payload.RootElement.GetProperty("monsters").EnumerateArray().Select(value=>value.GetString()!).ToArray();
                        string[] aiIds=units.Select(value=>"ai.pure-run."+value["unit.pure-run.".Length..].Replace("goat-",string.Empty)).ToArray();
                        var layoutId=new ContentId("battle-layout.pure-run.split-flank");
                        _layouts[layoutId]=new BattleLayoutDefinition(layoutId,[new GridPoint(1,4),new GridPoint(1,5),new GridPoint(2,4)],[new GridPoint(6,2),new GridPoint(6,7),new GridPoint(7,2),new GridPoint(7,7)],[new GridPoint(4,3),new GridPoint(5,4),new GridPoint(4,6),new GridPoint(5,5)]);
                        _encounters[id]=new EncounterDefinition(id,layoutId,units.Select((unit,index)=>new EncounterMonsterDefinition(new ContentId(unit),new ContentId(aiIds[index]),_ai[new ContentId(aiIds[index])].SkillIds)).ToArray());
                    }
                    break;
                case PureRunLayerFourResource layerFour when layerFour.KindValue == "event":
                    using (JsonDocument payload = JsonDocument.Parse(layerFour.PayloadJson))
                        _layerFourEventPayloads[payload.RootElement.GetProperty("sourceId").GetString()!] = layerFour.PayloadJson;
                    break;
            }
        }
        // Encounter resources can sort before AI entries in the canonical catalog; rebuild their skill bindings now.
        foreach (GodotResourceEntry entry in catalog.Entries.Where(value =>
                     value.ContentIdValue.StartsWith("encounter.pure-run.", StringComparison.Ordinal) &&
                     value.ResourceTypeIdValue == "encounter" &&
                     !value.ContentIdValue.EndsWith(".n4", StringComparison.Ordinal)))
        {
            var resource = ResourceLoader.Load<EncounterDefinitionResource>(entry.ResourceLocator)!;
            var id = new ContentId(entry.ContentIdValue);
            _encounters[id] = new EncounterDefinition(id, new ContentId(resource.LayoutContentId),
                Enumerable.Range(0, resource.MonsterUnitContentIds.Length).Select(index =>
                {
                    var aiId = new ContentId(resource.MonsterAiContentIds[index]);
                    return new EncounterMonsterDefinition(new ContentId(resource.MonsterUnitContentIds[index]), aiId, _ai[aiId].SkillIds);
                }).ToArray(),resource.HealthMultiplier,resource.OutputMultiplier,resource.MinimumStartingMana,
                Enum.Parse<EncounterClass>(resource.EncounterClassValue));
        }
        _runDefinition=(runResource ?? throw new InvalidOperationException("Run definition is missing.")).ToCoreDefinition();
        _run = new PureRunSessionService(_runDefinition, new GodotRunSaveStore());
    }

    private void ShowHome()
    {
        _logs.Clear();_visibleSnapshot=null;
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
        else if (resumed.Snapshot?.ActiveRun is PureRunState run && run.Phase != PureRunPhase.Ready) RouteMap(run);
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
        Control root = NewPage($"PURE RUN BATTLE — {EncounterLabel(encounterId)}", $"{encounterId.Value}   |   Left click: select/confirm   Right click or Esc: cancel   Enter: end turn", true);
        _logs.Clear();_playbackPaused=false;_playbackSpeed=1f;
        _board = new GodotIsometricBattleBoard { Position = Vector2.Zero, Size = new Vector2(1100, 900) };
        _board.CellPressed += OnBoardCellPressed;
        _board.CellHovered += HoverCell;
        _board.HoverCleared += ClearHover;
        root.AddChild(_board);
        _presentationPlayer = new GodotBattlePresentationPlayer();
        _presentationPlayer.Configure(_presentationProfile ?? new StandardUnitPresentationResource());
        _presentationPlayer.ConfigureSkills(_skillPresentationProfiles);
        _presentationPlayer.SetSpeed(_playbackSpeed);
        _presentationPlayer.FrameFinished+=OnPresentationFrameFinished;
        _board.AddChild(_presentationPlayer);
        _skillPanel = new VBoxContainer { Position = new Vector2(800, 125), Size = new Vector2(330, 650) }; root.AddChild(_skillPanel);
        _turnOrder=LabelAt(root,string.Empty,new Vector2(800,88),18);_turnOrder.Size=new Vector2(720,32);
        _hoverInfo=LabelAt(root,"Hover a cell",new Vector2(800,780),16);_hoverInfo.Size=new Vector2(720,80);_hoverInfo.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        var logPanel=new VBoxContainer{Position=new Vector2(1145,125),Size=new Vector2(390,650)};root.AddChild(logPanel);
        var controls=new HBoxContainer();logPanel.AddChild(controls);
        controls.AddChild(SmallButton("Pause/Resume",TogglePause));controls.AddChild(SmallButton("Step",()=>PlaybackStep(true)));_speedButton=SmallButton("Speed 1x",ToggleSpeed);controls.AddChild(_speedButton);
        var filters=new OptionButton();foreach(string name in new[]{"All","Gameplay","AI","Rejected"})filters.AddItem(name);filters.ItemSelected+=index=>{_logFilter=(int)index;RefreshLog();};logPanel.AddChild(filters);
        logPanel.AddChild(Button("Clear Log",()=>{_logs.Clear();RefreshLog();}));
        var scroll=new ScrollContainer{CustomMinimumSize=new Vector2(390,500)};logPanel.AddChild(scroll);
        _eventLog=new RichTextLabel{FitContent=false,CustomMinimumSize=new Vector2(370,500),ScrollActive=true};_eventLog.AddThemeFontSizeOverride("normal_font_size",15);scroll.AddChild(_eventLog);
        _status = _hoverInfo;
        RefreshBattle();
        if(_battle!.HasPendingAutomaticFrames)PlaybackStep(false);
    }

    private void RefreshBattle(BattleUiSnapshot? presented=null)
    {
        if (_battle is null || _board is null || _skillPanel is null) return;
        BattleUiSnapshot snapshot = presented??_battle.CaptureSnapshot();_visibleSnapshot=snapshot;
        BattleUiUnitSnapshot[] visible=snapshot.Units.Where(unit=>unit.IsAlive||snapshot.Corpses.Contains(unit.Cell)).ToArray();
        var visibleIds=visible.Select(unit=>unit.UnitId).ToHashSet();
        foreach(UnitInstanceId removed in _actors.Keys.Where(id=>!visibleIds.Contains(id)).ToArray())
        {if(GodotObject.IsInstanceValid(_actors[removed]))_actors[removed].QueueFree();_actors.Remove(removed);if(_unitMeters.Remove(removed,out Control? meter)&&GodotObject.IsInstanceValid(meter))meter.QueueFree();}
        foreach (BattleUiUnitSnapshot unit in visible)
        {
            if(!_actors.TryGetValue(unit.UnitId,out GodotUnitActor? actor)||!GodotObject.IsInstanceValid(actor))
            {actor=GodotUnitFactory.InstantiateActor(_unitResources[unit.DefinitionId]);actor.Scale=Vector2.One*.34f;actor.SetFacing(GodotPresentationFacingResolver.Initial(unit.PlayerNumber));_board.AddChild(actor);_actors[unit.UnitId]=actor;}
            if(!(_presentationPlayer?.IsPlaying??false))actor.Position = IsometricBattleBoardLayout.GridToScreen(unit.Cell);
            actor.SetDeathVisual(!unit.IsAlive);
            actor.SetStatuses(unit.Statuses);
            actor.ZIndex = 100 + (18-unit.Cell.X-unit.Cell.Y) * 12 + unit.Cell.X;
            if(!_unitMeters.TryGetValue(unit.UnitId,out Control? meter)||meter is not GodotCompactUnitMeter compact||!GodotObject.IsInstanceValid(meter))
            {
                if(meter is not null&&GodotObject.IsInstanceValid(meter))meter.QueueFree();
                compact=new GodotCompactUnitMeter();_board.AddChild(compact);_unitMeters[unit.UnitId]=compact;
            }
            compact.ZIndex=400+(18-unit.Cell.X-unit.Cell.Y)*12+unit.Cell.X;
            compact.Bind(actor,unit.CurrentHealth,unit.MaxHealth,unit.CurrentMana,unit.MaxMana);
        }
        foreach (Node child in _skillPanel.GetChildren()) child.QueueFree();
        _skillPanel.AddChild(Label($"Round {snapshot.Round} | Active {snapshot.ActiveUnitId.Value}\nMode {snapshot.TargetingMode} | {snapshot.Phase}", 25));
        bool aiPlayback=_battle.HasPendingAutomaticFrames||_presentationInputLocked;
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

    private void ApplyHighlights(BattleUiSnapshot snapshot)
    {
        var colors=new Dictionary<GridPoint,Color>();
        for(int y=0;y<IsometricBattleBoardLayout.GridSize;y++)for(int x=0;x<IsometricBattleBoardLayout.GridSize;x++)colors[new GridPoint(x,y)]=new Color(.32f,.42f,.47f,.18f);
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
        _board?.SetVisuals(colors,snapshot.BlockedCells??Array.Empty<GridPoint>());
    }

    private void OnBoardCellPressed(GridPoint cell)=>ApplyIntent(new ConfirmCellIntent(cell));

    private void HoverCell(GridPoint cell)
    {
        RestoreTargetingFacing();
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
        PreviewTargetingFacing(snapshot, cell);
        if(_hoverInfo is not null)_hoverInfo.Text=detail;ApplyHighlights(snapshot);
    }
    private void ClearHover(){RestoreTargetingFacing();_hoveredCell=null;if(_hoverInfo is not null)_hoverInfo.Text="Hover a cell";if(_visibleSnapshot is not null)ApplyHighlights(_visibleSnapshot);}

    private void PreviewTargetingFacing(BattleUiSnapshot snapshot, GridPoint cell)
    {
        if (!_actors.TryGetValue(snapshot.ActiveUnitId, out GodotUnitActor? actor) || !GodotObject.IsInstanceValid(actor)) return;
        GodotUnitFacing current = actor.PresentationFacing;
        GodotUnitFacing preview = current;
        if (snapshot.TargetingMode == BattleTargetingMode.Move && snapshot.LegalMoveCells.Contains(cell))
            preview = GodotPresentationFacingResolver.PreviewMove(snapshot.Units.Single(unit => unit.UnitId == snapshot.ActiveUnitId).Cell,
                _battle?.PreviewMovePath(cell) ?? Array.Empty<GridPoint>(), current);
        else if (snapshot.TargetingMode == BattleTargetingMode.Skill && snapshot.SkillPreview?.RangeCells.Contains(cell) == true)
            preview = GodotPresentationFacingResolver.PreviewTarget(snapshot.Units.Single(unit => unit.UnitId == snapshot.ActiveUnitId).Cell, cell, current);
        else return;
        _targetingFacingPreview = (snapshot.ActiveUnitId, current);
        actor.SetFacing(preview);
    }

    private void RestoreTargetingFacing()
    {
        if (_targetingFacingPreview is not { } preview) return;
        if (_actors.TryGetValue(preview.UnitId, out GodotUnitActor? actor) && GodotObject.IsInstanceValid(actor)) actor.SetFacing(preview.Facing);
        _targetingFacingPreview = null;
    }

    private void ApplyIntent(BattleUiIntent intent)
    {
        if (_battle is null) return;
        if(_presentationInputLocked)
        {
            AddLog(new BattleUiLogEntry(BattleUiLogCategory.Rejected,"presentation_in_progress","CommandRejectedEvent"));
            RefreshLog();
            return;
        }
        RestoreTargetingFacing();
        BattleUiIntentResult result = _battle.Submit(intent);
        AddEvents(result.Events);
        if(!result.Succeeded&&result.Events.Count==0&&result.FailureCode is not null)AddLog(new BattleUiLogEntry(BattleUiLogCategory.Rejected,result.FailureCode,"CommandRejectedEvent"));
        if(_battle.HasPendingAutomaticFrames){if(result.Presentation is BattlePresentationFrame pendingPresentation)BeginPresentation(pendingPresentation,true);else PlaybackStep(true);return;}
        if(result.Presentation is BattlePresentationFrame presentation)
        {
            // A terminal player action still owns its release, hit and defeat
            // presentation. Settlement starts only after that committed frame
            // reaches After; otherwise the page change hides the final action.
            _battleResultAfterPresentation=result.BattleResult;
            BeginPresentation(presentation,false);
            return;
        }
        if (result.BattleResult is PureRunBattleResult battleResult){CompleteBattle(battleResult);return;}
        RefreshBattle();
        if (!result.Succeeded) SetStatus(result.FailureCode);
    }

    private void CompleteBattle(PureRunBattleResult battleResult)
    {
        if(_settlementCommitted)return;
        _settlementCommitted=true;
        AddLog(new BattleUiLogEntry(BattleUiLogCategory.Gameplay,$"Submitting {EncounterLabel(battleResult.EncounterContentId)} BattleResult","EncounterNavigationEvent"));
        if(battleResult.EncounterContentId.Value=="encounter.pure-run.n4")
        {
            RunSessionResult layerFour=_run!.ApplyLayerFourBattleResult(battleResult);
            if(!layerFour.Succeeded){_settlementCommitted=false;SetStatus(layerFour.ErrorCode);return;}
            if(layerFour.Snapshot?.TerminalSummary is PureRunSummary summary)ShowSummary(summary);
            else ShowReadyForLayerFive(layerFour.Snapshot!.ActiveRun!);return;
        }
        if(battleResult.EncounterContentId.Value is "encounter.pure-run.e1" or "encounter.pure-run.e2" or "encounter.pure-run.special")
        {
            PureRunState? active=new GodotRunSaveStore().Load().Snapshot?.ActiveRun;
            if(active?.NodeTransaction?.NodeId.StartsWith("layer_06_",StringComparison.Ordinal)==true)
            {
                RunSessionResult layerSix=_run!.ApplyLayerFourBattleResult(battleResult);
                if(!layerSix.Succeeded){_settlementCommitted=false;SetStatus(layerSix.ErrorCode);return;}
                RouteMap(layerSix.Snapshot!.ActiveRun!);return;
            }
            PureRunFullRunService full=new(_consumables.Keys);
            bool boss=battleResult.EncounterContentId.Value.EndsWith(".special",StringComparison.Ordinal);
            RunSessionResult late=_run!.ApplyFullRunTransition(state=>boss?full.CompleteBoss(state,battleResult):full.CompleteLayerFive(state,battleResult));
            if(!late.Succeeded){_settlementCommitted=false;SetStatus(late.ErrorCode);return;}
            if(late.Snapshot?.TerminalSummary is PureRunSummary terminal)ShowSummary(terminal);else ShowSettlement(late.Snapshot!);return;
        }
        RunSessionResult settled=_run!.ApplyBattleResult(battleResult);if(!settled.Succeeded){_settlementCommitted=false;SetStatus(settled.ErrorCode);return;}ShowSettlement(settled.Snapshot!);
    }

    private void PlaybackStep(bool forced)
    {
        if(_battle is null||(_playbackPaused&&!forced)||(_presentationPlayer?.IsPlaying??false))return;
        BattleUiFrame? frame=_battle.DequeueAutomaticFrame();
        if(frame is not null){if(frame.Decision is { } decision)AddLog(new BattleUiLogEntry(BattleUiLogCategory.Ai,$"{decision.ActorId.Value} selected {decision.Intent}{(decision.SkillId is null?string.Empty:" + "+decision.SkillId.Value)} to {decision.Destination}; target {decision.TargetId?.Value??"none"} ({decision.TargetDefinitionId?.Value??"none"}); score {decision.Score:0.##} [distance {decision.DistanceScore:0.##}, damage {decision.DamageScore:0.##}, target {decision.TargetScore:0.##}, status {decision.StatusScore:0.##}]; candidates {decision.CandidateCount}",nameof(AiDecisionEvent)));AddEvents(frame.Events);BeginPresentation(frame.Presentation,true,forced&&_playbackPaused);return;}
        RefreshBattle();if(_battle.BattleResult is { } result)CompleteBattle(result);
    }
    private void TogglePause(){_playbackPaused=!_playbackPaused;_presentationPlayer?.SetPaused(_playbackPaused);AddLog(new BattleUiLogEntry(BattleUiLogCategory.Ai,_playbackPaused?"AI playback paused":"AI playback resumed","Playback"));if(!_playbackPaused&&_battle?.HasPendingAutomaticFrames==true&&!(_presentationPlayer?.IsPlaying??false))PlaybackStep(false);RefreshLog();}
    private void ToggleSpeed()
    {
        _playbackSpeed = _playbackSpeed switch { 1f => 2f, 2f => 4f, 4f => .5f, _ => 1f };
        _presentationPlayer?.SetSpeed(_playbackSpeed);
        if (_speedButton is not null) _speedButton.Text = $"Speed {_playbackSpeed:0.#}x";
        AddLog(new BattleUiLogEntry(BattleUiLogCategory.Ai,$"Playback {_playbackSpeed:0.#}x","Playback"));
        RefreshLog();
    }

    private void BeginPresentation(BattlePresentationFrame frame,bool continueAutomatic,bool pauseAfter=false)
    {
        _presentationInputLocked=true;
        _presentationAfter=frame.After;_continueAutomaticAfterPresentation=continueAutomatic;_pauseAfterCurrentFrame=pauseAfter;
        RefreshBattle(frame.Before);_presentationPlayer?.Play(frame,_actors);
        if(_playbackPaused&&!pauseAfter)_presentationPlayer?.SetPaused(true);
    }
    private void OnPresentationFrameFinished()
    {
        _presentationInputLocked=false;
        BattleUiSnapshot? after=_presentationAfter;_presentationAfter=null;if(after is not null)RefreshBattle(after);
        PureRunBattleResult? deferredResult=_battleResultAfterPresentation;_battleResultAfterPresentation=null;
        bool shouldContinue=_continueAutomaticAfterPresentation;_continueAutomaticAfterPresentation=false;
        if(deferredResult is not null){CompleteBattle(deferredResult);return;}
        if(_pauseAfterCurrentFrame){_pauseAfterCurrentFrame=false;_playbackPaused=true;RefreshLog();return;}
        if(shouldContinue&&!_playbackPaused)PlaybackStep(false);
    }

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
        if (run.Phase is PureRunPhase.AwaitingLayerFourChoice or PureRunPhase.ResolvingLayerFourNode or PureRunPhase.ReadyForLayerFive or
            PureRunPhase.AwaitingLayerSixChoice or PureRunPhase.ResolvingLayerSixNode or PureRunPhase.ReadyForBoss)
        { RouteMap(run); return; }
        if(run.Phase==PureRunPhase.ReadyForLayerSix&&run.PendingProgression.Count==0){RouteMap(run);return;}
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

    private void ShowLayerFourChoice(PureRunState run)
    {
        Control root=NewPage("LAYER 4 ROUTE","Choose exactly one route; completion ends the Phase 7C slice at ReadyForLayer5");
        var menu=new VBoxContainer{Position=new Vector2(500,220),Size=new Vector2(600,520)};root.AddChild(menu);
        menu.AddChild(Button("N4 Battle — split flank",()=>SelectLayerFourNode("layer_04_battle")));
        menu.AddChild(Button("Rest — restore 30% HP/MP",()=>SelectLayerFourNode("layer_04_rest")));
        menu.AddChild(Button("Store — deterministic 3 offers",()=>SelectLayerFourNode("layer_04_store")));
        menu.AddChild(Button("Mystery — deterministic assigned event",()=>SelectLayerFourNode("layer_04_event")));
        root.AddChild(PlaceControl(Button("Inventory",()=>ShowInventory(run)),new Vector2(650,760),new Vector2(300,55)));
    }

    private static PureRunMapDefinition LayerFourMap() => new(new ContentId("run-map.pure-run.layer4-v1"),2,new[]{
        new PureRunMapNodeDefinition("layer_04_battle",4,PureRunNodeKind.Battle,new ContentId("encounter.pure-run.n4")),
        new PureRunMapNodeDefinition("layer_04_rest",4,PureRunNodeKind.Rest,new ContentId("rest.pure-run.standard-v1")),
        new PureRunMapNodeDefinition("layer_04_store",4,PureRunNodeKind.Store,new ContentId("store.pure-run.standard-v1")),
        new PureRunMapNodeDefinition("layer_04_event",4,PureRunNodeKind.Mystery,new ContentId("event.pure-run.cursed-chest")),
        new PureRunMapNodeDefinition("layer_06_battle",6,PureRunNodeKind.Battle,new ContentId("encounter.pure-run.e1")),
        new PureRunMapNodeDefinition("layer_06_rest",6,PureRunNodeKind.Rest,new ContentId("rest.pure-run.standard-v1")),
        new PureRunMapNodeDefinition("layer_06_store",6,PureRunNodeKind.Store,new ContentId("store.pure-run.standard-v1")),
        new PureRunMapNodeDefinition("layer_06_event",6,PureRunNodeKind.Mystery,new ContentId("event.pure-run.cursed-chest"))});

    private void SelectLayerFourNode(string nodeId)
    {
        RunSessionResult result=_run!.ApplyMutation(state=>
        {
            LayerFourNodeResolution selected=new PureRunLayerFourNodeService().SelectNode(state,LayerFourMap(),nodeId);
            return new RunMutationResult(selected.Succeeded,selected.RejectionCode,selected.State);
        });
        if(!result.Succeeded){SetStatus(result.ErrorCode);return;}RouteLayerFour(result.Snapshot!.ActiveRun!);
    }

    private void RouteLayerFour(PureRunState run)
    {
        if(run.Phase==PureRunPhase.AwaitingLayerFourChoice){ShowLayerFourChoice(run);return;}
        if(run.Phase==PureRunPhase.ReadyForLayerFive){ShowReadyForLayerFive(run);return;}
        switch(run.NodeTransaction?.Kind)
        {
            case PureRunNodeKind.Battle: BeginLayerFourBattle(); break;
            case PureRunNodeKind.Rest: ShowRest(run); break;
            case PureRunNodeKind.Store: ShowStore(run); break;
            case PureRunNodeKind.Mystery: ShowMystery(run); break;
            default: SetStatus("layer4.route_missing"); break;
        }
    }

    private void RouteMap(PureRunState run)
    {
        if(run.Phase is PureRunPhase.AwaitingLayerFourChoice or PureRunPhase.ResolvingLayerFourNode){RouteLayerFour(run);return;}
        if(run.Phase==PureRunPhase.ReadyForLayerFive){ShowReadyForLayerFive(run);return;}
        if(run.Phase==PureRunPhase.ReadyForLayerSix)
        {
            if(run.PendingProgression.Count>0){ShowSettlement(new PureRunSaveSnapshot(run.Revision,run,null));return;}
            RunSessionResult unlocked=_run!.ApplyFullRunTransition(state=>new PureRunFullRunService(_consumables.Keys).UnlockLayerSix(state,LayerFourMap()));
            if(!unlocked.Succeeded){SetStatus(unlocked.ErrorCode);return;}ShowLayerSixChoice(unlocked.Snapshot!.ActiveRun!);return;
        }
        if(run.Phase==PureRunPhase.AwaitingLayerSixChoice){ShowLayerSixChoice(run);return;}
        if(run.Phase==PureRunPhase.ResolvingLayerSixNode){RouteLayerSixNode(run);return;}
        if(run.Phase==PureRunPhase.ReadyForBoss){ShowReadyForBoss(run);return;}
        RouteLayerFour(run);
    }

    private void BeginLayerFourBattle()
    {
        var encounterId=new ContentId("encounter.pure-run.n4");
        RunSessionResult result=_run!.ApplyMutation(state=>
        {
            LayerFourNodeResolution begun=new PureRunLayerFourNodeService().BeginN4(state,encounterId);
            return new RunMutationResult(begun.Succeeded,begun.RejectionCode,begun.State);
        });
        if(!result.Succeeded||result.Snapshot?.ActiveRun?.Checkpoint is null){SetStatus(result.ErrorCode);return;}
        PureRunState pending=result.Snapshot.ActiveRun;StartBattle(new EncounterRequest(pending.RunId,pending.Checkpoint.Revision,encounterId,pending.Checkpoint.Party));
    }

    private void ShowRest(PureRunState run)
    {
        Control root=NewPage("LAYER 4 — REST","Preview: living party members recover ceil(30% max HP/MP); dead characters remain dead.");
        LabelAt(root,string.Join('\n',run.Party.Select(c=>$"{c.CharacterId}: HP {c.CurrentHealth} → {(c.IsDead?c.CurrentHealth:Math.Min(c.MaxHealth,c.CurrentHealth+(int)Math.Ceiling(c.MaxHealth*.3)))} / {c.MaxHealth}, MP {c.CurrentMana} → {(c.IsDead?c.CurrentMana:Math.Min(c.MaxMana,c.CurrentMana+(int)Math.Ceiling(c.MaxMana*.3)))} / {c.MaxMana}")),new Vector2(360,250),26);
        root.AddChild(PlaceControl(Button("Confirm Rest",()=>CommitLayerFour(state=>new PureRunLayerFourNodeService().ConfirmRest(state))),new Vector2(650,650),new Vector2(300,65)));
    }

    private void ShowStore(PureRunState run)
    {
        if(run.MapState?.StoreOffers is not {Count:>0})
        {
            RunStoreOffer[] gear=_equipment.Values.Select(v=>new RunStoreOffer(v.ContentId,v.Price,false)).ToArray();
            RunStoreOffer[] items=_consumables.Values.Select(v=>new RunStoreOffer(v.ContentId,v.Price,true)).ToArray();
            RunSessionResult opened=_run!.ApplyMutation(state=>{LayerFourNodeResolution r=new PureRunLayerFourNodeService().OpenStore(state,gear,items);return new RunMutationResult(r.Succeeded,r.RejectionCode,r.State);});
            if(!opened.Succeeded){SetStatus(opened.ErrorCode);return;}run=opened.Snapshot!.ActiveRun!;
        }
        Control root=NewPage("LAYER 4 — STORE",$"Gold {run.Gold}. Stock is persisted and will not reroll after Reload.");
        var menu=new VBoxContainer{Position=new Vector2(430,210),Size=new Vector2(740,520)};root.AddChild(menu);
        foreach(RunStoreOfferState offer in run.MapState!.StoreOffers!)
        {Button buy=Button($"{offer.ContentId.Value} — {offer.Price} gold{(offer.Purchased?" [SOLD]":"")}",()=>PurchaseStore(offer.InstanceId));buy.Disabled=offer.Purchased;menu.AddChild(buy);}
        menu.AddChild(Button("Leave Store",()=>CommitLayerFour(state=>new PureRunLayerFourNodeService().LeaveStore(state))));
    }

    private void PurchaseStore(ItemInstanceId id)
    {
        RunSessionResult result=_run!.ApplyMutation(state=>{LayerFourNodeResolution r=new PureRunLayerFourNodeService().Purchase(state,id,_consumables,_equipment);return new RunMutationResult(r.Succeeded,r.RejectionCode,r.State);});
        if(!result.Succeeded){SetStatus(result.ErrorCode);return;}ShowStore(result.Snapshot!.ActiveRun!);
    }

    private void ShowMystery(PureRunState run)
    {
        string sourceId=run.MapState!.MysteryEventAssignments[run.NodeTransaction!.NodeId];
        using JsonDocument document=JsonDocument.Parse(_layerFourEventPayloads[sourceId]);JsonElement rootElement=document.RootElement;
        Control root=NewPage($"LAYER 4 — {rootElement.GetProperty("title").GetString()}",rootElement.GetProperty("description").GetString()!);
        var menu=new VBoxContainer{Position=new Vector2(330,180),Size=new Vector2(940,620)};root.AddChild(menu);
        if(run.MapState.MysteryResolution is RunMysteryResolutionState resolved)
        {
            menu.AddChild(Label($"{resolved.OptionId}: {(resolved.Succeeded?"Success":"Failure")} — roll {resolved.Roll}, chance {resolved.SuccessRate}%\nEffect: {resolved.Effect} {resolved.Amount}",24));
            menu.AddChild(Button("Confirm Result",()=>CommitLayerFour(state=>new PureRunLayerFourNodeService().ConfirmMystery(state,_consumables))));return;
        }
        foreach(JsonElement option in rootElement.GetProperty("options").EnumerateArray())
        {
            string optionId=option.GetProperty("id").GetString()!;string attribute=option.GetProperty("attribute").GetString()!;
            foreach(RunCharacterState character in run.Party.Where(c=>!c.IsDead))
            {int value=AttributeValue(character.Attributes,attribute);int rate=Math.Clamp(option.GetProperty("baseSuccessRate").GetInt32()+(value-5)*5,5,95);menu.AddChild(Button($"{option.GetProperty("text").GetString()} — {character.CharacterId}: {rate}%",()=>ResolveMystery(sourceId,optionId,character.CharacterId)));}
        }
    }

    private void ResolveMystery(string sourceId,string optionId,string characterId)
    {
        using JsonDocument document=JsonDocument.Parse(_layerFourEventPayloads[sourceId]);JsonElement option=document.RootElement.GetProperty("options").EnumerateArray().Single(v=>v.GetProperty("id").GetString()==optionId);RunStoreResult loaded=new GodotRunSaveStore().Load();RunCharacterState character=loaded.Snapshot!.ActiveRun!.Party.Single(v=>v.CharacterId==characterId);JsonElement success=option.GetProperty("success");JsonElement failure=option.TryGetProperty("failure",out JsonElement f)&&f.ValueKind!=JsonValueKind.Null?f:success;
        RunSessionResult result=_run!.ApplyMutation(state=>{LayerFourNodeResolution r=new PureRunLayerFourNodeService().ResolveMystery(state,sourceId,optionId,characterId,option.GetProperty("baseSuccessRate").GetInt32(),AttributeValue(character.Attributes,option.GetProperty("attribute").GetString()!),success.GetProperty("type").GetString()!,success.GetProperty("amount").GetInt32(),EffectContentId(success),failure.GetProperty("type").GetString()!,failure.GetProperty("amount").GetInt32(),EffectContentId(failure));return new RunMutationResult(r.Succeeded,r.RejectionCode,r.State);});
        if(!result.Succeeded){SetStatus(result.ErrorCode);return;}ShowMystery(result.Snapshot!.ActiveRun!);
    }

    private static ContentId? EffectContentId(JsonElement effect){if(!effect.TryGetProperty("itemId",out JsonElement item))return null;string value=item.GetString()!;return value switch{"cleansing_potion"=>new ContentId("item.consumable.cleansing-potion"),"Assets/Tactics/ScriptableObjects/Buffs/EventDamageReduction.asset"=>new ContentId("buff.event-damage-reduction"),"Assets/Tactics/ScriptableObjects/Buffs/EventDamageTakenUp.asset"=>new ContentId("buff.event-damage-taken-up"),_=>new ContentId(value)};}
    private static int AttributeValue(UnitAttributes a,string name)=>name switch{"Strength"=>a.Strength,"Agility"=>a.Agility,"Constitution"=>a.Constitution,"Intelligence"=>a.Intelligence,"Charisma"=>a.Charisma,"Luck"=>a.Luck,"None"=>5,_=>5};
    private void CommitLayerFour(Func<PureRunState,LayerFourNodeResolution> command){RunSessionResult result=_run!.ApplyLayerFourMutation(state=>{LayerFourNodeResolution r=command(state);return new RunMutationResult(r.Succeeded,r.RejectionCode,r.State);});if(!result.Succeeded){SetStatus(result.ErrorCode);return;}if(result.Snapshot?.TerminalSummary is PureRunSummary summary)ShowSummary(summary);else RouteMap(result.Snapshot!.ActiveRun!);}

    private void ShowReadyForLayerFive(PureRunState run)
    {
        string route=run.NodeTransaction?.Kind.ToString()??"Unknown";
        Control root=NewPage("READY FOR LAYER 5",$"Layer 4 {route} resolved. Continue into the deterministic Elite encounter.");
        LabelAt(root,$"Run {run.RunId}\nRevision {run.Revision}\nGold {run.Gold} | Battles {run.BattlesCompleted} | Kills {run.EnemiesDefeated}\nParty: {string.Join(" | ",run.Party.Select(c=>$"{c.CharacterId} HP {c.CurrentHealth}/{c.MaxHealth} MP {c.CurrentMana}/{c.MaxMana}"))}\nTransactions: {string.Join(", ",run.AppliedTransactionKeys)}",new Vector2(260,260),22);
        root.AddChild(PlaceControl(Button("Begin Layer 5 Elite",BeginLayerFive),new Vector2(650,650),new Vector2(300,65)));
    }

    private void BeginLayerFive(){RunSessionResult result=_run!.ApplyFullRunTransition(state=>new PureRunFullRunService(_consumables.Keys).BeginLayerFive(state,LayerFourMap()));if(!result.Succeeded||result.EncounterRequest is null){SetStatus(result.ErrorCode);return;}StartBattle(result.EncounterRequest);}
    private void ShowLayerSixChoice(PureRunState run){Control root=NewPage("LAYER 6 ROUTE","Choose one final route before the Special Boss.");var menu=new VBoxContainer{Position=new Vector2(500,220),Size=new Vector2(600,520)};root.AddChild(menu);menu.AddChild(Button("Elite Battle",()=>SelectLayerSixNode("layer_06_battle")));menu.AddChild(Button("Rest",()=>SelectLayerSixNode("layer_06_rest")));menu.AddChild(Button("Store",()=>SelectLayerSixNode("layer_06_store")));menu.AddChild(Button("Mystery",()=>SelectLayerSixNode("layer_06_event")));}
    private void SelectLayerSixNode(string nodeId){RunSessionResult result=_run!.ApplyMutation(state=>{LayerFourNodeResolution selected=new PureRunLayerFourNodeService().SelectNode(state,LayerFourMap(),nodeId);return new RunMutationResult(selected.Succeeded,selected.RejectionCode,selected.State);});if(!result.Succeeded){SetStatus(result.ErrorCode);return;}RouteLayerSixNode(result.Snapshot!.ActiveRun!);}
    private void RouteLayerSixNode(PureRunState run){switch(run.NodeTransaction?.Kind){case PureRunNodeKind.Battle:BeginLayerSixBattle();break;case PureRunNodeKind.Rest:ShowRest(run);break;case PureRunNodeKind.Store:ShowStore(run);break;case PureRunNodeKind.Mystery:ShowMystery(run);break;default:SetStatus("layer6.route_missing");break;}}
    private void BeginLayerSixBattle(){RunSessionResult result=_run!.ApplyMutation(state=>{ContentId id=new PureRunMapService(LayerFourMap()).SelectLateEncounter(state.Seed,"layer_06_battle");LayerFourNodeResolution begun=new PureRunLayerFourNodeService().BeginN4(state,id);return new RunMutationResult(begun.Succeeded,begun.RejectionCode,begun.State);});if(!result.Succeeded||result.Snapshot?.ActiveRun?.Checkpoint is null){SetStatus(result.ErrorCode);return;}PureRunState pending=result.Snapshot.ActiveRun;StartBattle(new EncounterRequest(pending.RunId,pending.Checkpoint.Revision,pending.EncounterContentId,pending.Checkpoint.Party));}
    private void ShowReadyForBoss(PureRunState run){Control root=NewPage("READY FOR LAYER 7","Layer 6 committed. The Special Boss is the terminal encounter.");root.AddChild(PlaceControl(Button("Begin Special Boss",BeginBoss),new Vector2(650,560),new Vector2(300,70)));}
    private void BeginBoss(){RunSessionResult result=_run!.ApplyFullRunTransition(state=>new PureRunFullRunService(_consumables.Keys).BeginBoss(state,LayerFourMap()));if(!result.Succeeded||result.EncounterRequest is null){SetStatus(result.ErrorCode);return;}StartBattle(result.EncounterRequest);}

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
        Control root=NewPage("PROGRESSION",$"{character.CharacterId}: attribute allocation → skill selection");
        var menu=new VBoxContainer{Position=new Vector2(430,160),Size=new Vector2(740,650)};root.AddChild(menu);
        var progressionService=new RunInventoryProgressionService();
        if (pending.ProposedAttributes is not UnitAttributes proposed)
        {
            menu.AddChild(Label($"Step 1/2 — choose one attribute\nSTR {character.Attributes.Strength}  AGI {character.Attributes.Agility}  CON {character.Attributes.Constitution}\nINT {character.Attributes.Intelligence}  CHA {character.Attributes.Charisma}  LUCK {character.Attributes.Luck}",22));
            foreach (string attribute in new[] { "Strength", "Agility", "Constitution", "Intelligence", "Charisma", "Luck" })
            {
                string selectedAttribute = attribute;
                menu.AddChild(Button($"+1 {selectedAttribute}", () => AllocateProgressionAttribute(pending.TransactionKey,
                    Raise(character.Attributes, selectedAttribute))));
            }
        }
        else
        {
            RunCharacterState preview = new(character.CharacterId, character.UnitContentId, character.Level, proposed,
                character.CurrentHealth, character.MaxHealth, character.CurrentMana, character.MaxMana, character.IsDead,
                character.LearnedSkills, character.Equipment, character.CarriedConsumables, character.LearnedSkillStates);
            menu.AddChild(Label($"Step 2/2 — choose a skill\nSTR {proposed.Strength}  AGI {proposed.Agility}  CON {proposed.Constitution}\nINT {proposed.Intelligence}  CHA {proposed.Charisma}  LUCK {proposed.Luck}",22));
            SkillDefinition[] candidates=progressionService.GrowthOffer(run,preview,_skills,_runDefinition!).ToArray();
            foreach(SkillDefinition skill in candidates)
                menu.AddChild(Button(GrowthChoiceLabel(preview,skill),()=>
                    CommitMutation(state=>new RunInventoryProgressionService().CompleteProgression(state,state.Revision,pending.TransactionKey,proposed,skill.ContentId,_skills,_runDefinition!))));
            if(candidates.Length==0)
            {
                menu.AddChild(Label("No skill candidate is legal after this allocation. Attribute-only confirmation is allowed.",18));
                menu.AddChild(Button("Confirm Attribute",()=>CommitMutation(state=>progressionService.CompleteProgression(state,state.Revision,pending.TransactionKey,proposed,null,_skills,_runDefinition!))));
            }
        }
        root.AddChild(PlaceControl(Button("Back",()=>ShowSettlement(new PureRunSaveSnapshot(run.Revision,run,null))),new Vector2(650,820),new Vector2(300,55)));
    }

    private static string GrowthChoiceLabel(RunCharacterState character,SkillDefinition skill)
    {
        RunLearnedSkillState? learned=character.LearnedSkillStates.FirstOrDefault(value=>value.BranchId==skill.BranchId);
        string raw=skill.BranchId.Split('.').Last();
        string name=string.Join(' ',raw.Split('-').Select(value=>char.ToUpperInvariant(value[0])+value[1..]));
        string requirement=string.IsNullOrEmpty(skill.RequiredAttribute)?string.Empty:$" (requires {skill.RequiredAttribute} {skill.MinimumAttribute})";
        return learned is null
            ? $"Learn {name} Lv{skill.Level}{requirement}"
            : $"Upgrade {name} Lv{learned.Level} → Lv{skill.Level}{requirement}";
    }

    private void AllocateProgressionAttribute(string transactionKey, UnitAttributes attributes)
    {
        RunSessionResult result = _run!.ApplyMutation(state => new RunInventoryProgressionService()
            .AllocateProgressionAttributes(state, state.Revision, transactionKey, attributes));
        if (!result.Succeeded || result.Snapshot?.ActiveRun is not PureRunState run)
        {
            SetStatus(result.ErrorCode);
            return;
        }
        PendingProgression pending = run.PendingProgression.Single(value => value.TransactionKey == transactionKey);
        ShowProgression(run, pending);
    }

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

    private Control NewPage(string title, string subtitle, bool battleBackdrop = false)
    {
        // The old page owns every actor and meter. Queueing the page frees those
        // children, so page navigation must forget their managed references rather
        // than attempting to QueueFree the disposed children during the next refresh.
        _page?.QueueFree();
        _actors.Clear();
        _unitMeters.Clear();
        _visibleSnapshot=null;
        _hoveredCell=null;
        _targetingFacingPreview=null;
        DisposePresentationPlayer();_board=null;
        _skillPanel=null;
        _turnOrder=null;
        _speedButton=null;
        _hoverInfo=null;
        _eventLog=null;
        var root = new Control(); root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(root); _page = root;
        Control background = battleBackdrop ? new GodotBattleBackdrop() : new ColorRect { Color = new Color("657784") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); root.AddChild(background);
        LabelAt(root, title, new Vector2(70, 35), 40); LabelAt(root, subtitle, new Vector2(70, 82), 20); return root;
    }

    private void DisposePresentationPlayer()
    {
        if(_presentationPlayer is null)return;
        _presentationPlayer.FrameFinished-=OnPresentationFrameFinished;
        _presentationPlayer.Clear();
        _presentationPlayer=null;
        _presentationAfter=null;
        _battleResultAfterPresentation=null;
        _continueAutomaticAfterPresentation=false;
        _pauseAfterCurrentFrame=false;
        _presentationInputLocked=false;
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
