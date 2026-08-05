#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Tween;
using Tactics.Editor.PresentationGraph;
using UnityEditor;
using UnityEngine;
using PresentationPreviewRenderResult =
    Tactics.EditorTools.PresentationWorkbenchWindow.PresentationPreviewRenderResult;
using PresentationPreviewScope =
    Tactics.EditorTools.PresentationWorkbenchWindow.PresentationPreviewScope;
using PresentationPreviewScopeKind =
    Tactics.EditorTools.PresentationWorkbenchWindow.PresentationPreviewScopeKind;

namespace Tactics.EditorTools
{
    /// <summary>
    /// Transaction boundary shared by the workbench and MCP presentation authoring tools.
    /// </summary>
    public static class PresentationAuthoringFacade
    {
        internal static Action<string> CommitFaultInjector { get; set; }

        public static JArray ListGraphs()
        {
            var result = new JArray();
            foreach (string guid in AssetDatabase.FindAssets("t:BattlePresentationGraph"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BattlePresentationGraph graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(path);
                if (graph == null)
                    continue;
                result.Add(new JObject
                {
                    ["guid"] = guid,
                    ["path"] = path,
                    ["displayName"] = graph.DisplayName,
                    ["revision"] = Revision(graph)
                });
            }
            return result;
        }

        public static JObject GetGraph(string path)
        {
            BattlePresentationGraph graph = LoadGraph(path);
            JObject snapshot = Snapshot(graph);
            snapshot["revision"] = RevisionFromSnapshot(snapshot);
            snapshot["diagnostics"] = Validate(graph);
            snapshot["dependencies"] = Dependencies(graph);
            snapshot["sharedReferences"] = SharedReferences(graph);
            snapshot["leafAssets"] = LeafAssets(graph);
            return snapshot;
        }

        public static JObject ValidateChangeSet(JObject changeSet)
        {
            return (JObject)ValidateChangeSets(new JArray(changeSet))[0];
        }

        public static JArray ValidateChangeSets(JArray changeSets)
        {
            List<JObject> batch = changeSets?.OfType<JObject>().ToList() ?? new List<JObject>();
            if (batch.Count == 0)
                throw new ArgumentException("'changeSets' requires at least one ChangeSet.");
            var prepared = new List<PreparedChangeSet>();
            var preparedAssets = new List<PreparedAsset>();
            try
            {
                var assetGroups = new List<List<PreparedAsset>>();
                foreach (JObject changeSet in batch)
                {
                    List<PreparedAsset> assets = PrepareAssetChanges(changeSet["assetChanges"] as JArray);
                    assetGroups.Add(assets);
                    preparedAssets.AddRange(assets);
                }
                Dictionary<string, UnityEngine.Object> stagedAssets = preparedAssets.ToDictionary(
                    value => value.Path,
                    value => value.Sandbox,
                    StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < batch.Count; index++)
                    prepared.Add(PrepareGraphChangeSet(batch[index], assetGroups[index], stagedAssets));
                ValidatePreparedPaths(prepared);
                return new JArray(prepared.Select(ValidationResult));
            }
            finally
            {
                foreach (PreparedChangeSet item in prepared)
                {
                    if (item.GraphSandbox != null)
                        UnityEngine.Object.DestroyImmediate(item.GraphSandbox);
                }
                DestroyPreparedAssets(preparedAssets);
            }
        }

        public static JObject ApplyChangeSet(JObject changeSet)
        {
            return (JObject)ApplyChangeSets(new JArray(changeSet))[0];
        }

        private static JObject ValidationResult(PreparedChangeSet prepared)
        {
            JObject after = Snapshot(prepared.GraphSandbox);
            return new JObject
            {
                ["graphPath"] = prepared.GraphPath,
                ["beforeRevision"] = prepared.BeforeRevision,
                ["predictedRevision"] = RevisionFromSnapshot(after),
                ["changed"] = prepared.GraphSource == null ||
                              !JToken.DeepEquals(
                                  ContentSnapshot(Snapshot(prepared.GraphSource)),
                                  ContentSnapshot(after)),
                ["operations"] = prepared.OperationResults,
                ["assetChanges"] = AssetChangeResults(prepared.Assets),
                ["diagnostics"] = prepared.Diagnostics,
                ["valid"] = prepared.Diagnostics.Count == 0,
                ["predictedSnapshot"] = after
            };
        }

        public static JArray ApplyChangeSets(JArray changeSets)
        {
            List<JObject> batch = changeSets?.OfType<JObject>().ToList() ?? new List<JObject>();
            if (batch.Count == 0)
                throw new ArgumentException("'changeSets' requires at least one ChangeSet.");

            var prepared = new List<PreparedChangeSet>();
            var preparedAssets = new List<PreparedAsset>();
            var backups = new Dictionary<UnityEngine.Object, PreparedBackup>();
            var createdPaths = new List<string>();
            var createdFolders = new List<string>();
            var results = new JArray();
            int batchGroup = -1;
            try
            {
                var assetGroups = new List<List<PreparedAsset>>();
                foreach (JObject changeSet in batch)
                {
                    List<PreparedAsset> assets = PrepareAssetChanges(changeSet["assetChanges"] as JArray);
                    assetGroups.Add(assets);
                    preparedAssets.AddRange(assets);
                }
                Dictionary<string, UnityEngine.Object> stagedAssets = preparedAssets.ToDictionary(
                    value => value.Path,
                    value => value.Sandbox,
                    StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < batch.Count; index++)
                    prepared.Add(PrepareGraphChangeSet(batch[index], assetGroups[index], stagedAssets));
                ValidatePreparedPaths(prepared);

                Undo.IncrementCurrentGroup();
                batchGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Apply Presentation ChangeSet Batch");
                foreach (PreparedAsset asset in prepared.SelectMany(value => value.Assets))
                {
                    CommitPreparedAsset(asset, backups, createdPaths, createdFolders);
                    CommitFaultInjector?.Invoke($"asset:{asset.Path}");
                }

                Dictionary<string, UnityEngine.Object> persistentAssets = prepared
                    .SelectMany(value => value.Assets)
                    .ToDictionary(
                        value => value.Path,
                        value => value.Source != null
                            ? value.Source
                            : AssetDatabase.LoadMainAssetAtPath(value.Path),
                        StringComparer.OrdinalIgnoreCase);
                foreach (PreparedChangeSet item in prepared)
                {
                    BattlePresentationGraph committedSandbox = BuildGraphSandbox(item.ChangeSet, persistentAssets);
                    try
                    {
                        ApplyOperations(
                            committedSandbox,
                            item.ChangeSet["operations"] as JArray,
                            persistentAssets);
                        BattlePresentationGraph target;
                        if (item.GraphSource == null)
                        {
                            EnsureAssetFolder(item.GraphPath, createdFolders);
                            committedSandbox.hideFlags = HideFlags.None;
                            AssetDatabase.CreateAsset(committedSandbox, item.GraphPath);
                            Undo.RegisterCreatedObjectUndo(committedSandbox, "Create Presentation Graph");
                            createdPaths.Add(item.GraphPath);
                            target = committedSandbox;
                        }
                        else
                        {
                            BackupObject(item.GraphSource, backups);
                            Undo.RecordObject(item.GraphSource, "Apply Presentation ChangeSet");
                            EditorUtility.CopySerialized(committedSandbox, item.GraphSource);
                            EditorUtility.SetDirty(item.GraphSource);
                            target = item.GraphSource;
                        }

                        results.Add(new JObject
                        {
                            ["graphPath"] = item.GraphPath,
                            ["previousRevision"] = item.BeforeRevision,
                            ["revision"] = Revision(target),
                            ["operations"] = item.OperationResults,
                            ["assetChanges"] = AssetChangeResults(item.Assets),
                            ["diagnostics"] = item.Diagnostics,
                            ["valid"] = item.Diagnostics.Count == 0
                        });
                        CommitFaultInjector?.Invoke($"graph:{item.GraphPath}");
                    }
                    finally
                    {
                        if (AssetDatabase.GetAssetPath(committedSandbox).Length == 0)
                            UnityEngine.Object.DestroyImmediate(committedSandbox);
                    }
                }

                Undo.CollapseUndoOperations(batchGroup);
                AssetDatabase.SaveAssets();
                return results;
            }
            catch
            {
                if (batchGroup >= 0)
                    Undo.RevertAllDownToGroup(batchGroup);
                foreach ((UnityEngine.Object target, PreparedBackup backup) in backups)
                {
                    if (target == null || backup?.Snapshot == null)
                        continue;
                    EditorUtility.CopySerialized(backup.Snapshot, target);
                    if (backup.WasDirty)
                        EditorUtility.SetDirty(target);
                    else
                        EditorUtility.ClearDirty(target);
                }
                foreach (string path in createdPaths.AsEnumerable().Reverse())
                    AssetDatabase.DeleteAsset(path);
                CleanupCreatedFolders(createdFolders);
                throw;
            }
            finally
            {
                foreach (PreparedBackup backup in backups.Values)
                    UnityEngine.Object.DestroyImmediate(backup.Snapshot);
                foreach (PreparedChangeSet item in prepared)
                {
                    if (item.GraphSandbox != null)
                        UnityEngine.Object.DestroyImmediate(item.GraphSandbox);
                }
                DestroyPreparedAssets(preparedAssets);
            }
        }

        private static PreparedChangeSet PrepareGraphChangeSet(
            JObject changeSet,
            List<PreparedAsset> assets,
            IReadOnlyDictionary<string, UnityEngine.Object> staged)
        {
            string graphPath = RequireString(changeSet, "graphPath");
            JObject createGraph = changeSet["createGraph"] as JObject;
            BattlePresentationGraph source;
            string beforeRevision;
            if (createGraph != null)
            {
                if (changeSet["expectedRevision"] != null)
                    throw new ArgumentException("'createGraph' and 'expectedRevision' are mutually exclusive.");
                if (AssetDatabase.LoadMainAssetAtPath(graphPath) != null)
                    throw new InvalidOperationException($"AssetAlreadyExists: '{graphPath}'.");
                source = null;
                beforeRevision = null;
            }
            else
            {
                string expectedRevision = RequireString(changeSet, "expectedRevision");
                source = LoadGraph(graphPath);
                beforeRevision = Revision(source);
                if (!string.Equals(expectedRevision, beforeRevision, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"RevisionConflict: expected '{expectedRevision}', actual '{beforeRevision}' for '{graphPath}'.");
                }
            }

            BattlePresentationGraph sandbox = null;
            try
            {
                sandbox = BuildGraphSandbox(changeSet, staged);
                JArray operationResults = ApplyOperations(sandbox, changeSet["operations"] as JArray, staged);
                JArray diagnostics = Validate(sandbox);
                bool allowInvalidDraft = changeSet.Value<bool?>("allowInvalidDraft") == true;
                if (diagnostics.Count > 0 && !allowInvalidDraft)
                {
                    throw new InvalidOperationException("ValidationFailed: " + diagnostics[0]?["message"]);
                }

                return new PreparedChangeSet(
                    changeSet,
                    graphPath,
                    source,
                    beforeRevision,
                    sandbox,
                    assets,
                    operationResults,
                    diagnostics);
            }
            catch
            {
                if (sandbox != null)
                    UnityEngine.Object.DestroyImmediate(sandbox);
                throw;
            }
        }

        private static BattlePresentationGraph BuildGraphSandbox(
            JObject changeSet,
            IReadOnlyDictionary<string, UnityEngine.Object> stagedAssets)
        {
            string graphPath = RequireString(changeSet, "graphPath");
            JObject createGraph = changeSet["createGraph"] as JObject;
            BattlePresentationGraph graph = createGraph != null
                ? ScriptableObject.CreateInstance<BattlePresentationGraph>()
                : UnityEngine.Object.Instantiate(LoadGraph(graphPath));
            graph.name = Path.GetFileNameWithoutExtension(graphPath);
            graph.hideFlags = HideFlags.HideAndDontSave;
            if (createGraph != null)
            {
                PatchGraph(graph, createGraph);
                if (createGraph["phases"] is JArray phases)
                    ReplacePreviewScenario(graph, phases);
            }
            return graph;
        }

        private static void ValidatePreparedPaths(IReadOnlyList<PreparedChangeSet> changeSets)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PreparedChangeSet changeSet in changeSets)
            {
                if (!paths.Add(changeSet.GraphPath))
                    throw new InvalidOperationException($"DuplicateTransactionPath: '{changeSet.GraphPath}'.");
                foreach (PreparedAsset asset in changeSet.Assets)
                {
                    if (!paths.Add(asset.Path))
                        throw new InvalidOperationException($"DuplicateTransactionPath: '{asset.Path}'.");
                }
            }
        }

        private static void CommitPreparedAsset(
            PreparedAsset prepared,
            IDictionary<UnityEngine.Object, PreparedBackup> backups,
            ICollection<string> createdPaths,
            ICollection<string> createdFolders)
        {
            if (prepared.Source == null)
            {
                EnsureAssetFolder(prepared.Path, createdFolders);
                prepared.Sandbox.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(prepared.Sandbox, prepared.Path);
                Undo.RegisterCreatedObjectUndo(prepared.Sandbox, "Create Presentation Leaf Asset");
                prepared.WasCreated = true;
                createdPaths.Add(prepared.Path);
                return;
            }

            BackupObject(prepared.Source, backups);
            Undo.RecordObject(prepared.Source, "Edit Presentation Leaf Asset");
            EditorUtility.CopySerialized(prepared.Sandbox, prepared.Source);
            EditorUtility.SetDirty(prepared.Source);
        }

        private static void BackupObject(
            UnityEngine.Object target,
            IDictionary<UnityEngine.Object, PreparedBackup> backups)
        {
            if (target == null || backups.ContainsKey(target))
                return;
            UnityEngine.Object backup = UnityEngine.Object.Instantiate(target);
            backup.hideFlags = HideFlags.HideAndDontSave;
            backups.Add(target, new PreparedBackup(backup, EditorUtility.IsDirty(target)));
        }

        private static void CleanupCreatedFolders(IEnumerable<string> createdFolders)
        {
            foreach (string folder in createdFolders
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderByDescending(value => value.Length))
            {
                if (AssetDatabase.IsValidFolder(folder) && AssetDatabase.FindAssets(string.Empty, new[] { folder }).Length == 0)
                    AssetDatabase.DeleteAsset(folder);
            }
        }

        public static JObject Preview(string path, PresentationCueKind cue, int width = 640, int height = 360)
        {
            return Preview(
                path,
                new PresentationPreviewScope
                {
                    Kind = PresentationPreviewScopeKind.Entry,
                    Cue = cue
                },
                width,
                height,
                1337);
        }

        public static JObject Preview(string path, JObject request)
        {
            PresentationPreviewScope scope = ParsePreviewScope(request?["scope"], request);
            return Preview(
                path,
                scope,
                request?.Value<int?>("width") ?? 640,
                request?.Value<int?>("height") ?? 360,
                request?.Value<int?>("randomSeed") ?? 1337);
        }

        private static JObject Preview(
            string path,
            PresentationPreviewScope scope,
            int width,
            int height,
            int randomSeed)
        {
            BattlePresentationGraph graph = LoadGraph(path);
            width = Mathf.Clamp(width, 64, 2048);
            height = Mathf.Clamp(height, 64, 1024);
            PresentationPreviewRenderResult preview = null;
            try
            {
                preview = PresentationWorkbenchWindow.RenderOffscreen(
                    graph,
                    scope,
                    width,
                    height,
                    randomSeed);
                var timeline = new JArray(preview.Timeline.Select(value => new JObject
                {
                    ["event"] = value.Event,
                    ["nodeId"] = value.NodeId,
                    ["nodeType"] = value.NodeType,
                    ["time"] = value.Time,
                    ["duration"] = value.Duration,
                    ["lane"] = value.Lane,
                    ["phaseIndex"] = value.PhaseIndex,
                    ["marker"] = value.Marker
                }));
                return new JObject
                {
                    ["graphPath"] = path,
                    ["requestedScope"] = PreviewScopeSnapshot(preview.RequestedScope),
                    ["resolvedScope"] = PreviewScopeSnapshot(preview.ResolvedScope),
                    ["width"] = width,
                    ["height"] = height,
                    ["randomSeed"] = preview.RandomSeed,
                    ["imageBase64"] = Convert.ToBase64String(preview.Texture.EncodeToPNG()),
                    ["renderKind"] = "preview-render-utility",
                    ["timeline"] = timeline,
                    ["diagnostics"] = Validate(graph),
                    ["actualFallbacks"] = new JArray(preview.ActualFallbacks)
                };
            }
            finally
            {
                if (preview?.Texture != null)
                    UnityEngine.Object.DestroyImmediate(preview.Texture);
            }
        }

        private static PresentationPreviewScope ParsePreviewScope(JToken scopeToken, JObject request)
        {
            JObject scope = scopeToken as JObject;
            if (scopeToken == null)
            {
                string legacyCue = request?.Value<string>("cue");
                return string.IsNullOrWhiteSpace(legacyCue)
                    ? new PresentationPreviewScope()
                    : new PresentationPreviewScope
                    {
                        Kind = PresentationPreviewScopeKind.Entry,
                        Cue = ParseEnumValue<PresentationCueKind>(legacyCue, "cue")
                    };
            }

            string kindText = scope != null
                ? RequireString(scope, "kind")
                : scopeToken.Value<string>();
            PresentationPreviewScopeKind kind = ParseEnumValue<PresentationPreviewScopeKind>(kindText, "scope");
            JObject fields = scope ?? request;
            return new PresentationPreviewScope
            {
                Kind = kind,
                PhaseIndex = fields?.Value<int?>("phaseIndex") ?? 0,
                Cue = fields?["cue"] != null
                    ? ParseEnum<PresentationCueKind>(fields, "cue")
                    : PresentationCueKind.Action,
                NodeId = fields?.Value<string>("nodeId") ?? fields?.Value<string>("forkNodeId")
            };
        }

        private static JObject PreviewScopeSnapshot(PresentationPreviewScope scope)
        {
            return new JObject
            {
                ["kind"] = char.ToLowerInvariant(scope.Kind.ToString()[0]) + scope.Kind.ToString().Substring(1),
                ["phaseIndex"] = scope.PhaseIndex,
                ["cue"] = scope.Cue.ToString(),
                ["nodeId"] = scope.NodeId
            };
        }

        public static string Revision(BattlePresentationGraph graph)
        {
            return RevisionFromSnapshot(Snapshot(graph));
        }

        private static JArray ApplyOperations(
            BattlePresentationGraph graph,
            JArray operations,
            IReadOnlyDictionary<string, UnityEngine.Object> stagedAssets)
        {
            var results = new JArray();
            if (operations == null)
                return results;
            List<JObject> typedOperations = operations.OfType<JObject>().ToList();
            for (int operationIndex = 0; operationIndex < typedOperations.Count; operationIndex++)
            {
                JObject operation = typedOperations[operationIndex];
                string kind = RequireString(operation, "kind");
                switch (kind)
                {
                    case "setGraph":
                        PatchGraph(graph, operation);
                        break;
                    case "replacePreviewScenario":
                        ReplacePreviewScenario(graph, operation["phases"] as JArray);
                        break;
                    case "addNode":
                    {
                        PresentationNodeType type = ParseEnum<PresentationNodeType>(operation, "nodeType");
                        PresentationNodeRecord node = graph.AddNode(
                            type,
                            new Vector2(operation.Value<float?>("x") ?? 0f, operation.Value<float?>("y") ?? 0f));
                        string stableNodeId = operation.Value<string>("nodeId") ??
                                              operation.Value<string>("resolvedNodeId");
                        if (string.IsNullOrWhiteSpace(stableNodeId))
                            stableNodeId = StableOperationId(operation, operationIndex, "node");
                        if (!string.IsNullOrWhiteSpace(stableNodeId))
                            node.NodeId = stableNodeId;
                        PatchNode(node, operation);
                        operation["resolvedNodeId"] = node.NodeId;
                        break;
                    }
                    case "updateNode":
                        PatchNode(RequireNode(graph, operation), operation);
                        break;
                    case "moveNode":
                    {
                        PresentationNodeRecord node = RequireNode(graph, operation);
                        node.Position = new Vector2(operation.Value<float>("x"), operation.Value<float>("y"));
                        break;
                    }
                    case "removeNode":
                        if (!graph.RemoveNode(RequireString(operation, "nodeId")))
                            throw new InvalidOperationException("NodeNotFound.");
                        break;
                    case "addEdge":
                    {
                        PresentationEdgeRecord edge = graph.AddEdge(
                            RequireString(operation, "sourceNodeId"),
                            RequireString(operation, "targetNodeId"));
                        if (edge == null)
                            throw new InvalidOperationException("InvalidEdge.");
                        string stableEdgeId = operation.Value<string>("edgeId") ??
                                              operation.Value<string>("resolvedEdgeId");
                        if (string.IsNullOrWhiteSpace(stableEdgeId))
                            stableEdgeId = StableOperationId(operation, operationIndex, "edge");
                        if (!string.IsNullOrWhiteSpace(stableEdgeId))
                            edge.EdgeId = stableEdgeId;
                        operation["resolvedEdgeId"] = edge.EdgeId;
                        break;
                    }
                    case "removeEdge":
                        if (!graph.RemoveEdge(RequireString(operation, "edgeId")))
                            throw new InvalidOperationException("EdgeNotFound.");
                        break;
                    case "reconnectEdge":
                    {
                        string edgeId = RequireString(operation, "edgeId");
                        PresentationEdgeRecord edge = graph.Edges.Find(value => value.EdgeId == edgeId)
                            ?? throw new InvalidOperationException("EdgeNotFound.");
                        edge.SourceNodeId = RequireString(operation, "sourceNodeId");
                        edge.TargetNodeId = RequireString(operation, "targetNodeId");
                        break;
                    }
                    case "bindNodeAsset":
                        BindNodeAsset(
                            RequireNode(graph, operation),
                            operation.Value<string>("assetPath"),
                            stagedAssets);
                        break;
                    case "unbindNodeAsset":
                        BindNodeAsset(RequireNode(graph, operation), null, stagedAssets);
                        break;
                    default:
                        throw new InvalidOperationException($"UnsupportedOperation: '{kind}'.");
                }
                results.Add(new JObject { ["kind"] = kind, ["status"] = "validated" });
            }
            return results;
        }

        private static string StableOperationId(JObject operation, int operationIndex, string kind)
        {
            var canonical = (JObject)operation.DeepClone();
            canonical.Remove("resolvedNodeId");
            canonical.Remove("resolvedEdgeId");
            using var sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(
                $"{kind}:{operationIndex}:{CanonicalizeJson(canonical).ToString(Formatting.None)}"));
            return BitConverter.ToString(digest, 0, 16).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static JToken CanonicalizeJson(JToken value)
        {
            return value switch
            {
                JObject objectValue => new JObject(objectValue.Properties()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => new JProperty(property.Name, CanonicalizeJson(property.Value)))),
                JArray arrayValue => new JArray(arrayValue.Select(CanonicalizeJson)),
                _ => value.DeepClone()
            };
        }

        private static void PatchGraph(BattlePresentationGraph graph, JObject patch)
        {
            if (patch["displayName"] != null)
                graph.DisplayName = patch.Value<string>("displayName");
            if (patch["version"] != null)
                graph.Version = patch.Value<int>("version");
            if (patch["defaultPreviewEntry"] != null)
                graph.DefaultPreviewEntry = ParseEnum<PresentationCueKind>(patch, "defaultPreviewEntry");
            if (patch["previewActorPath"] != null)
                graph.PreviewActorPrefab = LoadOptional<GameObject>(patch.Value<string>("previewActorPath"));
            if (patch["previewTargetPath"] != null)
                graph.PreviewTargetPrefab = LoadOptional<GameObject>(patch.Value<string>("previewTargetPath"));
        }

        private static void PatchNode(PresentationNodeRecord node, JObject patch)
        {
            if (patch["enabled"] != null)
                node.Enabled = patch.Value<bool>("enabled");
            switch (node)
            {
                case PresentationEntryNodeRecord entry when patch["cue"] != null:
                    entry.Cue = ParseEnum<PresentationCueKind>(patch, "cue");
                    break;
                case PresentationUnitTweenNodeRecord tween:
                    if (patch["action"] != null)
                        tween.Action = ParseEnum<UnitVisualAction>(patch, "action");
                    if (patch["emitReleaseMarker"] != null)
                        tween.EmitReleaseMarker = patch.Value<bool>("emitReleaseMarker");
                    break;
                case PresentationProjectileNodeRecord projectile:
                    if (patch["speed"] != null) projectile.Speed = Mathf.Max(0f, patch.Value<float>("speed"));
                    if (patch["fallbackTravelTime"] != null)
                        projectile.FallbackTravelTime = Mathf.Max(0f, patch.Value<float>("fallbackTravelTime"));
                    if (patch["emitImpactMarker"] != null)
                        projectile.EmitImpactMarker = patch.Value<bool>("emitImpactMarker");
                    break;
                case PresentationProceduralVfxNodeRecord vfx when patch["cue"] != null:
                    vfx.Cue = ParseEnum<SkillVfxCueKind>(patch, "cue");
                    break;
                case PresentationDelayNodeRecord delay when patch["duration"] != null:
                    delay.Duration = patch.Value<float>("duration");
                    break;
                case PresentationMarkerNodeRecord marker when patch["marker"] != null:
                    marker.Marker = ParseEnum<PresentationMarkerKind>(patch, "marker");
                    break;
                case PresentationForkNodeRecord fork when patch["joinNodeId"] != null:
                    fork.JoinNodeId = patch.Value<string>("joinNodeId");
                    break;
            }
        }

        private static void BindNodeAsset(
            PresentationNodeRecord node,
            string assetPath,
            IReadOnlyDictionary<string, UnityEngine.Object> stagedAssets)
        {
            UnityEngine.Object staged = null;
            if (!string.IsNullOrWhiteSpace(assetPath) && stagedAssets != null)
                stagedAssets.TryGetValue(assetPath, out staged);
            switch (node)
            {
                case PresentationProjectileNodeRecord projectile:
                    projectile.Profile = staged as ProjectileVisualProfile ??
                        LoadOptional<ProjectileVisualProfile>(assetPath);
                    break;
                case PresentationPrefabFxNodeRecord prefabFx:
                    prefabFx.Profile = staged as VisualCueProfile ?? LoadOptional<VisualCueProfile>(assetPath);
                    break;
                case PresentationProceduralVfxNodeRecord procedural:
                    procedural.Recipe = staged as SkillVfxRecipe ?? LoadOptional<SkillVfxRecipe>(assetPath);
                    break;
                default:
                    throw new InvalidOperationException("NodeDoesNotSupportAssetBinding.");
            }
        }

        private static void ReplacePreviewScenario(BattlePresentationGraph graph, JArray phases)
        {
            graph.PreviewPhases.Clear();
            if (phases == null)
                return;
            foreach (JObject value in phases.OfType<JObject>())
            {
                var phase = new PresentationPreviewPhaseRecord
                {
                    ContinuationCue = ParseEnum<PresentationCueKind>(value, "continuationCue"),
                    AdvanceKind = ParseEnum<PresentationPreviewAdvanceKind>(value, "advanceKind"),
                    PlayTargetHitReaction = value.Value<bool?>("playTargetHitReaction") == true
                };
                foreach (JToken cue in value["cues"] as JArray ?? new JArray())
                {
                    if (!Enum.TryParse(cue.Value<string>(), true, out PresentationCueKind parsed))
                        throw new ArgumentException($"Invalid preview cue: '{cue}'.");
                    phase.Cues.Add(parsed);
                }
                graph.PreviewPhases.Add(phase);
            }
        }

        private static JObject Snapshot(BattlePresentationGraph graph)
        {
            var nodes = new JArray(graph.Nodes.Where(node => node != null)
                .OrderBy(node => node.NodeId, StringComparer.Ordinal)
                .Select(node => NodeSnapshot(graph, node)));
            var edges = new JArray(graph.Edges.Where(edge => edge != null)
                .OrderBy(edge => edge.EdgeId, StringComparer.Ordinal)
                .Select(edge => new JObject
                {
                    ["edgeId"] = edge.EdgeId,
                    ["sourceNodeId"] = edge.SourceNodeId,
                    ["targetNodeId"] = edge.TargetNodeId
                }));
            string path = AssetDatabase.GetAssetPath(graph);
            return new JObject
            {
                ["guid"] = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path),
                ["path"] = path,
                ["displayName"] = graph.DisplayName,
                ["version"] = graph.Version,
                ["defaultPreviewEntry"] = graph.DefaultPreviewEntry.ToString(),
                ["previewActor"] = AssetReference(graph.PreviewActorPrefab),
                ["previewTarget"] = AssetReference(graph.PreviewTargetPrefab),
                ["previewScenario"] = new JArray(graph.PreviewPhases.Select(phase => new JObject
                {
                    ["cues"] = new JArray(phase.Cues.Select(cue => cue.ToString())),
                    ["continuationCue"] = phase.ContinuationCue.ToString(),
                    ["advanceKind"] = phase.AdvanceKind.ToString(),
                    ["playTargetHitReaction"] = phase.PlayTargetHitReaction
                })),
                ["nodes"] = nodes,
                ["edges"] = edges
            };
        }

        private static JObject NodeSnapshot(BattlePresentationGraph graph, PresentationNodeRecord node)
        {
            var result = new JObject
            {
                ["nodeId"] = node.NodeId,
                ["nodeType"] = node.NodeType.ToString(),
                ["enabled"] = node.Enabled,
                ["x"] = node.Position.x,
                ["y"] = node.Position.y
            };
            switch (node)
            {
                case PresentationEntryNodeRecord entry: result["cue"] = entry.Cue.ToString(); break;
                case PresentationUnitTweenNodeRecord tween:
                    result["action"] = tween.Action.ToString();
                    result["emitReleaseMarker"] = tween.EmitReleaseMarker;
                    result["profile"] = AssetReference(GetPreviewTweenProfile(graph));
                    break;
                case PresentationProjectileNodeRecord projectile:
                    result["profile"] = AssetReference(projectile.Profile);
                    result["speed"] = projectile.Speed;
                    result["fallbackTravelTime"] = projectile.FallbackTravelTime;
                    result["emitImpactMarker"] = projectile.EmitImpactMarker;
                    break;
                case PresentationPrefabFxNodeRecord prefabFx: result["profile"] = AssetReference(prefabFx.Profile); break;
                case PresentationProceduralVfxNodeRecord procedural:
                    result["recipe"] = AssetReference(procedural.Recipe);
                    result["cue"] = procedural.Cue.ToString();
                    break;
                case PresentationDelayNodeRecord delay: result["duration"] = delay.Duration; break;
                case PresentationMarkerNodeRecord marker: result["marker"] = marker.Marker.ToString(); break;
                case PresentationForkNodeRecord fork: result["joinNodeId"] = fork.JoinNodeId; break;
            }
            return result;
        }

        private static JArray Dependencies(BattlePresentationGraph graph)
        {
            var result = new JArray();
            foreach (string path in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(graph), false)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                result.Add(new JObject { ["guid"] = AssetDatabase.AssetPathToGUID(path), ["path"] = path });
            }
            return result;
        }

        private static JArray SharedReferences(BattlePresentationGraph graph)
        {
            var result = new JArray();
            UnityEngine.Object[] leaves = EnumerateGraphLeafAssets(graph).ToArray();
            string[] graphGuids = AssetDatabase.FindAssets("t:BattlePresentationGraph");
            foreach (UnityEngine.Object leaf in leaves)
            {
                var references = new JArray();
                foreach (string graphGuid in graphGuids)
                {
                    string graphPath = AssetDatabase.GUIDToAssetPath(graphGuid);
                    BattlePresentationGraph candidate = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(graphPath);
                    if (candidate == null)
                        continue;
                    foreach (PresentationNodeRecord node in candidate.Nodes.Where(node =>
                                 NodeReferencesLeaf(candidate, node, leaf)))
                    {
                        references.Add(new JObject
                        {
                            ["graphGuid"] = graphGuid,
                            ["graphPath"] = graphPath,
                            ["nodeId"] = node.NodeId
                        });
                    }
                }
                string leafPath = AssetDatabase.GetAssetPath(leaf);
                result.Add(new JObject
                {
                    ["guid"] = AssetDatabase.AssetPathToGUID(leafPath),
                    ["path"] = leafPath,
                    ["references"] = references
                });
            }
            return result;
        }

        private static JArray LeafAssets(BattlePresentationGraph graph)
        {
            var result = new JArray();
            foreach (UnityEngine.Object leaf in EnumerateGraphLeafAssets(graph))
            {
                string leafPath = AssetDatabase.GetAssetPath(leaf);
                var references = new JArray();
                foreach (string graphGuid in AssetDatabase.FindAssets("t:BattlePresentationGraph"))
                {
                    string graphPath = AssetDatabase.GUIDToAssetPath(graphGuid);
                    BattlePresentationGraph candidate = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(graphPath);
                    if (candidate == null)
                        continue;
                    foreach (PresentationNodeRecord node in candidate.Nodes.Where(node =>
                                 NodeReferencesLeaf(candidate, node, leaf)))
                    {
                        references.Add(new JObject
                        {
                            ["graphGuid"] = graphGuid,
                            ["graphPath"] = graphPath,
                            ["nodeId"] = node.NodeId
                        });
                    }
                }

                result.Add(new JObject
                {
                    ["guid"] = AssetDatabase.AssetPathToGUID(leafPath),
                    ["path"] = leafPath,
                    ["type"] = leaf.GetType().Name,
                    ["revision"] = AssetRevision(leaf),
                    ["references"] = references
                });
            }
            return result;
        }

        private static IEnumerable<UnityEngine.Object> EnumerateGraphLeafAssets(BattlePresentationGraph graph)
        {
            IEnumerable<UnityEngine.Object> nodeAssets = graph.Nodes
                .Select(GetLeafAssetForSnapshot)
                .Where(value => value != null);
            StandardUnitTweenProfile tweenProfile = GetPreviewTweenProfile(graph);
            return tweenProfile != null
                ? nodeAssets.Append(tweenProfile).Distinct()
                : nodeAssets.Distinct();
        }

        private static bool NodeReferencesLeaf(
            BattlePresentationGraph graph,
            PresentationNodeRecord node,
            UnityEngine.Object leaf)
        {
            if (GetLeafAssetForSnapshot(node) == leaf)
                return true;
            return node is PresentationUnitTweenNodeRecord &&
                   GetPreviewTweenProfile(graph) == leaf;
        }

        private static StandardUnitTweenProfile GetPreviewTweenProfile(BattlePresentationGraph graph)
        {
            GameObject actor = graph?.PreviewActorPrefab;
            if (actor == null)
                return null;
            UnitTweenVisual visual = actor.GetComponent<UnitTweenVisual>();
            return visual != null ? visual.Profile : null;
        }

        private static UnityEngine.Object GetLeafAssetForSnapshot(PresentationNodeRecord node)
        {
            return node switch
            {
                PresentationProjectileNodeRecord projectile => projectile.Profile,
                PresentationPrefabFxNodeRecord prefabFx => prefabFx.Profile,
                PresentationProceduralVfxNodeRecord procedural => procedural.Recipe,
                _ => null
            };
        }

        private static JArray Validate(BattlePresentationGraph graph)
        {
            var result = new JArray();
            BattlePresentationGraphValidation.Validate(graph, out List<PresentationGraphDiagnostic> graphErrors);
            foreach (PresentationGraphDiagnostic error in graphErrors)
                result.Add(new JObject { ["code"] = error.Code, ["nodeId"] = error.NodeId, ["message"] = error.Message });
            PresentationPreviewScenarioValidation.Validate(graph, out List<string> scenarioErrors);
            foreach (string error in scenarioErrors)
                result.Add(new JObject { ["code"] = "PreviewScenario", ["message"] = error });
            return result;
        }

        private static JObject AssetReference(UnityEngine.Object asset)
        {
            if (asset == null)
                return null;
            string path = AssetDatabase.GetAssetPath(asset);
            var result = new JObject
            {
                ["guid"] = AssetDatabase.AssetPathToGUID(path),
                ["path"] = path,
                ["type"] = asset.GetType().Name
            };
            if (IsSupportedLeafType(asset.GetType()))
                result["revision"] = AssetRevision(asset);
            return result;
        }

        private static string RevisionFromSnapshot(JObject snapshot)
        {
            JObject content = ContentSnapshot(snapshot);
            using var sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(content.ToString(Formatting.None)));
            return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static JObject ContentSnapshot(JObject snapshot)
        {
            var content = (JObject)snapshot.DeepClone();
            content.Remove("guid");
            content.Remove("path");
            content.Remove("revision");
            content.Remove("diagnostics");
            content.Remove("dependencies");
            RemoveNonIdentityReferenceFields(content);
            return content;
        }

        private static void RemoveNonIdentityReferenceFields(JToken value)
        {
            if (value is JObject objectValue)
            {
                bool assetReference = objectValue["guid"] != null && objectValue["path"] != null;
                foreach (JProperty property in objectValue.Properties().ToList())
                {
                    if (property.Name == "revision" || assetReference && property.Name == "path")
                        property.Remove();
                    else
                        RemoveNonIdentityReferenceFields(property.Value);
                }
                return;
            }
            if (value is JArray arrayValue)
            {
                foreach (JToken child in arrayValue)
                    RemoveNonIdentityReferenceFields(child);
            }
        }

        private static List<PreparedAsset> PrepareAssetChanges(JArray changes)
        {
            var result = new List<PreparedAsset>();
            if (changes == null)
                return result;
            try
            {
                foreach (JObject change in changes.OfType<JObject>())
                {
                    string kind = RequireString(change, "kind");
                    string path;
                    UnityEngine.Object source = null;
                    UnityEngine.Object sandbox;
                    switch (kind)
                    {
                        case "createLeafAsset":
                            path = RequireString(change, "assetPath");
                            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                                throw new InvalidOperationException($"AssetAlreadyExists: '{path}'.");
                            sandbox = ScriptableObject.CreateInstance(ResolveLeafAssetType(
                                RequireString(change, "assetType")));
                            break;
                        case "copyLeafAsset":
                        {
                            string sourcePath = RequireString(change, "sourcePath");
                            path = RequireString(change, "assetPath");
                            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                                throw new InvalidOperationException($"AssetAlreadyExists: '{path}'.");
                            UnityEngine.Object copySource = LoadSupportedLeaf(sourcePath);
                            CheckExpectedAssetRevision(copySource, change);
                            sandbox = UnityEngine.Object.Instantiate(copySource);
                            break;
                        }
                        case "updateLeafAsset":
                            path = RequireString(change, "assetPath");
                            source = LoadSupportedLeaf(path);
                            CheckExpectedAssetRevision(source, change);
                            sandbox = UnityEngine.Object.Instantiate(source);
                            break;
                        default:
                            throw new InvalidOperationException($"UnsupportedAssetChange: '{kind}'.");
                    }

                    sandbox.name = Path.GetFileNameWithoutExtension(path);
                    sandbox.hideFlags = HideFlags.HideAndDontSave;
                    result.Add(new PreparedAsset(kind, path, source, sandbox));
                    PatchLeafAsset(sandbox, change["fields"] as JObject);
                    if (change["replaceRecipeBindings"] != null)
                    {
                        if (sandbox is not SkillVfxRecipe recipe)
                            throw new InvalidOperationException("replaceRecipeBindings requires SkillVfxRecipe.");
                        ReplaceRecipeBindings(recipe, change["replaceRecipeBindings"] as JArray);
                    }
                }
                return result;
            }
            catch
            {
                DestroyPreparedAssets(result);
                throw;
            }
        }

        private static void ReplaceRecipeBindings(SkillVfxRecipe recipe, JArray bindings)
        {
            if (bindings == null)
                throw new ArgumentException("'replaceRecipeBindings' must be an array.");
            var serialized = new SerializedObject(recipe);
            SerializedProperty bindingList = serialized.FindProperty("_bindings")
                ?? throw new InvalidOperationException("LeafFieldMissing: '_bindings'.");
            var seenCues = new HashSet<SkillVfxCueKind>();
            bindingList.arraySize = bindings.Count;
            for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
            {
                if (bindings[bindingIndex] is not JObject binding)
                    throw new ArgumentException($"Recipe binding {bindingIndex} must be an object.");
                SkillVfxCueKind cue = ParseEnum<SkillVfxCueKind>(binding, "cue");
                if (!seenCues.Add(cue))
                    throw new ArgumentException($"Duplicate recipe cue: '{cue}'.");
                SerializedProperty bindingProperty = bindingList.GetArrayElementAtIndex(bindingIndex);
                SetSerializedValue(bindingProperty.FindPropertyRelative("_cue"), cue.ToString());
                JArray layers = binding["layers"] as JArray
                    ?? throw new ArgumentException($"Recipe binding '{cue}' requires a layers array.");
                SerializedProperty layerList = bindingProperty.FindPropertyRelative("_layers");
                layerList.arraySize = layers.Count;
                for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
                {
                    if (layers[layerIndex] is not JObject layer)
                        throw new ArgumentException($"Recipe layer {bindingIndex}:{layerIndex} must be an object.");
                    ValidateRecipeLayer(layer, cue, layerIndex);
                    SerializedProperty layerProperty = layerList.GetArrayElementAtIndex(layerIndex);
                    foreach (JProperty field in layer.Properties())
                    {
                        string propertyName = "_" + char.ToLowerInvariant(field.Name[0]) + field.Name.Substring(1);
                        SerializedProperty target = layerProperty.FindPropertyRelative(propertyName)
                            ?? throw new InvalidOperationException(
                                $"UnsupportedRecipeLayerField: '{field.Name}'.");
                        SetSerializedValue(target, field.Value);
                    }
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateRecipeLayer(JObject layer, SkillVfxCueKind cue, int layerIndex)
        {
            foreach (JProperty field in layer.Properties().Where(value =>
                         value.Value.Type is JTokenType.Float or JTokenType.Integer))
            {
                double number = field.Value.Value<double>();
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new ArgumentException($"Recipe field '{field.Name}' must be finite.");
            }
            SkillVfxPrimitiveKind primitiveKind = ParseEnum<SkillVfxPrimitiveKind>(layer, "primitiveKind");
            if (layer["blendMode"] != null)
                ParseEnum<SkillVfxBlendMode>(layer, "blendMode");
            if (layer["shapeMode"] != null)
                ParseEnum<SkillVfxShapeMode>(layer, "shapeMode");
            float duration = layer.Value<float?>("duration") ?? 0.15f;
            float peakTime = layer.Value<float?>("peakTime") ?? 0.05f;
            float middleTime = layer.Value<float?>("middleTime") ?? 0.04f;
            float blocking = layer.Value<float?>("blockingMarker") ?? 0f;
            if (duration < 0.01f || peakTime < 0f || peakTime > duration ||
                middleTime < 0f || middleTime > peakTime || blocking < 0f || blocking > duration)
            {
                throw new ArgumentException(
                    $"Invalid recipe timing for '{cue}' layer {layerIndex}: " +
                    "0 <= middleTime <= peakTime <= duration and 0 <= blockingMarker <= duration are required.");
            }
            if ((primitiveKind is SkillVfxPrimitiveKind.ParticleBurst or
                    SkillVfxPrimitiveKind.ProjectileGhostTrail) && blocking > 0f)
            {
                throw new ArgumentException(
                    $"Recipe '{primitiveKind}' layer {layerIndex} must use blockingMarker 0.");
            }
            foreach (string name in new[] { "startSize", "middleSize", "peakSize", "endSize", "emission",
                         "rootWidth", "tipWidth", "particleSize", "particleSpeed", "particleDrag" })
            {
                if (layer.Value<float?>(name) is float value && value < 0f)
                    throw new ArgumentException($"Recipe field '{name}' cannot be negative.");
            }
            foreach (string name in new[] { "startAlpha", "middleAlpha", "peakAlpha", "endAlpha",
                         "radialInner", "radialOuter", "softness" })
            {
                if (layer.Value<float?>(name) is float value && (value < 0f || value > 1f))
                    throw new ArgumentException($"Recipe field '{name}' must be in [0, 1].");
            }
            if (layer.Value<int?>("particleCount") is int particleCount && particleCount < 0)
                throw new ArgumentException("Recipe particleCount cannot be negative.");
            if (layer.Value<int?>("maximumInstances") is int maximumInstances &&
                (maximumInstances < 1 || maximumInstances > 32))
            {
                throw new ArgumentException("Recipe maximumInstances must be in [1, 32].");
            }
            if (layer.Value<int?>("sortingOrderOffset") is int sortingOrder &&
                (sortingOrder < -50 || sortingOrder > 100))
            {
                throw new ArgumentException("Recipe sortingOrderOffset must be in [-50, 100].");
            }
            float lifetimeMin = layer.Value<float?>("particleLifetimeMin") ?? 0.12f;
            float lifetimeMax = layer.Value<float?>("particleLifetimeMax") ?? 0.18f;
            if (lifetimeMin < 0.01f || lifetimeMax < lifetimeMin)
                throw new ArgumentException("Recipe particle lifetime requires 0.01 <= min <= max.");
            float radialInner = layer.Value<float?>("radialInner") ?? 0.5f;
            float radialOuter = layer.Value<float?>("radialOuter") ?? 1f;
            float softness = layer.Value<float?>("softness") ?? 0.12f;
            if (radialInner < 0f || radialOuter < 0.01f || radialOuter > 1f || radialInner > radialOuter)
                throw new ArgumentException("Recipe radial range requires 0 <= inner <= outer <= 1.");
            if (softness < 0.001f || softness > 0.5f)
                throw new ArgumentException("Recipe softness must be in [0.001, 0.5].");
            float rootWidth = layer.Value<float?>("rootWidth") ?? 0.045f;
            float tipWidth = layer.Value<float?>("tipWidth") ?? 0.01f;
            if (rootWidth < 0.001f || tipWidth < 0.001f || tipWidth > rootWidth)
                throw new ArgumentException("Recipe widths require 0.001 <= tipWidth <= rootWidth.");
            foreach (string colorName in new[] { "color", "secondaryColor" })
            {
                if (layer[colorName] is JObject color)
                    ParseColor(color);
            }
        }

        private static void PatchLeafAsset(UnityEngine.Object asset, JObject fields)
        {
            if (fields == null)
                return;
            IReadOnlyDictionary<string, string> allowed = LeafFieldMap(asset.GetType());
            var serialized = new SerializedObject(asset);
            foreach (JProperty field in fields.Properties())
            {
                if (!allowed.TryGetValue(field.Name, out string propertyName))
                    throw new InvalidOperationException(
                        $"UnsupportedLeafField: '{asset.GetType().Name}.{field.Name}'.");
                SerializedProperty property = serialized.FindProperty(propertyName)
                    ?? throw new InvalidOperationException($"LeafFieldMissing: '{propertyName}'.");
                SetSerializedValue(property, field.Value);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IReadOnlyDictionary<string, string> LeafFieldMap(Type type)
        {
            if (type == typeof(ProjectileVisualProfile))
                return Map("visualKind", "sprite", "material", "flightPrefab", "impactPrefab",
                    "impactLifetime", "impactScale", "tint", "scale", "trajectoryStyle", "arcHeight",
                    "rotateAlongTangent", "pulseAmount", "pulseCycles", "sortingOrderOffset");
            if (type == typeof(VisualCueProfile))
                return Map("prefab", "anchor", "completionPolicy", "lifetime", "scale",
                    "sortingOrderOffset", "orientationMode", "stretchXToSourceTarget", "referenceDistance");
            if (type == typeof(StandardUnitTweenProfile))
                return Map("idleDuration", "idleLift", "idleScaleAmount", "moveCycleDuration",
                    "moveTiltDegrees", "moveLift", "moveSway", "moveSettleDuration",
                    "meleeWindupDuration", "meleeLungeDuration", "meleeImpactHold",
                    "meleeRecoverDuration", "meleeLungeDistance", "rangedAimDuration",
                    "rangedReleaseDuration", "rangedRecoverDuration", "rangedRecoilDistance",
                    "castChargeDuration", "castReleaseHold", "castRecoverDuration", "hitRecoilDuration",
                    "hitShakeDuration", "hitRecoverDuration", "hitRecoilDistance", "hitRotationDegrees",
                    "lethalShakeDuration", "lethalCollapseDuration", "lethalCollapseScaleX",
                    "lethalCollapseScaleY", "corpseDropDuration", "corpseImpactDuration",
                    "corpseSettleDuration", "corpseStartHeight");
            if (type == typeof(SkillVfxRecipe))
                return Map("transparentMaterial", "additiveMaterial");
            throw new InvalidOperationException($"UnsupportedLeafAssetType: '{type.Name}'.");
        }

        private static Dictionary<string, string> Map(params string[] names)
        {
            return names.ToDictionary(
                name => name,
                name => "_" + char.ToLowerInvariant(name[0]) + name.Substring(1),
                StringComparer.Ordinal);
        }

        private static void SetSerializedValue(SerializedProperty property, JToken value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    property.boolValue = value.Value<bool>();
                    break;
                case SerializedPropertyType.Integer:
                    property.longValue = value.Value<long>();
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = value.Value<float>();
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = value.Value<string>();
                    break;
                case SerializedPropertyType.Enum:
                {
                    string enumName = value.Value<string>();
                    int index = Array.FindIndex(property.enumNames,
                        name => string.Equals(name, enumName, StringComparison.OrdinalIgnoreCase));
                    if (index < 0)
                        throw new ArgumentException($"Invalid enum value '{enumName}' for {property.displayName}.");
                    property.enumValueIndex = index;
                    break;
                }
                case SerializedPropertyType.Color:
                    property.colorValue = ParseColor(value as JObject);
                    break;
                case SerializedPropertyType.ObjectReference:
                {
                    string path = value.Type == JTokenType.Null ? null : value.Value<string>();
                    UnityEngine.Object referencedAsset = string.IsNullOrWhiteSpace(path)
                        ? null
                        : AssetDatabase.LoadMainAssetAtPath(path);
                    if (!string.IsNullOrWhiteSpace(path) && referencedAsset == null)
                        throw new InvalidOperationException($"AssetNotFound: '{path}'.");
                    property.objectReferenceValue = referencedAsset;
                    if (referencedAsset != null && property.objectReferenceValue != referencedAsset)
                    {
                        throw new InvalidOperationException(
                            $"AssetTypeMismatch: '{path}' is incompatible with '{property.displayName}'.");
                    }
                    break;
                }
                default:
                    throw new InvalidOperationException(
                        $"UnsupportedLeafFieldType: '{property.propertyType}'.");
            }
        }

        private static Color ParseColor(JObject value)
        {
            if (value == null)
                throw new ArgumentException("Color value must be an object with r/g/b/a fields.");
            float red = value.Value<float>("r");
            float green = value.Value<float>("g");
            float blue = value.Value<float>("b");
            float alpha = value.Value<float?>("a") ?? 1f;
            if (!float.IsFinite(red) || !float.IsFinite(green) || !float.IsFinite(blue) ||
                !float.IsFinite(alpha) || red < 0f || green < 0f || blue < 0f ||
                alpha < 0f || alpha > 1f)
            {
                throw new ArgumentException(
                    "Color channels must be finite, RGB non-negative, and alpha in [0, 1].");
            }
            return new Color(red, green, blue, alpha);
        }

        private static Type ResolveLeafAssetType(string name)
        {
            return name switch
            {
                "StandardUnitTweenProfile" => typeof(StandardUnitTweenProfile),
                "ProjectileVisualProfile" => typeof(ProjectileVisualProfile),
                "VisualCueProfile" => typeof(VisualCueProfile),
                "SkillVfxRecipe" => typeof(SkillVfxRecipe),
                _ => throw new InvalidOperationException($"UnsupportedLeafAssetType: '{name}'.")
            };
        }

        private static UnityEngine.Object LoadSupportedLeaf(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
                throw new InvalidOperationException($"AssetNotFound: '{path}'.");
            LeafFieldMap(asset.GetType());
            return asset;
        }

        private static void CheckExpectedAssetRevision(UnityEngine.Object asset, JObject change)
        {
            string expected = RequireString(change, "expectedRevision");
            string actual = AssetRevision(asset);
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"RevisionConflict: expected '{expected}', actual '{actual}' for '{AssetDatabase.GetAssetPath(asset)}'.");
        }

        private static string AssetRevision(UnityEngine.Object asset)
        {
            using var sha = SHA256.Create();
            string json = SerializedAssetSnapshot(asset).ToString(Formatting.None);
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
            return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static JObject SerializedAssetSnapshot(UnityEngine.Object asset)
        {
            var result = new JObject { ["type"] = asset.GetType().FullName };
            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (property.propertyPath is "m_Script" or "m_Name" or "m_ObjectHideFlags" or
                    "m_EditorClassIdentifier" || property.propertyType == SerializedPropertyType.Generic)
                    continue;
                result[property.propertyPath] = SerializedPropertySnapshot(property);
            }
            return result;
        }

        private static JToken SerializedPropertySnapshot(SerializedProperty property)
        {
            return property.propertyType switch
            {
                SerializedPropertyType.Boolean => property.boolValue,
                SerializedPropertyType.Integer => property.longValue,
                SerializedPropertyType.Float => property.doubleValue,
                SerializedPropertyType.String => property.stringValue,
                SerializedPropertyType.Enum => property.enumValueIndex >= 0 &&
                                               property.enumValueIndex < property.enumNames.Length
                    ? property.enumNames[property.enumValueIndex]
                    : property.enumValueIndex,
                SerializedPropertyType.Color => ColorSnapshot(property.colorValue),
                SerializedPropertyType.ObjectReference => AssetReferenceIdentity(property.objectReferenceValue),
                SerializedPropertyType.Vector2 => VectorSnapshot(property.vector2Value),
                SerializedPropertyType.Vector3 => VectorSnapshot(property.vector3Value),
                SerializedPropertyType.Vector4 => VectorSnapshot(property.vector4Value),
                SerializedPropertyType.Quaternion => QuaternionSnapshot(property.quaternionValue),
                SerializedPropertyType.ArraySize => property.intValue,
                _ => property.propertyType.ToString()
            };
        }

        private static JObject AssetReferenceIdentity(UnityEngine.Object asset)
        {
            if (asset == null)
                return null;
            string path = AssetDatabase.GetAssetPath(asset);
            return new JObject
            {
                ["guid"] = AssetDatabase.AssetPathToGUID(path),
                ["type"] = asset.GetType().FullName
            };
        }

        private static JObject ColorSnapshot(Color value) => new()
        {
            ["r"] = value.r, ["g"] = value.g, ["b"] = value.b, ["a"] = value.a
        };

        private static JObject VectorSnapshot(Vector2 value) => new() { ["x"] = value.x, ["y"] = value.y };
        private static JObject VectorSnapshot(Vector3 value) => new()
        {
            ["x"] = value.x, ["y"] = value.y, ["z"] = value.z
        };
        private static JObject VectorSnapshot(Vector4 value) => new()
        {
            ["x"] = value.x, ["y"] = value.y, ["z"] = value.z, ["w"] = value.w
        };
        private static JObject QuaternionSnapshot(Quaternion value) => new()
        {
            ["x"] = value.x, ["y"] = value.y, ["z"] = value.z, ["w"] = value.w
        };

        private static bool IsSupportedLeafType(Type type)
        {
            return type == typeof(StandardUnitTweenProfile) ||
                   type == typeof(ProjectileVisualProfile) ||
                   type == typeof(VisualCueProfile) ||
                   type == typeof(SkillVfxRecipe);
        }

        private static JArray AssetChangeResults(IEnumerable<PreparedAsset> assets)
        {
            return new JArray(assets.Select(asset => new JObject
            {
                ["kind"] = asset.Kind,
                ["path"] = asset.Path,
                ["revision"] = AssetRevision(asset.WasCreated
                    ? AssetDatabase.LoadMainAssetAtPath(asset.Path)
                    : asset.Sandbox)
            }));
        }

        private static void DestroyPreparedAssets(IEnumerable<PreparedAsset> assets)
        {
            if (assets == null)
                return;
            foreach (PreparedAsset asset in assets)
            {
                if (!asset.WasCreated && asset.Sandbox != null)
                    UnityEngine.Object.DestroyImmediate(asset.Sandbox);
            }
        }

        private static void EnsureAssetFolder(string assetPath, ICollection<string> createdFolders)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;
            string current = "Assets";
            foreach (string part in folder.Split('/').Skip(1))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, part);
                    createdFolders?.Add(next);
                }
                current = next;
            }
        }

        private sealed class PreparedChangeSet
        {
            internal PreparedChangeSet(
                JObject changeSet,
                string graphPath,
                BattlePresentationGraph graphSource,
                string beforeRevision,
                BattlePresentationGraph graphSandbox,
                List<PreparedAsset> assets,
                JArray operationResults,
                JArray diagnostics)
            {
                ChangeSet = changeSet;
                GraphPath = graphPath;
                GraphSource = graphSource;
                BeforeRevision = beforeRevision;
                GraphSandbox = graphSandbox;
                Assets = assets;
                OperationResults = operationResults;
                Diagnostics = diagnostics;
            }

            internal JObject ChangeSet { get; }
            internal string GraphPath { get; }
            internal BattlePresentationGraph GraphSource { get; }
            internal string BeforeRevision { get; }
            internal BattlePresentationGraph GraphSandbox { get; }
            internal List<PreparedAsset> Assets { get; }
            internal JArray OperationResults { get; }
            internal JArray Diagnostics { get; }
        }

        private sealed class PreparedAsset
        {
            internal PreparedAsset(string kind, string path, UnityEngine.Object source, UnityEngine.Object sandbox)
            {
                Kind = kind;
                Path = path;
                Source = source;
                Sandbox = sandbox;
            }

            internal string Kind { get; }
            internal string Path { get; }
            internal UnityEngine.Object Source { get; }
            internal UnityEngine.Object Sandbox { get; }
            internal bool WasCreated { get; set; }
        }

        private sealed class PreparedBackup
        {
            internal PreparedBackup(UnityEngine.Object snapshot, bool wasDirty)
            {
                Snapshot = snapshot;
                WasDirty = wasDirty;
            }

            internal UnityEngine.Object Snapshot { get; }
            internal bool WasDirty { get; }
        }

        private static BattlePresentationGraph LoadGraph(string path)
        {
            BattlePresentationGraph graph = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(path);
            return graph != null ? graph : throw new InvalidOperationException($"PresentationGraphNotFound: '{path}'.");
        }

        private static T LoadOptional<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null ? asset : throw new InvalidOperationException($"AssetNotFound: '{path}'.");
        }

        private static PresentationNodeRecord RequireNode(BattlePresentationGraph graph, JObject operation)
        {
            string id = RequireString(operation, "nodeId");
            return graph.FindNode(id) ?? throw new InvalidOperationException($"NodeNotFound: '{id}'.");
        }

        private static string RequireString(JObject value, string name)
        {
            string result = value.Value<string>(name);
            return !string.IsNullOrWhiteSpace(result)
                ? result
                : throw new ArgumentException($"'{name}' is required.");
        }

        private static T ParseEnum<T>(JObject value, string name) where T : struct
        {
            string text = RequireString(value, name);
            return ParseEnumValue<T>(text, name);
        }

        private static T ParseEnumValue<T>(string text, string name) where T : struct
        {
            return Enum.TryParse(text, true, out T result)
                ? result
                : throw new ArgumentException($"Invalid {name}: '{text}'.");
        }
    }
}
#endif
