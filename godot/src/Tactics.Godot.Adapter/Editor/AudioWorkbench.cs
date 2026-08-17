#if TOOLS
using Godot;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

[Tool]
public partial class AudioWorkbench : VBoxContainer
{
    private GodotAudioRuntime? _runtime;
    private GodotAudioSettingsV1 _draft = GodotAudioSettingsV1.Default;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        WorkbenchUi.StylePage(this);
        var toolbar = WorkbenchUi.Toolbar(this); toolbar.AddChild(new Label { Text = "AUDIO MIXER" }); AddChild(toolbar);
        var notice = new Label { Text = "No distributable audio payload is registered. Add licensed AudioCueDefinition resources before content acceptance." }; WorkbenchUi.StyleStatus(notice, warning: true); AddChild(notice);
        _runtime = new GodotAudioRuntime();
        AddChild(_runtime);
        var mixer = WorkbenchUi.InspectorSection(this, "Master / Music / SFX / UI", new Color("4a90d9"));
        foreach (string bus in new[] { "Master", "Music", "SFX", "UI" })
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = bus, CustomMinimumSize = new Vector2(100, 0) });
            var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01, Value = bus == "Music" ? 0.8 : 1, CustomMinimumSize = new Vector2(300, 0) };
            slider.ValueChanged += _ => Apply(slider, bus);
            row.AddChild(slider);
            mixer.AddChild(row);
        }
        AddChild(mixer);
        var stop = new Button { Text = "Stop Preview" };
        stop.Pressed += () => _runtime.StopAll();
        AddChild(stop);
    }

    private void Apply(HSlider changed, string bus)
    {
        _draft = bus switch
        {
            "Master" => _draft with { Master = (float)changed.Value },
            "Music" => _draft with { Music = (float)changed.Value },
            "SFX" => _draft with { Sfx = (float)changed.Value },
            "UI" => _draft with { Ui = (float)changed.Value },
            _ => _draft
        };
        _runtime?.ApplySettings(_draft);
    }
}
#endif
