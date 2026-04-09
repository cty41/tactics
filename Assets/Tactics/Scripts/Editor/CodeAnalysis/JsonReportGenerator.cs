#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Tactics.CodeAnalysis
{
    /// <summary>
    /// Generates JSON report for code analysis results using Newtonsoft.Json.
    /// </summary>
    public class JsonReportGenerator
    {
        private readonly string _outputDirectory;

        public JsonReportGenerator(string outputDirectory)
        {
            _outputDirectory = outputDirectory;
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        /// <summary>
        /// Generates complete analysis report with all sections populated.
        /// </summary>
        public void GenerateReport(
            RoslynCodeAnalyzer analyzer,
            HashSet<string> referencedScriptPaths,
            HashSet<string> seedPaths,
            Dictionary<string, List<string>> scriptToAssetsMap,
            List<DependencyChainEntry> dependencyChains,
            List<RuntimeAssetLoadInfo>? runtimeLoads = null,
            string reportFileName = "analysis-report.json")
        {
            var report = new AnalysisReport
            {
                AnalysisMetadata = new AnalysisMetadata
                {
                    Timestamp = DateTime.Now.ToString("o"),
                    TotalScripts = analyzer.AllScriptPaths.Count,
                    AnalyzedScripts = analyzer.AllSymbols.Count,
                    Scope = "All files in Assets/Tactics/Scripts (Roslyn AST analysis)"
                },
                SeedScripts = new List<SeedScriptInfo>(),
                DependencyChain = new List<DependencyChainInfo>(),
                SpecialEntrypoints = new List<SpecialEntrypointInfo>(),
                ReferencedScripts = new List<ReferencedScriptInfo>(),
                IsolatedScripts = new List<IsolatedScriptInfo>(),
                ExternalDependencies = new List<ExternalDependencyInfoOutput>(),
                RuntimeAssetLoads = new List<RuntimeAssetLoadInfoOutput>()
            };

            // Build seed scripts from asset references
            foreach (var symbol in analyzer.AllSymbols.Where(s => string.IsNullOrEmpty(s.ParentTypeName)))
            {
                if (seedPaths.Contains(symbol.FilePath))
                {
                    var sources = new List<string>();
                    if (scriptToAssetsMap.TryGetValue(symbol.FilePath, out var assets))
                    {
                        sources.AddRange(assets);
                    }

                    report.SeedScripts.Add(new SeedScriptInfo
                    {
                        ClassName = symbol.TypeName,
                        NamespaceName = symbol.Namespace,
                        FilePath = symbol.FilePath,
                        Source = sources.Count > 0 ? string.Join(", ", sources) : "Code dependency (entry point)"
                    });
                }
            }

            // Build dependency chain (always populated now)
            foreach (var chain in dependencyChains)
            {
                report.DependencyChain.Add(new DependencyChainInfo
                {
                    FilePath = chain.FilePath,
                    DependsOn = chain.DependsOn,
                    ReferencedBy = chain.ReferencedBy
                });
            }

            // Build special entrypoints
            foreach (var attr in analyzer.SpecialAttributes)
            {
                report.SpecialEntrypoints.Add(new SpecialEntrypointInfo
                {
                    FilePath = attr.FilePath,
                    AttributeName = attr.AttributeName,
                    Description = attr.Description,
                    ClassName = attr.ClassName,
                    Namespace = attr.Namespace
                });
            }

            // Build referenced scripts (including transitive dependencies)
            foreach (var symbol in analyzer.AllSymbols.Where(s => string.IsNullOrEmpty(s.ParentTypeName)))
            {
                if (referencedScriptPaths.Contains(symbol.FilePath))
                {
                    var sources = new List<string>();
                    if (seedPaths.Contains(symbol.FilePath))
                    {
                        if (scriptToAssetsMap.TryGetValue(symbol.FilePath, out var assets))
                        {
                            sources.Add($"Scene/Prefab/Asset: {string.Join(", ", assets)}");
                        }
                    }
                    else
                    {
                        sources.Add("Code dependency");
                    }

                    // Also check if it's a special entrypoint
                    var specialAttrs = analyzer.SpecialAttributes.Where(a => a.FilePath == symbol.FilePath).ToList();
                    foreach (var sa in specialAttrs)
                    {
                        sources.Add($"[{sa.AttributeName}] - {sa.Description}");
                    }

                    if (sources.Count == 0)
                        sources.Add("Unknown (seed)");

                    report.ReferencedScripts.Add(new ReferencedScriptInfo
                    {
                        ClassName = symbol.TypeName,
                        NamespaceName = symbol.Namespace,
                        FilePath = symbol.FilePath,
                        ReferencedBy = sources
                    });
                }
            }

            // Build isolated scripts
            var specialAttrPaths = new HashSet<string>(analyzer.SpecialAttributes.Select(a => a.FilePath), StringComparer.OrdinalIgnoreCase);
            foreach (var symbol in analyzer.AllSymbols.Where(s => string.IsNullOrEmpty(s.ParentTypeName)))
            {
                if (!referencedScriptPaths.Contains(symbol.FilePath))
                {
                    var category = CategorizeSymbol(symbol);
                    var hasSpecialAttr = specialAttrPaths.Contains(symbol.FilePath);
                    var confidence = DetermineConfidence(symbol, category, hasSpecialAttr);

                    report.IsolatedScripts.Add(new IsolatedScriptInfo
                    {
                        ClassName = symbol.TypeName,
                        NamespaceName = symbol.Namespace,
                        FilePath = symbol.FilePath,
                        Category = category,
                        Confidence = confidence,
                        IsAbstract = symbol.IsAbstract,
                        IsInterface = symbol.IsInterface,
                        IsStatic = symbol.IsStatic,
                        IsPartial = symbol.IsPartial,
                        HasSpecialAttribute = hasSpecialAttr
                    });
                }
            }

            // Build external dependencies from using statements
            var externalNamespaces = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var symbol in analyzer.AllSymbols.Where(s => string.IsNullOrEmpty(s.ParentTypeName)))
            {
                foreach (var usingNs in symbol.ExternalUsingNamespaces)
                {
                    if (!IsSystemNamespace(usingNs) && !IsUnityNamespace(usingNs))
                    {
                        if (!externalNamespaces.ContainsKey(usingNs))
                            externalNamespaces[usingNs] = new List<string>();
                        if (!externalNamespaces[usingNs].Contains(symbol.FilePath))
                            externalNamespaces[usingNs].Add(symbol.FilePath);
                    }
                }
            }

            foreach (var kvp in externalNamespaces.OrderBy(k => k.Key))
            {
                report.ExternalDependencies.Add(new ExternalDependencyInfoOutput
                {
                    NamespaceName = kvp.Key,
                    UsedBy = kvp.Value.ToList()
                });
            }

            // Build runtime asset loads
            if (runtimeLoads != null)
            {
                foreach (var load in runtimeLoads)
                {
                    report.RuntimeAssetLoads.Add(new RuntimeAssetLoadInfoOutput
                    {
                        AssetPath = load.AssetPath,
                        SourceFile = load.SourceFile,
                        LineNumber = load.LineNumber,
                        MethodName = load.MethodName,
                        Location = load.Location,
                        DiscoveredScripts = load.DiscoveredScripts
                    });
                }
            }

            // Write JSON using Newtonsoft.Json
            var jsonPath = Path.Combine(_outputDirectory, reportFileName);
            var jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = null
            };
            var jsonContent = JsonConvert.SerializeObject(report, jsonSettings);
            File.WriteAllText(jsonPath, jsonContent);

            Debug.Log($"[JsonReportGenerator] Report written to {jsonPath}");
        }

        #region Categorization

        private static string CategorizeSymbol(SymbolInfo symbol)
        {
            if (symbol.IsInterface)
                return "Interface";
            if (symbol.IsEnum)
                return "Enum";
            if (symbol.IsStruct)
                return "Struct";
            if (symbol.IsAbstract && symbol.IsClass)
                return "AbstractClass";
            if (symbol.IsStatic)
                return "StaticClass";
            if (symbol.FilePath.Contains("/Editor/"))
                return "EditorScript";
            if (symbol.FilePath.Contains("/Tbsf/"))
                return "ThirdPartyFramework";

            return "ConcreteClass";
        }

        private static string DetermineConfidence(SymbolInfo symbol, string category, bool hasSpecialAttr)
        {
            if (category == "ThirdPartyFramework")
                return "N/A - Do Not Delete";
            if (hasSpecialAttr)
                return "Low - Has special attribute";
            if (category == "ConcreteClass")
                return "High";
            if (category == "AbstractClass" || category == "Interface")
                return "Medium - May be used by inheritance";
            if (category == "StaticClass")
                return "Medium";
            if (category == "EditorScript")
                return "Low";

            return "Medium";
        }

        private static bool IsSystemNamespace(string ns)
        {
            return ns.StartsWith("System") || ns.StartsWith("Microsoft") ||
                   ns.StartsWith("Mono.Cecil") || ns == "JetBrains.Annotations";
        }

        private static bool IsUnityNamespace(string ns)
        {
            return ns.StartsWith("UnityEngine") || ns.StartsWith("UnityEditor") ||
                   ns.StartsWith("Unity") || ns == "TMPro" || ns.StartsWith("DG.Tweening") ||
                   ns.StartsWith("Sirenix");
        }

        #endregion
    }

    #region Report Data Classes

    [Serializable]
    public class AnalysisReport
    {
        public AnalysisMetadata AnalysisMetadata { get; set; } = default!;
        public List<SeedScriptInfo> SeedScripts { get; set; } = new();
        public List<DependencyChainInfo> DependencyChain { get; set; } = new();
        public List<SpecialEntrypointInfo> SpecialEntrypoints { get; set; } = new();
        public List<ReferencedScriptInfo> ReferencedScripts { get; set; } = new();
        public List<IsolatedScriptInfo> IsolatedScripts { get; set; } = new();
        public List<ExternalDependencyInfoOutput> ExternalDependencies { get; set; } = new();
        public List<RuntimeAssetLoadInfoOutput> RuntimeAssetLoads { get; set; } = new();
    }

    [Serializable]
    public class AnalysisMetadata
    {
        public string Timestamp { get; set; } = string.Empty;
        public int TotalScripts;
        public int AnalyzedScripts;
        public string Scope { get; set; } = string.Empty;

        // For backward compatibility with old JSON readers
        public string timestamp { get => Timestamp; set => Timestamp = value; }
        public int totalScripts { get => TotalScripts; set => TotalScripts = value; }
        public int analyzedScripts { get => AnalyzedScripts; set => AnalyzedScripts = value; }
        public string scope { get => Scope; set => Scope = value; }
    }

    [Serializable]
    public class SeedScriptInfo
    {
        public string ClassName { get; set; } = string.Empty;
        public string NamespaceName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;

        // Backward compatibility
        public string className { get => ClassName; set => ClassName = value; }
        public string namespaceName { get => NamespaceName; set => NamespaceName = value; }
        public string filePath { get => FilePath; set => FilePath = value; }
        public string source { get => Source; set => Source = value; }
    }

    [Serializable]
    public class DependencyChainInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public List<string> DependsOn { get; set; } = new();
        public List<string> ReferencedBy { get; set; } = new();

        // Backward compatibility
        public string filePath { get => FilePath; set => FilePath = value; }
        public List<string> dependsOn { get => DependsOn; set => DependsOn = value; }
        public List<string> referencedBy { get => ReferencedBy; set => ReferencedBy = value; }
    }

    [Serializable]
    public class SpecialEntrypointInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;

        // Backward compatibility
        public string filePath { get => FilePath; set => FilePath = value; }
        public string attributeName { get => AttributeName; set => AttributeName = value; }
        public string description { get => Description; set => Description = value; }
        public string className { get => ClassName; set => ClassName = value; }
        public string namespaceName { get => Namespace; set => Namespace = value; }
    }

    [Serializable]
    public class ReferencedScriptInfo
    {
        public string ClassName { get; set; } = string.Empty;
        public string NamespaceName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public List<string> ReferencedBy { get; set; } = new();

        // Backward compatibility
        public string className { get => ClassName; set => ClassName = value; }
        public string namespaceName { get => NamespaceName; set => NamespaceName = value; }
        public string filePath { get => FilePath; set => FilePath = value; }
        public List<string> referencedBy { get => ReferencedBy; set => ReferencedBy = value; }
    }

    [Serializable]
    public class IsolatedScriptInfo
    {
        public string ClassName { get; set; } = string.Empty;
        public string NamespaceName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Confidence { get; set; } = string.Empty;
        public bool IsAbstract;
        public bool IsInterface;
        public bool IsStatic;
        public bool IsPartial;
        public bool HasSpecialAttribute;

        // Backward compatibility
        public string className { get => ClassName; set => ClassName = value; }
        public string namespaceName { get => NamespaceName; set => NamespaceName = value; }
        public string filePath { get => FilePath; set => FilePath = value; }
        public string category { get => Category; set => Category = value; }
        public string confidence { get => Confidence; set => Confidence = value; }
        public bool isAbstract { get => IsAbstract; set => IsAbstract = value; }
        public bool isInterface { get => IsInterface; set => IsInterface = value; }
        public bool isStatic { get => IsStatic; set => IsStatic = value; }
    }

    [Serializable]
    public class ExternalDependencyInfoOutput
    {
        public string NamespaceName { get; set; } = string.Empty;
        public List<string> UsedBy { get; set; } = new();

        // Backward compatibility
        public string namespaceName { get => NamespaceName; set => NamespaceName = value; }
        public List<string> usedBy { get => UsedBy; set => UsedBy = value; }
    }

    [Serializable]
    public class RuntimeAssetLoadInfoOutput
    {
        public string AssetPath { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public List<string> DiscoveredScripts { get; set; } = new();

        // Backward compatibility
        public string assetPath { get => AssetPath; set => AssetPath = value; }
        public string sourceFile { get => SourceFile; set => SourceFile = value; }
        public int lineNumber { get => LineNumber; set => LineNumber = value; }
        public string methodName { get => MethodName; set => MethodName = value; }
        public string location { get => Location; set => Location = value; }
        public List<string> discoveredScripts { get => DiscoveredScripts; set => DiscoveredScripts = value; }
    }

    #endregion
}
