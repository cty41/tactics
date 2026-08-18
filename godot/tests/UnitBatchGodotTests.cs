using System.Text.Json;
using GdUnit4;
using Godot;
using Tactics.Application.Battle;
using Tactics.Application.Units;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Statuses;
using Tactics.Core.Skills;
using Tactics.Core.Units;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class UnitBatchGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void DemonboundUsesAuthoredMoveFourContractWithoutChangingFrozenUnits()
    {
        var resource = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/demonbound/PureRunDemonbound.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(resource).IsNotNull();
        if (resource is null) return;

        UnitDefinition definition = resource.ToCoreDefinition();
        AssertThat(resource.DerivedStatModeValue).IsEqual("explicit");
        AssertThat(definition.DerivedStatMode).IsEqual(UnitDerivedStatMode.Explicit);
        AssertThat(definition.Speed).IsEqual(4f);
        AssertThat(definition.DerivedStats.MoveRange).IsEqual(4);
        AssertThat(definition.DerivedStats.Initiative).IsEqual(8f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DemonboundPlayableBalanceUsesFourPointBareMeleeDamage()
    {
        var resource = ResourceLoader.Load<PlayableLv1BalanceProfileResource>(
            "res://content/ui/PlayableLv1BalanceProfile.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(resource).IsNotNull();
        if (resource is null) return;

        (int physical, int magical) = resource.ToCoreProfile().Attacks(
            new ContentId("unit.pure-run.demonbound"));
        AssertThat(physical).IsEqual(4);
        AssertThat(magical).IsEqual(2);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DemonboundSkillResourcesMatchTheFormalManaDamageAndCorruptionTables()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/ContentCatalog.tres", string.Empty, ResourceLoader.CacheMode.Ignore)!;
        var expected = new Dictionary<string, (int Mana, int Damage, int Corruption)>
        {
            ["skill.demonbound.meditation"] = (0, 0, 0),
            ["skill.demonbound.bane.lv1"] = (3, 0, 2), ["skill.demonbound.bane.lv2"] = (3, 0, 2),
            ["skill.demonbound.bane.lv3"] = (3, 0, 2), ["skill.demonbound.cleave.lv1"] = (4, 6, 2),
            ["skill.demonbound.cleave.lv2"] = (4, 6, 2),
            ["skill.demonbound.infernal-blast.lv1"] = (5, 4, 3),
            ["skill.demonbound.infernal-blast.lv2"] = (3, 4, 3),
            ["skill.demonbound.infernal-blast.lv3"] = (3, 4, 4),
            ["skill.demonbound.hellfire.lv1"] = (5, 5, 4),
            ["skill.demonbound.hellfire.lv2"] = (5, 5, 4),
            ["skill.demonbound.mindfulness.lv1"] = (0, 0, 0),
            ["skill.demonbound.mindfulness.lv2"] = (0, 0, 0),
            ["skill.demonbound.mindfulness.lv3"] = (0, 0, 0),
            ["skill.demonbound.regeneration.lv1"] = (5, 0, 5),
            ["skill.demonbound.regeneration.lv2"] = (5, 0, 6)
        };

        SkillDefinition[] skills = catalog.Entries.Where(entry => expected.ContainsKey(entry.ContentIdValue))
            .Select(entry => ResourceLoader.Load<SkillDefinitionResource>(entry.DiagnosticPathValue,
                string.Empty, ResourceLoader.CacheMode.Ignore)!.ToCoreDefinition()).ToArray();

        AssertThat(skills.Length).IsEqual(16);
        foreach (SkillDefinition skill in skills)
        {
            (int mana, int damage, int corruption) = expected[skill.ContentId.Value];
            AssertThat(skill.ManaCost).IsEqual(mana);
            AssertThat(skill.Damage).IsEqual(damage);
            AssertThat(skill.ExecutionProfile.CorruptionCost).IsEqual(corruption);
            AssertThat(skill.Role).IsEqual(SkillRole.Demonbound);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BaneWeaponStatusShowsPurpleBladePlaceholderAndClearsWithStatus()
    {
        var resource = ResourceLoader.Load<UnitDefinitionResource>(
            "res://content/demonbound/PureRunDemonbound.tres", string.Empty, ResourceLoader.CacheMode.Ignore);
        AssertThat(resource).IsNotNull();
        if (resource is null) return;
        GodotUnitActor actor = GodotUnitFactory.InstantiateActor(resource);

        actor.SetStatuses([new BattleUiStatusSnapshot(new ContentId("buff.demonbound.bane-weapon"),
            StatusEffectKind.BaneWeapon, StatusPolarity.Beneficial, 2, 1)]);
        bool visible = actor.IsBaneBladeGlowVisible;
        Color glow = actor.BaneBladeGlow!.DefaultColor;
        actor.SetStatuses(Array.Empty<BattleUiStatusSnapshot>());

        AssertThat(visible).IsTrue();
        AssertThat(glow.B).IsGreater(glow.R);
        AssertThat(actor.IsBaneBladeGlowVisible).IsFalse();
        actor.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CatalogResourcesAndFactoryMatchTheSharedGolden()
    {
        using JsonDocument golden = LoadGolden();
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/units/ContentCatalog.tres");
        AssertThat(catalog).IsNotNull();
        if (catalog is null)
            return;

        UnitBatchValidation validation = UnitBatchValidator.Validate(catalog);
        IReadOnlyDictionary<string, JsonElement> vectors = golden.RootElement.GetProperty("units")
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("contentId").GetString()!, StringComparer.Ordinal);
        foreach ((string contentId, JsonElement vector) in vectors)
        {
            bool loaded = catalog.TryGet(contentId, out Resource? resource);
            AssertThat(loaded).IsTrue();
            AssertThat(resource).IsInstanceOf<UnitDefinitionResource>();
            if (resource is not UnitDefinitionResource definition)
                continue;
            JsonElement derived = vector.GetProperty("derived");
            var state = GodotUnitFactory.CreateBattleState(
                definition,
                new UnitInstanceId($"test.{definition.SourceId}.0"),
                new GridPoint(2, 3),
                1,
                7);
            AssertThat(definition.ContentIdValue).IsEqual(contentId);
            AssertThat(definition.MaxHealth).IsEqual(derived.GetProperty("maxHealth").GetInt32());
            AssertThat(definition.MaxMana).IsEqual(derived.GetProperty("maxMana").GetInt32());
            AssertThat(definition.StartingMana).IsEqual(derived.GetProperty("startingMana").GetInt32());
            AssertThat(definition.MoveRange).IsEqual(derived.GetProperty("moveRange").GetInt32());
            AssertThat(definition.Initiative).IsEqual(derived.GetProperty("initiative").GetSingle());
            AssertThat(state.Unit.DefinitionId.Value).IsEqual(contentId);
            AssertThat(state.Unit.InstanceId.Value).IsNotEqual(contentId);
            AssertThat(state.CurrentHealth).IsEqual(definition.MaxHealth);
            AssertThat(state.CurrentMana).IsEqual(definition.StartingMana);
        }

        AssertThat(validation.CatalogEntryCount).IsEqual(13);
        AssertThat(validation.UnitCount).IsEqual(12);
        AssertThat(validation.SpawnedStates.Count).IsEqual(12);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SharedActorUsesDirectionalTexturesBodyOnlyMirroringAndSafeDeathFallback()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/units/ContentCatalog.tres");
        AssertThat(catalog).IsNotNull();
        if (catalog is null || !catalog.TryGet("unit.pure-run.mage", out Resource? mageResource) ||
            mageResource is not UnitDefinitionResource mage)
        {
            return;
        }
        catalog.TryGet("unit.pure-run.skeleton-warrior", out Resource? skeletonResource);
        var skeleton = skeletonResource as UnitDefinitionResource;
        AssertThat(skeleton).IsNotNull();
        if (skeleton is null)
            return;

        GodotUnitActor mageActor = GodotUnitFactory.InstantiateActor(mage);
        Texture2D shadow = mageActor.Shadow!.Texture!;
        mageActor.SetFacing(GodotUnitFacing.South);
        bool southUsesDr = mageActor.Body!.Texture == mage.DownRightTexture && !mageActor.Body.FlipH &&
            mageActor.Body.Offset.IsEqualApprox(mage.DownRightBodyOffset);
        mageActor.SetFacing(GodotUnitFacing.North);
        bool northUsesUl = mageActor.Body.Texture == mage.UpLeftTexture && !mageActor.Body.FlipH &&
            mageActor.Body.Offset.IsEqualApprox(mage.UpLeftBodyOffset);
        mageActor.SetFacing(GodotUnitFacing.East);
        bool eastUsesMirroredUl = mageActor.Body.Texture == mage.UpLeftTexture && mageActor.Body.FlipH;
        mageActor.SetFacing(GodotUnitFacing.West);
        bool westUsesMirroredDrBodyOnly = mageActor.Body.Texture == mage.DownRightTexture &&
            mageActor.Body.FlipH && !mageActor.Shadow.FlipH &&
            mageActor.Shadow.Texture == shadow;
        mageActor.SetDeathVisual(true);
        bool deathUsesUnmirroredDeathTexture = mageActor.Body.Texture == mage.DeathTexture &&
            !mageActor.Body.FlipH && mageActor.Body.Offset.IsEqualApprox(mage.DeathBodyOffset);
        mageActor.SetStatuses([new BattleUiStatusSnapshot(new ContentId("buff.poison"),
            StatusEffectKind.Poison, StatusPolarity.Harmful, 2, 1)]);
        mageActor.SetDeathVisual(true);
        bool deathClearsStatusPresentation = mageActor.StatusOverlay!.StatusCount == 0;
        bool spriteGeometryMatchesFrozenUnityContract =
            mage.DownRightBodyOffset.IsEqualApprox(new Vector2(0f, -108f)) &&
            mage.UpLeftBodyOffset.IsEqualApprox(new Vector2(0f, -108f)) &&
            mage.DeathBodyOffset.IsEqualApprox(Vector2.Zero) &&
            mageActor.Shadow.Position.IsEqualApprox(new Vector2(0f, 3.84f)) &&
            mageActor.Shadow.Scale.IsEqualApprox(new Vector2(1.6f, 1.6f)) &&
            Mathf.IsEqualApprox(mageActor.Shadow.Modulate.A, 0.9f);

        GodotUnitActor skeletonActor = GodotUnitFactory.InstantiateActor(skeleton);
        skeletonActor.SetDeathVisual(true);
        bool summonUsesSafeLivingFallback = !skeletonActor.IsShowingDeath &&
            skeletonActor.Body!.Texture == skeleton.DownRightTexture;
        mageActor.Free();
        skeletonActor.Free();

        AssertThat(southUsesDr).IsTrue();
        AssertThat(northUsesUl).IsTrue();
        AssertThat(eastUsesMirroredUl).IsTrue();
        AssertThat(westUsesMirroredDrBodyOnly).IsTrue();
        AssertThat(deathUsesUnmirroredDeathTexture).IsTrue();
        AssertThat(deathClearsStatusPresentation).IsTrue();
        AssertThat(spriteGeometryMatchesFrozenUnityContract).IsTrue();
        AssertThat(summonUsesSafeLivingFallback).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GoatBodyMaskMaterialAndCpuReferencePreserveTheTintBoundary()
    {
        var catalog = ResourceLoader.Load<GodotResourceCatalog>(
            "res://content/units/ContentCatalog.tres");
        AssertThat(catalog).IsNotNull();
        if (catalog is null ||
            !catalog.TryGet("unit.pure-run.goat-ranged", out Resource? goatResource) ||
            goatResource is not UnitDefinitionResource goat ||
            !catalog.TryGet("unit.pure-run.fire-demon", out Resource? fireResource) ||
            fireResource is not UnitDefinitionResource fireDemon)
        {
            return;
        }

        GodotUnitActor goatActor = GodotUnitFactory.InstantiateActor(goat);
        GodotUnitActor fireActor = GodotUnitFactory.InstantiateActor(fireDemon);
        bool goatUsesMaterial = goat.BodyTintModeValue == UnitBodyTintModes.GoatBodyMaskV1 &&
            goat.BodyTintMaterial is not null && goatActor.Body!.Material == goat.BodyTintMaterial &&
            goatActor.Body.Modulate.IsEqualApprox(Colors.White) &&
            goat.BodyTintMaterial.Shader?.ResourcePath ==
                "res://src/Tactics.Godot.Adapter/Runtime/Shaders/GoatBodyTint.gdshader" &&
            goat.BodyTintMaterial.GetShaderParameter("body_tint").AsColor().IsEqualApprox(goat.BodyTint) &&
            goat.BodyTintMaterial.GetShaderParameter("base_body_color").AsColor()
                .IsEqualApprox(goat.BaseBodyColor);
        bool fireUsesMultiply = fireDemon.BodyTintModeValue == UnitBodyTintModes.Multiply &&
            fireActor.Body!.Material is null && fireActor.Body.Modulate.IsEqualApprox(fireDemon.BodyTint);

        Color bodyResult = GodotUnitTintReference.Apply(
            goat.BaseBodyColor,
            goat.BodyTintModeValue,
            goat.BodyTint,
            goat.BaseBodyColor);
        var farSource = new Color(0.92f, 0.86f, 0.68f, 0.75f);
        Color farResult = GodotUnitTintReference.Apply(
            farSource,
            goat.BodyTintModeValue,
            goat.BodyTint,
            goat.BaseBodyColor);
        bool baseMapsToTint = bodyResult.IsEqualApprox(goat.BodyTint);
        bool farColorAndAlphaRemain = farResult.IsEqualApprox(farSource);
        Image firstCopy = GodotUnitTintReference.CopyTextureImage(goat.DownRightTexture!);
        firstCopy.Resize(64, 64, Image.Interpolation.Lanczos);
        Image secondCopy = GodotUnitTintReference.CopyTextureImage(goat.DownRightTexture!);
        bool captureCopyDoesNotMutateSharedTexture = secondCopy.GetSize() == new Vector2I(256, 256);

        goatActor.SetDeathVisual(true);
        bool deathKeepsMaterial = goatActor.Body!.Material == goat.BodyTintMaterial;
        goatActor.SetBodyTintEnabled(false);
        bool comparisonDisablesOnlyMaterial = goatActor.Body.Material is null &&
            goatActor.Body.Modulate.IsEqualApprox(Colors.White);
        goatActor.Free();
        fireActor.Free();

        AssertThat(goatUsesMaterial).IsTrue();
        AssertThat(fireUsesMultiply).IsTrue();
        AssertThat(baseMapsToTint).IsTrue();
        AssertThat(farColorAndAlphaRemain).IsTrue();
        AssertThat(captureCopyDoesNotMutateSharedTexture).IsTrue();
        AssertThat(deathKeepsMaterial).IsTrue();
        AssertThat(comparisonDisablesOnlyMaterial).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GeneratedGalleryAndSpawnFixtureLoadAsTypedPackedScenes()
    {
        var galleryScene = ResourceLoader.Load<PackedScene>("res://content/units/UnitGallery.tscn");
        var fixtureScene = ResourceLoader.Load<PackedScene>("res://content/units/UnitSpawnFixture.tscn");
        AssertThat(galleryScene).IsNotNull();
        AssertThat(fixtureScene).IsNotNull();
        if (galleryScene is null || fixtureScene is null)
            return;

        Node gallery = galleryScene.Instantiate();
        Node fixture = fixtureScene.Instantiate();
        AssertThat(gallery).IsInstanceOf<GodotUnitGallery>();
        AssertThat(fixture).IsInstanceOf<GodotUnitSpawnFixture>();
        if (gallery is GodotUnitGallery typedGallery)
        {
            typedGallery.BuildGallery();
            AssertThat(typedGallery.Actors.Count).IsEqual(12);
            AssertThat(typedGallery.Actors[0].Position).IsEqual(new Vector2(212.5f, 193.75f));
            AssertThat(typedGallery.Actors[11].Position).IsEqual(new Vector2(1375f, 768.75f));
            AssertThat(typedGallery.Actors.All(actor =>
                actor.Scale.IsEqualApprox(new Vector2(0.725f, 0.725f)))).IsTrue();
            Label amazonLabel = typedGallery.GetChildren()
                .OfType<Label>()
                .Single(label => label.Text == "Amazon");
            AssertThat(amazonLabel.Position).IsEqual(new Vector2(81.25f, 246.25f));
            AssertThat(amazonLabel.Size).IsEqual(new Vector2(262.5f, 42.5f));
            AssertThat(amazonLabel.GetThemeFontSize("font_size")).IsEqual(20);
            Label instructions = typedGallery.GetChildren()
                .OfType<Label>()
                .Single(label => label.Text.StartsWith("1 South", StringComparison.Ordinal));
            AssertThat(instructions.Position).IsEqual(new Vector2(25f, 5f));
            AssertThat(instructions.Size).IsEqual(new Vector2(1550f, 35f));
            ColorRect background = typedGallery.GetChildren().OfType<ColorRect>().Single();
            AssertThat(background.Size).IsEqual(new Vector2(1600f, 900f));
            AssertThat(typedGallery.Actors.All(actor =>
                actor.Facing == GodotUnitFacing.South && !actor.IsShowingDeath)).IsTrue();
            AssertThat(typedGallery.CurrentFacing).IsEqual(GodotUnitFacing.South);
            AssertThat(typedGallery.IsShowingDeathMode).IsFalse();
            AssertThat(typedGallery.IsGoatTintEnabled).IsTrue();
            typedGallery.SetAllFacing(GodotUnitFacing.West);
            AssertThat(typedGallery.Actors.All(actor => actor.Body!.FlipH)).IsTrue();
            AssertThat(typedGallery.Actors.All(actor => !actor.Shadow!.FlipH)).IsTrue();
            typedGallery.SetAllDeath(true);
            AssertThat(typedGallery.Actors.Count(actor => actor.IsShowingDeath)).IsEqual(9);
            typedGallery.SetGoatTintEnabled(false);
            AssertThat(typedGallery.Actors.Where(actor => actor.UsesGoatBodyMaskTint)
                .All(actor => actor.Body!.Material is null)).IsTrue();
            typedGallery.ResetPreview();
            AssertThat(typedGallery.Actors.All(actor =>
                actor.Facing == GodotUnitFacing.South && !actor.IsShowingDeath)).IsTrue();
            AssertThat(typedGallery.Actors.Where(actor => actor.UsesGoatBodyMaskTint)
                .All(actor => actor.Body!.Material is not null && actor.IsBodyTintEnabled)).IsTrue();
            AssertThat(typedGallery.CurrentFacing).IsEqual(GodotUnitFacing.South);
            AssertThat(typedGallery.IsShowingDeathMode).IsFalse();
            AssertThat(typedGallery.IsGoatTintEnabled).IsTrue();
        }
        if (fixture is GodotUnitSpawnFixture typedFixture)
        {
            IReadOnlyList<Tactics.Core.Battle.BattleUnitState> states = typedFixture.CreateStates();
            AssertThat(states.Count).IsEqual(12);
            AssertThat(states.Select(state => state.Unit.Position).Distinct().Count()).IsEqual(12);
            AssertThat(states.All(state =>
                state.Unit.Position.X >= 0 && state.Unit.Position.X < 10 &&
                state.Unit.Position.Y >= 0 && state.Unit.Position.Y < 10)).IsTrue();
            AssertThat(states.All(state =>
                state.Unit.Position.X >= 1 && state.Unit.Position.X <= 8 &&
                state.Unit.Position.Y >= 1 && state.Unit.Position.Y <= 8)).IsTrue();
            typedFixture.BuildPreview();
            GodotUnitActor[] actors = typedFixture.GetChildren().OfType<GodotUnitActor>().ToArray();
            AssertThat(actors.Length).IsEqual(12);
            AssertThat(actors.All(actor =>
                actor.Scale.IsEqualApprox(new Vector2(0.375f, 0.375f)))).IsTrue();
            for (int index = 0; index < actors.Length; index++)
            {
                Vector2 expectedCenter = GodotUnitSpawnFixture.GetCellCenter(states[index].Unit.Position);
                AssertThat(actors[index].Position).IsEqual(expectedCenter);
                Rect2 bounds = GodotUnitSpawnFixture.ComputeActorVisualBounds(actors[index]);
                AssertThat(IsWithin(bounds, GodotUnitSpawnFixture.BoardVisualSafeRect)).IsTrue();
                AssertThat(IsWithin(bounds, GodotUnitSpawnFixture.ViewportVisualSafeRect)).IsTrue();
            }
        }
        AssertThat(UnitPreviewLayout.CanvasSize).IsEqual(new Vector2I(1600, 900));
        AssertThat(GodotUnitSpawnFixture.GridOrigin).IsEqual(new Vector2(440f, 90f));
        AssertThat(GodotUnitSpawnFixture.CellSize).IsEqual(72f);
        AssertThat(GodotUnitSpawnFixture.ViewportSafeInset).IsEqual(24f);
        AssertThat(GodotUnitSpawnFixture.BoardSafeInset).IsEqual(8f);
        AssertThat(GodotUnitSpawnFixture.OverflowPolicy)
            .IsEqual(
                "internal-grid-overflow-allowed; board-frame-and-viewport-clipping-forbidden");
        AssertThat(GodotUnitSpawnFixture.PreviewBackgroundColor)
            .IsEqual(new Color("82909b"));
        AssertThat(GodotUnitSpawnFixture.PreviewGridColor)
            .IsEqual(new Color("455865"));
        gallery.Free();
        fixture.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ProjectWindowUsesSixteenByNineCanvasItemScaling()
    {
        AssertThat(ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32())
            .IsEqual(1600);
        AssertThat(ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32())
            .IsEqual(900);
        AssertThat(ProjectSettings.GetSetting("display/window/size/window_width_override").AsInt32())
            .IsEqual(1600);
        AssertThat(ProjectSettings.GetSetting("display/window/size/window_height_override").AsInt32())
            .IsEqual(900);
        AssertThat(ProjectSettings.GetSetting("display/window/stretch/mode").AsString())
            .IsEqual("canvas_items");
        AssertThat(ProjectSettings.GetSetting("display/window/stretch/aspect").AsString())
            .IsEqual("keep");
    }

    private static JsonDocument LoadGolden()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Golden", "unit-batch-v1.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static bool IsWithin(Rect2 bounds, Rect2 safeRect)
    {
        Vector2 boundsEnd = bounds.Position + bounds.Size;
        Vector2 safeEnd = safeRect.Position + safeRect.Size;
        return bounds.Position.X >= safeRect.Position.X &&
            bounds.Position.Y >= safeRect.Position.Y &&
            boundsEnd.X <= safeEnd.X &&
            boundsEnd.Y <= safeEnd.Y;
    }
}
