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
    public const int PauseOverlayZIndex = 4000;
    public static readonly Vector2 UnitMeterSize = new(44, 18);
    public const int UnitMeterBarHeight = 7;
    private readonly Dictionary<ContentId, UnitDefinition> _units = new();
    private readonly Dictionary<ContentId, UnitDefinitionResource> _unitResources = new();
    private readonly Dictionary<ContentId, SkillDefinition> _skills = new();
    private readonly Dictionary<ContentId, SkillUiMetadata> _skillUi = new();
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
    private Container? _skillPanel;
    private GodotIsometricBattleBoard? _board;
    private RichTextLabel? _eventLog;
    private GodotBattleCheatConsole? _cheatConsole;
    private Label? _hoverInfo;
    private Label? _turnOrder;
    private Button? _speedButton;
    private Button? _stepButton;
    private Button? _endTurnButton;
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
    private readonly PureRunFlowProjector _flowProjector = new();
    private GodotRogueMapView? _mapView;
    private Label? _mapDetail;
    private int _catalogCount;
    private string? _inventoryCharacterId;
    private bool _inventoryEquipmentTab = true;
    private string? _inventorySelectedInstanceId;
    private readonly Dictionary<string, UnitAttributes> _progressionDrafts = new(StringComparer.Ordinal);
    private GodotDamageNumberLayer? _damageNumbers;
    private GodotDroppedSpearLayer? _droppedSpears;
    private Control? _pauseMenu;
    private bool _pauseMenuPausedPlayback;
    private bool _pauseMenuControlsBattlePlayback;

    private enum InventoryReturnTarget { Home, Settlement, RunRoute }

    public bool IsReadyForInput => _run is not null && _page is not null && _units.Count == 12 &&
        _skills.Count >= 16 && _ai.Count == 6 && _layouts.Count >= 2 && _encounters.Count >= 3 && _catalogCount == 124;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        LoadCatalogs();
        ShowHome();
    }

    public override void _ExitTree()=>DisposePresentationPlayer();
    public override void _Process(double delta) { }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_battle is not null && inputEvent.IsActionPressed("toggle_console"))
        {
            if (_cheatConsole is not null) _cheatConsole.Visible = !_cheatConsole.Visible;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_cheatConsole?.Visible == true)
        {
            if (inputEvent is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
            { _cheatConsole.Visible = false; GetViewport().SetInputAsHandled(); }
            return;
        }
        if (_pauseMenu?.Visible == true)
        {
            if (inputEvent is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) ClosePauseMenu();
            return;
        }
        if (_battle is null)
        {
            if (inputEvent is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _pauseMenu is not null)
            { OpenPauseMenu(); GetViewport().SetInputAsHandled(); }
            return;
        }
        if (inputEvent is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Escape)
            {
                if (_visibleSnapshot?.TargetingMode != BattleTargetingMode.None) ApplyIntent(new CancelTargetingIntent());
                else OpenPauseMenu();
            }
            else if (key.Keycode is Key.Enter or Key.KpEnter) ApplyIntent(new EndTurnIntent());
        }
        else if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
            ApplyIntent(new CancelTargetingIntent());
    }

    private void LoadCatalogs()
    {
        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>("res://content/ContentCatalog.tres")
            ?? throw new InvalidOperationException("Canonical Catalog is missing.");
        _catalogCount = catalog.Entries.Length;
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
                    _skills[id] = skill.ToCoreDefinition(); _skillUi[id] = SkillUiMetadata.From(skill); break;
                case PoisonSpearSkillResource poison:
                    _skills[id] = poison.ToCoreDefinition(); _skillUi[id] = SkillUiMetadata.From(poison); break;
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
        PureRunContentValidator.ValidateSkillReferences(_runDefinition, _skills.Keys);
        _run = new PureRunSessionService(_runDefinition, new GodotRunSaveStore(), mapDefinition: LayerFourMap());
    }

    private void ShowHome()
    {
        _logs.Clear();_visibleSnapshot=null;
        _battle = null;
        Control root = NewPage("PURE RUN", "Seven-layer deterministic run");
        VBoxContainer menu = new() { Position = new Vector2(620, 310), Size = new Vector2(360, 320) };
        root.AddChild(menu);
        Button newRun = Button("New Run", () => StartNewRun()); menu.AddChild(newRun);
        RunStoreResult loaded = new GodotRunSaveStore().Load();
        Button continueRun = Button(loaded.Snapshot?.PendingRunSetup is null ? "Continue" : "Resume New Run Setup", ContinueRun);
        continueRun.Disabled = !loaded.Succeeded || loaded.Snapshot is null ||
            (loaded.Snapshot.ActiveRun is null && loaded.Snapshot.PendingRunSetup is null && loaded.Snapshot.TerminalSummary is null);
        menu.AddChild(continueRun);
        if (loaded.Snapshot?.ActiveRun is not null) menu.AddChild(Button("Inventory", () => ShowInventory(loaded.Snapshot.ActiveRun, InventoryReturnTarget.Home)));
        menu.AddChild(Button("Quit", () => GetTree().Quit()));
        string status = loaded.Snapshot?.PendingRunSetup is PendingRunSetup setup
            ? $"New Run setup: {setup.CurrentCharacterId}"
            : loaded.Snapshot?.ActiveRun is null ? "No active run" : $"Active run: {loaded.Snapshot.ActiveRun.EncounterContentId.Value}";
        _status = LabelAt(root, status, new Vector2(620, 560), 22);
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
        RunSessionResult started = _run!.BeginNewRunSetup(7);
        if (!started.Succeeded) { SetStatus(started.ErrorCode); return; }
        ShowNewRunSetup(started.Snapshot!);
    }

    private void ContinueRun()
    {
        RunStoreResult loaded = new GodotRunSaveStore().Load();
        if (loaded.Succeeded && loaded.Snapshot?.PendingRunSetup is not null)
        {
            ShowNewRunSetup(loaded.Snapshot);
            return;
        }
        RunSessionResult resumed = _run!.ResumeRun();
        if (!resumed.Succeeded) { SetStatus(resumed.ErrorCode); return; }
        if (resumed.EncounterRequest is EncounterRequest request) StartBattle(request);
        else if (resumed.Snapshot is not null) RouteRunState(resumed.Snapshot);
    }

    private void ShowNewRunSetup(PureRunSaveSnapshot snapshot)
    {
        PendingRunSetup setup = snapshot.PendingRunSetup ?? throw new InvalidOperationException("Pending setup is missing.");
        PureRunPartyTemplate template = _runDefinition!.Party[setup.CurrentCharacterIndex];
        Control root = NewPage("NEW RUN — STARTING SKILL", $"Choose 1 of 3 for {template.CharacterId} ({setup.CurrentCharacterIndex + 1}/3)");
        var choices = new VBoxContainer { Position = new Vector2(470, 230), Size = new Vector2(660, 430) };
        root.AddChild(choices);
        foreach (ContentId skill in template.EffectiveStartingSkillChoices)
        {
            ContentId captured = skill;
            SkillUiMetadata metadata = _skillUi[captured];
            choices.AddChild(Button($"{metadata.DisplayName} Lv{metadata.Level}\n{metadata.Description}\nMP {metadata.ManaCost}  Range {RangeLabel(metadata)}", () =>
            {
                RunSessionResult result = _run!.ChooseStartingSkill(template.CharacterId, captured);
                if (!result.Succeeded) { SetStatus(result.ErrorCode); return; }
                if (result.Snapshot!.PendingRunSetup is not null) ShowNewRunSetup(result.Snapshot);
                else RouteRunState(result.Snapshot);
            }));
        }
        root.AddChild(PlaceControl(Button("Cancel", () =>
        {
            RunSessionResult canceled = _run!.CancelNewRunSetup();
            if (!canceled.Succeeded) { SetStatus(canceled.ErrorCode); return; }
            ShowHome();
        }), new Vector2(650, 720), new Vector2(300, 60)));
        _status = LabelAt(root, "The previous active run is preserved until all three choices are confirmed.", new Vector2(470, 680), 18);
    }

    private void BeginReadyEncounter()
    {
        AddLog(new BattleUiLogEntry(BattleUiLogCategory.Gameplay,"Settlement Continue requested the next encounter","EncounterNavigationEvent"));
        RunSessionResult resumed = _run!.ResumeRun();
        if (!resumed.Succeeded)
        {
            SetStatus(resumed.ErrorCode);
            return;
        }
        if (resumed.EncounterRequest is EncounterRequest pendingRequest)
        {
            StartBattle(pendingRequest);
            return;
        }
        if (resumed.Snapshot?.ActiveRun is not { Phase: PureRunPhase.Ready } ||
            resumed.Snapshot.ActiveRun.PendingProgression.Count > 0)
        {
            RouteRunState(resumed.Snapshot!);
            return;
        }
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
        Transform2D boardFit = GodotBattleBoardFitter.Fit(GodotBattleBoardFitter.BoardBounds(), new Rect2(30, 90, 1540, 650));
        float boardScale = boardFit.X.Length();
        _board = new GodotIsometricBattleBoard { Position = boardFit.Origin, Scale = Vector2.One * boardScale, Size = new Vector2(1200, 650) };
        _board.PointerPressed += OnBoardPointerPressed;
        _board.CellHovered += HoverCell;
        _board.HoverCleared += ClearHover;
        _board.PointerMoved += UpdateHoveredMeter;
        _board.PointerExited += HideUnitMeters;
        root.AddChild(_board);
        _presentationPlayer = new GodotBattlePresentationPlayer();
        _presentationPlayer.Configure(_presentationProfile ?? new StandardUnitPresentationResource());
        _presentationPlayer.ConfigureSkills(_skillPresentationProfiles);
        _presentationPlayer.SetSpeed(_playbackSpeed);
        _presentationPlayer.FrameFinished+=OnPresentationFrameFinished;
        _damageNumbers = new GodotDamageNumberLayer();
        _damageNumbers.Configure(_actors);
        _presentationPlayer.NumberRequested += _damageNumbers.Spawn;
        _board.AddChild(_presentationPlayer);
        _board.AddChild(_damageNumbers);
        _droppedSpears = new GodotDroppedSpearLayer { ZIndex = 88 };
        _board.AddChild(_droppedSpears);
        var actionScroll = new ScrollContainer { Position = new Vector2(30, 765), Size = new Vector2(1120, 110) };
        root.AddChild(actionScroll);
        _skillPanel = new HBoxContainer { CustomMinimumSize = new Vector2(1120, 90) };
        actionScroll.AddChild(_skillPanel);
        _turnOrder=LabelAt(root,string.Empty,new Vector2(470,32),18);_turnOrder.Size=new Vector2(660,50);_turnOrder.HorizontalAlignment=HorizontalAlignment.Center;
        _hoverInfo=LabelAt(root,"Hover a unit or cell",new Vector2(30,115),16);_hoverInfo.Size=new Vector2(430,110);_hoverInfo.AutowrapMode=TextServer.AutowrapMode.WordSmart;
        var controls=new HBoxContainer{Position=new Vector2(1160,32),Size=new Vector2(410,55)};root.AddChild(controls);
        controls.AddChild(SmallButton("Pause/Resume",TogglePause));_stepButton=SmallButton("Step",()=>{if(_playbackPaused)PlaybackStep(true);});_stepButton.Disabled=true;controls.AddChild(_stepButton);_speedButton=SmallButton("Speed 1x",ToggleSpeed);controls.AddChild(_speedButton);
        _cheatConsole=new GodotBattleCheatConsole();_cheatConsole.ClearRequested+=()=>{_logs.Clear();RefreshLog();};root.AddChild(_cheatConsole);
        _endTurnButton=Button("End Turn (Enter)",()=>ApplyIntent(new EndTurnIntent()));root.AddChild(PlaceControl(_endTurnButton,new Vector2(1330,805),new Vector2(230,65)));
        BuildPauseMenu(root);
        _eventLog=null;
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
            {actor=GodotUnitFactory.InstantiateActor(_unitResources[unit.DefinitionId]);actor.Scale=Vector2.One*.34f;actor.SetFacing(GodotPresentationFacingResolver.Initial(unit.PlayerNumber));actor.ConfigurePresentation(_presentationProfile??new StandardUnitPresentationResource());_board.AddChild(actor);_actors[unit.UnitId]=actor;}
            if(!(_presentationPlayer?.IsPlaying??false))actor.Position = IsometricBattleBoardLayout.GridToScreen(unit.Cell);
            actor.SetDeathVisual(!unit.IsAlive);
            actor.SetSpearHeld(unit.DefinitionId.Value != "unit.pure-run.amazon" || !snapshot.DroppedSpears.ContainsKey(unit.UnitId));
            actor.SetStatuses(unit.IsAlive ? unit.Statuses : Array.Empty<BattleUiStatusSnapshot>());
            actor.ZIndex = 100 + (18-unit.Cell.X-unit.Cell.Y) * 12 + unit.Cell.X;
            if(!_unitMeters.TryGetValue(unit.UnitId,out Control? meter)||meter is not GodotCompactUnitMeter compact||!GodotObject.IsInstanceValid(meter))
            {
                if(meter is not null&&GodotObject.IsInstanceValid(meter))meter.QueueFree();
                compact=new GodotCompactUnitMeter();_board.AddChild(compact);_unitMeters[unit.UnitId]=compact;
            }
            compact.ZIndex=400+(18-unit.Cell.X-unit.Cell.Y)*12+unit.Cell.X;
            compact.Bind(actor,unit.CurrentHealth,unit.MaxHealth,unit.CurrentMana,unit.MaxMana);
        }
        _droppedSpears?.Sync(snapshot.DroppedSpears);
        foreach (Node child in _skillPanel.GetChildren()) child.QueueFree();
        BattleUiUnitSnapshot activeSnapshot=snapshot.Units.Single(unit=>unit.UnitId==snapshot.ActiveUnitId);
        if(_hoverInfo is not null)_hoverInfo.Text=$"{EncounterLabel(_currentEncounterId!.Value)} | {activeSnapshot.UnitId.Value}\nHP {activeSnapshot.CurrentHealth}/{activeSnapshot.MaxHealth}  MP {activeSnapshot.CurrentMana}/{activeSnapshot.MaxMana}\nStatus: {string.Join(", ",activeSnapshot.StatusIds.Select(id=>id.Value))}";
        bool aiPlayback=_battle.HasPendingAutomaticFrames||_presentationInputLocked;
        Button moveButton=ActionButton("Move", () => ApplyIntent(new BeginMoveIntent()));moveButton.Disabled=aiPlayback||snapshot.Phase!=PlayableBattlePhase.PlayerTurn;_skillPanel.AddChild(moveButton);
        bool spearDropped=snapshot.DroppedSpears.ContainsKey(snapshot.ActiveUnitId);
        foreach (SkillDefinition skill in snapshot.ActiveSkills.Where(skill => !skill.IsPassive&&(!skill.Hidden||skill.ExecutionKind==SkillExecutionKind.PickupSpear&&spearDropped)))
        {
            BattleUiUnitSnapshot activeUnit=snapshot.Units.Single(unit=>unit.UnitId==snapshot.ActiveUnitId);
            int uses=activeUnit.SuccessfulSkillUses.TryGetValue(skill.ContentId,out int count)?count:0;
            string? usageFailure=skill.IsBasicAbility&&uses>=1?"basic_ability_already_used":!skill.IsBasicAbility&&skill.MaxUsesPerTurn>0&&uses>=skill.MaxUsesPerTurn?"ability_use_limit_reached":null;
            string displayName = _skillUi.TryGetValue(skill.ContentId, out SkillUiMetadata? metadata)
                ? metadata.DisplayName
                : skill.ContentId.Value.Split('.').Reverse().Skip(skill.Level > 0 ? 1 : 0).FirstOrDefault() ?? skill.ContentId.Value;
            Button skillButton=ActionButton(FormatBattleActionLabel(displayName, skill.ManaCost, usageFailure is not null), () => ApplyIntent(new SelectSkillIntent(skill.ContentId)));skillButton.Disabled=aiPlayback||usageFailure is not null;skillButton.TooltipText=usageFailure??SkillTooltip(skill);_skillPanel.AddChild(skillButton);
        }
        foreach(SkillDefinition passive in snapshot.ActiveSkills.Where(skill=>skill.IsPassive))_skillPanel.AddChild(Label($"Passive: {passive.ContentId.Value}",16));
        if(_endTurnButton is not null)_endTurnButton.Disabled=aiPlayback||snapshot.Phase!=PlayableBattlePhase.PlayerTurn;
        ApplyHighlights(snapshot);
        bool presentationPlaying = _presentationPlayer?.IsPlaying ?? false;
        _board.FollowActiveActor(ShouldShowActiveMarker(_presentationInputLocked, presentationPlaying)
            ? _actors.GetValueOrDefault(snapshot.ActiveUnitId)
            : null);
        if(_turnOrder is not null)_turnOrder.Text=$"Round {snapshot.Round} | Turn: "+string.Join(" → ",snapshot.TurnOrder.Select((id,index)=>$"{(index==snapshot.ActiveTurnIndex?"▶":"")}{id.Value}{(snapshot.Units.First(unit=>unit.UnitId==id).IsAlive?string.Empty:"✝")}"));
        RefreshLog();
    }

    private void ApplyHighlights(BattleUiSnapshot snapshot)
    {
        var colors=new Dictionary<GridPoint,Color>();
        for(int y=0;y<IsometricBattleBoardLayout.GridSize;y++)for(int x=0;x<IsometricBattleBoardLayout.GridSize;x++)colors[new GridPoint(x,y)]=new Color(.32f,.42f,.47f,.18f);
        foreach(BattleUiUnitSnapshot unit in snapshot.Units.Where(unit=>unit.IsAlive))
            colors[unit.Cell]=unit.UnitId==snapshot.ActiveUnitId?colors[unit.Cell]:unit.PlayerNumber==0?new Color(.34f,.52f,.62f,.6f):colors[unit.Cell];
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

    private void OnBoardPointerPressed(Vector2 pointer)
    {
        UnitInstanceId? unitId = GodotUnitPointerResolver.Resolve(_actors, pointer);
        if (unitId is UnitInstanceId resolved && _visibleSnapshot?.Units.FirstOrDefault(unit => unit.UnitId == resolved) is BattleUiUnitSnapshot unit)
        {
            OnBoardCellPressed(unit.Cell);
            return;
        }
        if (IsometricBattleBoardLayout.TryScreenToGrid(pointer, out GridPoint cell)) OnBoardCellPressed(cell);
    }

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

    private void UpdateHoveredMeter(Vector2 pointer)
    {
        UnitInstanceId? hovered = GodotUnitPointerResolver.Resolve(_actors, pointer);
        if (hovered is UnitInstanceId unitId && _visibleSnapshot?.Units.FirstOrDefault(unit => unit.UnitId == unitId) is BattleUiUnitSnapshot unit)
            HoverCell(unit.Cell);
        foreach ((UnitInstanceId id, Control meter) in _unitMeters)
            if (GodotObject.IsInstanceValid(meter)) meter.Visible = hovered is UnitInstanceId value && value == id;
    }

    private void HideUnitMeters()
    {
        foreach (Control meter in _unitMeters.Values)
            if (GodotObject.IsInstanceValid(meter)) meter.Visible = false;
    }

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
        if (ShouldBlockBattleIntent(_cheatConsole?.Visible == true, _presentationInputLocked, _pauseMenu?.Visible == true))
        {
            string reason = _cheatConsole?.Visible == true ? "cheat_console_open" : "presentation_in_progress";
            AddLog(new BattleUiLogEntry(BattleUiLogCategory.Rejected,reason,"CommandRejectedEvent"));
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

    internal static bool ShouldBlockBattleIntent(bool cheatConsoleVisible, bool presentationInputLocked,
        bool pauseMenuVisible = false) => cheatConsoleVisible || presentationInputLocked || pauseMenuVisible;

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
            else ShowRunMap(layerFour.Snapshot!.ActiveRun!);return;
        }
        if(battleResult.EncounterContentId.Value is "encounter.pure-run.e1" or "encounter.pure-run.e2" or "encounter.pure-run.special")
        {
            PureRunState? active=new GodotRunSaveStore().Load().Snapshot?.ActiveRun;
            bool boss=battleResult.EncounterContentId.Value.EndsWith(".special",StringComparison.Ordinal);
            if(!boss&&active?.NodeTransaction?.NodeId.StartsWith("layer_06_",StringComparison.Ordinal)==true)
            {
                RunSessionResult layerSix=_run!.ApplyLayerFourBattleResult(battleResult);
                if(!layerSix.Succeeded){_settlementCommitted=false;SetStatus(layerSix.ErrorCode);return;}
                RouteMap(layerSix.Snapshot!.ActiveRun!);return;
            }
            PureRunFullRunService full=new(_consumables.Keys);
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
    private void TogglePause(){_playbackPaused=!_playbackPaused;if(_stepButton is not null)_stepButton.Disabled=!_playbackPaused;_presentationPlayer?.SetPaused(_playbackPaused);_damageNumbers?.SetPaused(_playbackPaused);AddLog(new BattleUiLogEntry(BattleUiLogCategory.Ai,_playbackPaused?"AI playback paused":"AI playback resumed","Playback"));if(!_playbackPaused&&_battle?.HasPendingAutomaticFrames==true&&!(_presentationPlayer?.IsPlaying??false))PlaybackStep(false);RefreshLog();}
    private void ToggleSpeed()
    {
        _playbackSpeed = _playbackSpeed switch { 1f => 2f, 2f => 4f, 4f => .5f, _ => 1f };
        _presentationPlayer?.SetSpeed(_playbackSpeed);
        _damageNumbers?.SetSpeed(_playbackSpeed);
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
    private void RefreshLog()
    {
        _cheatConsole?.SetEntries(_logs);
        if(_eventLog is null)return;
        IEnumerable<BattleUiLogEntry> shown=_logs;if(_logFilter>0)shown=shown.Where(item=>(int)item.Category==_logFilter-1);
        _eventLog.Text=string.Join('\n',shown.Select(item=>$"[{item.EventType}] {item.Message}"));_eventLog.ScrollToLine(Math.Max(0,_eventLog.GetLineCount()-1));
    }

    private void ShowSettlement(PureRunSaveSnapshot snapshot)
    {
        _battle = null;
        if (snapshot.TerminalSummary is PureRunSummary summary) { ShowSummary(summary); return; }
        PureRunState run = snapshot.ActiveRun!;
        if ((run.Phase is PureRunPhase.AwaitingLayerFourChoice or PureRunPhase.ResolvingLayerFourNode or PureRunPhase.ReadyForLayerFive or
            PureRunPhase.AwaitingLayerSixChoice or PureRunPhase.ResolvingLayerSixNode or PureRunPhase.ReadyForBoss) &&
            run.PendingProgression.Count == 0)
        { RouteMap(run); return; }
        if(run.Phase==PureRunPhase.ReadyForLayerSix&&run.PendingProgression.Count==0){RouteMap(run);return;}
        string completed=_currentEncounterId is ContentId completedId?EncounterLabel(completedId):$"Battle {run.BattlesCompleted}";
        string next=run.Phase switch
        {
            PureRunPhase.AwaitingLayerFourChoice => "Layer 4 Map",
            PureRunPhase.ReadyForLayerSix => "Layer 6 Map",
            _ => EncounterLabel(run.EncounterContentId)
        };
        Control root = NewPage("BATTLE SETTLEMENT", $"{completed} completed → Next: {next}");
        AddRunShell(root, run, "Settlement");
        string itemResult = SettlementDropLabel(run);
        LabelAt(root, $"Gold: {run.Gold}\nItems: {itemResult}\nPending Progression: {run.PendingProgression.LastOrDefault()?.CharacterId ?? "none"}\nDead: {string.Join(", ", run.Party.Where(value => value.IsDead).Select(value => value.CharacterId))}", new Vector2(480, 260), 28);
        root.AddChild(PlaceControl(Button("Inventory",()=>ShowInventory(run, InventoryReturnTarget.Settlement)),new Vector2(480,520),new Vector2(260,60)));
        PendingProgression? pending=run.PendingProgression.FirstOrDefault();
        bool continueRequested=false;
        Button nextButton = Button(pending is null ? $"Continue — {next}" : "Continue — Progression",()=>
        {
            if(continueRequested)return;continueRequested=true;
            if (pending is not null) ShowProgression(run, pending); else RouteRunState(snapshot);
        }); nextButton.Position = new Vector2(650, 610); nextButton.Size = new Vector2(300, 70); root.AddChild(nextButton);
    }

    private void ShowRunMap(PureRunState run)
    {
        Control root = NewPage("PURE RUN MAP", "Choose an available node. Drag or use the wheel to inspect the route.");
        PureRunFlowSnapshot flow = _flowProjector.Project(run, _runDefinition!, LayerFourMap());
        AddRunShell(root, run, "Map");
        _mapView = new GodotRogueMapView { Position = new Vector2(60, 135), Size = new Vector2(1040, 700) };
        _mapView.NodePressed += nodeId => ActivateMapNode(run, nodeId);
        _mapView.NodeHovered += node =>
        {
            if (_mapDetail is null) return;
            _mapDetail.Text = node is null
                ? "Hover a node to inspect it."
                : $"{node.Title}  |  {node.State}\n{node.ContentId?.Value ?? node.NodeId}\n{node.UnavailableReason ?? "Ready"}";
        };
        root.AddChild(_mapView);
        _mapView.SetSnapshot(flow.Map!, true);
        _mapDetail = LabelAt(root, "Hover a node to inspect it.", new Vector2(1140, 220), 19);
        _mapDetail.Size = new Vector2(390, 180); _mapDetail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (run.PendingProgression.FirstOrDefault() is PendingProgression pending)
        {
            LabelAt(root, "Progression must be completed before the next node.", new Vector2(1140, 430), 18);
            root.AddChild(PlaceControl(Button("Complete Progression", () => ShowProgression(run, pending)),
                new Vector2(1140, 475), new Vector2(360, 58)));
        }
        root.AddChild(PlaceControl(Button("Inventory", () => ShowInventory(run)), new Vector2(1140, 560), new Vector2(360, 58)));
        _status = LabelAt(root, string.Empty, new Vector2(1140, 790), 16);
        BuildPauseMenu(root, false);
    }

    private void ActivateMapNode(PureRunState run, string nodeId)
    {
        PureRunMapNodeSnapshot node = _flowProjector.ProjectMap(run, _runDefinition!, LayerFourMap()).Nodes
            .Single(value => value.NodeId == nodeId);
        if (node.State == PureRunMapNodeState.Pending)
        {
            RouteRunState(new PureRunSaveSnapshot(run.Revision, run, null));
            return;
        }
        if (node.State is not (PureRunMapNodeState.Available or PureRunMapNodeState.Current))
        { SetStatus(node.UnavailableReason ?? "map.node_locked"); return; }
        if (nodeId is "layer_01_battle" or "layer_02_battle" or "layer_03_battle")
        { BeginReadyEncounter(); return; }
        if (nodeId.StartsWith("layer_04_", StringComparison.Ordinal))
        { SelectLayerFourNode(nodeId); return; }
        if (nodeId == "layer_05_battle") { BeginLayerFive(); return; }
        if (nodeId.StartsWith("layer_06_", StringComparison.Ordinal))
        {
            if (run.Phase == PureRunPhase.ReadyForLayerSix)
            {
                RunSessionResult unlocked = _run!.ApplyFullRunTransition(state =>
                    new PureRunFullRunService(_consumables.Keys).UnlockLayerSix(state, LayerFourMap()));
                if (!unlocked.Succeeded) { SetStatus(unlocked.ErrorCode); return; }
            }
            SelectLayerSixNode(nodeId); return;
        }
        if (nodeId == "layer_07_battle") BeginBoss();
    }

    private void AddRunShell(Control root, PureRunState run, string page)
    {
        var panel = new ColorRect { Color = new Color("1c2a33e6"), Position = new Vector2(1115, 125), Size = new Vector2(420, 82) };
        root.AddChild(panel);
        LabelAt(root, $"{page}  |  Gold {run.Gold}  |  Bag {run.BackpackConsumables.Count + run.BackpackEquipment.Count}\n" +
            string.Join("   ", run.Party.Select(value => $"{value.CharacterId} L{value.Level} HP {value.CurrentHealth}/{value.MaxHealth} MP {value.CurrentMana}/{value.MaxMana}")),
            new Vector2(1130, 138), 15).Size = new Vector2(390, 62);
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
        RouteRunState(new PureRunSaveSnapshot(run.Revision, run, null));
    }

    private void RouteRunState(PureRunSaveSnapshot snapshot)
    {
        if (snapshot.TerminalSummary is PureRunSummary summary) { ShowSummary(summary); return; }
        if (snapshot.PendingRunSetup is not null) { ShowNewRunSetup(snapshot); return; }
        PureRunState run = snapshot.ActiveRun ?? throw new InvalidOperationException("Active run is missing.");
        if(run.Phase==PureRunPhase.ResolvingLayerFourNode){RouteLayerFour(run);return;}
        if(run.Phase==PureRunPhase.ResolvingLayerSixNode){RouteLayerSixNode(run);return;}
        if(run.Phase==PureRunPhase.PendingBattle&&run.Checkpoint is not null)
        {
            StartBattle(new EncounterRequest(run.RunId,run.Checkpoint.Revision,run.Checkpoint.EncounterContentId,run.Checkpoint.Party));
            return;
        }
        if (run.PendingProgression.FirstOrDefault() is PendingProgression pending)
        {
            ShowProgression(run, pending);
            return;
        }
        ShowRunMap(run);
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
        Control root=NewPage($"{LayerLabel(run)} — REST","Preview: living party members recover ceil(30% max HP/MP); dead characters remain dead.");
        AddRunShell(root,run,"Rest");
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
        Control root=NewPage($"{LayerLabel(run)} — STORE",$"Gold {run.Gold}. Stock is persisted and will not reroll after Reload.");
        AddRunShell(root,run,"Store");
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
        Control root=NewPage($"{LayerLabel(run)} — {rootElement.GetProperty("title").GetString()}",rootElement.GetProperty("description").GetString()!);
        AddRunShell(root,run,"Mystery");
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

    private void ShowInventory(PureRunState run) => ShowInventory(run, InventoryReturnTarget.RunRoute);

    private void ShowInventory(PureRunState run, InventoryReturnTarget returnTarget)
    {
        if (_inventoryCharacterId is null || run.Party.All(value => value.CharacterId != _inventoryCharacterId))
            _inventoryCharacterId = run.Party[0].CharacterId;
        RunCharacterState selectedCharacter = run.Party.Single(value => value.CharacterId == _inventoryCharacterId);
        Control root=NewPage("INVENTORY","Backpack, equipment loadout, carried consumable and derived character state");
        AddRunShell(root,run,"Inventory");
        var columns=new HBoxContainer{Position=new Vector2(65,150),Size=new Vector2(1470,610)};root.AddChild(columns);
        var characters=new VBoxContainer{CustomMinimumSize=new Vector2(390,580)};columns.AddChild(characters);
        foreach(RunCharacterState character in run.Party)
        {
            RunCharacterState captured = character;
            characters.AddChild(Button($"{(captured.CharacterId == selectedCharacter.CharacterId ? "▶ " : string.Empty)}{captured.CharacterId}  Lv{captured.Level}", () =>
            {
                _inventoryCharacterId = captured.CharacterId; _inventorySelectedInstanceId = null;
                ShowInventory(run, returnTarget);
            }));
        }
        characters.AddChild(Label($"\nHP {selectedCharacter.CurrentHealth}/{selectedCharacter.MaxHealth}  MP {selectedCharacter.CurrentMana}/{selectedCharacter.MaxMana}\n"+
            $"STR {selectedCharacter.Attributes.Strength} AGI {selectedCharacter.Attributes.Agility} CON {selectedCharacter.Attributes.Constitution}\n"+
            $"INT {selectedCharacter.Attributes.Intelligence} CHA {selectedCharacter.Attributes.Charisma} LUCK {selectedCharacter.Attributes.Luck}\n\nSkills:\n"+
            string.Join('\n',selectedCharacter.LearnedSkillStates.Select(value=>$"{value.BranchId} Lv{value.Level}")),18));

        var backpack=new VBoxContainer{CustomMinimumSize=new Vector2(500,580)};columns.AddChild(backpack);
        var tabs=new HBoxContainer();backpack.AddChild(tabs);
        tabs.AddChild(Button(_inventoryEquipmentTab?"[ Equipment ]":"Equipment",()=>{_inventoryEquipmentTab=true;_inventorySelectedInstanceId=null;ShowInventory(run,returnTarget);}));
        tabs.AddChild(Button(!_inventoryEquipmentTab?"[ Consumables ]":"Consumables",()=>{_inventoryEquipmentTab=false;_inventorySelectedInstanceId=null;ShowInventory(run,returnTarget);}));
        if (_inventoryEquipmentTab)
        {
            if (run.BackpackEquipment.Count == 0) backpack.AddChild(Label("Equipment backpack is empty.\nEquipment is obtained from Store or Mystery routes.",18));
            foreach (RunEquipmentState item in run.BackpackEquipment)
            {
                RunEquipmentState captured=item;
                backpack.AddChild(Button($"{captured.InstanceId.Value}  |  {_equipment[captured.DefinitionId].DisplayName}",()=>
                { _inventorySelectedInstanceId=captured.InstanceId.Value; ShowInventory(run,returnTarget); }));
            }
        }
        else
        {
            if (run.BackpackConsumables.Count == 0) backpack.AddChild(Label("Consumable backpack is empty.\nNormal battles use the frozen deterministic drop chance.",18));
            foreach (BattleConsumableState item in run.BackpackConsumables)
            {
                BattleConsumableState captured=item;
                backpack.AddChild(Button($"{captured.InstanceId.Value}  |  {_consumables[captured.DefinitionId].DisplayName}  {captured.RemainingCharges}/{captured.MaxCharges}",()=>
                { _inventorySelectedInstanceId=captured.InstanceId.Value; ShowInventory(run,returnTarget); }));
            }
        }

        var detail=new VBoxContainer{CustomMinimumSize=new Vector2(520,580)};columns.AddChild(detail);
        detail.AddChild(Label($"Loadout — {selectedCharacter.CharacterId}",22));
        foreach(EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
        {
            RunEquipmentState? equipped=selectedCharacter.Equipment.FirstOrDefault(value=>value.Slot==slot);
            detail.AddChild(Label($"{slot}: {(equipped is null?"empty":_equipment[equipped.DefinitionId].DisplayName)}",17));
            if(equipped is not null)
            {
                EquipmentSlot capturedSlot=slot;
                detail.AddChild(Button($"Unequip {slot}",()=>CommitInventoryMutation(state=>new RunInventoryProgressionService().Unequip(state,state.Revision,selectedCharacter.CharacterId,capturedSlot,_equipment,_units[selectedCharacter.UnitContentId].Speed),returnTarget)));
            }
        }
        detail.AddChild(Label($"Carried: {(selectedCharacter.CarriedConsumables.FirstOrDefault() is BattleConsumableState carried?_consumables[carried.DefinitionId].DisplayName:"empty")}",17));
        if(selectedCharacter.CarriedConsumables.Count>0)
            detail.AddChild(Button("Unload carried consumable",()=>CommitInventoryMutation(state=>new RunInventoryProgressionService().Unload(state,state.Revision,selectedCharacter.CharacterId),returnTarget)));

        RunEquipmentState? selectedEquipment=run.BackpackEquipment.FirstOrDefault(value=>value.InstanceId.Value==_inventorySelectedInstanceId);
        BattleConsumableState? selectedConsumable=run.BackpackConsumables.FirstOrDefault(value=>value.InstanceId.Value==_inventorySelectedInstanceId);
        if(selectedEquipment is not null)
        {
            EquipmentDefinition definition=_equipment[selectedEquipment.DefinitionId];
            detail.AddChild(Label($"\n{definition.DisplayName}\n{definition.Slot} | {definition.Rarity} | Price {definition.Price}\nSource: Store / Mystery\nBonuses: STR {definition.AttributeBonuses.Strength} AGI {definition.AttributeBonuses.Agility} CON {definition.AttributeBonuses.Constitution} INT {definition.AttributeBonuses.Intelligence} CHA {definition.AttributeBonuses.Charisma} LUCK {definition.AttributeBonuses.Luck}",17));
            detail.AddChild(Button(selectedCharacter.Equipment.Any(value=>value.Slot==definition.Slot)?"Replace equipped item":"Equip",()=>CommitInventoryMutation(state=>new RunInventoryProgressionService().Equip(state,state.Revision,selectedCharacter.CharacterId,selectedEquipment.InstanceId,_equipment,_units[selectedCharacter.UnitContentId].Speed),returnTarget)));
        }
        else if(selectedConsumable is not null)
        {
            ConsumableDefinition definition=_consumables[selectedConsumable.DefinitionId];
            detail.AddChild(Label($"\n{definition.DisplayName}\nPrice {definition.Price}\n{definition.Description}\nSource: deterministic Battle drop / Store / Mystery",17));
            detail.AddChild(Button(selectedCharacter.CarriedConsumables.Count>0?"Replace carried consumable":"Carry",()=>CommitInventoryMutation(state=>new RunInventoryProgressionService().Carry(state,state.Revision,selectedCharacter.CharacterId,selectedConsumable.InstanceId),returnTarget)));
        }
        root.AddChild(PlaceControl(Button("Back",()=>ReturnFromInventory(run,returnTarget)),new Vector2(650,815),new Vector2(300,55)));
    }

    private void CommitInventoryMutation(Func<PureRunState,RunMutationResult> mutation, InventoryReturnTarget returnTarget)
    {
        RunSessionResult result=_run!.ApplyMutation(mutation);
        if(!result.Succeeded||result.Snapshot?.ActiveRun is not PureRunState run){SetStatus(result.ErrorCode);return;}
        _inventorySelectedInstanceId=null;
        ShowInventory(run,returnTarget);
    }

    private void ReturnFromInventory(PureRunState run, InventoryReturnTarget returnTarget)
    {
        _inventorySelectedInstanceId=null;
        if(returnTarget==InventoryReturnTarget.Home){ShowHome();return;}
        if(returnTarget==InventoryReturnTarget.Settlement){ShowSettlement(new PureRunSaveSnapshot(run.Revision,run,null));return;}
        RouteRunState(new PureRunSaveSnapshot(run.Revision,run,null));
    }

    private void ShowProgression(PureRunState run, PendingProgression pending)
    {
        RunCharacterState character=run.Party.Single(value=>value.CharacterId==pending.CharacterId);
        Control root=NewPage("PROGRESSION",$"{character.CharacterId}: attribute allocation → skill selection");
        AddRunShell(root,run,"Progression");
        var menu=new VBoxContainer{Position=new Vector2(430,160),Size=new Vector2(740,650)};root.AddChild(menu);
        var progressionService=new RunInventoryProgressionService();
        if (!_progressionDrafts.TryGetValue(pending.TransactionKey, out UnitAttributes proposed))
        {
            menu.AddChild(Label($"Step 1/2 — choose one attribute\nSTR {character.Attributes.Strength}  AGI {character.Attributes.Agility}  CON {character.Attributes.Constitution}\nINT {character.Attributes.Intelligence}  CHA {character.Attributes.Charisma}  LUCK {character.Attributes.Luck}",22));
            foreach (string attribute in new[] { "Strength", "Agility", "Constitution", "Intelligence", "Charisma", "Luck" })
            {
                string selectedAttribute = attribute;
                menu.AddChild(Button($"+1 {selectedAttribute}", () => PreviewProgressionAttribute(run, pending,
                    Raise(character.Attributes, selectedAttribute))));
            }
        }
        else
        {
            RunCharacterState preview = new(character.CharacterId, character.UnitContentId, character.Level, proposed,
                character.CurrentHealth, character.MaxHealth, character.CurrentMana, character.MaxMana, character.IsDead,
                character.LearnedSkills, character.Equipment, character.CarriedConsumables, character.LearnedSkillStates,
                character.StartingSkillContentId);
            menu.AddChild(Label($"Step 2/2 — choose a skill\nSTR {proposed.Strength}  AGI {proposed.Agility}  CON {proposed.Constitution}\nINT {proposed.Intelligence}  CHA {proposed.Charisma}  LUCK {proposed.Luck}",22));
            menu.AddChild(Label("Current skills:\n" + string.Join('\n', character.LearnedSkillStates.Select(value =>
            {
                SkillUiMetadata known = _skillUi[value.DefinitionId];
                return $"{known.DisplayName} Lv{value.Level} — {(known.IsPassive ? "Passive" : "Active")} — MP {known.ManaCost}\n{known.Description}";
            })), 17));
            SkillDefinition[] candidates=progressionService.PreviewGrowthOffer(run,pending.TransactionKey,proposed,_skills,_runDefinition!).ToArray();
            foreach(SkillDefinition skill in candidates)
                menu.AddChild(Button(GrowthChoiceLabel(preview,skill,_skillUi[skill.ContentId]),()=>
                    CompleteProgression(pending.TransactionKey,proposed,skill.ContentId)));
            if(candidates.Length==0)
            {
                menu.AddChild(Label("No skill candidate is legal after this allocation. Attribute-only confirmation is allowed.",18));
                menu.AddChild(Button("Confirm Attribute",()=>CompleteProgression(pending.TransactionKey,proposed,null)));
            }
        }
    }

    private static string GrowthChoiceLabel(RunCharacterState character,SkillDefinition skill,SkillUiMetadata metadata)
    {
        RunLearnedSkillState? learned=character.LearnedSkillStates.FirstOrDefault(value=>value.BranchId==skill.BranchId);
        string title = learned is null
            ? $"Learn {metadata.DisplayName} Lv{skill.Level}"
            : $"Upgrade {metadata.DisplayName} Lv{learned.Level} → Lv{skill.Level}";
        string requirement=string.IsNullOrEmpty(metadata.RequiredAttribute)?"None":$"{metadata.RequiredAttribute} {metadata.MinimumAttribute}";
        string prerequisite=string.IsNullOrEmpty(metadata.PrerequisiteBranchId)?"None":metadata.PrerequisiteBranchId;
        return $"{title}\n{metadata.Description}\n{(metadata.IsPassive ? "Passive" : "Active")}  MP {metadata.ManaCost}  Range {RangeLabel(metadata)}\nRequires: {requirement}  Prerequisite: {prerequisite}";
    }

    private static string RangeLabel(SkillUiMetadata metadata) => metadata.MinRange == metadata.MaxRange
        ? metadata.MaxRange.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : $"{metadata.MinRange}-{metadata.MaxRange}";

    internal static string SettlementDropLabel(PureRunState run)
    {
        string prefix = $"drop-{run.BattlesCompleted}-";
        ContentId[] drops = run.BackpackConsumables
            .Where(value => value.InstanceId.Value.StartsWith(prefix, StringComparison.Ordinal))
            .Select(value => value.DefinitionId).ToArray();
        return drops.Length == 0 ? "No item drop" : string.Join(", ", drops.Select(value => value.Value));
    }

    private void PreviewProgressionAttribute(PureRunState run, PendingProgression pending, UnitAttributes attributes)
    {
        _progressionDrafts[pending.TransactionKey] = attributes;
        ShowProgression(run, pending);
    }

    private void CompleteProgression(string transactionKey, UnitAttributes attributes, ContentId? skillId)
    {
        RunSessionResult result = _run!.ApplyMutation(state => new RunInventoryProgressionService()
            .CompleteProgression(state,state.Revision,transactionKey,attributes,skillId,_skills,_runDefinition!));
        if(!result.Succeeded){SetStatus(result.ErrorCode);return;}
        _progressionDrafts.Remove(transactionKey);
        ShowSettlement(result.Snapshot!);
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

    private void BuildPauseMenu(Control root, bool controlsBattlePlayback = true)
    {
        var overlay = new ColorRect { Color = new Color(0,0,0,.72f), Visible = false, MouseFilter = MouseFilterEnum.Stop, ZIndex = PauseOverlayZIndex };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddChild(overlay); _pauseMenu = overlay; _pauseMenuControlsBattlePlayback = controlsBattlePlayback;
        var menu = new VBoxContainer { Position = new Vector2(620,220), Size = new Vector2(360,360) };
        overlay.AddChild(menu);
        menu.AddChild(Label("PAUSED\nGame is paused",30));
        menu.AddChild(Button("CONTINUE",ClosePauseMenu));
        menu.AddChild(Button("OPTIONS",()=>ShowPauseOptions(menu)));
        menu.AddChild(Button("MAIN MENU",()=>{ClosePauseMenu();ShowHome();}));
    }

    private void ShowPauseOptions(VBoxContainer menu)
    {
        bool ownsPlaybackPause = _pauseMenuPausedPlayback;
        foreach(Node child in menu.GetChildren())child.QueueFree();
        menu.AddChild(Label("OPTIONS\nPresentation speed is controlled from the battle HUD.",24));
        bool controlsBattlePlayback = _pauseMenuControlsBattlePlayback;
        menu.AddChild(Button("BACK",()=>{_pauseMenu?.QueueFree();_pauseMenu=null;BuildPauseMenu(_page!, controlsBattlePlayback);OpenPauseMenu(false);_pauseMenuPausedPlayback=ownsPlaybackPause;}));
    }

    private void OpenPauseMenu(bool pausePlayback=true)
    {
        if(_pauseMenu is null)return;
        _pauseMenu.Visible=true;
        _pauseMenuPausedPlayback=pausePlayback&&_pauseMenuControlsBattlePlayback&&!_playbackPaused;
        if(_pauseMenuPausedPlayback)TogglePause();
    }

    private void ClosePauseMenu()
    {
        if(_pauseMenu is null)return;
        _pauseMenu.Visible=false;
        if(_pauseMenuPausedPlayback&&_playbackPaused)TogglePause();
        _pauseMenuPausedPlayback=false;
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
        _mapView=null;
        _mapDetail=null;
        _pauseMenu=null;
        _pauseMenuControlsBattlePlayback=false;
        _damageNumbers=null;
        var root = new Control(); root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(root); _page = root;
        Control background = battleBackdrop ? new GodotBattleBackdrop() : new ColorRect { Color = new Color("657784") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); root.AddChild(background);
        if (!battleBackdrop)
        {
            LabelAt(root, title, new Vector2(70, 35), 40);
            LabelAt(root, subtitle, new Vector2(70, 82), 20);
        }
        return root;
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
    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(168, 54) };
        button.AddThemeFontSizeOverride("font_size", 15);
        button.Pressed += action;
        return button;
    }
    internal static string FormatBattleActionLabel(string displayName, int manaCost, bool used)
    {
        string detail = manaCost > 0 ? $"MP {manaCost}" : string.Empty;
        if (used) detail = string.IsNullOrEmpty(detail) ? "Used" : $"{detail} · Used";
        return string.IsNullOrEmpty(detail) ? displayName : $"{displayName}\n{detail}";
    }
    internal static bool ShouldShowActiveMarker(bool presentationInputLocked, bool presentationPlaying) =>
        !presentationInputLocked && !presentationPlaying;
    private static Label Label(string text, int size) { var label = new Label { Text = text }; label.AddThemeFontSizeOverride("font_size", size); return label; }
    private static Label LabelAt(Control parent, string text, Vector2 position, int size) { Label label = Label(text, size); label.Position = position; parent.AddChild(label); return label; }
    private void SetStatus(string? text) { if (_status is not null) _status.Text = text ?? string.Empty; }
    private static string SkillTooltip(SkillDefinition skill)=>skill.ExecutionKind==SkillExecutionKind.Fireball
        ? "Lv1: single target; hits the first enemy on the selected ray. Splash begins at Lv2."
        : $"Range {skill.MinRange}-{skill.MaxRange}; damage {skill.Damage}.";
    private static string EncounterLabel(ContentId id)=>id.Value.EndsWith(".n1",StringComparison.Ordinal)?"N1":id.Value.EndsWith(".n2",StringComparison.Ordinal)?"N2":id.Value.EndsWith(".n3",StringComparison.Ordinal)?"N3":id.Value;
    private static string LayerLabel(PureRunState run)=>run.NodeTransaction?.NodeId.StartsWith("layer_06_",StringComparison.Ordinal)==true?"LAYER 6":"LAYER 4";
}
