#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class TacticsContentWorkbench : VBoxContainer
{
    private EditorUndoRedoManager? _undoRedo;
    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;

    public override void _Ready()
    {
        if (_undoRedo is null) throw new InvalidOperationException("Editor UndoRedo manager is required.");
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        var header = new Label { Text = "Tactics Content Workbench" };
        header.AddThemeFontSizeOverride("font_size", 26);
        AddChild(header);
        var tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        AddTab(tabs, CreateMap(), "Map");
        AddTab(tabs, new EventTreasureWorkbench(false), "Event");
        AddTab(tabs, new EventTreasureWorkbench(true), "Treasure");
        AddTab(tabs, new EncounterFixtureWorkbench(), "Encounter Fixture");
        AddTab(tabs, CreatePresentation(), "Skill / Presentation");
        AddTab(tabs, CreateAi(), "AI");
        AddTab(tabs, new ContentCatalogWorkbench("audio", "Audio Contracts"), "Audio");
        AddTab(tabs, new ContentCatalogWorkbench(string.Empty, "QA / Catalog Evidence"), "QA");
        AddChild(tabs);
    }

    private PureRunMapWorkbench CreateMap()
    {
        var panel = new PureRunMapWorkbench();
        panel.Configure(_undoRedo!);
        return panel;
    }

    private TacticsGraphWorkbench CreatePresentation()
    {
        var panel = new TacticsGraphWorkbench();
        panel.Configure(_undoRedo!);
        return panel;
    }

    private AiDefinitionWorkbench CreateAi()
    {
        var panel = new AiDefinitionWorkbench();
        panel.Configure(_undoRedo!);
        return panel;
    }

    private static void AddTab(TabContainer tabs, Control panel, string title)
    {
        panel.Name = title;
        tabs.AddChild(panel);
    }
}
#endif
