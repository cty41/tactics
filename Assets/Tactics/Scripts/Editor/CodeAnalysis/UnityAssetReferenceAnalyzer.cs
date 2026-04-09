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
    /// Analyzes Unity assets (scenes, prefabs) to find script references.
    /// Uses AssetDatabase.GetDependencies() API for reliable dependency resolution.
    /// </summary>
    public class UnityAssetReferenceAnalyzer
    {
        private readonly Dictionary<string, string> _guidToScriptPath = new();
        private readonly List<AssetScriptReference> _sceneReferences = new();
        private readonly List<AssetScriptReference> _prefabReferences = new();

        public IReadOnlyList<AssetScriptReference> SceneReferences => _sceneReferences;
        public IReadOnlyList<AssetScriptReference> PrefabReferences => _prefabReferences;

        /// <summary>
        /// Builds a mapping from GUID to script path using AssetDatabase.
        /// Used for looking up script GUIDs found in asset dependencies.
        /// </summary>
        public void BuildGuidMapping()
        {
            _guidToScriptPath.Clear();

            var csFiles = Directory.GetFiles(Path.Combine(Application.dataPath, "Tactics", "Scripts"), "*.cs", SearchOption.AllDirectories);
            foreach (var file in csFiles)
            {
                // Convert absolute to relative: "D:\project\Assets\Tactics\Scripts\xx.cs" -> "Assets/Tactics/Scripts/xx.cs"
                var relativePath = file.Replace(Application.dataPath, "Assets").Replace('\\', '/');
                var guid = AssetDatabase.AssetPathToGUID(relativePath);
                if (!string.IsNullOrEmpty(guid))
                {
                    _guidToScriptPath[guid] = relativePath;
                }
            }

            Debug.Log($"[UnityAssetReferenceAnalyzer] Built GUID mapping for {_guidToScriptPath.Count} scripts");
        }

        /// <summary>
        /// Analyzes all scenes in the Tactics/Scenes folder.
        /// Uses AssetDatabase.GetDependencies() to find script references.
        /// </summary>
        public void AnalyzeScenes()
        {
            _sceneReferences.Clear();

            var scenePaths = new List<string>
            {
                "Assets/Tactics/Scenes/Home.unity",
                "Assets/Tactics/Scenes/Splash.unity",
                "Assets/Tactics/Scenes/Test1.unity"
            };

            foreach (var scenePath in scenePaths)
            {
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning($"[UnityAssetReferenceAnalyzer] Scene not found: {scenePath}");
                    continue;
                }

                AnalyzeAssetDependencies(scenePath, _sceneReferences);
            }

            Debug.Log($"[UnityAssetReferenceAnalyzer] Found {_sceneReferences.Count} script references in scenes");
        }

        /// <summary>
        /// Analyzes all prefabs in the Tactics/Arts folders.
        /// Uses AssetDatabase.GetDependencies() to find script references.
        /// </summary>
        public void AnalyzePrefabs()
        {
            _prefabReferences.Clear();

            var prefabFolders = new List<string>
            {
                "Assets/Tactics/Arts/Prefabs",
                "Assets/Tactics/Arts/UI"
            };

            foreach (var prefabFolder in prefabFolders)
            {
                if (!Directory.Exists(prefabFolder))
                {
                    Debug.LogWarning($"[UnityAssetReferenceAnalyzer] Prefab folder not found: {prefabFolder}");
                    continue;
                }

                var prefabFiles = Directory.GetFiles(prefabFolder, "*.prefab", SearchOption.AllDirectories);
                foreach (var prefabFile in prefabFiles)
                {
                    // Convert absolute path to relative: "D:\project\Assets\Tactics\Arts\Prefabs\xx.prefab" -> "Assets/Tactics/Arts/Prefabs/xx.prefab"
                    var relativePath = prefabFile.Replace(Application.dataPath, "Assets").Replace('\\', '/');
                    AnalyzePrefabFile(relativePath, _prefabReferences);
                }
            }

            Debug.Log($"[UnityAssetReferenceAnalyzer] Found {_prefabReferences.Count} script references in prefabs");
        }

        /// <summary>
        /// Analyzes an asset file to find script references using AssetDatabase.GetDependencies().
        /// This method relies solely on Unity's dependency tracking - no YAML parsing.
        /// </summary>
        private void AnalyzeAssetDependencies(string assetPath, List<AssetScriptReference> references)
        {
            try
            {
                var dependencies = AssetDatabase.GetDependencies(assetPath, false);

                foreach (var dep in dependencies)
                {
                    if (dep.EndsWith(".cs"))
                    {
                        var guid = AssetDatabase.AssetPathToGUID(dep);
                        if (!references.Any(r => r.ScriptPath == dep))
                        {
                            references.Add(new AssetScriptReference
                            {
                                AssetPath = assetPath,
                                ScriptPath = dep,
                                ScriptGuid = guid
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityAssetReferenceAnalyzer] Failed to analyze {assetPath}: {e.Message}");
            }
        }

        /// <summary>
        /// Scans a prefab file for MonoScript references using YAML GUID matching.
        /// This is required because AssetDatabase.GetDependencies() does NOT return
        /// script dependencies for prefabs - only for scenes.
        /// </summary>
        private void ScanPrefabMonoScripts(string prefabPath, List<AssetScriptReference> references)
        {
            try
            {
                // Convert relative to absolute path
                // "Assets/Tactics/Arts/Prefabs/xxx.prefab" -> "D:\project\Assets\Tactics\Arts\Prefabs\xxx.prefab"
                var relativePath = prefabPath.StartsWith("Assets/") ? prefabPath.Substring(7) : prefabPath;
                var absolutePath = Path.Combine(Application.dataPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                
                if (!File.Exists(absolutePath))
                {
                    Debug.LogWarning($"[UnityAssetReferenceAnalyzer] Prefab file not found: {absolutePath}");
                    return;
                }

                var content = File.ReadAllText(absolutePath);

                foreach (var kvp in _guidToScriptPath)
                {
                    var guid = kvp.Key;
                    var scriptPath = kvp.Value;

                    // Check if this script's GUID appears in the prefab
                    if (content.Contains(guid))
                    {
                        if (!references.Any(r => r.ScriptPath == scriptPath))
                        {
                            references.Add(new AssetScriptReference
                            {
                                AssetPath = prefabPath,
                                ScriptPath = scriptPath,
                                ScriptGuid = guid
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityAssetReferenceAnalyzer] Failed to scan prefab {prefabPath}: {e.Message}");
            }
        }

        /// <summary>
        /// Analyzes a prefab file to find script references.
        /// Uses Guid scanning because GetDependencies() does NOT return .cs files for prefabs.
        /// </summary>
        private void AnalyzePrefabFile(string prefabPath, List<AssetScriptReference> references)
        {
            // Primary method: Scan YAML for MonoScript GUIDs (required for prefabs)
            ScanPrefabMonoScripts(prefabPath, references);
        }

        /// <summary>
        /// Gets all unique script paths referenced by scenes and prefabs.
        /// </summary>
        public HashSet<string> GetAllReferencedScriptPaths()
        {
            var paths = new HashSet<string>();
            foreach (var refInfo in _sceneReferences)
            {
                paths.Add(refInfo.ScriptPath);
            }
            foreach (var refInfo in _prefabReferences)
            {
                paths.Add(refInfo.ScriptPath);
            }
            return paths;
        }

        /// <summary>
        /// Gets detailed reference information including which asset references each script.
        /// </summary>
        public Dictionary<string, List<string>> GetScriptToAssetsMap()
        {
            var map = new Dictionary<string, List<string>>();

            foreach (var refInfo in _sceneReferences)
            {
                if (!map.ContainsKey(refInfo.ScriptPath))
                {
                    map[refInfo.ScriptPath] = new List<string>();
                }
                if (!map[refInfo.ScriptPath].Contains(refInfo.AssetPath))
                {
                    map[refInfo.ScriptPath].Add(refInfo.AssetPath);
                }
            }

            foreach (var refInfo in _prefabReferences)
            {
                if (!map.ContainsKey(refInfo.ScriptPath))
                {
                    map[refInfo.ScriptPath] = new List<string>();
                }
                if (!map[refInfo.ScriptPath].Contains(refInfo.AssetPath))
                {
                    map[refInfo.ScriptPath].Add(refInfo.AssetPath);
                }
            }

            return map;
        }
    }

    public class AssetScriptReference
    {
        public string AssetPath { get; set; } = string.Empty;
        public string AssetType => AssetPath.EndsWith(".unity") ? "Scene" : "Prefab";
        public string ScriptPath { get; set; } = string.Empty;
        public string ScriptGuid { get; set; } = string.Empty;
    }
}
