#if TOOLS
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using Tactics.Application.Content;
using Tactics.Godot.Adapter.Runtime;

namespace Tactics.Godot.Adapter.Editor;

/// <summary>
/// Converts the disposable typed draft into final Godot resources through ResourceSaver.
/// </summary>
public static class PoisonSpearAssetFactory
{
    private const string BatchId = "poison-spear-lv1-real";
    private const string DefaultRoot = "res://content/poison_spear";

    public static void BuildLv1(string? draftPath = null, string root = DefaultRoot)
    {
        string projectPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(ProjectSettings.GlobalizePath("res://")));
        string repositoryPath = Directory.GetParent(projectPath)?.FullName
            ?? throw new InvalidOperationException("Cannot resolve migration repository root.");
        draftPath ??= Path.Combine(
            repositoryPath,
            "Tools",
            "migration",
            "out",
            "poison-spear-lv1.draft.json");
        PoisonSpearMigrationDraft draft = PoisonSpearMigrationDraft.Load(draftPath);
        ContentSnapshot snapshot = draft.CompileApplicationSnapshot();
        if (snapshot.Entries.Count != 6)
            throw new InvalidOperationException("Poison Spear real draft must compile exactly six content entries.");

        EnsureDirectory(root);
        string ledgerPath = Path.Combine(
            repositoryPath,
            "Tools",
            "migration",
            "manifest",
            "state",
            $"{BatchId}.json");
        var transaction = new ResourceSaveTransaction(ledgerPath, draft.Source);
        string[] targetPaths =
        {
            $"{root}/PoisonBuff.tres",
            $"{root}/PoisonSpearSkillLv1.tres",
            $"{root}/PoisonSpearPresentationLv1.tres",
            $"{root}/PoisonSpear10x10Fixture.tres",
            $"{root}/PoisonSpearProjectile.tscn",
            $"{root}/PoisonSpearImpact.tscn",
            $"{root}/ContentCatalog.tres"
        };
        transaction.Preflight(targetPaths, () => ValidateAdoptableTechnicalSpike(root));

        try
        {
            PoisonSpearDraftContent poisonDraft = draft.Get("buff.poison");
            PoisonSpearDraftContent skillDraft = draft.Get("skill.poison-spear.lv1");
            PoisonSpearDraftContent presentationDraft = draft.Get("presentation.poison-spear.lv1");
            PoisonSpearDraftContent fixtureDraft = draft.Get("encounter.poison-spear.10x10");
            PoisonSpearDraftContent projectileDraft = draft.Get("projectile.poison-spear");
            PoisonSpearDraftContent impactDraft = draft.Get("impact.poison-spear");

            var poison = LoadOrCreate<PoisonBuffResource>($"{root}/PoisonBuff.tres");
            poison.ContentIdValue = poisonDraft.ContentId;
            poison.SchemaVersion = poisonDraft.SchemaVersion;
            poison.DisplayName = poisonDraft.String("name");
            poison.DefaultDuration = poisonDraft.Integer("duration");
            poison.DamagePerTurn = poisonDraft.Integer("tickDamage");
            poison.DamageCategory = poisonDraft.String("damageCategory");
            poison.EffectType = poisonDraft.String("effectType");
            poison.Polarity = poisonDraft.String("polarity");
            poison.RefreshStrategy = poisonDraft.String("refreshStrategy");
            poison.TriggerTiming = poisonDraft.String("triggerTiming");

            var presentation = LoadOrCreate<PoisonSpearPresentationResource>(
                $"{root}/PoisonSpearPresentationLv1.tres");
            PopulatePresentation(presentation, presentationDraft, root);

            var skill = LoadOrCreate<PoisonSpearSkillResource>($"{root}/PoisonSpearSkillLv1.tres");
            skill.ContentIdValue = skillDraft.ContentId;
            skill.SchemaVersion = skillDraft.SchemaVersion;
            skill.DisplayName = skillDraft.String("displayName");
            skill.Description = skillDraft.String("description");
            skill.Range = skillDraft.Integer("range");
            skill.ManaCost = skillDraft.Integer("manaCost");
            skill.Damage = skillDraft.Integer("damage");
            skill.PoisonTurns = skillDraft.Integer("poisonDuration");
            skill.PoisonDamagePerTurn = skillDraft.Integer("poisonTickDamage");
            skill.RequiresLineOfSight = skillDraft.Boolean("requiresLineOfSight");
            skill.ProjectileSpeed = skillDraft.Single("projectileSpeed");
            skill.ProjectileTravelTime = skillDraft.Single("projectileTravelTime");
            skill.DropOnHit = skillDraft.Boolean("dropOnHit");
            skill.DropSearchRadius = skillDraft.Integer("runtimeDropSearchRadius");
            skill.DropsSpearOnCompletion = skillDraft.Boolean("dropsSpearOnCompletion");
            skill.Poison = poison;
            skill.Presentation = presentation;

            JsonElement fixtureProperties = fixtureDraft.Properties;
            var fixture = LoadOrCreate<PoisonSpearFixtureResource>(
                $"{root}/PoisonSpear10x10Fixture.tres");
            fixture.ContentIdValue = fixtureDraft.ContentId;
            fixture.BoardWidth = fixtureDraft.Integer("boardWidth");
            fixture.BoardHeight = fixtureDraft.Integer("boardHeight");
            fixture.CasterCell = Vector(fixtureProperties.GetProperty("casterCell"));
            fixture.TargetCell = Vector(fixtureProperties.GetProperty("targetCell"));

            Color tint = ParseColor(projectileDraft.String("tint"));
            SaveResource(poison, $"{root}/PoisonBuff.tres");
            SaveResource(presentation, $"{root}/PoisonSpearPresentationLv1.tres");
            SaveResource(skill, $"{root}/PoisonSpearSkillLv1.tres");
            SaveResource(fixture, $"{root}/PoisonSpear10x10Fixture.tres");
            SaveProjectileScene(
                $"{root}/PoisonSpearProjectile.tscn",
                skill.ProjectileTravelTime,
                projectileDraft.Single("scale"),
                projectileDraft.Single("arcHeight"),
                tint,
                projectileDraft.Boolean("rotateAlongTangent"));
            SaveImpactScene(
                $"{root}/PoisonSpearImpact.tscn",
                impactDraft.Single("lifetime"),
                impactDraft.Single("scale"),
                tint);

            var catalog = LoadOrCreate<GodotResourceCatalog>($"{root}/ContentCatalog.tres");
            catalog.Entries = new[]
            {
                Entry(poison.ContentIdValue, "buff", $"{root}/PoisonBuff.tres"),
                Entry(
                    skill.ContentIdValue,
                    "skill",
                    $"{root}/PoisonSpearSkillLv1.tres",
                    poison.ContentIdValue,
                    presentation.ContentIdValue),
                Entry(
                    presentation.ContentIdValue,
                    "presentation",
                    $"{root}/PoisonSpearPresentationLv1.tres",
                    "impact.poison-spear",
                    "projectile.poison-spear"),
                Entry(fixture.ContentIdValue, "encounter", $"{root}/PoisonSpear10x10Fixture.tres", skill.ContentIdValue),
                Entry("projectile.poison-spear", "packed-scene", $"{root}/PoisonSpearProjectile.tscn"),
                Entry("impact.poison-spear", "packed-scene", $"{root}/PoisonSpearImpact.tscn")
            };
            SaveResource(catalog, $"{root}/ContentCatalog.tres");

            var semantics = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [$"{root}/PoisonBuff.tres"] = poisonDraft.Properties,
                [$"{root}/PoisonSpearSkillLv1.tres"] = skillDraft.Properties,
                [$"{root}/PoisonSpearPresentationLv1.tres"] = presentationDraft.Properties,
                [$"{root}/PoisonSpear10x10Fixture.tres"] = fixtureDraft.Properties,
                [$"{root}/PoisonSpearProjectile.tscn"] = projectileDraft.Properties,
                [$"{root}/PoisonSpearImpact.tscn"] = impactDraft.Properties,
                [$"{root}/ContentCatalog.tres"] = JsonSerializer.SerializeToElement(
                    draft.Contents.Select(item => item.ContentId).Order(StringComparer.Ordinal).ToArray())
            };
            transaction.Commit(targetPaths, semantics);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void PopulatePresentation(
        PoisonSpearPresentationResource presentation,
        PoisonSpearDraftContent draft,
        string root)
    {
        JsonElement graph = draft.Property("graph");
        JsonElement[] nodes = graph.GetProperty("nodes").EnumerateArray().ToArray();
        JsonElement[] edges = graph.GetProperty("edges").EnumerateArray().ToArray();
        JsonElement actionNode = nodes.Single(node => NodeType(node) == "PresentationUnitTweenNodeRecord");
        JsonElement projectileNode = nodes.Single(node => NodeType(node) == "PresentationProjectileNodeRecord");
        string actionNodeId = actionNode.GetProperty("id").GetString()!;
        string projectileNodeId = projectileNode.GetProperty("id").GetString()!;

        presentation.ContentIdValue = draft.ContentId;
        presentation.SchemaVersion = graph.GetProperty("schemaVersion").GetInt32();
        presentation.NodeIds = new[] { "__poison_spear.runtime", actionNodeId, projectileNodeId };
        presentation.NodeTypes = new[] { "sequence", "unit.tween.ranged", "projectile.flight-impact" };
        presentation.NodeChildren = new[] { $"{actionNodeId},{projectileNodeId}", string.Empty, string.Empty };
        presentation.PlanRootNodeId = "__poison_spear.runtime";
        presentation.AuthoringNodeIds = nodes.Select(node => node.GetProperty("id").GetString()!).ToArray();
        presentation.AuthoringNodeTypes = nodes.Select(NodeType).ToArray();
        presentation.AuthoringNodeKinds = nodes.Select(node => NodeType(node) switch
        {
            "PresentationEntryNodeRecord" => "entry",
            "PresentationFinishNodeRecord" => "finish",
            _ => "leaf"
        }).ToArray();
        presentation.AuthoringNodeCues = nodes.Select(node =>
        {
            JsonElement fields = node.GetProperty("fields");
            return fields.TryGetProperty("cue", out JsonElement cue) ? cue.GetString() ?? string.Empty : string.Empty;
        }).ToArray();
        presentation.AuthoringNodeEnabled = nodes.Select(node =>
            node.GetProperty("enabled").GetBoolean() ? 1 : 0).ToArray();
        presentation.AuthoringNodePositions = nodes.Select(node =>
        {
            JsonElement position = node.GetProperty("position");
            return new Vector2(
                position.GetProperty("x").GetSingle(),
                position.GetProperty("y").GetSingle());
        }).ToArray();
        presentation.EdgeIds = edges.Select(edge => edge.GetProperty("id").GetString()!).ToArray();
        presentation.EdgeSources = edges.Select(edge => edge.GetProperty("source").GetString()!).ToArray();
        presentation.EdgeTargets = edges.Select(edge => edge.GetProperty("target").GetString()!).ToArray();
        presentation.ProjectileScenePath = $"{root}/PoisonSpearProjectile.tscn";
        presentation.ImpactScenePath = $"{root}/PoisonSpearImpact.tscn";
        presentation.ProjectileSpeed = draft.Single("projectileSpeed");
        presentation.FallbackTravelTime = draft.Single("fallbackTravelTime");
        presentation.ValidateAuthoringGraph();
        presentation.BuildExecutionPlan();
        PoisonSpearPresentationEditorService.SynchronizeRevision(presentation);
    }

    private static string NodeType(JsonElement node) => node.GetProperty("type").GetString()
        ?? throw new InvalidOperationException("Presentation graph node type is missing.");

    private static void ValidateAdoptableTechnicalSpike(string root)
    {
        PoisonSpearSkillResource? skill = ResourceLoader.Load<PoisonSpearSkillResource>(
            $"{root}/PoisonSpearSkillLv1.tres");
        PoisonSpearPresentationResource? presentation = ResourceLoader.Load<PoisonSpearPresentationResource>(
            $"{root}/PoisonSpearPresentationLv1.tres");
        PoisonSpearFixtureResource? fixture = ResourceLoader.Load<PoisonSpearFixtureResource>(
            $"{root}/PoisonSpear10x10Fixture.tres");
        if (skill is null || presentation is null || fixture is null ||
            skill.ContentIdValue != "skill.poison-spear.lv1" || skill.Damage != 8 ||
            skill.PoisonTurns != 3 || (skill.Range != 6 && skill.Range != 5) ||
            presentation.ContentIdValue != "presentation.poison-spear.lv1" ||
            fixture.ContentIdValue != "encounter.poison-spear.10x10")
        {
            throw new InvalidOperationException(
                "Existing Poison Spear targets are not the known technical Spike; refusing first adoption.");
        }
    }

    private static T LoadOrCreate<T>(string path) where T : Resource, new() =>
        File.Exists(ProjectSettings.GlobalizePath(path))
            ? ResourceLoader.Load<T>(path) ?? throw new InvalidOperationException(
                $"Existing resource '{path}' could not be loaded as {typeof(T).Name}.")
            : new T();

    private static GodotResourceEntry Entry(
        string contentId,
        string resourceTypeId,
        string path,
        params string[] references)
    {
        long uid = UidForPath(path);
        if (uid == ResourceUid.InvalidId)
            throw new InvalidOperationException($"Saved resource '{path}' has no UID.");

        return new GodotResourceEntry
        {
            ContentIdValue = contentId,
            ResourceTypeIdValue = resourceTypeId,
            ResourceUidValue = ResourceUid.IdToText(uid),
            DiagnosticPathValue = path,
            SchemaVersion = 1,
            ReferenceContentIds = references.Order(StringComparer.Ordinal).ToArray()
        };
    }

    private static void SaveProjectileScene(
        string path,
        float flightSeconds,
        float sourceScale,
        float arcHeight,
        Color tint,
        bool rotateAlongTangent) => SaveScene<PoisonSpearProjectile>(path, "PoisonSpearProjectile", node =>
    {
        node.FlightSeconds = flightSeconds;
        node.SourceScale = sourceScale;
        node.ArcHeight = arcHeight;
        node.Tint = tint;
        node.RotateAlongTangent = rotateAlongTangent;
    });

    private static void SaveImpactScene(
        string path,
        float tailSeconds,
        float sourceScale,
        Color tint) => SaveScene<PoisonSpearImpact>(path, "PoisonSpearImpact", node =>
    {
        node.TailSeconds = tailSeconds;
        node.SourceScale = sourceScale;
        node.Tint = tint;
    });

    private static void SaveScene<T>(string path, string name, Action<T> configure) where T : Node, new()
    {
        PackedScene packedScene = ResourceLoader.Load<PackedScene>(path) ?? new PackedScene();
        T root;
        if (packedScene.CanInstantiate())
        {
            Node instance = packedScene.Instantiate();
            root = instance as T ?? throw new InvalidOperationException(
                $"Existing scene '{path}' does not instantiate {typeof(T).Name}.");
        }
        else
        {
            root = new T();
        }
        root.Name = name;
        configure(root);
        Error packError = packedScene.Pack(root);
        root.Free();
        if (packError != Error.Ok)
            throw new InvalidOperationException($"Cannot pack '{path}': {packError}");
        SaveResource(packedScene, path);
    }

    private static Vector2I Vector(JsonElement array)
    {
        int[] values = array.EnumerateArray().Select(item => item.GetInt32()).ToArray();
        if (values.Length != 2)
            throw new InvalidOperationException("Expected a two-element board coordinate.");
        return new Vector2I(values[0], values[1]);
    }

    private static Color ParseColor(string text)
    {
        float[] values = text.Split(',').Select(value => float.Parse(
            value,
            System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (values.Length != 4)
            throw new InvalidOperationException($"Invalid Unity color '{text}'.");
        return new Color(values[0], values[1], values[2], values[3]);
    }

    private static void EnsureDirectory(string resourceDirectory)
    {
        Error error = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(resourceDirectory));
        if (error != Error.Ok && error != Error.AlreadyExists)
            throw new InvalidOperationException($"Cannot create '{resourceDirectory}': {error}");
    }

    private static void SaveResource(Resource resource, string path)
    {
        long existingUid = UidForPath(path);
        long uid = existingUid != ResourceUid.InvalidId
            ? existingUid
            : ResourceUid.CreateIdForPath(path);
        if (!ResourceUid.HasId(uid))
            ResourceUid.AddId(uid, path);
        else if (!string.Equals(ResourceUid.GetIdPath(uid), path, StringComparison.Ordinal))
            throw new InvalidOperationException($"Resource UID collision for '{path}'.");
        DeterministicResourceSaver.Save(resource,path,uid);
    }

    private static long UidForPath(string path)
    {
        string uidText = ResourceUid.PathToUid(path);
        return uidText.StartsWith("uid://", StringComparison.Ordinal)
            ? ResourceUid.TextToId(uidText)
            : ResourceUid.InvalidId;
    }

    private sealed class ResourceSaveTransaction
    {
        private readonly string _ledgerPath;
        private readonly PoisonSpearDraftSource _source;
        private readonly Dictionary<string, byte[]?> _backups = new(StringComparer.OrdinalIgnoreCase);
        private JsonDocument? _previousLedger;

        public ResourceSaveTransaction(
            string ledgerPath,
            PoisonSpearDraftSource source)
        {
            _ledgerPath = ledgerPath;
            _source = source;
        }

        public void Preflight(IEnumerable<string> resourcePaths, Action validateFirstAdoption)
        {
            string[] paths = resourcePaths.ToArray();
            foreach (string resourcePath in paths)
            {
                string absolutePath = ProjectSettings.GlobalizePath(resourcePath);
                _backups[absolutePath] = File.Exists(absolutePath) ? File.ReadAllBytes(absolutePath) : null;
            }
            _backups[_ledgerPath] = File.Exists(_ledgerPath) ? File.ReadAllBytes(_ledgerPath) : null;

            if (!File.Exists(_ledgerPath))
            {
                validateFirstAdoption();
                return;
            }

            _previousLedger = JsonDocument.Parse(File.ReadAllText(_ledgerPath));
            JsonElement root = _previousLedger.RootElement;
            if (root.GetProperty("batchId").GetString() != BatchId ||
                root.GetProperty("source").GetProperty("exportHash").GetString() != _source.ExportHash)
            {
                throw new InvalidOperationException("Poison Spear migration ledger source binding changed.");
            }
            JsonElement[] recordedArtifacts = root.GetProperty("artifacts").EnumerateArray().ToArray();
            var expectedHashes = recordedArtifacts.ToDictionary(
                item => item.GetProperty("resourcePath").GetString()!,
                item => item.GetProperty("targetHash").GetString()!,
                StringComparer.Ordinal);
            foreach (JsonElement artifact in recordedArtifacts)
            {
                string resourcePath = artifact.GetProperty("resourcePath").GetString()!;
                long uid = ResourceUid.TextToId(artifact.GetProperty("resourceUid").GetString()!);
                if (!ResourceUid.HasId(uid))
                    ResourceUid.AddId(uid, resourcePath);
                else if (!string.Equals(ResourceUid.GetIdPath(uid), resourcePath, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Recorded UID now maps to another resource: {resourcePath}");
            }
            foreach (string resourcePath in paths)
            {
                string absolutePath = ProjectSettings.GlobalizePath(resourcePath);
                if (!expectedHashes.TryGetValue(resourcePath, out string? expectedHash) ||
                    !File.Exists(absolutePath) || Hash(File.ReadAllBytes(absolutePath)) != expectedHash)
                {
                    throw new InvalidOperationException(
                        $"Generated target changed after the last migration: {resourcePath}");
                }
            }
        }

        public void Commit(
            IEnumerable<string> resourcePaths,
            IReadOnlyDictionary<string, JsonElement> semanticModels)
        {
            var artifacts = resourcePaths.Order(StringComparer.Ordinal).Select(resourcePath =>
            {
                string absolutePath = ProjectSettings.GlobalizePath(resourcePath);
                if (!File.Exists(absolutePath))
                    throw new InvalidOperationException($"Generated target is missing: {resourcePath}");
                long uid = UidForPath(resourcePath);
                if (uid == ResourceUid.InvalidId)
                    throw new InvalidOperationException($"Generated target has no Resource UID: {resourcePath}");
                return new
                {
                    resourcePath,
                    resourceUid = ResourceUid.IdToText(uid),
                    targetHash = Hash(File.ReadAllBytes(absolutePath)),
                    semanticHash = Hash(Encoding.UTF8.GetBytes(semanticModels[resourcePath].GetRawText()))
                };
            }).ToArray();
            var ledger = new
            {
                schemaVersion = 1,
                batchId = BatchId,
                source = new
                {
                    sourceTag = _source.SourceTag,
                    sourceCommit = _source.SourceCommit,
                    exporterVersion = _source.ExporterVersion,
                    exportHash = _source.ExportHash
                },
                artifacts
            };
            string payload = JsonSerializer.Serialize(ledger, new JsonSerializerOptions { WriteIndented = true }) + "\n";
            Directory.CreateDirectory(Path.GetDirectoryName(_ledgerPath)!);
            string temporaryPath = _ledgerPath + ".tmp";
            File.WriteAllText(temporaryPath, payload, new UTF8Encoding(false));
            File.Move(temporaryPath, _ledgerPath, overwrite: true);
        }

        public void Rollback()
        {
            foreach ((string path, byte[]? payload) in _backups.Reverse())
            {
                if (payload is null)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, payload);
            }
            _previousLedger?.Dispose();
        }

        private static string Hash(byte[] payload) =>
            "sha256:" + Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }
}
#endif
