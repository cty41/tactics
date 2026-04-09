#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tactics.CodeAnalysis
{
    /// <summary>
    /// Editor menu for running code reference analysis.
    /// Access via: Tools > Code Analysis > Analyze Script References
    /// </summary>
    public static class CodeAnalysisMenu
    {
        private const string OutputDirectory = "Assets/Tactics/Scripts/Editor/CodeAnalysis/Output";

        [MenuItem("Tools/Code Analysis/Analyze Script References")]
        public static void RunAnalysis()
        {
            Debug.Log("[CodeAnalysis] Starting script reference analysis...");

            try
            {
                // Step 1: Scan all scripts
                var analyzer = new ReferenceAnalyzer();
                analyzer.ScanAllScripts();

                // Step 2: Analyze Unity assets (scenes, prefabs)
                var assetAnalyzer = new UnityAssetReferenceAnalyzer();
                assetAnalyzer.BuildGuidMapping();
                assetAnalyzer.AnalyzeScenes();
                assetAnalyzer.AnalyzePrefabs();

                // Step 3: Get all referenced scripts from assets
                var seedPaths = assetAnalyzer.GetAllReferencedScriptPaths();
                var scriptToAssetsMap = assetAnalyzer.GetScriptToAssetsMap();

                // Step 4: Analyze code-level dependencies
                Debug.Log("[CodeAnalysis] Analyzing code dependencies...");
                var depTracker = new DependencyTracker(analyzer.SymbolIndex);
                depTracker.AnalyzeDependencies();

                // Get transitive dependencies from seed scripts
                var allReferencedPaths = depTracker.GetTransitiveReferences(new HashSet<string>(seedPaths));

                // Step 5: Generate report
                var reportGenerator = new JsonReportGenerator(OutputDirectory);
                reportGenerator.GenerateReport(
                    analyzer.SymbolIndex,
                    analyzer.SpecialAttributes,
                    analyzer.AllScriptPaths,
                    allReferencedPaths,
                    seedPaths,
                    scriptToAssetsMap
                );

                // Step 6: Show summary
                ShowSummary(
                    analyzer.SymbolIndex.AllSymbols.Count,
                    analyzer.AllScriptPaths.Count,
                    seedPaths.Count,
                    allReferencedPaths.Count,
                    analyzer.SymbolIndex.AllSymbols.Count - allReferencedPaths.Count
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
        public static bool RunAnalysisValidate()
        {
            // Always enabled
            return true;
        }

        [MenuItem("Tools/Code Analysis/Open Output Folder")]
        public static void OpenOutputFolder()
        {
            var fullPath = System.IO.Path.Combine(Application.dataPath, "..", OutputDirectory);
            fullPath = System.IO.Path.GetFullPath(fullPath);
            
            if (System.IO.Directory.Exists(fullPath))
            {
                EditorUtility.RevealInFinder(fullPath);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Output Folder Not Found",
                    $"Output folder does not exist:\n{fullPath}\n\nPlease run analysis first.",
                    "OK"
                );
            }
        }

        [MenuItem("Tools/Code Analysis/Open Output Folder", true)]
        public static bool OpenOutputFolderValidate()
        {
            var fullPath = System.IO.Path.Combine(Application.dataPath, "..", OutputDirectory);
            fullPath = System.IO.Path.GetFullPath(fullPath);
            return System.IO.Directory.Exists(fullPath);
        }

        private static void ShowSummary(int symbolsCount, int totalScripts, int seedCount, int referencedCount, int isolatedCount)
        {
            var summary = $@"Code Analysis Summary
=====================
Total Scripts: {totalScripts}
Total Symbols: {symbolsCount}
Seed Scripts (Scene/Prefab): {seedCount}
Referenced (with dependencies): {referencedCount}
Isolated Scripts: {isolatedCount}

Report saved to: {OutputDirectory}/analysis-report.json

Note: Review isolated scripts before deletion.
- Interfaces/Abstract classes may be used by inheritance
- Editor scripts may have menu commands
- Third-party framework code should not be deleted";

            Debug.Log(summary);
            
            EditorUtility.DisplayDialog(
                "Code Analysis Complete",
                summary,
                "OK"
            );
        }
    }
}
