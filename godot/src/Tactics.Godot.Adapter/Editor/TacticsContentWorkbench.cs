#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class TacticsContentWorkbench : VBoxContainer
{
    private EditorUndoRedoManager? _undoRedo;
    private AuthoringWorkspaceCoordinator? _workspace;
    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;

    public override void _Ready()
    {
        if (_undoRedo is null) throw new InvalidOperationException("Editor UndoRedo manager is required.");
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        var header = new Label { Text = "Tactics Content Workbench" };
        header.AddThemeFontSizeOverride("font_size", 26);
        AddChild(header);
        _workspace = new AuthoringWorkspaceCoordinator(); _workspace.Configure(_undoRedo); AddChild(_workspace);
        var lifecycle = new AuthoringLifecycleWorkbench();
        lifecycle.Configure(_undoRedo, _workspace);
        AddChild(lifecycle);
        var tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        AddTab(tabs, CreateMap(), "Map");
        AddTab(tabs, CreateEventTreasure(false), "Event");
        AddTab(tabs, CreateEventTreasure(true), "Treasure");
        AddTab(tabs, CreateEncounter(), "Encounter Fixture");
        AddTab(tabs, CreateSkillPresentation(), "Skill / Presentation");
        AddTab(tabs, CreateAi(), "AI");
        AddTab(tabs, new AudioWorkbench(), "Audio");
        AddTab(tabs, new ContentCatalogWorkbench(string.Empty, "QA / Catalog Evidence"), "QA");
        AddChild(tabs);
    }

    private PureRunMapWorkbench CreateMap()
    {
        var panel = new PureRunMapWorkbench();
        panel.Configure(_undoRedo!);
        _workspace!.Register(panel);
        return panel;
    }

    private EventTreasureWorkbench CreateEventTreasure(bool treasureMode)
    {
        var panel = new EventTreasureWorkbench(treasureMode);
        panel.Configure(_undoRedo!);
        _workspace!.Register(panel);
        return panel;
    }

    private Control CreateEncounter()
    {
        var tabs = new TabContainer();
        var preview = new EncounterFixtureWorkbench { Name = "Fixed Seed Preview" };
        var authoring = new EncounterAuthoringWorkbench { Name = "Authoring" }; authoring.Configure(_undoRedo!); authoring.ConfigurePreview(preview); _workspace!.Register(authoring); tabs.AddChild(authoring);
        tabs.AddChild(preview);
        return tabs;
    }

    private TacticsGraphWorkbench CreatePresentation()
    {
        var panel = new TacticsGraphWorkbench();
        panel.Configure(_undoRedo!);
        return panel;
    }

    private Control CreateSkillPresentation()
    {
        var tabs = new TabContainer();
        var skill = new SkillAuthoringWorkbench { Name = "Skill Definition" }; skill.Configure(_undoRedo!); _workspace!.Register(skill); tabs.AddChild(skill);
        var profiles = new PresentationProfileWorkbench { Name = "Native Profiles" }; profiles.Configure(_undoRedo!); _workspace!.Register(profiles); tabs.AddChild(profiles);
        TacticsGraphWorkbench presentation = CreatePresentation(); presentation.Name = "Poison Graph + Preview"; tabs.AddChild(presentation);
        return tabs;
    }

    private AiDefinitionWorkbench CreateAi()
    {
        var panel = new AiDefinitionWorkbench();
        panel.Configure(_undoRedo!);
        _workspace!.Register(panel);
        return panel;
    }

    private static void AddTab(TabContainer tabs, Control panel, string title)
    {
        panel.Name = title;
        tabs.AddChild(panel);
    }
}
#endif
