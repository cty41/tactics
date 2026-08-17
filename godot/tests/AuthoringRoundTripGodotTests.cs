using GdUnit4;
using Godot;
using Tactics.Application.Authoring;
using Tactics.Godot.Adapter.Editor;
using Tactics.Godot.Adapter.Runtime;
using static GdUnit4.Assertions;

namespace Tactics.Godot.Tests;

[TestSuite]
public class AuthoringRoundTripGodotTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void FormalAiRevisionsAndSourceHashesAreStableAcrossUncachedReads()
    {
        var service = new TacticsAuthoringEditorService();
        StoredAuthoringDocument[] first = service.List("ai").ToArray();
        StoredAuthoringDocument[] second = service.List("ai").ToArray();
        AssertThat(first.Length).IsGreaterEqual(6);
        AssertThat(second.Select(value => value.Revision).ToArray()).IsEqual(first.Select(value => value.Revision).ToArray());
        foreach ((StoredAuthoringDocument before, StoredAuthoringDocument after) in first.Zip(second))
        {
            var beforeResource = (AiDefinitionResource)before.Resource;
            var afterResource = (AiDefinitionResource)after.Resource;
            AssertThat(afterResource.DecisionGraphHash).IsEqual(beforeResource.DecisionGraphHash);
            AssertThat(afterResource.DecisionGraphHash).IsNotEqual(before.Revision);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EveryFormalSkillStagesWithoutLosingCoreFieldsOrSummonIdentity()
    {
        var service = new TacticsAuthoringEditorService();
        AuthoringResourceHandlerRegistry handlers = AuthoringResourceHandlerRegistry.CreateDefault();
        IAuthoringResourceHandler handler = handlers.Get("skill");
        StoredAuthoringDocument[] skills = service.List("skill").ToArray();
        AssertThat(skills.Length).IsGreater(0);
        foreach (StoredAuthoringDocument stored in skills)
        {
            var source = (SkillAuthoringDocument)stored.Document;
            var stagedResource = (SkillDefinitionResource)handler.Stage(stored.Resource, source);
            var staged = (SkillAuthoringDocument)handler.Read(stagedResource);
            AssertThat(AuthoringRevision.Compute(staged)).IsEqual(stored.Revision);
            AssertThat(staged.Definition.ExecutionProfile.SummonDefinitionId?.Value ?? string.Empty)
                .IsEqual(source.Definition.ExecutionProfile.SummonDefinitionId?.Value ?? string.Empty);
            AssertThat(stagedResource.ToCoreDefinition()).IsEqual(source.Definition);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FormalEncounterLayoutPairsPassCombinedValidationAndMapAliasResolves()
    {
        var service = new TacticsAuthoringEditorService();
        Dictionary<string, BattleLayoutAuthoringDocument> layouts = service.List("battle-layout")
            .ToDictionary(value => value.Document.ContentId, value => (BattleLayoutAuthoringDocument)value.Document, StringComparer.Ordinal);
        StoredAuthoringDocument[] encounters = service.List("encounter").ToArray();
        AssertThat(encounters.Length).IsGreater(0);
        EncounterAuthoringDocument[] documents = encounters.Select(value => (EncounterAuthoringDocument)value.Document).ToArray();
        foreach (EncounterAuthoringDocument encounter in documents)
            EncounterLayoutAuthoringValidator.Validate(encounter, layouts[encounter.LayoutContentId]);
        StoredAuthoringDocument splitEncounter = encounters.First(value => ((EncounterAuthoringDocument)value.Document).LayoutContentId == "battle-layout.pure-run.split-flank");
        AuthoringValidationResult validation = service.Validate("encounter", splitEncounter.Document.ContentId, splitEncounter.Snapshot, splitEncounter.Revision);
        AssertThat(validation.Succeeded).IsTrue();
        AssertThat(service.List("map").Count).IsEqual(1);
        AssertThat(service.List("run-map").Count).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EveryEditableFormalDocumentValidatesAgainstCanonicalCatalog()
    {
        var service = new TacticsAuthoringEditorService();
        foreach (StoredAuthoringDocument stored in service.List())
        {
            AuthoringValidationResult validation = service.Validate(stored.Entry.ResourceTypeIdValue,
                stored.Document.ContentId, stored.Snapshot, stored.Revision);
            AssertThat(validation.Succeeded).OverrideFailureMessage(stored.Document.ContentId + ": " +
                string.Join("; ", validation.Diagnostics.Select(value => value.Message))).IsTrue();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void NativePresentationPreviewStopsWithoutTweensOrTemporaryNodes()
    {
        var service = new TacticsAuthoringEditorService();
        StoredAuthoringDocument[] profiles = service.List("presentation")
            .GroupBy(value => ((PresentationProfileAuthoringDocument)value.Document).ResourceClass, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        AssertThat(profiles.Length).IsEqual(3);

        var stage = new PresentationProfilePreviewStage();
        stage._Ready();
        stage.InitializeActors();
        stage.InitializeActors();
        AssertThat(stage.ActorCount).IsEqual(2);
        foreach (StoredAuthoringDocument profile in profiles)
        {
            stage.Configure(profile.Resource);
            stage.Play(2f, "Full");
            stage.SetPaused(true);
            stage.Stop();
            AssertThat(stage.ActiveTweenCount).OverrideFailureMessage(profile.Document.ContentId).IsEqual(0);
            AssertThat(stage.TemporaryNodeCount).OverrideFailureMessage(profile.Document.ContentId).IsEqual(0);
        }
        stage.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void NativePresentationPreviewWaitsForTypedActorsWithoutPartialConstruction()
    {
        PresentationPreviewActorLoadResult ready = PresentationProfilePreviewStage.ProbeActorResources();
        AssertThat((int)ready.State).IsEqual((int)EditorResourceLoadState.Ready);
        int probes = 0;
        var stage = new PresentationProfilePreviewStage
        {
            ActorResourceProbe = () => probes++ == 0
                ? new PresentationPreviewActorLoadResult(EditorResourceLoadState.ReloadPending, null, null,
                    "synthetic assembly reload")
                : ready
        };

        stage._Ready();
        stage.InitializeActors();
        AssertThat(stage.ActorCount).IsEqual(0);
        stage.InitializeActors();
        stage.InitializeActors();
        AssertThat(stage.ActorCount).IsEqual(2);
        AssertThat(probes).IsEqual(2);
        stage._ExitTree();
        AssertThat(stage.ActorCount).IsEqual(0);
        stage.Free();
    }
}
