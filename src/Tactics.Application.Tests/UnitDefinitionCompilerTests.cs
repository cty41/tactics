using System.Text.Json;
using NUnit.Framework;
using Tactics.Application.Units;
using Tactics.Core.Content;

namespace Tactics.Application.Tests;

public sealed class UnitDefinitionCompilerTests
{
    [Test]
    public void Compile_ConsumesTheExactTwelveDefinitionGolden()
    {
        UnitDefinitionDraft[] drafts = LoadGoldenDrafts();

        UnitDefinitionCompileResult result = new UnitDefinitionCompiler().Compile(drafts);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True, string.Join(Environment.NewLine, result.Diagnostics));
            Assert.That(result.Definitions, Has.Count.EqualTo(12));
            Assert.That(result.Definitions!.ContainsKey(new ContentId("unit.pure-run.amazon")), Is.True);
            Assert.That(result.Definitions[new ContentId("unit.pure-run.necromancer")].Definition.DerivedStats.MaxMana,
                Is.EqualTo(18));
            Assert.That(result.Definitions[new ContentId("unit.pure-run.goat-ranged")].Visual.BodyTint.Blue,
                Is.EqualTo(0.439f));
            Assert.That(result.Definitions[new ContentId("unit.pure-run.goat-ranged")].Visual.BodyTintMode,
                Is.EqualTo(UnitBodyTintModes.GoatBodyMaskV1));
            Assert.That(result.Definitions[new ContentId("unit.pure-run.fire-demon")].Visual.BodyTintMode,
                Is.EqualTo(UnitBodyTintModes.Multiply));
            Assert.That(result.Definitions[new ContentId("unit.pure-run.mage")].Visual.DownRightPivot,
                Is.EqualTo(new UnitSpritePivot(0.5f, 0.078125f)));
            Assert.That(result.Definitions[new ContentId("unit.pure-run.mage")].Visual.DeathPivot,
                Is.EqualTo(new UnitSpritePivot(0.5f, 0.5f)));
            Assert.That(result.Definitions[new ContentId("unit.pure-run.mage")].Visual.ShadowScale,
                Is.EqualTo(0.8f));
        });
    }

    [Test]
    public void Compile_RejectsDuplicateMissingAndUnexpectedDefinitions()
    {
        UnitDefinitionDraft[] drafts = LoadGoldenDrafts();
        UnitDefinitionCompileResult duplicate = new UnitDefinitionCompiler().Compile(drafts.Append(drafts[0]));
        UnitDefinitionCompileResult incomplete = new UnitDefinitionCompiler().Compile(
            drafts.Skip(1).Append(drafts[0] with { ContentId = "unit.pure-run.unexpected" }));

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(duplicate.Diagnostics.Select(item => item.Code), Does.Contain("unit.duplicate_id"));
            Assert.That(incomplete.Succeeded, Is.False);
            Assert.That(incomplete.Diagnostics.Select(item => item.Code), Does.Contain("unit.missing_definition"));
            Assert.That(incomplete.Diagnostics.Select(item => item.Code), Does.Contain("unit.unexpected_definition"));
        });
    }

    [Test]
    public void Compile_RejectsInvalidIdentityNumbersMovementAndReferences()
    {
        UnitDefinitionDraft valid = LoadGoldenDrafts()[0];
        var compiler = new UnitDefinitionCompiler(requireCompletePureRunBatch: false);
        UnitDefinitionDraft[] invalidDrafts =
        {
            valid with { ContentId = "Unit.Bad" },
            valid with { Speed = float.NaN },
            valid with { AttackFactor = -1 },
            valid with { MovementKind = "swim" },
            valid with { ActorContentId = string.Empty },
            valid with { DownRightTexture = string.Empty }
        };

        string[] codes = invalidDrafts
            .SelectMany(draft => compiler.Compile(new[] { draft }).Diagnostics)
            .Select(item => item.Code)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(codes, Does.Contain("unit.invalid_content_id"));
            Assert.That(codes, Does.Contain("unit.invalid_number"));
            Assert.That(codes, Does.Contain("unit.invalid_factor"));
            Assert.That(codes, Does.Contain("unit.unknown_movement_kind"));
            Assert.That(codes, Does.Contain("unit.missing_actor"));
            Assert.That(codes, Does.Contain("unit.missing_texture"));
        });
    }

    [Test]
    public void Compile_RejectsDerivedAndCorpseVisualDrift()
    {
        UnitDefinitionDraft valid = LoadGoldenDrafts()[0];
        var compiler = new UnitDefinitionCompiler(requireCompletePureRunBatch: false);
        UnitDefinitionCompileResult derived = compiler.Compile(new[] { valid with { MoveRange = 4 } });
        UnitDefinitionCompileResult corpse = compiler.Compile(new[] { valid with { DeathTexture = null } });

        Assert.Multiple(() =>
        {
            Assert.That(derived.Diagnostics.Select(item => item.Code), Does.Contain("unit.derived_mismatch"));
            Assert.That(corpse.Diagnostics.Select(item => item.Code), Does.Contain("unit.corpse_visual_mismatch"));
        });
    }

    [Test]
    public void Compile_RejectsUnknownTintModeInvalidBaseColorAndFamilyMismatch()
    {
        UnitDefinitionDraft goat = LoadGoldenDrafts()
            .Single(item => item.ContentId == "unit.pure-run.goat-charger");
        UnitDefinitionDraft player = LoadGoldenDrafts()
            .Single(item => item.ContentId == "unit.pure-run.mage");
        var compiler = new UnitDefinitionCompiler(requireCompletePureRunBatch: false);

        string[] codes = new[]
            {
                goat with { BodyTintMode = "unknown" },
                goat with { BaseBodyColorRed = float.NaN },
                goat with { BodyTintMode = UnitBodyTintModes.Multiply },
                player with { BodyTintMode = UnitBodyTintModes.GoatBodyMaskV1 }
            }
            .SelectMany(draft => compiler.Compile(new[] { draft }).Diagnostics)
            .Select(item => item.Code)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(codes, Does.Contain("unit.unknown_tint_mode"));
            Assert.That(codes, Does.Contain("unit.invalid_base_body_color"));
            Assert.That(codes.Count(code => code == "unit.tint_mode_family_mismatch"), Is.EqualTo(3));
        });
    }

    [Test]
    public void Compile_RejectsInvalidSpriteGeometry()
    {
        UnitDefinitionDraft valid = LoadGoldenDrafts()[0];
        var compiler = new UnitDefinitionCompiler(requireCompletePureRunBatch: false);

        string[] codes = new[]
            {
                valid with { DownRightPivotY = 1.1f },
                valid with { BodyPixelsPerUnit = 0 },
                valid with { ShadowScale = 0f },
                valid with { ShadowOpacity = 1.1f }
            }
            .SelectMany(draft => compiler.Compile(new[] { draft }).Diagnostics)
            .Select(item => item.Code)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(codes, Does.Contain("unit.invalid_sprite_pivot"));
            Assert.That(codes.Count(code => code == "unit.invalid_visual_scale"), Is.EqualTo(2));
            Assert.That(codes, Does.Contain("unit.invalid_shadow_opacity"));
        });
    }

    private static UnitDefinitionDraft[] LoadGoldenDrafts()
    {
        using JsonDocument golden = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Golden", "unit-batch-v1.json")));
        string actorContentId = golden.RootElement.GetProperty("source").GetProperty("actorContentId").GetString()!;
        JsonElement spriteContract = golden.RootElement.GetProperty("spriteContract");
        return golden.RootElement.GetProperty("units").EnumerateArray()
            .Select(unit => CreateDraft(unit, actorContentId, spriteContract))
            .ToArray();
    }

    private static UnitDefinitionDraft CreateDraft(
        JsonElement unit,
        string actorContentId,
        JsonElement spriteContract)
    {
        JsonElement attributes = unit.GetProperty("attributes");
        JsonElement derived = unit.GetProperty("derived");
        JsonElement combat = unit.GetProperty("combat");
        JsonElement visual = unit.GetProperty("visual");
        float[] tint = visual.GetProperty("bodyTint").EnumerateArray().Select(item => item.GetSingle()).ToArray();
        float[] baseBodyColor = visual.GetProperty("baseBodyColor").EnumerateArray()
            .Select(item => item.GetSingle())
            .ToArray();
        float[] shadowOffset = visual.GetProperty("shadowOffset").EnumerateArray().Select(item => item.GetSingle()).ToArray();
        JsonElement livingSprite = spriteContract.GetProperty("living");
        JsonElement deathSprite = spriteContract.GetProperty("death");
        JsonElement shadowSprite = spriteContract.GetProperty("shadow");
        float[] livingPivot = livingSprite.GetProperty("pivot").EnumerateArray().Select(item => item.GetSingle()).ToArray();
        float[] deathPivot = deathSprite.GetProperty("pivot").EnumerateArray().Select(item => item.GetSingle()).ToArray();
        float[] shadowScale = shadowSprite.GetProperty("localScale").EnumerateArray().Select(item => item.GetSingle()).ToArray();
        float[] shadowColor = shadowSprite.GetProperty("color").EnumerateArray().Select(item => item.GetSingle()).ToArray();
        return new UnitDefinitionDraft
        {
            SchemaVersion = 1,
            ContentId = unit.GetProperty("contentId").GetString()!,
            SourceId = unit.GetProperty("sourceId").GetString()!,
            DisplayName = unit.GetProperty("displayName").GetString()!,
            FamilyId = unit.GetProperty("familyId").GetString()!,
            RoleId = unit.GetProperty("roleId").GetString()!,
            Strength = attributes.GetProperty("strength").GetInt32(),
            Agility = attributes.GetProperty("agility").GetInt32(),
            Constitution = attributes.GetProperty("constitution").GetInt32(),
            Intelligence = attributes.GetProperty("intelligence").GetInt32(),
            Charisma = attributes.GetProperty("charisma").GetInt32(),
            Luck = attributes.GetProperty("luck").GetInt32(),
            Speed = unit.GetProperty("speed").GetSingle(),
            MaxHealth = derived.GetProperty("maxHealth").GetInt32(),
            MaxMana = derived.GetProperty("maxMana").GetInt32(),
            StartingMana = derived.GetProperty("startingMana").GetInt32(),
            MoveRange = derived.GetProperty("moveRange").GetInt32(),
            Initiative = derived.GetProperty("initiative").GetSingle(),
            AttackRange = combat.GetProperty("attackRange").GetInt32(),
            AttackFactor = combat.GetProperty("attackFactor").GetSingle(),
            DefenceFactor = combat.GetProperty("defenceFactor").GetSingle(),
            MovementKind = unit.GetProperty("movementKind").GetString()!,
            CanProduceCorpse = unit.GetProperty("canProduceCorpse").GetBoolean(),
            ActorContentId = actorContentId,
            DownRightTexture = visual.GetProperty("downRightTexture").GetString()!,
            UpLeftTexture = visual.GetProperty("upLeftTexture").GetString()!,
            DeathTexture = visual.GetProperty("deathTexture").ValueKind == JsonValueKind.Null
                ? null
                : visual.GetProperty("deathTexture").GetString(),
            ShadowTexture = visual.GetProperty("shadowTexture").GetString()!,
            DownRightPivotX = livingPivot[0],
            DownRightPivotY = livingPivot[1],
            UpLeftPivotX = livingPivot[0],
            UpLeftPivotY = livingPivot[1],
            DeathPivotX = deathPivot[0],
            DeathPivotY = deathPivot[1],
            BodyPixelsPerUnit = livingSprite.GetProperty("pixelsPerUnit").GetInt32(),
            ShadowPixelsPerUnit = shadowSprite.GetProperty("pixelsPerUnit").GetInt32(),
            ShadowOffsetX = shadowOffset[0],
            ShadowOffsetY = shadowOffset[1],
            ShadowScale = shadowScale[0],
            ShadowOpacity = shadowColor[3],
            BodyTintRed = tint[0],
            BodyTintGreen = tint[1],
            BodyTintBlue = tint[2],
            BodyTintAlpha = tint[3],
            BodyTintMode = visual.GetProperty("tintMode").GetString()!,
            BaseBodyColorRed = baseBodyColor[0],
            BaseBodyColorGreen = baseBodyColor[1],
            BaseBodyColorBlue = baseBodyColor[2],
            BaseBodyColorAlpha = baseBodyColor[3],
            DeferredDependencies = unit.GetProperty("deferredDependencies").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray()
        };
    }
}
