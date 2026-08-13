using Godot;
using Tactics.Application.Battle;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Non-modal diagnostic overlay matching the Unity cheat-console input boundary.</summary>
public partial class GodotBattleCheatConsole : PanelContainer
{
    private readonly RichTextLabel _log = new();
    private readonly OptionButton _filter = new();
    private IReadOnlyList<BattleUiLogEntry> _entries = Array.Empty<BattleUiLogEntry>();
    public event Action? ClearRequested;

    public override void _Ready()
    {
        Position = Vector2.Zero; Size = new Vector2(1600, 225); ZIndex = 2000;
        MouseFilter = MouseFilterEnum.Stop; Visible = false;
        var panel = new VBoxContainer(); AddChild(panel);
        var header = new HBoxContainer(); panel.AddChild(header);
        foreach (string name in new[] { "All", "Gameplay", "AI", "Rejected" }) _filter.AddItem(name);
        _filter.ItemSelected += _ => Refresh(); header.AddChild(_filter);
        var clear = new Button { Text = "Clear" }; clear.Pressed += () => ClearRequested?.Invoke(); header.AddChild(clear);
        header.AddChild(new Label { Text = "CheatConsole (` toggles; gameplay remains live)", MouseFilter = MouseFilterEnum.Ignore });
        _log.CustomMinimumSize = new Vector2(1560, 165); _log.ScrollActive = true; _log.MouseFilter = MouseFilterEnum.Stop;
        panel.AddChild(_log);
    }

    public void SetEntries(IReadOnlyList<BattleUiLogEntry> entries) { _entries = entries; Refresh(); }

    private void Refresh()
    {
        if (!IsInsideTree()) return;
        IEnumerable<BattleUiLogEntry> values = _filter.Selected switch
        {
            1 => _entries.Where(item => item.Category == BattleUiLogCategory.Gameplay),
            2 => _entries.Where(item => item.Category == BattleUiLogCategory.Ai),
            3 => _entries.Where(item => item.Category == BattleUiLogCategory.Rejected),
            _ => _entries
        };
        _log.Text = string.Join('\n', values.TakeLast(100).Select(item => $"[{item.Category}] {item.Message} [{item.EventType}]"));
    }
}
