#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tactics.CodeAnalysis
{
    /// <summary>
    /// Editor menu for running code reference analysis using Roslyn AST parsing.
    /// Access via: Tools > Code Analysis > Analyze Script References
    /// </summary>
    public static class CodeAnalysisMenu
    {
        private const string OutputDirectory = "Assets/Tactics/Scripts/Editor/CodeAnalysis/Output";

        [MenuItem("Tools/Code Analysis/Analyze Script References")]
        public static void RunAnalysis()
        {
            Debug.Log("[CodeAnalysis] Starting Roslyn-based script reference analysis...");

            try
            {
                // Step 1: Roslyn AST scan - extracts types, references, special attributes
                Debug.Log("[CodeAnalysis] Step 1/5: Parsing C# files with Roslyn...");
                var roslynAnalyzer = new RoslynCodeAnalyzer();
                roslynAnalyzer.ScanAllScripts();

                // Step 2: Unity asset scanning - scenes, prefabs, ScriptableObjects
                Debug.Log("[CodeAnalysis] Step 2/5: Scanning Unity assets...");
                var assetAnalyzer = new UnityAssetReferenceAnalyzer();
                assetAnalyzer.BuildGuidMapping();
                assetAnalyzer.AnalyzeAllScenes();
                assetAnalyzer.AnalyzeAllPrefabs();
                assetAnalyzer.AnalyzeScriptableObjects();

                // Step 3: Collect seed scripts (directly referenced by assets)
                var seedPaths = assetAnalyzer.GetAllReferencedScriptPaths();
                var scriptToAssetsMap = assetAnalyzer.GetScriptToAssetsMap();

                // Step 3b: Add special attribute scripts as seeds
                // Scripts with [RuntimeInitializeOnLoadMethod], [InitializeOnLoad], [MenuItem], [CreateAssetMenu]
                // are entry points even if not directly referenced by scenes/prefabs
                foreach (var attr in roslynAnalyzer.SpecialAttributes)
                {
                    seedPaths.Add(attr.FilePath);
                }

                // Step 3c: Detect runtime-loaded assets via GameAssetManager.Load/LoadAsync
                // Scripts attached to prefabs loaded at runtime are implicitly used
                Debug.Log("[CodeAnalysis] Step 3c/5: Detecting runtime-loaded asset references...");
                var runtimeAnalyzer = new RuntimeReferenceAnalyzer();
                runtimeAnalyzer.ScanForRuntimeLoads();
                runtimeAnalyzer.DiscoverReferencedScripts();
                runtimeAnalyzer.AddDiscoveredScriptsToSeeds(seedPaths);

                Debug.Log($"[CodeAnalysis] Step 3/5: Identified {seedPaths.Count} seed scripts ({assetAnalyzer.SceneReferences.Count} scene refs, {assetAnalyzer.PrefabReferences.Count} prefab refs, {assetAnalyzer.AssetReferences.Count} SO refs, {roslynAnalyzer.SpecialAttributes.Count} special attributes, {runtimeAnalyzer.RuntimeLoads.Count} runtime loads)");

                // Step 4: Transitive dependency analysis
                Debug.Log("[CodeAnalysis] Step 4/5: Computing transitive dependencies...");
                var allReferencedPaths = roslynAnalyzer.GetTransitiveReferences(seedPaths);
                var dependencyChains = roslynAnalyzer.BuildDependencyChains(allReferencedPaths);

                // Step 5: Generate report
                Debug.Log("[CodeAnalysis] Step 5/5: Generating report...");
                var reportGenerator = new JsonReportGenerator(OutputDirectory);
                reportGenerator.GenerateReport(
                    roslynAnalyzer,
                    allReferencedPaths,
                    seedPaths,
                    scriptToAssetsMap,
                    dependencyChains,
                    runtimeAnalyzer.RuntimeLoads.ToList()
                );

                // Show summary
                var topLevelCount = roslynAnalyzer.AllSymbols.Count(s => string.IsNullOrEmpty(s.ParentTypeName));
                var isolatedCount = topLevelCount - allReferencedPaths.Count;
                ShowSummary(
                    roslynAnalyzer.AllSymbols.Count,
                    roslynAnalyzer.AllScriptPaths.Count,
                    seedPaths.Count,
                    allReferencedPaths.Count,
                    Math.Max(0, isolatedCount),
                    runtimeAnalyzer.RuntimeLoads.Count,
                    runtimeAnalyzer.DiscoveredScriptPaths.Count
                );

                Debug.Log("[CodeAnalysis] Analysis completed successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CodeAnalysis] Analysis failed: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog(
                    "Code Analysis Failed",
                    $"Analysis failed with error:\n{e.Message}",
                    "OK"
                );
            }
        }

        [MenuItem("Tools/Code Analysis/Analyze Script References", true)]
        public static bool RunAnalysisValidate() => true;

        [MenuItem("Tools/Code Analysis/Open Output Folder")]
        public static void OpenOutputFolder()
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDirectory));
            if (Directory.Exists(fullPath))
            {
                EditorUtility.RevealInFinder(fullPath);
            }
            else
            {
                EditorUtility.DisplayDialog("Output Folder Not Found",
                    $"Output folder does not exist:\n{fullPath}\n\nPlease run analysis first.", "OK");
            }
        }

        [MenuItem("Tools/Code Analysis/Open Output Folder", true)]
        public static bool OpenOutputFolderValidate()
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDirectory));
            return Directory.Exists(fullPath);
        }

        private static void ShowSummary(int symbolsCount, int totalScripts, int seedCount, int referencedCount, int isolatedCount, int runtimeLoadCount, int runtimeDiscoveredScriptCount)
        {
            var summary = $@"Code Analysis Summary (Roslyn AST)
 =====================================
 Total Scripts: {totalScripts}
 Total Symbols (incl. nested): {symbolsCount}
 Seed Scripts (Assets + Special): {seedCount}
 Referenced (with dependencies): {referencedCount}
 Isolated Scripts: {isolatedCount}
 Runtime Asset Loads Detected: {runtimeLoadCount}
 Scripts from Runtime Loads: {runtimeDiscoveredScriptCount}

 Report saved to: {OutputDirectory}/analysis-report.json

 Improvements over previous version:
 - Roslyn AST parsing (no regex false positives)
 - Scans ALL scenes, prefabs, ScriptableObjects
 - Special attributes as entry points
 - Nested class indexing
 - Full dependency chain reporting
 - Runtime asset load detection (GameAssetManager.Load/LoadAsync)

 Note: Review isolated scripts before deletion.
 - Interfaces/Abstract classes may be used by inheritance
 - Editor scripts may have menu commands
 - Third-party framework code should not be deleted";

            Debug.Log(summary);
            EditorUtility.DisplayDialog("Code Analysis Complete", summary, "OK");
        }
    }
}
