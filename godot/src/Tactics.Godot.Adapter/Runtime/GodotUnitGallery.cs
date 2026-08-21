using Godot;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// Programmatic visual fixture displaying the complete current Unit catalog.
/// </summary>
[GlobalClass]
public partial class GodotUnitGallery : Node2D
{
    public static readonly Color PreviewBackgroundColor = new("596875");

    // Gallery rows use Actor ground/corpse anchors; texture centers are derived from sprite pivots.
    internal const string LayoutContract = "ground-baseline-native-1600x900-v3";
    internal const float ActorScale = 0.65f;
    internal const float FirstColumnGroundX = 160f;
    internal const float FirstRowGroundY = 190f;
    internal const float ColumnSpacing = 320f;
    internal const float RowSpacing = 280f;
    internal const float LabelOffsetX = -110f;
    internal const float LabelOffsetY = 52.5f;
    internal const float LabelWidth = 220f;
    internal const float LabelHeight = 42.5f;
    internal const int PreviewFontSize = 20;

    private const int ColumnCount = 5;

    private readonly List<GodotUnitActor> _actors = [];
    private bool _showDeath;
    private bool _goatTintEnabled = true;
    private GodotUnitFacing _currentFacing = GodotUnitFacing.South;
    private Label? _status;

    [Export] public GodotResourceCatalog? Catalog { get; set; }

    public IReadOnlyList<GodotUnitActor> Actors => _actors;
    public GodotUnitFacing CurrentFacing => _currentFacing;
    public bool IsShowingDeathMode => _showDeath;
    public bool IsGoatTintEnabled => _goatTintEnabled;

    public override void _Ready()
    {
        BuildGallery();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;
        bool handled = true;
        switch (key.Keycode)
        {
            case Key.Key1:
                SetAllFacing(GodotUnitFacing.South);
                break;
            case Key.Key2:
                SetAllFacing(GodotUnitFacing.North);
                break;
            case Key.Key3:
                SetAllFacing(GodotUnitFacing.East);
                break;
            case Key.Key4:
                SetAllFacing(GodotUnitFacing.West);
                break;
            case Key.D:
                SetAllDeath(!_showDeath);
                break;
            case Key.T:
                SetGoatTintEnabled(!_goatTintEnabled);
                break;
            case Key.R:
                ResetPreview();
                break;
            default:
                handled = false;
                break;
        }
        if (handled)
            GetViewport().SetInputAsHandled();
    }

    public void BuildGallery()
    {
        if (_actors.Count > 0)
            return;
        if (Catalog is null)
            throw new InvalidOperationException("Unit gallery has no ContentCatalog.");
        Catalog.Validate();

        var background = new ColorRect
        {
            Color = PreviewBackgroundColor,
            Size = UnitPreviewLayout.CanvasRect.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = -100
        };
        AddChild(background);

        var instructions = new Label
        {
            Text = "1 South (DR)  |  2 North (UL)  |  3 East (UL mirrored)  |  4 West (DR mirrored)",
            Position = new Vector2(25f, 5f),
            Size = new Vector2(1550f, 35f),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color("e3edf7")
        };
        instructions.AddThemeFontSizeOverride("font_size", PreviewFontSize);
        AddChild(instructions);

        _status = new Label
        {
            Position = new Vector2(25f, 37.5f),
            Size = new Vector2(1550f, 35f),
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color("9ec7df")
        };
        _status.AddThemeFontSizeOverride("font_size", PreviewFontSize);
        AddChild(_status);
        UpdateStatus();

        GodotResourceEntry[] entries = Catalog.Entries
            .Where(entry => entry.ResourceTypeIdValue == "unit")
            .OrderBy(entry => entry.ContentIdValue, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < entries.Length; index++)
        {
            if (!Catalog.TryGet(entries[index].ContentIdValue, out Resource? loaded) ||
                loaded is not UnitDefinitionResource definition)
            {
                throw new InvalidOperationException(
                    $"Unit gallery cannot load '{entries[index].ContentIdValue}'.");
            }

            GodotUnitActor actor = GodotUnitFactory.InstantiateActor(definition);
            actor.Position = GetActorGroundPosition(index);
            actor.Scale = new Vector2(ActorScale, ActorScale);
            actor.SetFacing(GodotUnitFacing.South);
            actor.SetDeathVisual(false);
            actor.SetBodyTintEnabled(true);
            AddChild(actor);
            _actors.Add(actor);

            var label = new Label
            {
                Text = definition.DisplayName,
                Position = GetLabelPosition(index),
                Size = new Vector2(LabelWidth, LabelHeight),
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color("e3edf7")
            };
            label.AddThemeFontSizeOverride("font_size", PreviewFontSize);
            AddChild(label);
        }
    }

    internal static Vector2 GetActorGroundPosition(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return new Vector2(
            FirstColumnGroundX + (index % ColumnCount) * ColumnSpacing,
            FirstRowGroundY + (index / ColumnCount) * RowSpacing);
    }

    internal static Vector2 GetLabelPosition(int index)
    {
        return GetActorGroundPosition(index) + new Vector2(LabelOffsetX, LabelOffsetY);
    }

    public void SetAllFacing(GodotUnitFacing facing)
    {
        _showDeath = false;
        _currentFacing = facing;
        foreach (GodotUnitActor actor in _actors)
        {
            actor.SetDeathVisual(false);
            actor.SetFacing(facing);
        }
        UpdateStatus();
    }

    public void SetAllDeath(bool enabled)
    {
        _showDeath = enabled;
        foreach (GodotUnitActor actor in _actors)
            actor.SetDeathVisual(enabled);
        UpdateStatus();
    }

    public void SetGoatTintEnabled(bool enabled)
    {
        _goatTintEnabled = enabled;
        foreach (GodotUnitActor actor in _actors.Where(actor => actor.UsesGoatBodyMaskTint))
            actor.SetBodyTintEnabled(enabled);
        UpdateStatus();
    }

    public void ResetPreview()
    {
        _showDeath = false;
        _goatTintEnabled = true;
        _currentFacing = GodotUnitFacing.South;
        foreach (GodotUnitActor actor in _actors)
        {
            actor.SetFacing(GodotUnitFacing.South);
            actor.SetDeathVisual(false);
            actor.SetBodyTintEnabled(true);
        }
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_status is null)
            return;
        _status.Text =
            $"Facing: {_currentFacing}  |  Visual: {(_showDeath ? "Death" : "Living")}" +
            $"  |  Goat Tint: {(_goatTintEnabled ? "On" : "Off")}" +
            "  |  D Death  |  T Goat Tint Compare  |  R South/Living";
    }
}
