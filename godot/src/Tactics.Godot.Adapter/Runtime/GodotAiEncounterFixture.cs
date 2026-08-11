using Godot;
using Tactics.Core.AI;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Encounters;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>1600x900 gameplay fixture exposing deterministic AI scenario selection and logs.</summary>
public partial class GodotAiEncounterFixture : Control
{
    public const int CanvasWidth = 1600;
    public const int CanvasHeight = 900;
    private const string BatchCatalogPath = "res://content/ai_encounters/ContentCatalog.tres";
    private const string GlobalCatalogPath = "res://content/ContentCatalog.tres";
    private static readonly string[] Scenarios = { "N1", "N2", "N3", "Elite Charger", "Elite Poison Caster" };
    private readonly AiDecisionService _decisions = new();
    private readonly AiTurnService _turns = new();
    private Dictionary<ContentId, SkillDefinition> _skills = new();
    private Dictionary<ContentId, AiDefinition> _ai = new();
    private Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private Dictionary<UnitInstanceId, AiDefinition> _unitAi = new();
    private Dictionary<UnitInstanceId, int> _patternCursors = new();
    private BattleState? _state;
    private Label? _title;
    private Label? _log;
    private int _index;
    private int _step;
    private string _last = "Ready.";

    public override void _Ready()
    {
        LoadDefinitions();
        BuildUi();
        ResetScenario();
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Left) { _index = (_index + Scenarios.Length - 1) % Scenarios.Length; ResetScenario(); }
        else if (key.Keycode == Key.Right) { _index = (_index + 1) % Scenarios.Length; ResetScenario(); }
        else if (key.Keycode == Key.Space) ExecuteStep();
        else if (key.Keycode is Key.Enter or Key.KpEnter) ExecuteRound();
        else if (key.Keycode == Key.R) ResetScenario();
    }

    public string ExecuteStep()
    {
        if (_state is null) return "ERROR: scenario is not initialized.";
        UnitInstanceId actorId = _state.ActiveUnitId;
        AiDefinition definition = _unitAi[actorId];
        int patternIndex = _patternCursors.GetValueOrDefault(actorId);
        AiTurnPlan plan = _decisions.Decide(_state, definition, _skills, patternIndex);
        AiPlanExecutionResult result = _turns.Execute(_state, plan, _skills);
        _state = result.State;
        _patternCursors[actorId] = result.NextPatternIndex;
        _step++;
        string scores = string.Join("; ", plan.Candidates.Where(value => value.IsLegal).Take(6).Select(value => $"{value.Intent}/{value.SkillId?.Value ?? "-"}={value.TotalScore:0.##}"));
        string events = string.Join(" -> ", result.Events.Select(value => value.GetType().Name));
        _last = $"Step {_step}: actor={actorId}; candidates={plan.Candidates.Count}; selected={plan.Selected.Intent}/{plan.Selected.SkillId?.Value ?? "-"}; score={plan.Selected.TotalScore:0.##}; pattern={plan.UsesPattern}; cursor={patternIndex}->{result.NextPatternIndex}\nScores: {scores}\nCommands/events: {events}";
        Refresh();
        return _last;
    }

    private void ExecuteRound()
    {
        if (_state is null) return;
        int startRound = _state.Round;
        int commands = 0;
        while (_state.Round == startRound && commands < 64) { ExecuteStep(); commands++; }
        _last = commands >= 64 ? "ERROR: structured command limit reached (64)." : $"Auto round complete: {commands} AI turns; now round {_state.Round}.";
        Refresh();
    }

    private void ResetScenario()
    {
        (_state, _unitAi) = CreateScenario(_index);
        _patternCursors = _unitAi.Keys.ToDictionary(value => value, _ => 0);
        _step = 0;
        _last = "Reset: fixed seed=6 and deterministic event sequence restored.";
        Refresh();
    }

    private (BattleState State, Dictionary<UnitInstanceId, AiDefinition> UnitAi) CreateScenario(int index)
    {
        BattleLayoutDefinition layout = LoadLayout(index == 2 ? "battle-layout.pure-run.center-blocker" : "battle-layout.pure-run.open");
        (string[] unitIds, string[] aiIds) = index < 3 ? LoadEncounter($"encounter.pure-run.n{index + 1}") :
            index == 3 ? (new[] { "unit.pure-run.goat-elite-charger" }, new[] { "ai.pure-run.elite-charger" }) :
            (new[] { "unit.pure-run.goat-elite-poison-caster" }, new[] { "ai.pure-run.elite-poison-caster" });
        var cells = Enumerable.Range(0, BoardSpec.Width).SelectMany(x => Enumerable.Range(0, BoardSpec.Height).Select(y =>
            new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState(blocksMovement: layout.BlockedCells.Contains(new GridPoint(x, y)))))).ToDictionary();
        var units = new List<BattleUnitState>();
        for (int i = 0; i < 3; i++)
        {
            UnitInstanceId id = new($"fixture.party.{i}");
            units.Add(new BattleUnitState(new UnitState(id, new ContentId("unit.pure-run.amazon"), layout.PartySpawns[i], 3, 10, 0, i), 40, 40, maxMana: 30, currentMana: 30));
        }
        var order = new List<UnitInstanceId>();
        var bindings = new Dictionary<UnitInstanceId, AiDefinition>();
        for (int i = 0; i < aiIds.Length; i++)
        {
            UnitInstanceId id = new($"fixture.enemy.{i}");
            units.Add(new BattleUnitState(new UnitState(id, new ContentId(unitIds[i]), layout.EnemySpawns[i], 3, 10, 1, i), 35, 35, maxMana: 30, currentMana: 30, physicalAttack: 6, magicalAttack: 6));
            order.Add(id);
            bindings.Add(id, _ai[new ContentId(aiIds[i])]);
        }
        return (new BattleState(new BoardSnapshot(cells), units, order, randomState: 6), bindings);
    }

    private void LoadDefinitions()
    {
        GodotResourceCatalog global = ResourceLoader.Load<GodotResourceCatalog>(GlobalCatalogPath) ?? throw new InvalidOperationException("Global Catalog is missing.");
        _paths = global.Entries.ToDictionary(entry => entry.ContentIdValue, entry => entry.ResourceLocator, StringComparer.Ordinal);
        foreach (GodotResourceEntry entry in global.Entries.Where(value => value.ResourceTypeIdValue == "skill"))
        {
            Resource resource = ResourceLoader.Load(entry.ResourceLocator) ?? throw new InvalidOperationException($"Missing skill: {entry.ContentIdValue}");
            if (resource is SkillDefinitionResource skill) _skills.Add(new ContentId(entry.ContentIdValue), skill.ToCoreDefinition());
            else if (resource is PoisonSpearSkillResource poison) _skills.Add(new ContentId(entry.ContentIdValue), new SkillDefinition(new ContentId(poison.ContentIdValue), "amazon.poison_spear", SkillRole.Amazon, SkillKind.Active, 1, poison.ManaCost, 1, poison.Range, SkillExecutionKind.PoisonSpear, poison.Damage, SkillDamageKind.Physical, new ContentId("buff.poison"), poison.PoisonTurns, externalDependency: true));
        }
        foreach (GodotResourceEntry entry in global.Entries.Where(value => value.ResourceTypeIdValue == "ai"))
            _ai.Add(new ContentId(entry.ContentIdValue), (ResourceLoader.Load<AiDefinitionResource>(entry.ResourceLocator) ?? throw new InvalidOperationException($"Missing AI: {entry.ContentIdValue}")).ToCoreDefinition());
        _ = ResourceLoader.Load<GodotResourceCatalog>(BatchCatalogPath) ?? throw new InvalidOperationException("AI/Encounter Catalog is missing.");
    }

    private BattleLayoutDefinition LoadLayout(string id) =>
        (ResourceLoader.Load<BattleLayoutResource>(_paths[id]) ?? throw new InvalidOperationException($"Missing layout: {id}")).ToCoreDefinition();

    private (string[] Units, string[] Ai) LoadEncounter(string id)
    {
        EncounterDefinitionResource resource = ResourceLoader.Load<EncounterDefinitionResource>(_paths[id]) ?? throw new InvalidOperationException($"Missing encounter: {id}");
        return (resource.MonsterUnitContentIds, resource.MonsterAiContentIds);
    }

    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("738491") }; background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        var panel = new VBoxContainer { Position = new Vector2(80, 60), Size = new Vector2(1440, 780) }; panel.AddThemeConstantOverride("separation", 18); background.AddChild(panel);
        var heading = new Label { Text = "Pure Run AI / Encounter Fixture" }; heading.AddThemeFontSizeOverride("font_size", 34); panel.AddChild(heading);
        var help = new Label { Text = "Left/Right: scenario   Space: single real AI turn   Enter: auto round   R: deterministic reset" }; help.AddThemeFontSizeOverride("font_size", 22); panel.AddChild(help);
        _title = new Label(); _title.AddThemeFontSizeOverride("font_size", 30); panel.AddChild(_title);
        _log = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(1400, 600) }; _log.AddThemeFontSizeOverride("font_size", 21); panel.AddChild(_log);
    }

    private void Refresh()
    {
        if (_title is null || _log is null || _state is null) return;
        _title.Text = $"{_index + 1}/5  {Scenarios[_index]} — round {_state.Round}, active {_state.ActiveUnitId}";
        string units = string.Join(" | ", _state.Units.Values.OrderBy(value => value.Unit.InstanceId.Value, StringComparer.Ordinal).Select(value => $"{value.Unit.InstanceId}: HP {value.CurrentHealth}, MP {value.CurrentMana}, cell {value.Unit.Position}"));
        _log.Text = $"Seed/state: {_state.RandomState}  Step: {_step}\n{_last}\n\nState: {units}\n\nPresentation: migrated Unit sprites + semantic cues only; formal enemy VFX not copied.";
    }
}
