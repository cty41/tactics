#if TOOLS
using System.Globalization;
using Godot;
using Tactics.Application.Authoring;

namespace Tactics.Godot.Adapter.Editor;

public static class PresentationProfileAuthoringEditorService
{
    private static readonly HashSet<string> ProgrammaticKinds = new(StringComparer.Ordinal)
        { "amplify-damage", "bone-spear", "fireball", "ice-bolt", "lightning", "poison-spear", "thrust" };
    public static PresentationProfileAuthoringDocument Read(Resource resource)
    {
        using Variant contentIdValue = resource.Get("ContentIdValue");
        string contentId = contentIdValue.AsString();
        if (string.IsNullOrWhiteSpace(contentId)) throw new InvalidOperationException("Presentation Resource has no ContentIdValue.");
        var properties = new Dictionary<string, PresentationAuthoringValue>(StringComparer.Ordinal);
        global::Godot.Collections.Array<global::Godot.Collections.Dictionary> descriptors = resource.GetPropertyList();
        using global::Godot.Collections.Array descriptorStorage = (global::Godot.Collections.Array)descriptors;
        foreach (global::Godot.Collections.Dictionary descriptor in descriptors)
        {
            using Variant nameValue = descriptor["name"];
            using Variant typeValue = descriptor["type"];
            using Variant usageValue = descriptor["usage"];
            string name = nameValue.AsString(); Variant.Type type = (Variant.Type)typeValue.AsInt32();
            PropertyUsageFlags usage = (PropertyUsageFlags)usageValue.AsInt32();
            if (name == "ContentIdValue" || name == "script" || !usage.HasFlag(PropertyUsageFlags.ScriptVariable) || !Supported(type)) continue;
            using Variant value = resource.Get(name); properties[name] = Encode(type, value);
        }
        return new PresentationProfileAuthoringDocument(contentId, resource.GetType().Name, properties);
    }

    public static void Write(Resource resource, PresentationProfileAuthoringDocument document)
    {
        using Variant contentIdValue = resource.Get("ContentIdValue");
        if (contentIdValue.AsString() != document.ContentId || resource.GetType().Name != document.ResourceClass) throw new InvalidOperationException("Presentation profile identity or class differs.");
        PresentationProfileAuthoringDocument current = Read(resource);
        if (!current.Properties.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(document.Properties.Keys)) throw new InvalidOperationException("Presentation profile properties cannot be added or removed.");
        if (document.Properties.TryGetValue("AuthoringGraphJsonValue", out PresentationAuthoringValue? graphValue) && !string.IsNullOrWhiteSpace(graphValue.Value))
        {
            PresentationGraphAuthoringDocument graph = PresentationGraphAuthoringJson.Deserialize(graphValue.Value);
            graph.Validate(document.Properties.Keys.Where(value => value != "AuthoringGraphJsonValue"));
            graph.ValidateRuntimeCompatibility();
        }
        foreach ((string name, PresentationAuthoringValue value) in document.Properties)
        {
            if (current.Properties[name].Kind != value.Kind) throw new InvalidOperationException($"Presentation property '{name}' cannot change type.");
            ValidateValue(name, value, current.Properties[name]);
        }
        foreach ((string name, PresentationAuthoringValue value) in document.Properties)
        {
            using Variant decoded = Decode(value);
            resource.Set(name, decoded);
        }
        PresentationProfileAuthoringDocument reloaded = Read(resource);
        if (AuthoringRevision.Compute(reloaded) != AuthoringRevision.Compute(document)) throw new InvalidOperationException("Presentation profile write did not preserve the validated document.");
    }
    private static bool Supported(Variant.Type type) => type is Variant.Type.String or Variant.Type.Int or Variant.Type.Float or Variant.Type.Bool or Variant.Type.Color or Variant.Type.Vector2;
    private static PresentationAuthoringValue Encode(Variant.Type type, Variant value) => type switch
    {
        Variant.Type.String => new(PresentationAuthoringValueKind.String, value.AsString()), Variant.Type.Int => new(PresentationAuthoringValueKind.Integer, value.AsInt64().ToString(CultureInfo.InvariantCulture)), Variant.Type.Float => new(PresentationAuthoringValueKind.Number, value.AsDouble().ToString("R", CultureInfo.InvariantCulture)), Variant.Type.Bool => new(PresentationAuthoringValueKind.Boolean, value.AsBool().ToString()), Variant.Type.Color => new(PresentationAuthoringValueKind.Color, Format(value.AsColor())), Variant.Type.Vector2 => new(PresentationAuthoringValueKind.Vector2, Format(value.AsVector2())), _ => throw new InvalidOperationException()
    };
    private static Variant Decode(PresentationAuthoringValue value) => value.Kind switch
    {
        PresentationAuthoringValueKind.String => value.Value, PresentationAuthoringValueKind.Integer => long.Parse(value.Value, CultureInfo.InvariantCulture), PresentationAuthoringValueKind.Number => double.Parse(value.Value, CultureInfo.InvariantCulture), PresentationAuthoringValueKind.Boolean => bool.Parse(value.Value), PresentationAuthoringValueKind.Color => ParseColor(value.Value), PresentationAuthoringValueKind.Vector2 => ParseVector2(value.Value), _ => throw new InvalidOperationException()
    };
    private static string Format(Color value) => string.Join(',', new[] { value.R, value.G, value.B, value.A }.Select(item => item.ToString("R", CultureInfo.InvariantCulture)));
    private static string Format(Vector2 value) => string.Join(',', new[] { value.X, value.Y }.Select(item => item.ToString("R", CultureInfo.InvariantCulture)));
    private static Color ParseColor(string value) { float[] parts = value.Split(',').Select(item => float.Parse(item, CultureInfo.InvariantCulture)).ToArray(); if (parts.Length != 4) throw new FormatException("Color requires r,g,b,a."); return new Color(parts[0], parts[1], parts[2], parts[3]); }
    private static Vector2 ParseVector2(string value) { float[] parts = value.Split(',').Select(item => float.Parse(item, CultureInfo.InvariantCulture)).ToArray(); if (parts.Length != 2) throw new FormatException("Vector2 requires x,y."); return new Vector2(parts[0], parts[1]); }
    private static void ValidateValue(string name, PresentationAuthoringValue value, PresentationAuthoringValue current)
    {
        using Variant decoded = Decode(value);
        if (name is "PayloadBoundary" or "MarkerContract" && value.Value != current.Value) throw new InvalidOperationException($"Presentation contract field '{name}' is read-only.");
        if (name == "ProgrammaticKind" && !ProgrammaticKinds.Contains(value.Value)) throw new InvalidOperationException($"Unknown ProgrammaticKind '{value.Value}'.");
        if (name.Contains("Duration", StringComparison.Ordinal) && decoded.AsDouble() < 0) throw new InvalidOperationException($"Presentation duration '{name}' cannot be negative.");
        if (name.StartsWith("Maximum", StringComparison.Ordinal) && decoded.AsInt64() < 0) throw new InvalidOperationException($"Presentation count '{name}' cannot be negative.");
        if (name == "MaximumVisibleStatuses" && decoded.AsInt64() <= 0) throw new InvalidOperationException("MaximumVisibleStatuses must be positive.");
        if (value.Kind == PresentationAuthoringValueKind.Color)
        {
            Color color = decoded.AsColor(); if (!float.IsFinite(color.R) || !float.IsFinite(color.G) || !float.IsFinite(color.B) || !float.IsFinite(color.A) || color.R is < 0 or > 1 || color.G is < 0 or > 1 || color.B is < 0 or > 1 || color.A is < 0 or > 1) throw new InvalidOperationException($"Presentation color '{name}' must use finite 0..1 channels.");
        }
        if (value.Kind == PresentationAuthoringValueKind.Vector2)
        {
            Vector2 vector = decoded.AsVector2(); if (!float.IsFinite(vector.X) || !float.IsFinite(vector.Y)) throw new InvalidOperationException($"Presentation vector '{name}' must be finite.");
        }
    }
}
#endif
