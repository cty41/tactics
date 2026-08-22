#if TOOLS
using System.Text.Json;
using Tactics.Application.Units;
using Tactics.Core.Content;

namespace Tactics.Godot.Adapter.Editor;

internal sealed class UnitMigrationDraft
{
    public int SchemaVersion { get; init; }
    public string BatchId { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public UnitDraftSource Source { get; init; } = new();
    public UnitTintContract TintContract { get; init; } = new();
    public UnitSpriteContract SpriteContract { get; init; } = new();
    public string ActorContentId { get; init; } = string.Empty;
    public UnitDraftRecord[] Units { get; init; } = Array.Empty<UnitDraftRecord>();

    public IReadOnlyDictionary<ContentId, CompiledUnitDefinition> CompileApplicationDefinitions()
    {
        UnitDefinitionCompileResult result = new UnitDefinitionCompiler().Compile(
            Units.Select(unit => unit.ToApplicationDraft(ActorContentId, SpriteContract)));
        if (!result.Succeeded || result.Definitions is null)
        {
            throw new InvalidOperationException(
                "Unit typed draft failed Application compilation: " +
                string.Join("; ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        }
        return result.Definitions;
    }

    public static UnitMigrationDraft Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Pure Run Unit typed migration draft is missing.", path);
        UnitMigrationDraft? draft = JsonSerializer.Deserialize<UnitMigrationDraft>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (draft is null || draft.SchemaVersion != 1 || draft.BatchId != "pure-run-units-v1" ||
            draft.Classification != "disposable_typed_unit_migration_draft" ||
            draft.ActorContentId != UnitDefinitionCompiler.ActorContentId || draft.Units.Length != 12 ||
            draft.TintContract.Id != "unity-goat-body-tint-v1" ||
            draft.TintContract.GodotShaderPath != UnitTintContract.CanonicalGodotShaderPath ||
            draft.SpriteContract.Id != UnitSpriteContract.ContractId)
        {
            throw new InvalidOperationException("Pure Run Unit typed migration draft identity is invalid.");
        }
        draft.SpriteContract.Validate();
        return draft;
    }
}

internal sealed class UnitSpriteContract
{
    public const string ContractId = "unity-unit-sprite-geometry-v1";

    public string Id { get; init; } = string.Empty;
    public UnitSpriteImportContract Living { get; init; } = new();
    public UnitSpriteImportContract Death { get; init; } = new();
    public UnitShadowImportContract Shadow { get; init; } = new();

    public void Validate()
    {
        Living.Validate("living");
        Death.Validate("death");
        Shadow.Validate();
    }
}

internal class UnitSpriteImportContract
{
    public int Alignment { get; init; }
    public float[] Pivot { get; init; } = Array.Empty<float>();
    public int PixelsPerUnit { get; init; }

    public void Validate(string label)
    {
        if (Pivot.Length != 2 || Pivot.Any(value => !float.IsFinite(value) || value < 0f || value > 1f) ||
            PixelsPerUnit <= 0)
        {
            throw new InvalidOperationException($"Unit {label} Sprite import contract is invalid.");
        }
    }
}

internal sealed class UnitShadowImportContract : UnitSpriteImportContract
{
    public float[] LocalPosition { get; init; } = Array.Empty<float>();
    public float[] LocalScale { get; init; } = Array.Empty<float>();
    public float[] Color { get; init; } = Array.Empty<float>();

    public void Validate()
    {
        base.Validate("shadow");
        if (LocalPosition.Length != 3 || LocalScale.Length != 3 || Color.Length != 4 ||
            LocalPosition.Concat(LocalScale).Concat(Color).Any(value => !float.IsFinite(value)) ||
            LocalScale.Any(value => value <= 0f) || Color.Any(value => value < 0f || value > 1f))
        {
            throw new InvalidOperationException("Unit Shadow Transform/color contract is invalid.");
        }
    }
}

internal sealed class UnitTintContract
{
    public const string CanonicalGodotShaderPath =
        "res://src/Tactics.Godot.Adapter/Runtime/Shaders/GoatBodyTint.gdshader";

    public string Id { get; init; } = string.Empty;
    public string UnityShaderPath { get; init; } = string.Empty;
    public string UnityShaderGitBlobSha1 { get; init; } = string.Empty;
    public string GodotShaderPath { get; init; } = string.Empty;
    public string MaterialShaderName { get; init; } = string.Empty;
    public float[] MaterialColor { get; init; } = Array.Empty<float>();
    public float MaterialThresholdAudit { get; init; }
    public float[] MaskSmoothstep { get; init; } = Array.Empty<float>();
    public float[] LuminanceWeights { get; init; } = Array.Empty<float>();
    public float MinimumBaseLuminance { get; init; }
}

internal sealed class UnitDraftSource
{
    public string SourceTag { get; init; } = string.Empty;
    public string SourceCommit { get; init; } = string.Empty;
    public string UnityVersion { get; init; } = string.Empty;
    public string ExporterVersion { get; init; } = string.Empty;
    public string ExportHash { get; init; } = string.Empty;
    public string DerivedContract { get; init; } = string.Empty;
}

internal sealed class UnitDraftRecord
{
    public string ContentId { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string FamilyId { get; init; } = string.Empty;
    public string RoleId { get; init; } = string.Empty;
    public UnitDraftAttributes Attributes { get; init; } = new();
    public float Speed { get; init; }
    public UnitDraftDerived Derived { get; init; } = new();
    public UnitDraftCombat Combat { get; init; } = new();
    public string MovementKind { get; init; } = string.Empty;
    public bool CanProduceCorpse { get; init; }
    public UnitDraftVisual Visual { get; init; } = new();
    public string[] DeferredDependencies { get; init; } = Array.Empty<string>();

    public UnitDefinitionDraft ToApplicationDraft(
        string actorContentId,
        UnitSpriteContract spriteContract)
    {
        if (Visual.ShadowOffset.Length != 2 || Visual.BodyTint.Length != 4 ||
            Visual.BaseBodyColor.Length != 4)
            throw new InvalidOperationException($"Unit '{ContentId}' has malformed visual vectors.");
        (int Strength, int Agility, int Constitution, int Intelligence, int Charisma, int Luck, int MoveTrait) target =
            TargetAttributes();
        var attributes = new Tactics.Core.Units.UnitAttributes(target.Strength, target.Agility,
            target.Constitution, target.Intelligence, target.Charisma, target.Luck);
        Tactics.Core.Units.UnitDerivedStats derived = Tactics.Core.Units.UnitDerivedStatRules.Calculate(attributes,
            target.MoveTrait);
        return new UnitDefinitionDraft
        {
            SchemaVersion = 1,
            ContentId = ContentId,
            SourceId = SourceId,
            DisplayName = DisplayName,
            FamilyId = FamilyId,
            RoleId = RoleId,
            Strength = target.Strength,
            Agility = target.Agility,
            Constitution = target.Constitution,
            Intelligence = target.Intelligence,
            Charisma = target.Charisma,
            Luck = target.Luck,
            Speed = Speed,
            MovementTraitModifier = target.MoveTrait,
            MaxHealth = derived.MaxHealth,
            MaxMana = derived.MaxMana,
            StartingMana = derived.StartingMana,
            MoveRange = derived.MoveRange,
            Initiative = derived.Initiative,
            AttackRange = Combat.AttackRange,
            AttackFactor = Combat.AttackFactor,
            DefenceFactor = Combat.DefenceFactor,
            MovementKind = MovementKind,
            CanProduceCorpse = CanProduceCorpse,
            ActorContentId = actorContentId,
            DownRightTexture = Visual.DownRightTexture,
            UpLeftTexture = Visual.UpLeftTexture,
            UnarmedDownRightTexture = Visual.UnarmedDownRightTexture,
            UnarmedUpLeftTexture = Visual.UnarmedUpLeftTexture,
            DeathTexture = Visual.DeathTexture,
            ShadowTexture = Visual.ShadowTexture,
            DownRightPivotX = spriteContract.Living.Pivot[0],
            DownRightPivotY = spriteContract.Living.Pivot[1],
            UpLeftPivotX = spriteContract.Living.Pivot[0],
            UpLeftPivotY = spriteContract.Living.Pivot[1],
            DeathPivotX = spriteContract.Death.Pivot[0],
            DeathPivotY = spriteContract.Death.Pivot[1],
            BodyPixelsPerUnit = spriteContract.Living.PixelsPerUnit,
            ShadowPixelsPerUnit = spriteContract.Shadow.PixelsPerUnit,
            ShadowOffsetX = Visual.ShadowOffset[0],
            ShadowOffsetY = Visual.ShadowOffset[1],
            ShadowScale = spriteContract.Shadow.LocalScale[0],
            ShadowOpacity = spriteContract.Shadow.Color[3],
            BodyTintRed = Visual.BodyTint[0],
            BodyTintGreen = Visual.BodyTint[1],
            BodyTintBlue = Visual.BodyTint[2],
            BodyTintAlpha = Visual.BodyTint[3],
            BodyTintMode = Visual.TintMode,
            BaseBodyColorRed = Visual.BaseBodyColor[0],
            BaseBodyColorGreen = Visual.BaseBodyColor[1],
            BaseBodyColorBlue = Visual.BaseBodyColor[2],
            BaseBodyColorAlpha = Visual.BaseBodyColor[3],
            DeferredDependencies = DeferredDependencies
        };
    }

    private (int Strength, int Agility, int Constitution, int Intelligence, int Charisma, int Luck, int MoveTrait)
        TargetAttributes() => ContentId switch
        {
            "unit.pure-run.mage" => (4, 5, 3, 6, 6, 6, 0),
            "unit.pure-run.necromancer" => (5, 3, 6, 6, 6, 4, 0),
            "unit.pure-run.amazon" => (5, 5, 5, 5, 5, 5, 0),
            "unit.pure-run.skeleton-warrior" or "unit.pure-run.skeleton-mage" => (4, 4, 2, 2, 2, 2, -1),
            "unit.pure-run.fire-demon" => (5, 4, 3, 5, 5, 5, -1),
            "unit.pure-run.goat-charger" or "unit.pure-run.goat-elite-charger" =>
                (Attributes.Strength, 8, Attributes.Constitution, Attributes.Intelligence, Attributes.Charisma, Attributes.Luck, 0),
            "unit.pure-run.goat-ranged" =>
                (Attributes.Strength, 12, Attributes.Constitution, Attributes.Intelligence, Attributes.Charisma, Attributes.Luck, 0),
            "unit.pure-run.goat-support" =>
                (Attributes.Strength, 8, Attributes.Constitution, Attributes.Intelligence, Attributes.Charisma, Attributes.Luck, 0),
            "unit.pure-run.goat-aoe" or "unit.pure-run.goat-elite-poison-caster" =>
                (Attributes.Strength, 6, Attributes.Constitution, Attributes.Intelligence, Attributes.Charisma, Attributes.Luck, -1),
            _ => (Attributes.Strength, Attributes.Agility, Attributes.Constitution, Attributes.Intelligence,
                Attributes.Charisma, Attributes.Luck, 0)
        };
}

internal sealed class UnitDraftAttributes
{
    public int Strength { get; init; }
    public int Agility { get; init; }
    public int Constitution { get; init; }
    public int Intelligence { get; init; }
    public int Charisma { get; init; }
    public int Luck { get; init; }
}

internal sealed class UnitDraftDerived
{
    public int MaxHealth { get; init; }
    public int MaxMana { get; init; }
    public int StartingMana { get; init; }
    public int MoveRange { get; init; }
    public float Initiative { get; init; }
}

internal sealed class UnitDraftCombat
{
    public int AttackRange { get; init; }
    public float AttackFactor { get; init; }
    public float DefenceFactor { get; init; }
}

internal sealed class UnitDraftVisual
{
    public string DownRightTexture { get; init; } = string.Empty;
    public string UpLeftTexture { get; init; } = string.Empty;
    public string? UnarmedDownRightTexture { get; init; }
    public string? UnarmedUpLeftTexture { get; init; }
    public string? DeathTexture { get; init; }
    public string ShadowTexture { get; init; } = string.Empty;
    public float[] ShadowOffset { get; init; } = Array.Empty<float>();
    public float[] BodyTint { get; init; } = Array.Empty<float>();
    public string TintMode { get; init; } = string.Empty;
    public float[] BaseBodyColor { get; init; } = Array.Empty<float>();
}
#endif
