using Godot;
using Tactics.Application.Units;

namespace Tactics.Godot.Adapter.Runtime;

/// <summary>
/// CPU reference for deterministic captures of the Unit tint contracts used by Godot shaders.
/// </summary>
public static class GodotUnitTintReference
{
    private const float MaskStart = 0.10f;
    private const float MaskEnd = 0.28f;
    private const float MinimumBaseLuminance = 0.01f;
    private static readonly Vector3 LuminanceWeights = new(0.299f, 0.587f, 0.114f);

    public static Image CopyTextureImage(Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        Image source = texture.GetImage();
        return source.GetRegion(new Rect2I(Vector2I.Zero, source.GetSize()));
    }

    public static Color Apply(Color sourceSrgb, string tintMode, Color bodyTintSrgb, Color baseBodyColorSrgb)
    {
        Color sourceLinear = sourceSrgb.SrgbToLinear();
        Color tintLinear = bodyTintSrgb.SrgbToLinear();

        Color outputLinear = tintMode switch
        {
            UnitBodyTintModes.Multiply => new Color(
                sourceLinear.R * tintLinear.R,
                sourceLinear.G * tintLinear.G,
                sourceLinear.B * tintLinear.B,
                sourceSrgb.A * bodyTintSrgb.A),
            UnitBodyTintModes.GoatBodyMaskV1 => ApplyGoatBodyMask(
                sourceLinear,
                tintLinear,
                baseBodyColorSrgb.SrgbToLinear()),
            _ => throw new ArgumentOutOfRangeException(nameof(tintMode), tintMode, "Unknown Unit tint mode.")
        };

        Color outputSrgb = outputLinear.LinearToSrgb();
        return new Color(
            Mathf.Clamp(outputSrgb.R, 0f, 1f),
            Mathf.Clamp(outputSrgb.G, 0f, 1f),
            Mathf.Clamp(outputSrgb.B, 0f, 1f),
            Mathf.Clamp(outputLinear.A, 0f, 1f));
    }

    public static void Apply(Image image, string tintMode, Color bodyTint, Color baseBodyColor)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (tintMode == UnitBodyTintModes.Multiply && bodyTint == Colors.White)
            return;
        if (image.GetFormat() != Image.Format.Rgba8)
            image.Convert(Image.Format.Rgba8);

        for (int y = 0; y < image.GetHeight(); y++)
        {
            for (int x = 0; x < image.GetWidth(); x++)
                image.SetPixel(x, y, Apply(image.GetPixel(x, y), tintMode, bodyTint, baseBodyColor));
        }
    }

    private static Color ApplyGoatBodyMask(Color source, Color tint, Color baseBodyColor)
    {
        float redDistance = source.R - baseBodyColor.R;
        float greenDistance = source.G - baseBodyColor.G;
        float blueDistance = source.B - baseBodyColor.B;
        float sourceDistance = MathF.Sqrt(
            redDistance * redDistance + greenDistance * greenDistance + blueDistance * blueDistance);
        float interpolation = Mathf.Clamp((sourceDistance - MaskStart) / (MaskEnd - MaskStart), 0f, 1f);
        float smoothstep = interpolation * interpolation * (3f - 2f * interpolation);
        float mask = 1f - smoothstep;
        float sourceLuminance = source.R * LuminanceWeights.X +
            source.G * LuminanceWeights.Y + source.B * LuminanceWeights.Z;
        float baseLuminance = MathF.Max(
            baseBodyColor.R * LuminanceWeights.X +
            baseBodyColor.G * LuminanceWeights.Y +
            baseBodyColor.B * LuminanceWeights.Z,
            MinimumBaseLuminance);
        var recoloredBody = new Vector3(tint.R, tint.G, tint.B) * (sourceLuminance / baseLuminance);
        return new Color(
            Mathf.Lerp(source.R, recoloredBody.X, mask),
            Mathf.Lerp(source.G, recoloredBody.Y, mask),
            Mathf.Lerp(source.B, recoloredBody.Z, mask),
            source.A);
    }
}
