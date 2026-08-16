using Godot;
using Tactics.Core.Battle;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Skills;
using Tactics.Core.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Native 1600x900 gameplay-only fixture for the migrated starting-skill batch.</summary>
public partial class GodotStartingSkillFixture : Control
{
    public const int CanvasWidth = 1600;
    public const int CanvasHeight = 900;
    public const string CatalogPath = "res://content/skills/ContentCatalog.tres";
    private FixtureSkill[] _skills = Array.Empty<FixtureSkill>();
    private Label? _title;
    private Label? _contract;
    private Label? _result;

    public int CurrentIndex { get; private set; }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _skills = LoadSkills();
        var background = new ColorRect { Color = new Color("738491") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var panel = new VBoxContainer { Position = new Vector2(120, 90), Size = new Vector2(1360, 720) };
        panel.AddThemeConstantOverride("separation", 22);
        background.AddChild(panel);
        var heading = new Label { Text = "Pure Run Starting Skills - Gameplay Fixture" };
        heading.AddThemeFontSizeOverride("font_size", 34);
        panel.AddChild(heading);
        var help = new Label { Text = "Left/Right: skill   Enter: execute deterministic scenario   R: reset" };
        help.AddThemeFontSizeOverride("font_size", 22);
        panel.AddChild(help);
        _title = new Label(); _title.AddThemeFontSizeOverride("font_size", 30); panel.AddChild(_title);
        _contract = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(1360, 180) };
        _contract.AddThemeFontSizeOverride("font_size", 23); panel.AddChild(_contract);
        _result = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(1360, 260) };
        _result.AddThemeFontSizeOverride("font_size", 22); panel.AddChild(_result);
        RefreshText("Ready. Execute the selected deterministic scenario.");
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key || _skills.Length == 0) return;
        if (key.Keycode == Key.Left) { CurrentIndex = (CurrentIndex + _skills.Length - 1) % _skills.Length; RefreshText("Ready."); }
        else if (key.Keycode == Key.Right) { CurrentIndex = (CurrentIndex + 1) % _skills.Length; RefreshText("Ready."); }
        else if (key.Keycode is Key.Enter or Key.KpEnter) RefreshText(ExecuteCurrentScenario());
        else if (key.Keycode == Key.R) RefreshText("Reset complete. No battle state is retained between scenarios.");
    }

    public string ExecuteCurrentScenario()
    {
        if (_skills.Length == 0) return "ERROR: Skill Catalog is empty.";
        SkillDefinition definition = _skills[CurrentIndex].Definition;
        BattleState state = CreateScenario(definition, out UnitInstanceId actorId, out UnitInstanceId? targetId, out GridPoint targetCell);
        BattleTransition transition = new BattleTransitionService().Apply(state, new UseSkillCommand(actorId, targetId, targetCell, definition));
        string events = string.Join(" -> ", transition.Events.Select(item => item.GetType().Name));
        string units = string.Join(" | ", transition.State.Units.Values.OrderBy(item => item.Unit.InstanceId.Value, StringComparer.Ordinal).Select(item => $"{item.Unit.InstanceId}: HP {item.CurrentHealth}, MP {item.CurrentMana}, statuses [{string.Join(',', item.Statuses.Keys.Select(id => id.Value))}]"));
        return $"Succeeded: {transition.Succeeded}\nEvents: {events}\nState: {units}\nCorpses: {transition.State.Corpses.Count}; Dropped spears: {transition.State.DroppedSpears.Count}";
    }

    private void RefreshText(string result)
    {
        if (_skills.Length == 0 || _title is null || _contract is null || _result is null) return;
        FixtureSkill skill = _skills[CurrentIndex];
        SkillDefinition definition = skill.Definition;
        _title.Text = $"{CurrentIndex + 1}/12  {definition.ContentId}  ({skill.DisplayName})";
        _contract.Text = $"SourceId: {definition.SourceId}\nExecution: {definition.ExecutionKind}; Mana: {definition.ManaCost}; Range: {definition.MinRange}..{definition.MaxRange}; Damage: {definition.Damage} {definition.DamageKind}\nStatus: {definition.StatusContentId} {definition.StatusDuration}; External: {definition.ExternalDependency}\nVisual payload: none; semantic cue only.";
        _result.Text = result;
    }

    private static FixtureSkill[] LoadSkills()
    {
        GodotResourceCatalog catalog = ResourceLoader.Load<GodotResourceCatalog>(CatalogPath) ?? throw new InvalidOperationException("Starting-skill Catalog is missing.");
        catalog.Validate();
        return catalog.Entries.OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal).Select(entry =>
        {
            Resource resource = ResourceLoader.Load(entry.ResourceLocator) ?? throw new InvalidOperationException($"Skill Resource is missing: {entry.ContentIdValue}");
            if (resource is SkillDefinitionResource skill) return new FixtureSkill(skill.ToCoreDefinition(), skill.DisplayName, skill.Description);
            if (resource is PoisonSpearSkillResource poison && entry.ContentIdValue == "skill.poison-spear.lv1")
            {
                return new FixtureSkill(poison.ToCoreDefinition(), poison.DisplayName, poison.Description);
            }
            throw new InvalidOperationException($"Skill Resource has the wrong type: {entry.ContentIdValue}");
        }).ToArray();
    }

    private static BattleState CreateScenario(SkillDefinition definition, out UnitInstanceId actorId, out UnitInstanceId? targetId, out GridPoint targetCell)
    {
        var cells = Enumerable.Range(0, BoardSpec.Width).SelectMany(x => Enumerable.Range(0, BoardSpec.Height).Select(y => new KeyValuePair<GridPoint, CellState>(new GridPoint(x, y), new CellState()))).ToDictionary();
        actorId = new UnitInstanceId("fixture.actor.0");
        var enemyId = new UnitInstanceId("fixture.enemy.0");
        targetId = enemyId;
        targetCell = definition.ExecutionKind == SkillExecutionKind.SummonSkeleton ? new GridPoint(3, 2) : new GridPoint(3, 1);
        var actor = new BattleUnitState(new UnitState(actorId, new ContentId("unit.pure-run.amazon"), new GridPoint(1, 1), 3, 10, 0, 0), 20, 20, maxMana: 30, currentMana: 30, physicalAttack: 4, magicalAttack: 5);
        var enemy = new BattleUnitState(new UnitState(enemyId, new ContentId("unit.pure-run.goat.charger"), new GridPoint(3, 1), 3, 8, 1, 1), 30, 30);
        BattleState state = new(new BoardSnapshot(cells), new[] { actor, enemy }, new[] { actorId, enemyId }, randomState: 2);
        if (definition.ExecutionKind == SkillExecutionKind.SummonSkeleton) { targetId = null; state = state.WithCorpse(targetCell); }
        if (definition.ExecutionKind == SkillExecutionKind.PickupSpear) { targetId = null; targetCell = new GridPoint(2, 2); state = state.WithDroppedSpear(actorId, targetCell); }
        if (definition.ExecutionKind == SkillExecutionKind.CombatTechniques) { targetId = actorId; targetCell = actor.Unit.Position; }
        return state;
    }

    private sealed record FixtureSkill(SkillDefinition Definition, string DisplayName, string Description);
}
