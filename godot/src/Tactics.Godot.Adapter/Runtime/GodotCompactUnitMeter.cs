using Godot;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Theme-independent diagnostic HP/MP overlay with exact pixel bounds.</summary>
public partial class GodotCompactUnitMeter : Control
{
    private static readonly Vector2 ActorOffset = new(-30, -54);
    private GodotUnitActor? _actor;
    private int _health;
    private int _maxHealth = 1;
    private int _mana;
    private int _maxMana = 1;

    public GodotCompactUnitMeter()
    {
        Size = GodotPlayableRunMain.UnitMeterSize;
        CustomMinimumSize = Vector2.Zero;
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        SetProcess(true);
    }

    public void Bind(GodotUnitActor actor, int health, int maxHealth, int mana, int maxMana)
    {
        _actor = actor;
        _health = Math.Max(0, health);
        _maxHealth = Math.Max(1, maxHealth);
        _mana = Math.Max(0, mana);
        _maxMana = Math.Max(1, maxMana);
        FollowActor();
        QueueRedraw();
    }

    public override void _Process(double delta) => FollowActor();

    public override void _Draw()
    {
        DrawMeter(new Rect2(0, 0, 60, 8), (float)_health / _maxHealth,
            new Color(0.12f, 0.2f, 0.16f, 0.92f), new Color(0.22f, 0.78f, 0.32f, 0.95f), $"HP {_health}/{_maxHealth}");
        DrawMeter(new Rect2(0, 10, 60, 8), (float)_mana / _maxMana,
            new Color(0.1f, 0.16f, 0.24f, 0.92f), new Color(0.24f, 0.5f, 0.92f, 0.95f), $"MP {_mana}/{_maxMana}");
    }

    private void DrawMeter(Rect2 rect, float ratio, Color background, Color fill, string text)
    {
        DrawRect(rect, background);
        DrawRect(new Rect2(rect.Position + Vector2.One,
            new Vector2((rect.Size.X - 2) * Math.Clamp(ratio, 0f, 1f), rect.Size.Y - 2)), fill);
        DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(2, 7), text,
            HorizontalAlignment.Left, -1, 7, Colors.White);
    }

    private void FollowActor()
    {
        if (_actor is not null && GodotObject.IsInstanceValid(_actor)) Position = _actor.Position + ActorOffset;
    }
}
