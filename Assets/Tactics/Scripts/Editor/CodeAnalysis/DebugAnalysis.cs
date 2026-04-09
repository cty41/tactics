#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tactics.CodeAnalysis
{
    /// <summary>
    /// Debug helper to verify GUID mapping and asset scanning.
    /// </summary>
    public static class DebugAnalysis
    {
        [MenuItem("Tools/Code Analysis/Debug GUID Mapping")]
        public static void DebugGuidMapping()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== GUID Mapping Debug ===\n");

            var guidToPath = new Dictionary<string, string>();
            var csFiles = AssetDatabase.FindAssets("t:Script", new[] { "Assets/Tactics/Scripts" });

            sb.AppendLine($"Found {csFiles.Length} script GUIDs\n");

            foreach (var guid in csFiles.Take(20))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                guidToPath[guid] = path;
                sb.AppendLine($"  {guid.Substring(0, 8)}... -> {path}");
            }

            if (csFiles.Length > 20)
                sb.AppendLine($"  ... ({csFiles.Length - 20} more)");

            sb.AppendLine($"\nTotal: {csFiles.Length} scripts mapped\n");

            // Check a scene for GUID references
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            if (sceneGuids.Length > 0)
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[0]);
                sb.AppendLine($"--- GUID Scan: {scenePath} ---");

                var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", scenePath.Replace('/', Path.DirectorySeparatorChar)));
                if (File.Exists(absolutePath))
                {
                    var content = File.ReadAllText(absolutePath);
                    int foundCount = 0;
                    foreach (var kvp in guidToPath)
                    {
                        if (content.Contains(kvp.Key))
                        {
                            sb.AppendLine($"  Found: {kvp.Value}");
                            foundCount++;
                            if (foundCount >= 10)
                            {
                                sb.AppendLine($"  ... (limited to 10, {guidToPath.Count - foundCount} more possible)");
                                break;
                            }
                        }
                    }
                    sb.AppendLine($"  Total scripts found: {foundCount} (of {guidToPath.Count} mapped)\n");
                }
            }

            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/Code Analysis/Debug Asset Dependencies")]
        public static void DebugAssetDependencies()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Asset Dependencies Debug ===\n");

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            if (sceneGuids.Length == 0)
            {
                sb.AppendLine("No scenes found.");
                Debug.Log(sb.ToString());
                return;
            }

            var scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[0]);
            sb.AppendLine($"Scene: {scenePath}");

            try
            {
                var deps = AssetDatabase.GetDependencies(scenePath, false);
                sb.AppendLine($"Total dependencies: {deps.Length}\n");

                var csDeps = deps.Where(d => d.EndsWith(".cs")).OrderBy(d => d).ToList();
                sb.AppendLine($"Script dependencies ({csDeps.Count}):\n");
                foreach (var dep in csDeps)
                {
                    sb.AppendLine($"  {dep}");
                }
            }
            catch (Exception e)
            {
                sb.AppendLine($"Error: {e.Message}");
            }

            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/Code Analysis/Debug Prefab Script References")]
        public static void DebugPrefabScriptRefs()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Prefab Script References Debug ===\n");

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            sb.AppendLine($"Found {prefabGuids.Length} prefabs\n");

            var analyzer = new UnityAssetReferenceAnalyzer();
            analyzer.BuildGuidMapping();

            int totalRefs = 0;
            int prefabsWithScripts = 0;

            foreach (var guid in prefabGuids.Take(50))
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!prefabPath.StartsWith("Assets/Tactics/")) continue;

                var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", prefabPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(absolutePath)) continue;

                var content = File.ReadAllText(absolutePath);
                var foundScripts = new List<string>();

                foreach (var kvp in analyzer.GetAllScriptGuidsForDebug())
                {
                    if (content.Contains(kvp.Key))
                    {
                        foundScripts.Add(kvp.Value);
                    }
                }

                if (foundScripts.Count > 0)
                {
                    prefabsWithScripts++;
                    totalRefs += foundScripts.Count;
                    sb.AppendLine($"{prefabPath}:");
                    foreach (var s in foundScripts)
                    {
                        sb.AppendLine($"  - {Path.GetFileNameWithoutExtension(s)}");
                    }
                    sb.AppendLine();
                }
            }

            if (prefabGuids.Length > 50)
                sb.AppendLine($"(Scanned first 50 of {prefabGuids.Length} prefabs)\n");

            sb.AppendLine($"Results: {totalRefs} script references across {prefabsWithScripts} prefabs");

            Debug.Log(sb.ToString());
        }
    }

    // Extension to expose GUID mapping for debug
    public static class UnityAssetReferenceAnalyzerDebugExtensions
    {
        public static Dictionary<string, string> GetAllScriptGuidsForDebug(this UnityAssetReferenceAnalyzer analyzer)
        {
            // Use reflection since the field is private
            var field = typeof(UnityAssetReferenceAnalyzer).GetField("_guidToScriptPath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(analyzer) as Dictionary<string, string> ?? new Dictionary<string, string>();
        }
    }
}
