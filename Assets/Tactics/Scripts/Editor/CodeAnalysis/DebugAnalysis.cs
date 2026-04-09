#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
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
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== GUID Mapping Debug ===\n");

            // Build GUID mapping for Tactics/Scripts
            var guidToPath = new Dictionary<string, string>();
            var csFiles = Directory.GetFiles("Assets/Tactics/Scripts", "*.cs", SearchOption.AllDirectories);
            
            sb.AppendLine($"Found {csFiles.Length} .cs files\n");

            foreach (var file in csFiles)
            {
                var relativePath = file.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/');
                var guid = AssetDatabase.AssetPathToGUID(relativePath);
                if (!string.IsNullOrEmpty(guid))
                {
                    guidToPath[guid] = relativePath;
                }
            }

            sb.AppendLine($"Built GUID mapping for {guidToPath.Count} scripts in Assets/Tactics/Scripts\n");

            // Check scene dependencies using AssetDatabase
            var scenePath = "Assets/Tactics/Scenes/Home.unity";
            if (File.Exists(scenePath))
            {
                sb.AppendLine($"--- Scene Dependencies: {scenePath} ---");
                var deps = AssetDatabase.GetDependencies(scenePath, false);
                int scriptCount = 0;
                foreach (var dep in deps)
                {
                    if (dep.EndsWith(".cs"))
                    {
                        sb.AppendLine($"  Script Dep: {dep}");
                        scriptCount++;
                    }
                }
                sb.AppendLine($"  Total script dependencies: {scriptCount}\n");
            }

            // Check content of scene for specific GUIDs
            scenePath = "Assets/Tactics/Scenes/Test1.unity";
            if (File.Exists(scenePath))
            {
                sb.AppendLine($"--- GUID Scan: {scenePath} ---");
                var content = File.ReadAllText(scenePath);
                int foundCount = 0;
                foreach (var kvp in guidToPath)
                {
                    if (content.Contains(kvp.Key))
                    {
                        sb.AppendLine($"  Found: {kvp.Key} -> {kvp.Value}");
                        foundCount++;
                        if (foundCount >= 10)
                        {
                            sb.AppendLine("  ... (limited to 10 for readability)");
                            break;
                        }
                    }
                }
                sb.AppendLine($"  Total project scripts found in scene: {foundCount} (of {guidToPath.Count} mapped)\n");
            }

            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/Code Analysis/Debug Asset Dependencies")]
        public static void DebugAssetDependencies()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Asset Dependencies Debug ===\n");

            var scenePath = "Assets/Tactics/Scenes/Home.unity";
            sb.AppendLine($"Scene: {scenePath}");

            var deps = AssetDatabase.GetDependencies(scenePath, false);
            sb.AppendLine($"Total dependencies: {deps.Length}");

            int csCount = 0;
            foreach (var dep in deps)
            {
                if (dep.EndsWith(".cs"))
                {
                    sb.AppendLine($"  .cs: {dep}");
                    csCount++;
                }
            }

            sb.AppendLine($"\nScript dependencies: {csCount}");

            Debug.Log(sb.ToString());
        }
    }
}
