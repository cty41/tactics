#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Editor;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class TacticsEditorPlugin : EditorPlugin
{
    private TacticsContentWorkbench? _workbench;
    private bool _headless;
    private bool _filesystemSubscribed;
    private TacticsAuthoringEditorBridge? _authoringBridge;

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

            // Headless import/generation runs can temporarily expose scripted resources as base Resource
            // while EditorFileSystem updates the C# global-class cache. The authoring UI has no headless
            // consumer, so keep the plugin lifecycle observable without constructing its typed panels.
            if (DisplayServer.GetName() == "headless")
            {
                _headless = true;
                SetProcess(false);
                GD.Print("[Tactics Tooling] Main screen skipped in headless editor mode.");
                return;
            }

            SetProcess(true);
            _authoringBridge = new TacticsAuthoringEditorBridge();
            _authoringBridge.Configure(GetUndoRedo());
            AddChild(_authoringBridge);
            EditorInterface.Singleton.GetResourceFilesystem().FilesystemChanged += OnFilesystemChanged;
            _filesystemSubscribed = true;
        }
        catch (Exception exception)
        {
            GD.PushError($"[Tactics Tooling] Failed to initialize: {exception}");
            CleanupWorkbench();
            CleanupBridge();
        }
    }

    public override void _ExitTree()
    {
        GD.Print("[Tactics Tooling] EditorPlugin exiting tree.");
        if (_filesystemSubscribed)
        {
            EditorInterface.Singleton.GetResourceFilesystem().FilesystemChanged -= OnFilesystemChanged;
            _filesystemSubscribed = false;
        }
        CleanupWorkbench();
        CleanupBridge();
    }

    public override bool _HasMainScreen() => true;

    public override void _Process(double delta)
    {
        _ = delta;
        if (_headless) return;
        if (_workbench is not null || EditorInterface.Singleton.GetResourceFilesystem().IsScanning()) return;
        SetProcess(false);
        RegisterMainScreenWhenReady();
    }

    public override void _MakeVisible(bool visible)
    {
        if (_workbench is not null && GodotObject.IsInstanceValid(_workbench))
            _workbench.Visible = visible;
    }

    public override string _GetPluginName() => "Tactics Tooling";

    public override Texture2D _GetPluginIcon() =>
        EditorInterface.Singleton.GetEditorTheme().GetIcon("Node", "EditorIcons");

    private void RegisterMainScreenWhenReady()
    {
        if (_headless) return;
        if (!IsInsideTree()) return;
        if (EditorInterface.Singleton.GetResourceFilesystem().IsScanning()) return;
        if (_workbench is not null) return;
        _workbench = new TacticsContentWorkbench();
        _workbench.Configure(GetUndoRedo());
        EditorInterface.Singleton.GetEditorMainScreen().AddChild(_workbench);
        _MakeVisible(false);
        _authoringBridge?.MarkReady();
        GD.Print("[Tactics Tooling] Main screen registered after filesystem scan.");
    }

    private void OnFilesystemChanged() => CallDeferred(nameof(RegisterMainScreenWhenReady));

    private void CleanupWorkbench()
    {
        if (_workbench is not null && GodotObject.IsInstanceValid(_workbench))
            _workbench.QueueFree();
        _workbench = null;
    }

    private void CleanupBridge()
    {
        if (_authoringBridge is not null && GodotObject.IsInstanceValid(_authoringBridge)) _authoringBridge.QueueFree();
        _authoringBridge = null;
    }
}
#endif
