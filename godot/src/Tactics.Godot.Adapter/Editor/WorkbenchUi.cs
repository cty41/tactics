#if TOOLS
using Godot;

namespace Tactics.Godot.Adapter.Editor;

internal readonly record struct WorkbenchThemeTokens(
    Color Background,
    Color Panel,
    Color RaisedPanel,
    Color Text,
    Color MutedText,
    Color Divider,
    Color Selection,
    Color Warning,
    Color Error,
    Color Success)
{
    internal static WorkbenchThemeTokens Resolve(Control control)
    {
        Color text = control.GetThemeColor("font_color", "Label");
        float luminance = 0.2126f * text.R + 0.7152f * text.G + 0.0722f * text.B;
        bool dark = luminance > 0.5f;
        for (Node? current = control; current is not null; current = current.GetParent())
        {
            if (!current.HasMeta("tactics_workbench_dark_theme")) continue;
            dark = current.GetMeta("tactics_workbench_dark_theme").AsBool();
            break;
        }
        Color background = dark ? new Color("202124") : new Color("eceff3");
        Color panel = dark ? new Color("292b2f") : new Color("f7f8fa");
        Color raised = dark ? new Color("32353a") : Colors.White;
        Color divider = dark ? new Color("4a4d52") : new Color("c7ccd3");
        return new WorkbenchThemeTokens(
            background, panel, raised, text,
            text.Lerp(background, 0.42f), divider,
            new Color("fff176"), new Color("f0a43c"), new Color("ef5350"), new Color("66bb6a"));
    }
}

internal static class WorkbenchUi
{
    internal const int ToolbarHeight = 30;
    internal const int InspectorWidth = 300;
    internal const int ResourcePaneWidth = 230;

    internal static StyleBoxFlat PanelStyle(Color color, int radius = 4, int borderWidth = 0, Color? border = null)
    {
        var style = new StyleBoxFlat
        {
            BgColor = color,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 5,
            ContentMarginBottom = 5
        };
        if (borderWidth > 0)
        {
            style.BorderWidthLeft = borderWidth;
            style.BorderWidthTop = borderWidth;
            style.BorderWidthRight = borderWidth;
            style.BorderWidthBottom = borderWidth;
            style.BorderColor = border ?? Colors.White;
        }
        return style;
    }

    internal static void StylePage(Control page)
    {
        WorkbenchThemeTokens tokens = WorkbenchThemeTokens.Resolve(page);
        page.SetMeta("tactics_workbench_dark_theme", tokens.Text.R + tokens.Text.G + tokens.Text.B > 1.5f);
        page.SetMeta("tactics_workbench_page", true);
        page.AddThemeConstantOverride("separation", 6);
        page.AddThemeStyleboxOverride("panel", PanelStyle(tokens.Background));
    }

    internal static WorkbenchToolbar Toolbar(Control owner)
    {
        var toolbar = new WorkbenchToolbar { CustomMinimumSize = new Vector2(0, ToolbarHeight) };
        toolbar.AddThemeConstantOverride("separation", 4);
        return toolbar;
    }

    internal static WorkbenchPane Pane(Control owner, int minimumWidth = 0)
    {
        var pane = new WorkbenchPane
        {
            CustomMinimumSize = new Vector2(minimumWidth, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        pane.AddThemeConstantOverride("separation", 6);
        return pane;
    }

    internal static WorkbenchInspectorSection InspectorSection(Control owner, string title, Color? accent = null, bool collapsed = false)
    {
        WorkbenchThemeTokens tokens = WorkbenchThemeTokens.Resolve(owner);
        var section = new WorkbenchInspectorSection { Accent = accent ?? tokens.Divider };
        section.AddThemeConstantOverride("separation", 4);
        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", 14);
        heading.AddThemeColorOverride("font_color", accent ?? tokens.Text);
        section.AddChild(heading);
        if (collapsed) section.SetMeta("collapsed_by_default", true);
        return section;
    }

    internal static void StyleStatus(Label status, bool error = false, bool warning = false)
    {
        WorkbenchThemeTokens tokens = WorkbenchThemeTokens.Resolve(status);
        status.SetMeta("tactics_workbench_status", true);
        status.SetMeta("tactics_workbench_status_error", error);
        status.SetMeta("tactics_workbench_status_warning", warning);
        status.AddThemeColorOverride("font_color", error ? tokens.Error : warning ? tokens.Warning : tokens.Success);
        status.AddThemeStyleboxOverride("normal", PanelStyle(tokens.Panel, 3, 1, tokens.Divider));
    }

    internal static void StyleGraph(GraphEdit graph)
    {
        WorkbenchThemeTokens tokens = WorkbenchThemeTokens.Resolve(graph);
        graph.AddThemeStyleboxOverride("panel", PanelStyle(tokens.Background));
        graph.AddThemeColorOverride("grid_major", tokens.Divider with { A = 0.42f });
        graph.AddThemeColorOverride("grid_minor", tokens.Divider with { A = 0.18f });
        graph.MinimapEnabled = true;
        graph.ShowGrid = true;
    }

    internal static void StyleGraphNode(GraphNode node, Color accent, bool enabled = true, bool orphan = false)
    {
        WorkbenchThemeTokens tokens = WorkbenchThemeTokens.Resolve(node);
        node.SetMeta("tactics_workbench_graph_accent", accent);
        node.SetMeta("tactics_workbench_graph_enabled", enabled);
        node.SetMeta("tactics_workbench_graph_orphan", orphan);
        Color border = orphan ? tokens.Error : accent;
        node.AddThemeStyleboxOverride("panel", PanelStyle(tokens.Panel, 7, 2, border));
        node.AddThemeStyleboxOverride("panel_selected", PanelStyle(tokens.RaisedPanel, 7, 3, tokens.Selection));
        node.AddThemeStyleboxOverride("titlebar", PanelStyle(accent.Darkened(0.25f), 6));
        node.AddThemeStyleboxOverride("titlebar_selected", PanelStyle(tokens.Selection.Darkened(0.45f), 6));
        node.Modulate = enabled ? Colors.White : new Color(1, 1, 1, 0.42f);
    }

    internal static void RefreshTheme(Control root)
    {
        foreach (Control control in EnumerateControls(root)) control.RemoveMeta("tactics_workbench_dark_theme");
        foreach (Control control in EnumerateControls(root))
        {
            if (control.HasMeta("tactics_workbench_page")) StylePage(control);
            if (control is GraphEdit graph) StyleGraph(graph);
            if (control is GraphNode node && node.HasMeta("tactics_workbench_graph_accent"))
                StyleGraphNode(node, node.GetMeta("tactics_workbench_graph_accent").AsColor(),
                    node.GetMeta("tactics_workbench_graph_enabled").AsBool(), node.GetMeta("tactics_workbench_graph_orphan").AsBool());
            if (control is Label label && label.HasMeta("tactics_workbench_status"))
                StyleStatus(label, label.GetMeta("tactics_workbench_status_error").AsBool(), label.GetMeta("tactics_workbench_status_warning").AsBool());
            control.QueueRedraw();
        }
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        yield return root;
        foreach (Node child in root.GetChildren())
            if (child is Control control)
                foreach (Control descendant in EnumerateControls(control)) yield return descendant;
    }
}

[Tool]
internal sealed partial class WorkbenchThemeWatcher : Node
{
    private Control? _root;
    private Color _lastText;
    internal void Configure(Control root) { _root = root; _lastText = root.GetThemeColor("font_color", "Label"); SetProcess(true); }
    public override void _Process(double delta)
    {
        _ = delta;
        if (_root is null || !GodotObject.IsInstanceValid(_root)) { SetProcess(false); return; }
        Color current = _root.GetThemeColor("font_color", "Label");
        if (current.IsEqualApprox(_lastText)) return;
        _lastText = current;
        WorkbenchUi.RefreshTheme(_root);
    }
    public override void _ExitTree() { SetProcess(false); _root = null; }
}

[Tool]
internal partial class WorkbenchPageShell : VBoxContainer
{
    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), WorkbenchThemeTokens.Resolve(this).Background);
    public override void _Notification(int what) { if (what == NotificationThemeChanged) QueueRedraw(); }
}

[Tool]
internal partial class WorkbenchToolbar : HBoxContainer
{
    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), WorkbenchThemeTokens.Resolve(this).RaisedPanel);
    public override void _Notification(int what) { if (what == NotificationThemeChanged) QueueRedraw(); }
}

[Tool]
internal partial class WorkbenchPane : VBoxContainer
{
    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), WorkbenchThemeTokens.Resolve(this).Panel);
    public override void _Notification(int what) { if (what == NotificationThemeChanged) QueueRedraw(); }
}

[Tool]
internal partial class WorkbenchInspectorSection : VBoxContainer
{
    internal Color Accent { get; init; } = Colors.Gray;
    public override void _Draw()
    {
        WorkbenchThemeTokens tokens = WorkbenchThemeTokens.Resolve(this);
        DrawStyleBox(WorkbenchUi.PanelStyle(tokens.RaisedPanel, 5, 1, Accent), new Rect2(Vector2.Zero, Size));
    }
    public override void _Notification(int what) { if (what == NotificationThemeChanged) QueueRedraw(); }
}

internal static class WorkbenchGraphStyle
{
    internal static void Apply(GraphEdit graph) => WorkbenchUi.StyleGraph(graph);
    internal static void Apply(GraphNode node, Color accent, bool enabled = true, bool orphan = false) =>
        WorkbenchUi.StyleGraphNode(node, accent, enabled, orphan);
}

[Tool]
internal sealed partial class CircularMapGraphNode : GraphNode
{
    internal const float Diameter = 44f;

    internal void Configure(string glyph, Color fill, string tooltip)
    {
        Title = string.Empty;
        TooltipText = tooltip;
        Resizable = false;
        CustomMinimumSize = new Vector2(Diameter, Diameter);
        Size = new Vector2(Diameter, Diameter);
        GetTitlebarHBox().Visible = false;

        WorkbenchThemeTokens tokens = WorkbenchThemeTokens.Resolve(this);
        StyleBoxFlat normal = WorkbenchUi.PanelStyle(fill, 22, 2, Colors.White);
        StyleBoxFlat selected = WorkbenchUi.PanelStyle(fill.Lightened(0.08f), 22, 3, tokens.Selection);
        foreach (StyleBoxFlat style in new[] { normal, selected })
        {
            style.ContentMarginLeft = 0; style.ContentMarginRight = 0;
            style.ContentMarginTop = 0; style.ContentMarginBottom = 0;
        }
        AddThemeStyleboxOverride("panel", normal);
        AddThemeStyleboxOverride("panel_selected", selected);
        AddThemeStyleboxOverride("titlebar", new StyleBoxEmpty());
        AddThemeStyleboxOverride("titlebar_selected", new StyleBoxEmpty());

        var label = new Label
        {
            Text = glyph,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(Diameter, Diameter),
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 2);
        AddChild(label);
        SetSlot(0, true, 0, Colors.White, true, 0, Colors.White);
    }
}
#endif
