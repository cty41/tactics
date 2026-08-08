#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Editor;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class TacticsEditorPlugin : EditorPlugin
{
    private TacticsGraphWorkbench? _workbench;
    private EditorDock? _dock;

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

            // Use the Godot 4.7 dock API so the workbench has an explicit,
            // reopenable editor surface instead of relying on CanvasEditorBottom.
            _dock = new EditorDock();
            _dock.AddChild(_workbench);
            _dock.Title = "Tactics Tooling";
            _dock.DefaultSlot = EditorDock.DockSlot.Bottom;
            _dock.AvailableLayouts = EditorDock.DockLayout.Horizontal | EditorDock.DockLayout.Floating;
            AddDock(_dock);
            _dock.MakeVisible();
            GD.Print("[Tactics Tooling] Editor dock registered.");
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

    private void CleanupWorkbench()
    {
        if (_dock is not null)
        {
            RemoveDock(_dock);
            _dock.QueueFree();
        }

        _dock = null;
        _workbench = null;
    }
}
#endif
