#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class TacticsContentWorkbench : VBoxContainer
{
    internal static IReadOnlyList<string> TopLevelTabNames { get; } = Array.AsReadOnly(new[] { "Map", "Event", "Skill / Presentation" });
    private EditorUndoRedoManager? _undoRedo;
    private AuthoringWorkspaceCoordinator? _workspace;
    private bool _shuttingDown;
    private bool _lifecycleTestMode;
    public void Configure(EditorUndoRedoManager undoRedo) => _undoRedo = undoRedo;
    internal void ConfigureForLifecycleTest() => _lifecycleTestMode = true;

    public override void _Ready()
    {
        if (_lifecycleTestMode)
        {
            var button = new Button { Text = "lifecycle-test" };
            button.Pressed += () => { };
            AddChild(button);
            return;
        }
        if (_undoRedo is null) throw new InvalidOperationException("Editor UndoRedo manager is required.");
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        WorkbenchUi.StylePage(this);
        var themeWatcher = new WorkbenchThemeWatcher(); themeWatcher.Configure(this); AddChild(themeWatcher);
        var headerBar = WorkbenchUi.Toolbar(this);
        var header = new Label { Text = "TACTICS TOOLING" };
        header.AddThemeFontSizeOverride("font_size", 18);
        headerBar.AddChild(header);
        var subtitle = new Label { Text = "  Godot-native content authoring" };
        subtitle.AddThemeColorOverride("font_color", WorkbenchThemeTokens.Resolve(this).MutedText);
        headerBar.AddChild(subtitle);
        AddChild(headerBar);
        _workspace = new AuthoringWorkspaceCoordinator(); _workspace.Configure(_undoRedo); AddChild(_workspace);
        var lifecycle = new AuthoringLifecycleWorkbench();
        lifecycle.Configure(_undoRedo, _workspace);
        AddChild(lifecycle);
        var tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        AddTab(tabs, CreateMap(), TopLevelTabNames[0]);
        AddTab(tabs, CreateEventTreasure(), TopLevelTabNames[1]);
        AddTab(tabs, CreateSkillPresentation(), TopLevelTabNames[2]);
        AddChild(tabs);
        var footer = new Label { Text = "Drafts are sandboxed. Apply All creates one Editor Undo action." };
        WorkbenchUi.StyleStatus(footer); AddChild(footer);
    }

    public override void _ExitTree() => ShutdownForReload();

    public void ShutdownForReload()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;
        SetProcess(false);
        foreach (Node child in GetChildren().ToArray().Reverse())
        {
            RemoveChild(child);
            child.Free();
        }
        _workspace = null;
        _undoRedo = null;
    }

    private PureRunMapWorkbench CreateMap()
    {
        var panel = new PureRunMapWorkbench();
        panel.Configure(_undoRedo!);
        _workspace!.Register(panel);
        return panel;
    }

    private EventTreasureWorkbench CreateEventTreasure()
    {
        var panel = new EventTreasureWorkbench();
        panel.Configure(_undoRedo!);
        _workspace!.Register(panel);
        return panel;
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

    private static void AddTab(TabContainer tabs, Control panel, string title)
    {
        panel.Name = title;
        tabs.AddChild(panel);
    }
}
#endif
