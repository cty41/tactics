using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tactics.Editor.Migration
{
    /// <summary>
    /// Exports frozen Unity assets through AssetDatabase and SerializedObject without parsing Unity YAML.
    /// The resulting JSON is a disposable migration DTO, never a runtime content format.
    /// </summary>
    public static class TacticsContentExporter
    {
        private const string PoisonSpearSpecPath =
            "Tools/migration/manifest/export-batches/poison-spear-lv1.json";

        private const string PureRunUnitsSpecPath =
            "Tools/migration/manifest/export-batches/pure-run-units-v1.json";

        private const string PureRunBuffsItemsSpecPath =
            "Tools/migration/manifest/export-batches/pure-run-buffs-items-v1.json";

        private const string PureRunStartingSkillsSpecPath =
            "Tools/migration/manifest/export-batches/pure-run-starting-skills-v1.json";

        private const string PureRunAiEncounterSpecPath =
            "Tools/migration/manifest/export-batches/pure-run-ai-encounter-v1.json";

        private const string PureRunPersistenceSpecPath =
            "Tools/migration/manifest/export-batches/pure-run-persistence-v1.json";

        private const string PureRunUiInputSpecPath =
            "Tools/migration/manifest/export-batches/pure-run-ui-input-v1.json";

        private const string PureRunInventoryProgressionSpecPath =
            "Tools/migration/manifest/export-batches/pure-run-inventory-progression-v1.json";

        private const string PureRunLayer4MapNodesSpecPath =
            "Tools/migration/manifest/export-batches/pure-run-layer4-map-nodes-v1.json";

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Culture = CultureInfo.InvariantCulture,
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include
        };

        [MenuItem("Tactics/Migration/Export Poison Spear Lv1")]
        public static void ExportPoisonSpearBatch()
        {
            string outputPath = Export(PoisonSpearSpecPath);
            TLog.Info($"[Migration] Exported Poison Spear Lv1 DTO to '{outputPath}'.");
        }

        public static void ExportPoisonSpearBatchFromCommandLine()
        {
            ExportPoisonSpearBatch();
        }

        [MenuItem("Tactics/Migration/Export Pure Run Units V1")]
        public static void ExportPureRunUnitBatch()
        {
            string outputPath = Export(PureRunUnitsSpecPath);
            TLog.Info($"[Migration] Exported Pure Run Units V1 DTO to '{outputPath}'.");
        }

        public static void ExportPureRunUnitBatchFromCommandLine()
        {
            ExportPureRunUnitBatch();
        }

        [MenuItem("Tactics/Migration/Export Pure Run Buffs and Items V1")]
        public static void ExportPureRunBuffItemBatch()
        {
            string outputPath = Export(PureRunBuffsItemsSpecPath);
            TLog.Info($"[Migration] Exported Pure Run Buffs and Items V1 DTO to '{outputPath}'.");
        }

        public static void ExportPureRunBuffItemBatchFromCommandLine()
        {
            ExportPureRunBuffItemBatch();
        }

        [MenuItem("Tactics/Migration/Export Pure Run Starting Skills V1")]
        public static void ExportPureRunStartingSkillBatch()
        {
            string outputPath = Export(PureRunStartingSkillsSpecPath);
            TLog.Info($"[Migration] Exported Pure Run Starting Skills V1 DTO to '{outputPath}'.");
        }

        public static void ExportPureRunStartingSkillBatchFromCommandLine()
        {
            ExportPureRunStartingSkillBatch();
        }

        [MenuItem("Tactics/Migration/Export Pure Run AI and Encounter V1")]
        public static void ExportPureRunAiEncounterBatch()
        {
            string outputPath = Export(PureRunAiEncounterSpecPath);
            TLog.Info($"[Migration] Exported Pure Run AI and Encounter V1 DTO to '{outputPath}'.");
        }

        public static void ExportPureRunAiEncounterBatchFromCommandLine()
        {
            ExportPureRunAiEncounterBatch();
        }

        [MenuItem("Tactics/Migration/Export Pure Run Persistence V1")]
        public static void ExportPureRunPersistenceBatch()
        {
            string outputPath = Export(PureRunPersistenceSpecPath);
            TLog.Info($"[Migration] Exported Pure Run Persistence V1 DTO to '{outputPath}'.");
        }

        public static void ExportPureRunPersistenceBatchFromCommandLine()
        {
            ExportPureRunPersistenceBatch();
        }

        [MenuItem("Tactics/Migration/Export Pure Run UI and Input V1")]
        public static void ExportPureRunUiInputBatch()
        {
            string outputPath = Export(PureRunUiInputSpecPath);
            TLog.Info($"[Migration] Exported Pure Run UI and Input V1 DTO to '{outputPath}'.");
        }

        public static void ExportPureRunUiInputBatchFromCommandLine()
        {
            ExportPureRunUiInputBatch();
        }

        [MenuItem("Tactics/Migration/Export Pure Run Inventory and Progression V1")]
        public static void ExportPureRunInventoryProgressionBatch()
        {
            string outputPath = Export(PureRunInventoryProgressionSpecPath);
            TLog.Info($"[Migration] Exported Pure Run Inventory and Progression V1 DTO to '{outputPath}'.");
        }

        public static void ExportPureRunInventoryProgressionBatchFromCommandLine()
        {
            ExportPureRunInventoryProgressionBatch();
        }

        [MenuItem("Tactics/Migration/Export Pure Run Layer 4 Map and Nodes V1")]
        public static void ExportPureRunLayer4MapNodesBatch()
        {
            string outputPath = Export(PureRunLayer4MapNodesSpecPath);
            TLog.Info($"[Migration] Exported Pure Run Layer 4 Map and Nodes V1 DTO to '{outputPath}'.");
        }

        public static void ExportPureRunLayer4MapNodesBatchFromCommandLine()
        {
            ExportPureRunLayer4MapNodesBatch();
        }

        public static string Export(string relativeSpecPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Cannot resolve Unity project root.");
            string absoluteSpecPath = Path.Combine(projectRoot, NormalizeFilePath(relativeSpecPath));
            if (!File.Exists(absoluteSpecPath))
                throw new FileNotFoundException("Migration export specification is missing.", absoluteSpecPath);

            ExportSpecification specification = JsonConvert.DeserializeObject<ExportSpecification>(
                File.ReadAllText(absoluteSpecPath), JsonSettings)
                ?? throw new InvalidDataException($"Cannot parse migration export specification '{relativeSpecPath}'.");
            ValidateSpecification(specification);

            var document = new ExportDocument
            {
                SchemaVersion = specification.SchemaVersion,
                BatchId = specification.BatchId,
                ExporterVersion = specification.ExporterVersion,
                SourceTag = specification.SourceTag,
                SourceCommit = specification.SourceCommit,
                UnityVersion = Application.unityVersion,
                Assets = specification.Assets
                    .OrderBy(item => item.SourceKey, StringComparer.Ordinal)
                    .Select(ExportAsset)
                    .ToList()
            };

            string absoluteOutputPath = Path.Combine(projectRoot, NormalizeFilePath(specification.OutputPath));
            string outputDirectory = Path.GetDirectoryName(absoluteOutputPath)
                ?? throw new InvalidOperationException($"Cannot resolve output directory for '{absoluteOutputPath}'.");
            Directory.CreateDirectory(outputDirectory);

            string temporaryPath = absoluteOutputPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(document, JsonSettings) + Environment.NewLine);
            if (File.Exists(absoluteOutputPath))
                File.Replace(temporaryPath, absoluteOutputPath, null);
            else
                File.Move(temporaryPath, absoluteOutputPath);
            return absoluteOutputPath;
        }

        private static ExportedAsset ExportAsset(ExportAssetSpecification specification)
        {
            Object mainAsset = AssetDatabase.LoadMainAssetAtPath(specification.SourcePath);
            if (mainAsset == null)
                throw new InvalidOperationException($"Cannot load migration source '{specification.SourcePath}'.");
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mainAsset, out string guid, out long localFileId))
                throw new InvalidOperationException($"Cannot resolve GUID/LocalFileId for '{specification.SourcePath}'.");

            bool auditOnlyFile = string.Equals(
                specification.ExportMode,
                "audit-only-file",
                StringComparison.Ordinal);
            string absoluteSourcePath = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName
                    ?? throw new InvalidOperationException("Cannot resolve Unity project root."),
                NormalizeFilePath(specification.SourcePath));
            byte[] sourceBytes = auditOnlyFile
                ? File.ReadAllBytes(absoluteSourcePath)
                : Array.Empty<byte>();

            var result = new ExportedAsset
            {
                SourceKey = specification.SourceKey,
                SourcePath = specification.SourcePath,
                GitBlobSha1 = specification.GitBlobSha1,
                TargetContentIds = specification.TargetContentIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                SourceGuid = guid,
                SourceLocalFileId = localFileId,
                DependencyHash = AssetDatabase.GetAssetDependencyHash(specification.SourcePath).ToString(),
                MainAssetType = mainAsset.GetType().FullName ?? mainAsset.GetType().Name,
                ExportMode = auditOnlyFile ? "audit-only-file" : "serialized-object",
                SourceFileSha256 = auditOnlyFile ? ComputeSha256(sourceBytes) : null,
                SourceByteLength = auditOnlyFile ? sourceBytes.LongLength : 0,
                Objects = auditOnlyFile
                    ? new List<ExportedObject>()
                    : ExportObjects(specification.SourcePath, mainAsset),
                Dependencies = AssetDatabase.GetDependencies(specification.SourcePath, false)
                    .Where(path => !string.Equals(path, specification.SourcePath, StringComparison.Ordinal))
                    .Select(ExportDependency)
                    .OrderBy(item => item.SourcePath, StringComparer.Ordinal)
                    .ToList()
            };
            result.UnsupportedPropertyKinds = result.Objects
                .SelectMany(item => item.Properties)
                .Where(item => !item.Supported)
                .Select(item => item.PropertyType)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            return result;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return string.Concat(sha256.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static List<ExportedObject> ExportObjects(string sourcePath, Object mainAsset)
        {
            if (mainAsset is not GameObject)
            {
                var objects = new List<ExportedObject> { ExportObject("main", mainAsset) };
                if (mainAsset is Texture2D && AssetImporter.GetAtPath(sourcePath) is AssetImporter importer)
                    objects.Add(ExportObject("importer", importer));
                return objects.OrderBy(item => item.ObjectPath, StringComparer.Ordinal).ToList();
            }

            GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                var objects = new List<ExportedObject>();
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    string hierarchyPath = BuildHierarchyPath(transform, root.transform);
                    objects.Add(ExportObject($"{hierarchyPath}/GameObject", transform.gameObject));
                    Component[] components = transform.GetComponents<Component>();
                    for (int index = 0; index < components.Length; index++)
                    {
                        Component component = components[index];
                        if (component == null)
                        {
                            objects.Add(new ExportedObject
                            {
                                ObjectPath = $"{hierarchyPath}/MissingComponent#{index}",
                                ObjectType = "MissingComponent",
                                Properties = new List<ExportedProperty>()
                            });
                            continue;
                        }
                        objects.Add(ExportObject(
                            $"{hierarchyPath}/{component.GetType().FullName}#{index}",
                            component));
                    }
                }
                return objects.OrderBy(item => item.ObjectPath, StringComparer.Ordinal).ToList();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static ExportedObject ExportObject(string objectPath, Object source)
        {
            var serializedObject = new SerializedObject(source);
            SerializedProperty iterator = serializedObject.GetIterator();
            var properties = new List<ExportedProperty>();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyPath.EndsWith(".m_FileID", StringComparison.Ordinal) ||
                    iterator.propertyPath.EndsWith(".m_PathID", StringComparison.Ordinal))
                {
                    continue;
                }
                properties.Add(ExportProperty(iterator));
            }

            return new ExportedObject
            {
                ObjectPath = objectPath,
                ObjectType = source.GetType().FullName ?? source.GetType().Name,
                Properties = properties.OrderBy(item => item.PropertyPath, StringComparer.Ordinal).ToList()
            };
        }

        private static ExportedProperty ExportProperty(SerializedProperty property)
        {
            var exported = new ExportedProperty
            {
                PropertyPath = property.propertyPath,
                PropertyType = property.propertyType.ToString(),
                Supported = true
            };

            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        exported.Value = property.longValue.ToString(CultureInfo.InvariantCulture);
                        break;
                    case SerializedPropertyType.Boolean:
                        exported.Value = property.boolValue ? "true" : "false";
                        break;
                    case SerializedPropertyType.Float:
                        exported.Value = property.doubleValue.ToString("R", CultureInfo.InvariantCulture);
                        break;
                    case SerializedPropertyType.String:
                        exported.Value = property.stringValue;
                        break;
                    case SerializedPropertyType.Color:
                        exported.Value = Format(property.colorValue);
                        break;
                    case SerializedPropertyType.ObjectReference:
                        exported.Reference = ExportReference(property.objectReferenceValue);
                        break;
                    case SerializedPropertyType.LayerMask:
                        exported.Value = property.intValue.ToString(CultureInfo.InvariantCulture);
                        break;
                    case SerializedPropertyType.Enum:
                        exported.Value = property.enumValueIndex >= 0 && property.enumValueIndex < property.enumNames.Length
                            ? property.enumNames[property.enumValueIndex]
                            : property.enumValueIndex.ToString(CultureInfo.InvariantCulture);
                        break;
                    case SerializedPropertyType.Vector2:
                        exported.Value = Format(property.vector2Value);
                        break;
                    case SerializedPropertyType.Vector3:
                        exported.Value = Format(property.vector3Value);
                        break;
                    case SerializedPropertyType.Vector4:
                        exported.Value = Format(property.vector4Value);
                        break;
                    case SerializedPropertyType.Rect:
                        exported.Value = Format(property.rectValue);
                        break;
                    case SerializedPropertyType.ArraySize:
                        exported.Value = property.intValue.ToString(CultureInfo.InvariantCulture);
                        break;
                    case SerializedPropertyType.Character:
                        exported.Value = property.intValue.ToString(CultureInfo.InvariantCulture);
                        break;
                    case SerializedPropertyType.AnimationCurve:
                        exported.Value = Format(property.animationCurveValue);
                        break;
                    case SerializedPropertyType.Gradient:
                        exported.Value = Format(property.gradientValue);
                        break;
                    case SerializedPropertyType.Bounds:
                        exported.Value = Format(property.boundsValue);
                        break;
                    case SerializedPropertyType.Quaternion:
                        exported.Value = Format(property.quaternionValue);
                        break;
                    case SerializedPropertyType.Vector2Int:
                        exported.Value = Format(property.vector2IntValue);
                        break;
                    case SerializedPropertyType.Vector3Int:
                        exported.Value = Format(property.vector3IntValue);
                        break;
                    case SerializedPropertyType.RectInt:
                        exported.Value = Format(property.rectIntValue);
                        break;
                    case SerializedPropertyType.BoundsInt:
                        exported.Value = Format(property.boundsIntValue);
                        break;
                    case SerializedPropertyType.ManagedReference:
                        exported.Value = property.managedReferenceFullTypename;
                        break;
                    case SerializedPropertyType.Hash128:
                        exported.Value = property.hash128Value.ToString();
                        break;
                    case SerializedPropertyType.Generic:
                    case SerializedPropertyType.FixedBufferSize:
                        exported.Value = property.isArray
                            ? property.arraySize.ToString(CultureInfo.InvariantCulture)
                            : null;
                        break;
                    default:
                        exported.Supported = false;
                        break;
                }
            }
            catch (Exception exception)
            {
                exported.Supported = false;
                exported.Error = exception.GetType().Name;
            }
            return exported;
        }

        private static ExportedReference ExportReference(Object target)
        {
            if (target == null)
                return null;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string guid, out long localFileId);
            string sourcePath = AssetDatabase.GetAssetPath(target);
            return new ExportedReference
            {
                Name = target.name,
                ObjectType = target.GetType().FullName ?? target.GetType().Name,
                SourcePath = sourcePath,
                SourceGuid = guid,
                SourceLocalFileId = localFileId,
                GlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString(),
                DependencyHash = string.IsNullOrEmpty(sourcePath)
                    ? string.Empty
                    : AssetDatabase.GetAssetDependencyHash(sourcePath).ToString()
            };
        }

        private static ExportedDependency ExportDependency(string sourcePath)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(sourcePath);
            string guid = AssetDatabase.AssetPathToGUID(sourcePath);
            long localFileId = 0;
            if (asset != null)
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localFileId);
            return new ExportedDependency
            {
                SourcePath = sourcePath,
                SourceGuid = guid,
                SourceLocalFileId = localFileId,
                MainAssetType = asset == null ? string.Empty : asset.GetType().FullName ?? asset.GetType().Name
            };
        }

        private static string BuildHierarchyPath(Transform transform, Transform root)
        {
            var segments = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                segments.Push($"{current.name}[{current.GetSiblingIndex()}]");
                if (current == root)
                    break;
                current = current.parent;
            }
            return string.Join("/", segments);
        }

        private static string Format(Vector2 value) => FormattableString.Invariant($"{value.x:R},{value.y:R}");
        private static string Format(Vector3 value) => FormattableString.Invariant($"{value.x:R},{value.y:R},{value.z:R}");
        private static string Format(Vector4 value) => FormattableString.Invariant($"{value.x:R},{value.y:R},{value.z:R},{value.w:R}");
        private static string Format(Quaternion value) => FormattableString.Invariant($"{value.x:R},{value.y:R},{value.z:R},{value.w:R}");
        private static string Format(Color value) => FormattableString.Invariant($"{value.r:R},{value.g:R},{value.b:R},{value.a:R}");
        private static string Format(Rect value) => FormattableString.Invariant($"{value.x:R},{value.y:R},{value.width:R},{value.height:R}");
        private static string Format(Bounds value) => $"{Format(value.center)}|{Format(value.size)}";
        private static string Format(Vector2Int value) => FormattableString.Invariant($"{value.x},{value.y}");
        private static string Format(Vector3Int value) => FormattableString.Invariant($"{value.x},{value.y},{value.z}");
        private static string Format(RectInt value) => FormattableString.Invariant($"{value.x},{value.y},{value.width},{value.height}");
        private static string Format(BoundsInt value) => $"{Format(value.position)}|{Format(value.size)}";
        private static string Format(AnimationCurve value) => string.Join(";", value.keys.Select(key =>
            FormattableString.Invariant($"{key.time:R},{key.value:R},{key.inTangent:R},{key.outTangent:R},{key.inWeight:R},{key.outWeight:R},{(int)key.weightedMode}")));
        private static string Format(Gradient value)
        {
            string colors = string.Join(";", value.colorKeys.Select(key =>
                FormattableString.Invariant($"{key.time:R},{Format(key.color)}")));
            string alphas = string.Join(";", value.alphaKeys.Select(key =>
                FormattableString.Invariant($"{key.time:R},{key.alpha:R}")));
            return $"{value.mode}|{value.colorSpace}|{colors}|{alphas}";
        }

        private static void ValidateSpecification(ExportSpecification specification)
        {
            if (specification.SchemaVersion != 1)
                throw new InvalidDataException($"Unsupported export specification schema {specification.SchemaVersion}.");
            if (string.IsNullOrWhiteSpace(specification.BatchId) ||
                string.IsNullOrWhiteSpace(specification.ExporterVersion) ||
                string.IsNullOrWhiteSpace(specification.SourceTag) ||
                string.IsNullOrWhiteSpace(specification.SourceCommit) ||
                string.IsNullOrWhiteSpace(specification.OutputPath))
            {
                throw new InvalidDataException("Migration export specification has empty required fields.");
            }
            if (specification.Assets == null || specification.Assets.Count == 0)
                throw new InvalidDataException("Migration export specification contains no assets.");
            if (specification.Assets.Select(item => item.SourceKey).Distinct(StringComparer.Ordinal).Count() !=
                specification.Assets.Count)
            {
                throw new InvalidDataException("Migration export specification contains duplicate source keys.");
            }
            foreach (ExportAssetSpecification asset in specification.Assets)
            {
                if (string.IsNullOrWhiteSpace(asset.SourceKey) || string.IsNullOrWhiteSpace(asset.SourcePath) ||
                    string.IsNullOrWhiteSpace(asset.GitBlobSha1) || asset.TargetContentIds == null ||
                    asset.TargetContentIds.Length == 0)
                {
                    throw new InvalidDataException($"Invalid export asset specification '{asset.SourceKey}'.");
                }
                if (!string.IsNullOrEmpty(asset.ExportMode) &&
                    !string.Equals(asset.ExportMode, "serialized-object", StringComparison.Ordinal) &&
                    !string.Equals(asset.ExportMode, "audit-only-file", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Invalid export mode '{asset.ExportMode}' for '{asset.SourceKey}'.");
                }
            }
        }

        private static string NormalizeFilePath(string path) =>
            path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        [Serializable]
        private sealed class ExportSpecification
        {
            public int SchemaVersion { get; set; }
            public string BatchId { get; set; }
            public string ExporterVersion { get; set; }
            public string SourceTag { get; set; }
            public string SourceCommit { get; set; }
            public string OutputPath { get; set; }
            public List<ExportAssetSpecification> Assets { get; set; }
        }

        [Serializable]
        private sealed class ExportAssetSpecification
        {
            public string SourceKey { get; set; }
            public string SourcePath { get; set; }
            public string GitBlobSha1 { get; set; }
            public string[] TargetContentIds { get; set; }
            public string ExportMode { get; set; }
        }

        [Serializable]
        private sealed class ExportDocument
        {
            public int SchemaVersion { get; set; }
            public string BatchId { get; set; }
            public string ExporterVersion { get; set; }
            public string SourceTag { get; set; }
            public string SourceCommit { get; set; }
            public string UnityVersion { get; set; }
            public List<ExportedAsset> Assets { get; set; }
        }

        [Serializable]
        private sealed class ExportedAsset
        {
            public string SourceKey { get; set; }
            public string SourcePath { get; set; }
            public string GitBlobSha1 { get; set; }
            public string[] TargetContentIds { get; set; }
            public string SourceGuid { get; set; }
            public long SourceLocalFileId { get; set; }
            public string DependencyHash { get; set; }
            public string MainAssetType { get; set; }
            public string ExportMode { get; set; }
            public string SourceFileSha256 { get; set; }
            public long SourceByteLength { get; set; }
            public List<ExportedObject> Objects { get; set; }
            public List<ExportedDependency> Dependencies { get; set; }
            public string[] UnsupportedPropertyKinds { get; set; }
        }

        [Serializable]
        private sealed class ExportedObject
        {
            public string ObjectPath { get; set; }
            public string ObjectType { get; set; }
            public List<ExportedProperty> Properties { get; set; }
        }

        [Serializable]
        private sealed class ExportedProperty
        {
            public string PropertyPath { get; set; }
            public string PropertyType { get; set; }
            public bool Supported { get; set; }
            public string Value { get; set; }
            public ExportedReference Reference { get; set; }
            public string Error { get; set; }
        }

        [Serializable]
        private sealed class ExportedReference
        {
            public string Name { get; set; }
            public string ObjectType { get; set; }
            public string SourcePath { get; set; }
            public string SourceGuid { get; set; }
            public long SourceLocalFileId { get; set; }
            public string GlobalObjectId { get; set; }
            public string DependencyHash { get; set; }
        }

        [Serializable]
        private sealed class ExportedDependency
        {
            public string SourcePath { get; set; }
            public string SourceGuid { get; set; }
            public long SourceLocalFileId { get; set; }
            public string MainAssetType { get; set; }
        }
    }
}
