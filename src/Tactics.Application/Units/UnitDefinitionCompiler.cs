using System.Collections.ObjectModel;
using Tactics.Application.Content;
using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Application.Units;

public sealed record UnitDefinitionCompileResult(
    IReadOnlyDictionary<ContentId, CompiledUnitDefinition>? Definitions,
    IReadOnlyList<ContentDiagnostic> Diagnostics)
{
    public bool Succeeded =>
        Definitions is not null && Diagnostics.All(item => item.Severity != ContentDiagnosticSeverity.Error);
}

/// <summary>
/// Compiles the frozen Pure Run Unit DTO into typed gameplay and visual definitions.
/// </summary>
public sealed class UnitDefinitionCompiler
{
    public const int SchemaVersion = 1;
    public const string ActorContentId = "packed-scene.unit-actor";

    private static readonly string[] ExpectedPureRunIds =
    {
        "unit.pure-run.mage",
        "unit.pure-run.necromancer",
        "unit.pure-run.amazon",
        "unit.pure-run.skeleton-warrior",
        "unit.pure-run.skeleton-mage",
        "unit.pure-run.fire-demon",
        "unit.pure-run.goat-charger",
        "unit.pure-run.goat-ranged",
        "unit.pure-run.goat-aoe",
        "unit.pure-run.goat-support",
        "unit.pure-run.goat-elite-charger",
        "unit.pure-run.goat-elite-poison-caster"
    };

    private readonly bool _requireCompletePureRunBatch;

    public UnitDefinitionCompiler(bool requireCompletePureRunBatch = true)
    {
        _requireCompletePureRunBatch = requireCompletePureRunBatch;
    }

    public UnitDefinitionCompileResult Compile(IEnumerable<UnitDefinitionDraft> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        UnitDefinitionDraft[] materialized = drafts.ToArray();
        var diagnostics = new List<ContentDiagnostic>();
        var definitions = new Dictionary<ContentId, CompiledUnitDefinition>();
        var seenIds = new HashSet<ContentId>();

        foreach (UnitDefinitionDraft draft in materialized)
        {
            ContentId? contentId = ParseContentId(draft.ContentId, diagnostics);
            if (contentId is null)
                continue;

            if (!seenIds.Add(contentId.Value))
            {
                diagnostics.Add(Error("unit.duplicate_id", $"Duplicate Unit ContentId '{contentId}'.", contentId));
                continue;
            }

            int diagnosticStart = diagnostics.Count;
            ValidateDraft(draft, contentId.Value, diagnostics);
            if (diagnostics.Count != diagnosticStart)
                continue;

            try
            {
                UnitMovementKind movementKind = draft.MovementKind switch
                {
                    "land" => UnitMovementKind.Land,
                    "air" => UnitMovementKind.Air,
                    _ => UnitMovementKind.Swim
                };
                var attributes = new UnitAttributes(
                    draft.Strength,
                    draft.Agility,
                    draft.Constitution,
                    draft.Intelligence,
                    draft.Charisma,
                    draft.Luck);
                var derived = new UnitDerivedStats(
                    draft.MaxHealth,
                    draft.MaxMana,
                    draft.StartingMana,
                    draft.MoveRange,
                    draft.Initiative);
                var definition = new UnitDefinition(
                    contentId.Value,
                    draft.SourceId,
                    draft.DisplayName,
                    draft.FamilyId,
                    draft.RoleId,
                    attributes,
                    draft.Speed,
                    derived,
                    draft.AttackRange,
                    draft.AttackFactor,
                    draft.DefenceFactor,
                    movementKind,
                    draft.CanProduceCorpse);
                var visual = new UnitVisualDefinition(
                    draft.DownRightTexture,
                    draft.UpLeftTexture,
                    draft.UnarmedDownRightTexture,
                    draft.UnarmedUpLeftTexture,
                    draft.DeathTexture,
                    draft.ShadowTexture,
                    new UnitSpritePivot(draft.DownRightPivotX, draft.DownRightPivotY),
                    new UnitSpritePivot(draft.UpLeftPivotX, draft.UpLeftPivotY),
                    new UnitSpritePivot(draft.DeathPivotX, draft.DeathPivotY),
                    draft.BodyPixelsPerUnit,
                    draft.ShadowPixelsPerUnit,
                    draft.ShadowOffsetX,
                    draft.ShadowOffsetY,
                    draft.ShadowScale,
                    draft.ShadowOpacity,
                    new UnitBodyTint(
                        draft.BodyTintRed,
                        draft.BodyTintGreen,
                        draft.BodyTintBlue,
                        draft.BodyTintAlpha),
                    draft.BodyTintMode,
                    new UnitBodyTint(
                        draft.BaseBodyColorRed,
                        draft.BaseBodyColorGreen,
                        draft.BaseBodyColorBlue,
                        draft.BaseBodyColorAlpha));
                definitions.Add(
                    contentId.Value,
                    new CompiledUnitDefinition(
                        definition,
                        new ContentId(draft.ActorContentId),
                        visual,
                        draft.DeferredDependencies.OrderBy(item => item, StringComparer.Ordinal).ToArray()));
            }
            catch (Exception exception) when (exception is ArgumentException or ArithmeticException)
            {
                diagnostics.Add(Error("unit.invalid_definition", exception.Message, contentId));
            }
        }

        if (_requireCompletePureRunBatch)
            ValidateCompleteSet(seenIds, diagnostics);

        if (diagnostics.Any(item => item.Severity == ContentDiagnosticSeverity.Error))
            return new UnitDefinitionCompileResult(null, diagnostics);

        var ordered = definitions
            .OrderBy(item => item.Key.Value, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value);
        return new UnitDefinitionCompileResult(
            new ReadOnlyDictionary<ContentId, CompiledUnitDefinition>(ordered),
            diagnostics);
    }

    private static void ValidateDraft(
        UnitDefinitionDraft draft,
        ContentId contentId,
        ICollection<ContentDiagnostic> diagnostics)
    {
        if (draft.SchemaVersion != SchemaVersion)
            diagnostics.Add(Error("unit.unsupported_schema", $"Unit schema must be {SchemaVersion}.", contentId));
        if (!IsSnakeId(draft.SourceId) || string.IsNullOrWhiteSpace(draft.DisplayName) ||
            !IsToken(draft.FamilyId) || !IsToken(draft.RoleId))
        {
            diagnostics.Add(Error("unit.invalid_identity", "SourceId, DisplayName, FamilyId or RoleId is invalid.", contentId));
        }

        int[] attributes =
        {
            draft.Strength,
            draft.Agility,
            draft.Constitution,
            draft.Intelligence,
            draft.Charisma,
            draft.Luck
        };
        if (attributes.Any(value => value < 0))
            diagnostics.Add(Error("unit.invalid_attribute", "Unit attributes cannot be negative.", contentId));
        if (!float.IsFinite(draft.Speed) || draft.Speed < 0 ||
            !float.IsFinite(draft.Initiative) || draft.Initiative < 0 ||
            !float.IsFinite(draft.ShadowOffsetX) || !float.IsFinite(draft.ShadowOffsetY) ||
            !float.IsFinite(draft.ShadowScale) || !float.IsFinite(draft.ShadowOpacity))
        {
            diagnostics.Add(Error("unit.invalid_number", "Speed, initiative and offsets must be finite and non-negative where applicable.", contentId));
        }
        if (draft.AttackRange < 0 || draft.MoveRange < 0 || draft.MaxHealth <= 0 ||
            draft.MaxMana < 0 || draft.StartingMana < 0 || draft.StartingMana > draft.MaxMana)
        {
            diagnostics.Add(Error("unit.invalid_range", "Health, mana, move and attack ranges are invalid.", contentId));
        }
        if (!float.IsFinite(draft.AttackFactor) || draft.AttackFactor < 0 ||
            !float.IsFinite(draft.DefenceFactor) || draft.DefenceFactor < 0)
        {
            diagnostics.Add(Error("unit.invalid_factor", "Attack and defence factors must be finite and non-negative.", contentId));
        }
        if (draft.MovementKind is not ("land" or "air" or "swim"))
            diagnostics.Add(Error("unit.unknown_movement_kind", $"Unknown movement kind '{draft.MovementKind}'.", contentId));

        if (!TryContentId(draft.ActorContentId, out ContentId actorId))
            diagnostics.Add(Error("unit.missing_actor", "ActorContentId must be a canonical content ID.", contentId));
        else if (actorId.Value != ActorContentId)
            diagnostics.Add(Error("unit.invalid_actor", $"Pure Run Units must use '{ActorContentId}'.", contentId));

        if (!IsTexturePath(draft.DownRightTexture) || !IsTexturePath(draft.UpLeftTexture) ||
            !IsTexturePath(draft.ShadowTexture))
        {
            diagnostics.Add(Error("unit.missing_texture", "Directional and shadow texture references are required.", contentId));
        }
        if ((draft.UnarmedDownRightTexture is null) != (draft.UnarmedUpLeftTexture is null) ||
            draft.UnarmedDownRightTexture is not null &&
            (!IsTexturePath(draft.UnarmedDownRightTexture) || !IsTexturePath(draft.UnarmedUpLeftTexture)))
        {
            diagnostics.Add(Error("unit.invalid_unarmed_texture", "Unarmed directional textures must be configured as one valid pair.", contentId));
        }
        if (draft.CanProduceCorpse != IsTexturePath(draft.DeathTexture))
        {
            diagnostics.Add(Error(
                "unit.corpse_visual_mismatch",
                "Corpse-producing Units require one death texture and summons must not configure one.",
                contentId));
        }

        float[] pivots =
        {
            draft.DownRightPivotX,
            draft.DownRightPivotY,
            draft.UpLeftPivotX,
            draft.UpLeftPivotY,
            draft.DeathPivotX,
            draft.DeathPivotY
        };
        if (pivots.Any(value => !float.IsFinite(value) || value < 0f || value > 1f))
            diagnostics.Add(Error("unit.invalid_sprite_pivot", "Sprite pivots must be normalized finite values.", contentId));
        if (draft.BodyPixelsPerUnit <= 0 || draft.ShadowPixelsPerUnit <= 0 || draft.ShadowScale <= 0f)
        {
            diagnostics.Add(Error(
                "unit.invalid_visual_scale",
                "Sprite pixels-per-unit and shadow scale must be positive.",
                contentId));
        }
        if (draft.ShadowOpacity < 0f || draft.ShadowOpacity > 1f)
            diagnostics.Add(Error("unit.invalid_shadow_opacity", "Shadow opacity must be from zero to one.", contentId));

        float[] tint = { draft.BodyTintRed, draft.BodyTintGreen, draft.BodyTintBlue, draft.BodyTintAlpha };
        if (tint.Any(value => !float.IsFinite(value) || value < 0f || value > 1f))
            diagnostics.Add(Error("unit.invalid_tint", "Body tint components must be finite values from zero to one.", contentId));
        float[] baseBodyColor =
        {
            draft.BaseBodyColorRed,
            draft.BaseBodyColorGreen,
            draft.BaseBodyColorBlue,
            draft.BaseBodyColorAlpha
        };
        if (baseBodyColor.Any(value => !float.IsFinite(value) || value < 0f || value > 1f))
        {
            diagnostics.Add(Error(
                "unit.invalid_base_body_color",
                "Base body color components must be finite values from zero to one.",
                contentId));
        }
        if (!UnitBodyTintModes.IsSupported(draft.BodyTintMode))
        {
            diagnostics.Add(Error(
                "unit.unknown_tint_mode",
                $"Unknown Unit body tint mode '{draft.BodyTintMode}'.",
                contentId));
        }
        bool isGoat = draft.FamilyId == "goat";
        if (isGoat != (draft.BodyTintMode == UnitBodyTintModes.GoatBodyMaskV1))
        {
            diagnostics.Add(Error(
                "unit.tint_mode_family_mismatch",
                "Goat Units require goat-body-mask-v1 and all other Units require multiply.",
                contentId));
        }
        if (draft.DeferredDependencies.Any(string.IsNullOrWhiteSpace) ||
            draft.DeferredDependencies.Distinct(StringComparer.Ordinal).Count() != draft.DeferredDependencies.Count)
        {
            diagnostics.Add(Error("unit.invalid_deferred_dependency", "Deferred dependencies must be non-empty and unique.", contentId));
        }

        if (attributes.Any(value => value < 0) || !float.IsFinite(draft.Speed) || draft.Speed < 0)
            return;

        try
        {
            UnitDerivedStats expected = UnitDerivedStatRules.Calculate(
                new UnitAttributes(
                    draft.Strength,
                    draft.Agility,
                    draft.Constitution,
                    draft.Intelligence,
                    draft.Charisma,
                    draft.Luck),
                draft.Speed);
            if (draft.MaxHealth != expected.MaxHealth || draft.MaxMana != expected.MaxMana ||
                draft.StartingMana != expected.StartingMana || draft.MoveRange != expected.MoveRange ||
                draft.Initiative != expected.Initiative)
            {
                diagnostics.Add(Error(
                    "unit.derived_mismatch",
                    $"Explicit derived values do not match {UnitDerivedStatRules.ContractId}.",
                    contentId));
            }
        }
        catch (ArithmeticException exception)
        {
            diagnostics.Add(Error("unit.invalid_number", exception.Message, contentId));
        }
    }

    private static void ValidateCompleteSet(
        IReadOnlySet<ContentId> seenIds,
        ICollection<ContentDiagnostic> diagnostics)
    {
        var expected = ExpectedPureRunIds.Select(value => new ContentId(value)).ToHashSet();
        foreach (ContentId missing in expected.Except(seenIds).OrderBy(item => item.Value, StringComparer.Ordinal))
            diagnostics.Add(Error("unit.missing_definition", $"Missing Unit definition '{missing}'.", missing));
        foreach (ContentId unexpected in seenIds.Except(expected).OrderBy(item => item.Value, StringComparer.Ordinal))
            diagnostics.Add(Error("unit.unexpected_definition", $"Unexpected Unit definition '{unexpected}'.", unexpected));
    }

    private static ContentId? ParseContentId(string value, ICollection<ContentDiagnostic> diagnostics)
    {
        if (TryContentId(value, out ContentId contentId))
            return contentId;
        diagnostics.Add(new ContentDiagnostic(
            "unit.invalid_content_id",
            ContentDiagnosticSeverity.Error,
            $"Unit ContentId '{value}' is not canonical."));
        return null;
    }

    private static bool TryContentId(string value, out ContentId contentId)
    {
        try
        {
            contentId = new ContentId(value);
            return true;
        }
        catch (ArgumentException)
        {
            contentId = default;
            return false;
        }
    }

    private static bool IsTexturePath(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.StartsWith("res://assets/units/", StringComparison.Ordinal) &&
        value.EndsWith(".png", StringComparison.Ordinal);

    private static bool IsSnakeId(string value) => IsSegmented(value, '_');

    private static bool IsToken(string value) => IsSegmented(value, '-');

    private static bool IsSegmented(string value, char separator)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        bool previousWasSeparator = true;
        foreach (char character in value)
        {
            if (character == separator)
            {
                if (previousWasSeparator)
                    return false;
                previousWasSeparator = true;
                continue;
            }
            if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character))
                return false;
            previousWasSeparator = false;
        }
        return !previousWasSeparator;
    }

    private static ContentDiagnostic Error(string code, string message, ContentId? contentId) =>
        new(code, ContentDiagnosticSeverity.Error, message, contentId);
}
