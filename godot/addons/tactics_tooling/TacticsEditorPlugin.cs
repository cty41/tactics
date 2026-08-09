#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Editor;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class TacticsEditorPlugin : EditorPlugin
{
    private TacticsGraphWorkbench? _workbench;

    public override void _EnterTree()
    {
        GD.Print("[Tactics Tooling] EditorPlugin entering tree.");

        try
        {
            if (OS.GetCmdlineArgs().Contains("--build-poison-spear"))
            {
                PoisonSpearAssetFactory.BuildLv1();
                GetTree().Quit();
                return;
            }

            _workbench = new TacticsGraphWorkbench();
            _workbench.Configure(GetUndoRedo());
            EditorInterface.Singleton.GetEditorMainScreen().AddChild(_workbench);
            _MakeVisible(false);
            GD.Print("[Tactics Tooling] Main screen registered.");
        }
        catch (Exception exception)
        {
            GD.PushError($"[Tactics Tooling] Failed to initialize: {exception}");
            CleanupWorkbench();
        }
    }

    public override void _ExitTree()
    {
        GD.Print("[Tactics Tooling] EditorPlugin exiting tree.");
        CleanupWorkbench();
    }

    public override bool _HasMainScreen() => true;

    public override void _MakeVisible(bool visible)
    {
        if (_workbench is not null && GodotObject.IsInstanceValid(_workbench))
            _workbench.Visible = visible;
    }

    public override string _GetPluginName() => "Tactics Tooling";

    public override Texture2D _GetPluginIcon() =>
        EditorInterface.Singleton.GetEditorTheme().GetIcon("Node", "EditorIcons");

    private void CleanupWorkbench()
    {
        if (_workbench is not null && GodotObject.IsInstanceValid(_workbench))
            _workbench.QueueFree();
        _workbench = null;
    }
}
#endif
