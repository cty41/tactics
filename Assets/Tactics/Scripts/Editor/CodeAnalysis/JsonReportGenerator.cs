#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Tactics.CodeAnalysis
{
    /// <summary>
    /// Generates JSON report for code analysis results.
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
        /// Generates complete analysis report.
        /// </summary>
        public void GenerateReport(
            SymbolIndex symbolIndex,
            IReadOnlyList<SpecialAttributeInfo> specialAttributes,
            IReadOnlyList<string> allScriptPaths,
            HashSet<string> referencedScriptPaths,
            HashSet<string> seedPaths,
            Dictionary<string, List<string>> scriptToAssetsMap,
            string reportFileName = "analysis-report.json")
        {
            var report = new AnalysisReport
            {
                analysisMetadata = new AnalysisMetadata
                {
                    timestamp = DateTime.Now.ToString("o"),
                    totalScripts = allScriptPaths.Count,
                    analyzedScripts = symbolIndex.AllSymbols.Count,
                    scope = "All files in Assets/Tactics/Scripts"
                },
                seedScripts = new List<SeedScriptInfo>(),
                specialEntrypoints = new List<SpecialEntrypointInfo>(),
                referencedScripts = new List<ReferencedScriptInfo>(),
                isolatedScripts = new List<IsolatedScriptInfo>(),
                externalDependencies = new List<ExternalDependencyInfo>()
            };

            // Build seed scripts from asset references
            foreach (var symbol in symbolIndex.AllSymbols)
            {
                if (seedPaths.Contains(symbol.FilePath))
                {
                    var sources = new List<string>();
                    if (scriptToAssetsMap.TryGetValue(symbol.FilePath, out var assets))
                    {
                        sources.AddRange(assets);
                    }

                    report.seedScripts.Add(new SeedScriptInfo
                    {
                        className = symbol.TypeName,
                        namespaceName = symbol.Namespace,
                        filePath = symbol.FilePath,
                        source = sources.Count > 0 ? string.Join(", ", sources) : "Unknown reference"
                    });
                }
            }

            // Build special entrypoints
            foreach (var attr in specialAttributes)
            {
                report.specialEntrypoints.Add(new SpecialEntrypointInfo
                {
                    filePath = attr.FilePath,
                    attributeName = attr.AttributeName,
                    description = attr.Description
                });
            }

            // Build referenced scripts (including transitive dependencies)
            foreach (var symbol in symbolIndex.AllSymbols)
            {
                if (referencedScriptPaths.Contains(symbol.FilePath))
                {
                    var sources = new List<string>();
                    if (seedPaths.Contains(symbol.FilePath))
                    {
                        if (scriptToAssetsMap.TryGetValue(symbol.FilePath, out var assets))
                        {
                            sources.Add($"Scene/Prefab: {string.Join(", ", assets)}");
                        }
                    }
                    else
                    {
                        sources.Add("Code dependency");
                    }

                    report.referencedScripts.Add(new ReferencedScriptInfo
                    {
                        className = symbol.TypeName,
                        namespaceName = symbol.Namespace,
                        filePath = symbol.FilePath,
                        referencedBy = sources.Count > 0 ? sources : new List<string> { "Unknown" }
                    });
                }
            }

            // Build isolated scripts
            foreach (var symbol in symbolIndex.AllSymbols)
            {
                if (!referencedScriptPaths.Contains(symbol.FilePath))
                {
                    var category = CategorizeSymbol(symbol);
                    var confidence = DetermineConfidence(symbol, category);

                    report.isolatedScripts.Add(new IsolatedScriptInfo
                    {
                        className = symbol.TypeName,
                        namespaceName = symbol.Namespace,
                        filePath = symbol.FilePath,
                        category = category,
                        confidence = confidence,
                        isAbstract = symbol.IsAbstract,
                        isInterface = symbol.IsInterface,
                        isStatic = symbol.IsStatic
                    });
                }
            }

            // Build external dependencies from using statements
            var externalNamespaces = new Dictionary<string, List<string>>();
            foreach (var symbol in symbolIndex.AllSymbols)
            {
                foreach (var usingNs in symbol.UsingNamespaces)
                {
                    if (!symbolIndex.IsProjectType(usingNs) && !IsSystemNamespace(usingNs))
                    {
                        if (!externalNamespaces.ContainsKey(usingNs))
                        {
                            externalNamespaces[usingNs] = new List<string>();
                        }
                        if (!externalNamespaces[usingNs].Contains(symbol.FilePath))
                        {
                            externalNamespaces[usingNs].Add(symbol.FilePath);
                        }
                    }
                }
            }

            foreach (var kvp in externalNamespaces)
            {
                report.externalDependencies.Add(new ExternalDependencyInfo
                {
                    namespaceName = kvp.Key,
                    usedBy = kvp.Value.ToList()
                });
            }

            // Write JSON
            var jsonPath = Path.Combine(_outputDirectory, reportFileName);
            var jsonContent = JsonUtility.ToJson(report, true);
            File.WriteAllText(jsonPath, jsonContent);

            Debug.Log($"[JsonReportGenerator] Report written to {jsonPath}");
        }

        private string CategorizeSymbol(SymbolInfo symbol)
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

        private string DetermineConfidence(SymbolInfo symbol, string category)
        {
            // Higher confidence for deletion if it's a concrete class with no special attributes
            if (category == "ConcreteClass")
                return "High";
            if (category == "StaticClass")
                return "Medium";
            if (category == "EditorScript")
                return "Low";
            if (category == "ThirdPartyFramework")
                return "N/A - Do Not Delete";
            
            return "Medium";
        }

        private bool IsSystemNamespace(string ns)
        {
            return ns.StartsWith("System") || 
                   ns.StartsWith("UnityEngine") || 
                   ns.StartsWith("UnityEditor") ||
                   ns.StartsWith("MonoBehaviour") ||
                   ns == "UnityEditor" ||
                   ns == "UnityEngine";
        }
    }

    #region Report Data Classes

    [Serializable]
    public class AnalysisReport
    {
        public AnalysisMetadata analysisMetadata = default!;
        public List<SeedScriptInfo> seedScripts = default!;
        public List<DependencyChainInfo> dependencyChain = default!;
        public List<SpecialEntrypointInfo> specialEntrypoints = default!;
        public List<ReferencedScriptInfo> referencedScripts = default!;
        public List<IsolatedScriptInfo> isolatedScripts = default!;
        public List<ExternalDependencyInfo> externalDependencies = default!;
    }

    [Serializable]
    public class DependencyChainInfo
    {
        public string filePath = string.Empty;
        public List<string> dependsOn = default!;
        public List<string> referencedBy = default!;
    }

    [Serializable]
    public class AnalysisMetadata
    {
        public string timestamp = string.Empty;
        public int totalScripts;
        public int analyzedScripts;
        public string scope = string.Empty;
    }

    [Serializable]
    public class SeedScriptInfo
    {
        public string className = string.Empty;
        public string namespaceName = string.Empty;
        public string filePath = string.Empty;
        public string source = string.Empty;
    }

    [Serializable]
    public class SpecialEntrypointInfo
    {
        public string filePath = string.Empty;
        public string attributeName = string.Empty;
        public string description = string.Empty;
    }

    [Serializable]
    public class ReferencedScriptInfo
    {
        public string className = string.Empty;
        public string namespaceName = string.Empty;
        public string filePath = string.Empty;
        public List<string> referencedBy = default!;
    }

    [Serializable]
    public class IsolatedScriptInfo
    {
        public string className = string.Empty;
        public string namespaceName = string.Empty;
        public string filePath = string.Empty;
        public string category = string.Empty;
        public string confidence = string.Empty;
        public bool isAbstract;
        public bool isInterface;
        public bool isStatic;
    }

    [Serializable]
    public class ExternalDependencyInfo
    {
        public string namespaceName = string.Empty;
        public List<string> usedBy = default!;
    }

    #endregion
}
