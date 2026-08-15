using Godot;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>Builds the native Godot equivalent of the project's Unity UI Toolkit visual language.</summary>
public static class GodotTacticsTheme
{
    public const string PrimaryButton = "TacticsPrimaryButton";
    public const string SecondaryButton = "TacticsSecondaryButton";
    public const string CompactButton = "TacticsCompactButton";
    public const string ActionButton = "TacticsActionButton";
    public const string Panel = "TacticsPanel";
    public const string Card = "TacticsCard";

    public static readonly Color Background = new("090d10");
    public static readonly Color PanelBackground = new(0.02f, 0.02f, 0.02f, 0.94f);
    public static readonly Color PanelSoft = new(0.08f, 0.08f, 0.08f, 0.90f);
    public static readonly Color CardBackground = new(0.15f, 0.15f, 0.15f, 0.94f);
    public static readonly Color Accent = new("f9874b");
    public static readonly Color AccentHover = new("ffa064");
    public static readonly Color TextPrimary = new("f4f4f2");
    public static readonly Color TextSecondary = new("b6b8b9");
    public static readonly Color DisabledText = new("737679");
    public static readonly Color DisabledBackground = new(0.10f, 0.10f, 0.10f, 0.84f);

    public static Theme Create()
    {
        var theme = new Theme();
        theme.SetColor("font_color", "Label", TextPrimary);
        theme.SetColor("font_shadow_color", "Label", new Color(0, 0, 0, .65f));
        theme.SetConstant("shadow_offset_x", "Label", 1);
        theme.SetConstant("shadow_offset_y", "Label", 1);
        theme.SetFontSize("font_size", "Label", 18);

        theme.SetTypeVariation(PrimaryButton, "Button");
        theme.SetTypeVariation(SecondaryButton, "Button");
        theme.SetTypeVariation(CompactButton, "Button");
        theme.SetTypeVariation(ActionButton, "Button");
        ConfigureButton(theme, "Button", PanelSoft, Accent, 1, 8, 18);
        ConfigureButton(theme, PrimaryButton, PanelSoft, Accent, 2, 8, 20);
        ConfigureButton(theme, SecondaryButton, PanelSoft, Accent, 1, 8, 18);
        ConfigureButton(theme, CompactButton, PanelSoft, Accent, 1, 6, 15);
        ConfigureButton(theme, ActionButton, CardBackground, Accent, 1, 6, 15);

        theme.SetTypeVariation(Panel, "PanelContainer");
        theme.SetTypeVariation(Card, "PanelContainer");
        theme.SetStylebox("panel", "PanelContainer", Box(PanelBackground, Accent, 2, 12, 20));
        theme.SetStylebox("panel", Panel, Box(PanelBackground, Accent, 2, 12, 20));
        theme.SetStylebox("panel", Card, Box(CardBackground, new Color("5a5a5a"), 1, 8, 14));
        return theme;
    }

    private static void ConfigureButton(Theme theme, string type, Color normalColor, Color borderColor,
        int borderWidth, int radius, int fontSize, Color? fontColor = null)
    {
        theme.SetColor("font_color", type, fontColor ?? TextPrimary);
        theme.SetColor("font_hover_color", type, TextPrimary);
        theme.SetColor("font_pressed_color", type, TextPrimary);
        theme.SetColor("font_focus_color", type, TextPrimary);
        theme.SetColor("font_disabled_color", type, DisabledText);
        theme.SetFontSize("font_size", type, fontSize);
        theme.SetStylebox("normal", type, Box(normalColor, borderColor, borderWidth, radius, 12));
        theme.SetStylebox("hover", type, Box(new Color(0.12f, 0.12f, 0.12f, .96f), AccentHover, 2, radius, 12));
        theme.SetStylebox("pressed", type, Box(new Color(0.20f, 0.10f, 0.06f, .98f), Accent, 2, radius, 12));
        theme.SetStylebox("focus", type, Box(new Color(0, 0, 0, 0), AccentHover, 2, radius, 10));
        theme.SetStylebox("disabled", type, Box(DisabledBackground, new Color("45484a"), 1, radius, 12));
    }

    private static StyleBoxFlat Box(Color background, Color border, int borderWidth, int radius, float margin) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = borderWidth,
        BorderWidthTop = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthBottom = borderWidth,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius,
        CornerRadiusBottomRight = radius,
        ContentMarginLeft = margin,
        ContentMarginTop = margin * .55f,
        ContentMarginRight = margin,
        ContentMarginBottom = margin * .55f,
        AntiAliasing = true,
    };
}
