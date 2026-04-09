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
    /// Analyzes Unity assets (scenes, prefabs, ScriptableObjects) to find script references.
    /// Recursively scans ALL assets in the project, not just hardcoded paths.
    /// </summary>
    public class UnityAssetReferenceAnalyzer
    {
        private readonly Dictionary<string, string> _guidToScriptPath = new();
        private readonly List<AssetScriptReference> _allReferences = new();
        private readonly HashSet<string> _scannedAssetPaths = new(StringComparer.OrdinalIgnoreCase);

        // Scene and prefab scan results (for reporting)
        private readonly List<AssetScriptReference> _sceneReferences = new();
        private readonly List<AssetScriptReference> _prefabReferences = new();
        private readonly List<AssetScriptReference> _assetReferences = new();

        public IReadOnlyList<AssetScriptReference> SceneReferences => _sceneReferences;
        public IReadOnlyList<AssetScriptReference> PrefabReferences => _prefabReferences;
        public IReadOnlyList<AssetScriptReference> AssetReferences => _assetReferences;

        /// <summary>
        /// Builds a mapping from GUID to script path using AssetDatabase.
        /// Scans ALL .cs files in the Tactics/Scripts directory.
        /// </summary>
        public void BuildGuidMapping()
        {
            _guidToScriptPath.Clear();

            var tacticsScripts = AssetDatabase.FindAssets("t:Script", new[] { "Assets/Tactics/Scripts" });
            foreach (var guid in tacticsScripts)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".cs"))
                {
                    _guidToScriptPath[guid] = path;
                }
            }

            Debug.Log($"[UnityAssetReferenceAnalyzer] Built GUID mapping for {_guidToScriptPath.Count} scripts");
        }

        /// <summary>
        /// Analyzes ALL scenes in the project.
        /// Uses AssetDatabase.FindAssets("t:Scene") to discover scenes recursively.
        /// </summary>
        public void AnalyzeAllScenes()
        {
            _sceneReferences.Clear();

            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (var guid in sceneGuids)
            {
                var scenePath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(scenePath) || scenePath.Contains("/Packages/") ||
                    scenePath.Contains("/ThirdParty/"))
                    continue;

                AnalyzeSceneFile(scenePath);
            }

            Debug.Log($"[UnityAssetReferenceAnalyzer] Found {_sceneReferences.Count} script references in {GetUniqueAssetCount(_sceneReferences)} scenes");
        }

        /// <summary>
        /// Analyzes ALL prefabs in the project.
        /// Uses AssetDatabase.FindAssets("t:Prefab") to discover prefabs recursively.
        /// </summary>
        public void AnalyzeAllPrefabs()
        {
            _prefabReferences.Clear();

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in prefabGuids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(prefabPath) || prefabPath.Contains("/Packages/") ||
                    prefabPath.Contains("/ThirdParty/"))
                    continue;

                AnalyzePrefabFile(prefabPath);
            }

            Debug.Log($"[UnityAssetReferenceAnalyzer] Found {_prefabReferences.Count} script references in {GetUniqueAssetCount(_prefabReferences)} prefabs");
        }

        /// <summary>
        /// Analyzes ALL ScriptableObject .asset files for MonoScript GUID references.
        /// Scripts with [CreateAssetMenu] are referenced by .asset files.
        /// </summary>
        public void AnalyzeScriptableObjects()
        {
            _assetReferences.Clear();

            var assetGuids = AssetDatabase.FindAssets("t:ScriptableObject");
            foreach (var guid in assetGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/"))
                    continue;

                AnalyzeAssetFile(assetPath);
            }

            Debug.Log($"[UnityAssetReferenceAnalyzer] Found {_assetReferences.Count} script references in {GetUniqueAssetCount(_assetReferences)} scriptable objects");
        }

        #region Analysis Methods

        private void AnalyzeSceneFile(string scenePath)
        {
            if (_scannedAssetPaths.Contains(scenePath)) return;
            _scannedAssetPaths.Add(scenePath);

            // Method 1: AssetDatabase.GetDependencies (works for scenes)
            try
            {
                var deps = AssetDatabase.GetDependencies(scenePath, false);
                foreach (var dep in deps)
                {
                    if (dep.EndsWith(".cs"))
                    {
                        var depGuid = AssetDatabase.AssetPathToGUID(dep);
                        AddReference(scenePath, dep, depGuid);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityAssetReferenceAnalyzer] Failed to analyze scene {scenePath}: {e.Message}");
            }

            // Method 2: YAML GUID scan (fallback for edge cases)
            ScanYamlForScriptGuids(scenePath);
        }

        private void AnalyzePrefabFile(string prefabPath)
        {
            if (_scannedAssetPaths.Contains(prefabPath)) return;
            _scannedAssetPaths.Add(prefabPath);

            // Prefabs: AssetDatabase.GetDependencies does NOT return .cs dependencies
            // Must use YAML GUID scanning
            ScanYamlForScriptGuids(prefabPath);
        }

        private void AnalyzeAssetFile(string assetPath)
        {
            if (_scannedAssetPaths.Contains(assetPath)) return;
            _scannedAssetPaths.Add(assetPath);

            // ScriptableObject assets: YAML GUID scanning for m_Script
            ScanYamlForScriptGuids(assetPath);
        }

        /// <summary>
        /// Scans a serialized Unity asset file for MonoScript GUID references.
        /// Looks for patterns like:
        ///   m_Script: {fileID: 11500000, guid: GUID, type: 3}
        /// </summary>
        private void ScanYamlForScriptGuids(string assetPath)
        {
            try
            {
                var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(absolutePath)) return;

                var content = File.ReadAllText(absolutePath);

                // Find all guid: "GUID" patterns that appear near m_Script
                // Common pattern: m_Script: {fileID: 11500000, guid: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, type: 3}
                foreach (var kvp in _guidToScriptPath)
                {
                    var guid = kvp.Key;
                    var scriptPath = kvp.Value;

                    // Check if this script's GUID appears in the file
                    if (content.Contains(guid))
                    {
                        if (!HasReference(assetPath, scriptPath))
                        {
                            AddReference(assetPath, scriptPath, guid);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityAssetReferenceAnalyzer] Failed to scan {assetPath}: {e.Message}");
            }
        }

        #endregion

        #region Reference Tracking

        private void AddReference(string assetPath, string scriptPath, string scriptGuid)
        {
            var reference = new AssetScriptReference
            {
                AssetPath = assetPath,
                ScriptPath = scriptPath,
                ScriptGuid = scriptGuid
            };

            _allReferences.Add(reference);

            if (assetPath.EndsWith(".unity"))
                _sceneReferences.Add(reference);
            else if (assetPath.EndsWith(".prefab"))
                _prefabReferences.Add(reference);
            else
                _assetReferences.Add(reference);
        }

        private bool HasReference(string assetPath, string scriptPath)
        {
            return _allReferences.Any(r => r.AssetPath == assetPath && r.ScriptPath == scriptPath);
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Gets all unique script paths referenced by any asset.
        /// </summary>
        public HashSet<string> GetAllReferencedScriptPaths()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in _allReferences)
            {
                paths.Add(reference.ScriptPath);
            }
            return paths;
        }

        /// <summary>
        /// Gets a map: script path -> list of asset paths that reference it.
        /// </summary>
        public Dictionary<string, List<string>> GetScriptToAssetsMap()
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in _allReferences)
            {
                if (!map.ContainsKey(reference.ScriptPath))
                    map[reference.ScriptPath] = new List<string>();
                if (!map[reference.ScriptPath].Contains(reference.AssetPath))
                    map[reference.ScriptPath].Add(reference.AssetPath);
            }
            return map;
        }

        #endregion

        private static int GetUniqueAssetCount(List<AssetScriptReference> references)
        {
            return references.Select(r => r.AssetPath).Distinct().Count();
        }
    }
}
