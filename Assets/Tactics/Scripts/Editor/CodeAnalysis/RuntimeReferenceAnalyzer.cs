#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Tactics.CodeAnalysis
{
    /// <summary>
    /// Detects scripts referenced by prefabs that are loaded at runtime via GameAssetManager.
    /// Scans for patterns like:
    ///   GameAssetManager.Instance.LoadAsync<GameObject>("Assets/.../MyPrefab.prefab")
    ///   GameAssetManager.Instance.Load&lt;GameObject&gt;("Assets/.../MyPrefab.prefab")
    /// Then finds those prefab assets and extracts their attached script components.
    /// These scripts are added as seed paths since they are implicitly used at runtime.
    /// </summary>
    public class RuntimeReferenceAnalyzer
    {
        private const string ScriptsDirectory = "Assets/Tactics/Scripts";

        // Patterns for asset loading methods that take a path string argument
        private static readonly string[] LoadMethodNames = { "LoadAsync", "Load" };

        // File extensions that can have script components
        private static readonly string[] AssetExtensionsWithComponents = { ".prefab", ".unity" };

        private readonly List<RuntimeAssetLoadInfo> _runtimeLoads = new();
        private readonly HashSet<string> _discoveredScriptPaths = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<RuntimeAssetLoadInfo> RuntimeLoads => _runtimeLoads;
        public IReadOnlyCollection<string> DiscoveredScriptPaths => _discoveredScriptPaths;

        /// <summary>
        /// Scans all C# files for GameAssetManager.Load/LoadAsync calls with string literal path arguments.
        /// </summary>
        public void ScanForRuntimeLoads()
        {
            _runtimeLoads.Clear();
            _discoveredScriptPaths.Clear();

            var scriptsDir = Path.Combine(Application.dataPath, "Tactics", "Scripts");
            if (!Directory.Exists(scriptsDir))
            {
                Debug.LogWarning("[RuntimeReferenceAnalyzer] Scripts directory not found: " + scriptsDir);
                return;
            }

            var files = Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var relativePath = file.Replace(Application.dataPath, "Assets").Replace('\\', '/');
                ScanFile(relativePath, file);
            }

            Debug.Log($"[RuntimeReferenceAnalyzer] Found {_runtimeLoads.Count} runtime asset loads, {_discoveredScriptPaths.Count} referenced scripts");
        }

        /// <summary>
        /// For each discovered runtime load path, finds the asset and extracts attached scripts.
        /// Also discovers scripts on prefabs loaded via Addressables or Resources (if any).
        /// </summary>
        public void DiscoverReferencedScripts()
        {
            _discoveredScriptPaths.Clear();

            foreach (var loadInfo in _runtimeLoads)
            {
                var assetPath = loadInfo.AssetPath;

                // Only process prefab and scene paths (not material, texture, etc.)
                if (!AssetExtensionsWithComponents.Any(ext => assetPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    continue;

                DiscoverScriptsOnAsset(assetPath, loadInfo);
            }
        }

        private void ScanFile(string relativePath, string absolutePath)
        {
            try
            {
                var content = File.ReadAllText(absolutePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(content, path: relativePath);
                var root = syntaxTree.GetRoot();

                // Find all invocation expressions
                foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    AnalyzeInvocation(invocation, relativePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RuntimeReferenceAnalyzer] Failed to scan {relativePath}: {e.Message}");
            }
        }

        private void AnalyzeInvocation(InvocationExpressionSyntax invocation, string sourceFilePath)
        {
            // We're looking for patterns like:
            //   GameAssetManager.Instance.LoadAsync<GameObject>("path")
            //   mgr.LoadAsync<GameObject>("path")
            //   GameAssetManager.Instance.Load<GameObject>("path")
            //   mgr.LoadScene("path")
            //   mgr.LoadSceneAsync("path")

            string methodName;
            ExpressionSyntax? expressionToCheck = null;

            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                methodName = memberAccess.Name.Identifier.Text;
                expressionToCheck = memberAccess.Expression;
            }
            else if (invocation.Expression is GenericNameSyntax genericName)
            {
                // For chained calls like .LoadAsync<T>(), the generic name is on the method itself
                methodName = genericName.Identifier.Text;
                // Walk up to find the member access
                if (invocation.Expression.Parent is MemberAccessExpressionSyntax parentMember)
                {
                    expressionToCheck = parentMember.Expression;
                }
                else
                {
                    return; // Can't determine the receiver
                }
            }
            else
            {
                return;
            }

            // Check if this is one of our target methods
            if (!LoadMethodNames.Contains(methodName) && methodName != "LoadScene" && methodName != "LoadSceneAsync")
                return;

            // Check if the receiver looks like GameAssetManager
            if (!IsGameAssetManagerAccess(expressionToCheck))
                return;

            // Extract the path argument (first argument should be the asset path string)
            var arguments = invocation.ArgumentList.Arguments;
            if (arguments.Count == 0)
                return;

            var firstArg = arguments[0].Expression;
            string assetPath;

            if (firstArg is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                assetPath = literal.Token.ValueText;
            }
            else if (firstArg is InterpolatedStringExpressionSyntax)
            {
                // String interpolation - can't statically determine the path
                assetPath = $"[interpolated: {firstArg.ToString()}]";
            }
            else
            {
                // Variable or constant reference - can't statically determine
                return;
            }

            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return;

            var loadInfo = new RuntimeAssetLoadInfo
            {
                AssetPath = assetPath,
                SourceFile = sourceFilePath,
                LineNumber = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                MethodName = methodName,
                Location = invocation.ToString()
            };

            _runtimeLoads.Add(loadInfo);
        }

        private bool IsGameAssetManagerAccess(ExpressionSyntax? expression)
        {
            if (expression == null)
                return false;

            var text = expression.ToString();

            // Direct patterns:
            //   GameAssetManager.Instance
            //   GameAssetManager
            if (text.Contains("GameAssetManager"))
                return true;

            // Variable patterns (common naming):
            //   mgr, manager, assetManager, _assetManager, etc.
            // We can't be certain without semantic analysis, but these are strong indicators
            var receiver = text.Trim();

            // Check if it ends with common variable names for GameAssetManager
            if (receiver == "mgr" ||
                receiver == "manager" ||
                receiver == "assetManager" ||
                receiver == "_assetManager" ||
                receiver == "_manager" ||
                receiver == "gameAssetManager" ||
                receiver == "_gameAssetManager" ||
                receiver.EndsWith(".mgr") ||
                receiver.EndsWith(".manager"))
            {
                return true;
            }

            // Also check for variable declarations nearby that might assign GameAssetManager
            // This is a heuristic - look for patterns like "var mgr = GameAssetManager.Instance"
            // We handle this by checking if the expression itself is an identifier that could be GAM
            if (expression is IdentifierNameSyntax)
            {
                // For simple identifiers, we accept them if they look like manager variables
                return true;
            }

            return false;
        }

        private void DiscoverScriptsOnAsset(string assetPath, RuntimeAssetLoadInfo loadInfo)
        {
            // Find the asset in AssetDatabase
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                // Asset not found - might be a path that doesn't exist yet or is incorrect
                Debug.LogWarning($"[RuntimeReferenceAnalyzer] Asset not found for runtime load: {assetPath} (from {loadInfo.SourceFile}:{loadInfo.LineNumber})");
                return;
            }

            if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                DiscoverScriptsOnPrefab(assetPath, guid, loadInfo);
            }
            else if (assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                DiscoverScriptsOnScene(assetPath, guid, loadInfo);
            }
        }

        private void DiscoverScriptsOnPrefab(string prefabPath, string guid, RuntimeAssetLoadInfo loadInfo)
        {
            // Use AssetDatabase.GetDependencies to find all scripts referenced by this prefab
            var deps = AssetDatabase.GetDependencies(prefabPath, false);

            foreach (var dep in deps)
            {
                if (dep.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    if (_discoveredScriptPaths.Add(dep))
                    {
                        loadInfo.DiscoveredScripts.Add(dep);
                    }
                }
            }

            // Also scan the prefab YAML for MonoScript GUIDs (more thorough)
            ScanPrefabYamlForScripts(prefabPath, loadInfo);
        }

        private void ScanPrefabYamlForScripts(string prefabPath, RuntimeAssetLoadInfo loadInfo)
        {
            try
            {
                var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", prefabPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(absolutePath))
                    return;

                var content = File.ReadAllText(absolutePath);

                // Scan all scripts in the Tactics/Scripts directory
                var scriptsDir = Path.Combine(Application.dataPath, "Tactics", "Scripts");
                var scriptPaths = Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories);

                foreach (var scriptFile in scriptPaths)
                {
                    var relativePath = scriptFile.Replace(Application.dataPath, "Assets").Replace('\\', '/');
                    var scriptGuid = AssetDatabase.AssetPathToGUID(relativePath);

                    if (!string.IsNullOrEmpty(scriptGuid) && content.Contains(scriptGuid))
                    {
                        if (_discoveredScriptPaths.Add(relativePath))
                        {
                            loadInfo.DiscoveredScripts.Add(relativePath);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RuntimeReferenceAnalyzer] Failed to scan prefab YAML {prefabPath}: {e.Message}");
            }
        }

        private void DiscoverScriptsOnScene(string scenePath, string guid, RuntimeAssetLoadInfo loadInfo)
        {
            // Use AssetDatabase.GetDependencies to find all scripts referenced by this scene
            try
            {
                var deps = AssetDatabase.GetDependencies(scenePath, false);

                foreach (var dep in deps)
                {
                    if (dep.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_discoveredScriptPaths.Add(dep))
                        {
                            loadInfo.DiscoveredScripts.Add(dep);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RuntimeReferenceAnalyzer] Failed to analyze scene {scenePath}: {e.Message}");
            }
        }

        /// <summary>
        /// Adds all discovered script paths to the provided seed set.
        /// </summary>
        public void AddDiscoveredScriptsToSeeds(HashSet<string> seedPaths)
        {
            foreach (var scriptPath in _discoveredScriptPaths)
            {
                if (seedPaths.Add(scriptPath))
                {
                    Debug.Log($"[RuntimeReferenceAnalyzer] Added runtime-referenced script to seeds: {scriptPath}");
                }
            }
        }
    }

    /// <summary>
    /// Information about a runtime asset load detected via static code analysis.
    /// </summary>
    public class RuntimeAssetLoadInfo
    {
        public string AssetPath { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public List<string> DiscoveredScripts { get; set; } = new();
    }
}
