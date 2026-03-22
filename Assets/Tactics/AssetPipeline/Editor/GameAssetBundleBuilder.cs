using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Tactics.AssetPipeline;
using UnityEditor;
using UnityEngine;

namespace Tactics.AssetPipeline.Editor
{
    public static class GameAssetBundleBuilder
    {
        /// <summary>Uses active build target, default bundle root <c>GameAssetBundles</c> under project root, default Streaming copy.</summary>
        /// <returns>False if the build was aborted or failed before completion.</returns>
        public static bool Build(GameAssetBuildConfig config, bool clearDestination)
        {
            return Build(config, clearDestination, null, null, null);
        }

        /// <param name="buildTargetOverride">When null, uses <see cref="EditorUserBuildSettings.activeBuildTarget"/>.</param>
        /// <param name="bundleBuildRoot">When null or empty, uses <c>Path.Combine(Directory.GetCurrentDirectory(), "GameAssetBundles")</c>. Platform subfolder is appended.</param>
        /// <param name="streamingBundlesDestinationDirectory">When null or empty, uses <c>Assets/StreamingAssets/{config.streamingSubfolder}</c>.</param>
        /// <returns>False if the build was aborted or failed before completion.</returns>
        public static bool Build(
            GameAssetBuildConfig config,
            bool clearDestination,
            BuildTarget? buildTargetOverride,
            string bundleBuildRoot,
            string streamingBundlesDestinationDirectory)
        {
            if (config == null)
            {
                Debug.LogError("[GameAssetBundleBuilder] Config is null.");
                return false;
            }

            var target = buildTargetOverride ?? EditorUserBuildSettings.activeBuildTarget;
            var folderName = target.ToString();
            var root = string.IsNullOrWhiteSpace(bundleBuildRoot)
                ? Path.Combine(Directory.GetCurrentDirectory(), "GameAssetBundles")
                : bundleBuildRoot.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var tempRoot = Path.Combine(root, folderName);

            if (clearDestination && Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
            Directory.CreateDirectory(tempRoot);

            var (builds, validationFailed) = CollectBundleBuilds(config);
            if (validationFailed)
            {
                Debug.LogError(
                    "[GameAssetBundleBuilder] Build aborted: fix scene/non-scene mix in the bundle group(s) logged above. " +
                    "Unity does not allow SceneAsset (.unity) and other assets in the same AssetBundle.");
                return false;
            }

            if (builds.Count == 0)
            {
                EditorUtility.DisplayDialog("Game Asset Pipeline", "No valid bundle groups to build.", "OK");
                return false;
            }

            var buildManifest = BuildPipeline.BuildAssetBundles(tempRoot, builds.ToArray(),
                BuildAssetBundleOptions.ChunkBasedCompression, target);

            if (buildManifest == null)
            {
                Debug.LogError("[GameAssetBundleBuilder] BuildPipeline.BuildAssetBundles returned null.");
                return false;
            }

            var configuredNames = new HashSet<string>(builds.Select(b => b.assetBundleName), StringComparer.Ordinal);
            var manifestRecords = new List<BundleRecord>();

            foreach (var bundleName in buildManifest.GetAllAssetBundles())
            {
                if (!configuredNames.Contains(bundleName))
                    continue;

                var deps = buildManifest.GetDirectDependencies(bundleName) ?? Array.Empty<string>();
                var resolvedDeps = deps.Where(d => configuredNames.Contains(d)).ToArray();
                var fileOnDisk = FindBundleFileOnDisk(tempRoot, bundleName);
                if (string.IsNullOrEmpty(fileOnDisk))
                {
                    Debug.LogError($"[GameAssetBundleBuilder] Could not find file for bundle '{bundleName}'.");
                    continue;
                }

                var fullPath = Path.Combine(tempRoot, fileOnDisk);
                manifestRecords.Add(new BundleRecord
                {
                    name = bundleName,
                    file = fileOnDisk,
                    hash = ComputeShortHash(fullPath),
                    size = new FileInfo(fullPath).Length,
                    deps = resolvedDeps
                });
            }

            var assetRecords = new List<AssetRecord>();
            foreach (var b in builds)
            {
                foreach (var assetPath in b.assetNames)
                {
                    var norm = assetPath.Replace('\\', '/');
                    assetRecords.Add(new AssetRecord { path = norm, bundle = b.assetBundleName });
                }
            }

            var gameManifest = new GameAssetManifest
            {
                version = 1,
                bundles = manifestRecords.ToArray(),
                assets = assetRecords.ToArray()
            };

            var json = JsonUtility.ToJson(gameManifest, true);
            var jsonTemp = Path.Combine(tempRoot, GameAssetPaths.ManifestFileName);
            File.WriteAllText(jsonTemp, json, Encoding.UTF8);

            var subfolder = string.IsNullOrWhiteSpace(config.streamingSubfolder)
                ? GameAssetPaths.StreamingBundlesFolder
                : config.streamingSubfolder.Trim().Replace('\\', '/').Trim('/');

            var destDir = string.IsNullOrWhiteSpace(streamingBundlesDestinationDirectory)
                ? Path.Combine(Application.dataPath, "StreamingAssets", subfolder)
                : streamingBundlesDestinationDirectory.Trim().TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            if (clearDestination && Directory.Exists(destDir))
            {
                foreach (var file in Directory.GetFiles(destDir))
                {
                    if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;
                    FileUtil.DeleteFileOrDirectory(file);
                }
            }

            Directory.CreateDirectory(destDir);

            foreach (var r in manifestRecords)
            {
                var src = Path.Combine(tempRoot, r.file);
                var dst = Path.Combine(destDir, r.file);
                File.Copy(src, dst, true);
            }

            File.Copy(jsonTemp, Path.Combine(destDir, GameAssetPaths.ManifestFileName), true);

            AssetDatabase.Refresh();
            Debug.Log($"[GameAssetBundleBuilder] Built {manifestRecords.Count} bundle(s) -> {destDir} (intermediate: {tempRoot})");
            return true;
        }

        private const int MaxPathsPerSectionInLog = 15;

        private static (List<AssetBundleBuild> builds, bool validationFailed) CollectBundleBuilds(
            GameAssetBuildConfig config)
        {
            var builds = new List<AssetBundleBuild>();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            var validationFailed = false;

            foreach (var group in config.groups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.bundleName))
                    continue;

                var name = group.bundleName.Trim();
                if (!usedNames.Add(name))
                {
                    Debug.LogError($"[GameAssetBundleBuilder] Duplicate bundle name: {name}");
                    continue;
                }

                var assets = CollectGroupAssets(group);
                if (assets.Count == 0)
                {
                    Debug.LogWarning($"[GameAssetBundleBuilder] Group '{name}' has no assets; skipped.");
                    continue;
                }

                if (!TryValidateSingleBundleContent(name, assets))
                {
                    validationFailed = true;
                    continue;
                }

                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = name,
                    assetNames = assets.ToArray()
                });
            }

            return (builds, validationFailed);
        }

        private static bool IsSceneAssetPath(string normalizedPath)
        {
            var t = AssetDatabase.GetMainAssetTypeAtPath(normalizedPath);
            var isSceneType = t == typeof(SceneAsset);
            var ext = Path.GetExtension(normalizedPath);
            var isUnityExt = string.Equals(ext, ".unity", StringComparison.OrdinalIgnoreCase);

            if (isUnityExt && !isSceneType && t != null)
            {
                Debug.LogWarning(
                    $"[GameAssetBundleBuilder] Path ends with .unity but main asset type is {t.Name}: {normalizedPath}");
            }

            return isSceneType || isUnityExt;
        }

        /// <summary>
        /// Returns false if the bundle mixes SceneAsset (.unity) with other assets (Unity forbids this).
        /// </summary>
        private static bool TryValidateSingleBundleContent(string bundleName, ICollection<string> normalizedPaths)
        {
            var scenes = new List<string>();
            var nonScenes = new List<string>();

            foreach (var p in normalizedPaths)
            {
                if (IsSceneAssetPath(p))
                    scenes.Add(p);
                else
                    nonScenes.Add(p);
            }

            if (scenes.Count == 0 || nonScenes.Count == 0)
                return true;

            var sb = new StringBuilder();
            sb.AppendLine(
                $"[GameAssetBundleBuilder] Bundle '{bundleName}' mixes scenes and non-scene assets (Unity error: \"Cannot mark assets and scenes in one AssetBundle\").");
            sb.AppendLine($"Counts: {scenes.Count} scene(s), {nonScenes.Count} non-scene asset(s).");
            sb.AppendLine("Scenes:");
            AppendPathsCapped(sb, scenes, MaxPathsPerSectionInLog);
            sb.AppendLine("Non-scene assets (sample):");
            AppendPathsCapped(sb, nonScenes, MaxPathsPerSectionInLog);
            sb.AppendLine(
                "Remediation: use a scene-only bundle (e.g. main_scenes) and a separate asset bundle; narrow rootFolder; add excludeFolders for folders that contain .unity under an asset root.");

            Debug.LogError(sb.ToString());
            return false;
        }

        private static void AppendPathsCapped(StringBuilder sb, List<string> paths, int maxCount)
        {
            var n = Math.Min(paths.Count, maxCount);
            for (var i = 0; i < n; i++)
                sb.AppendLine($"  - {paths[i]}");
            if (paths.Count > maxCount)
                sb.AppendLine($"  ... and {paths.Count - maxCount} more.");
        }

        /// <summary>
        /// Excludes Editor-folder assets, scripts, and asmdef files from bundle assignment.
        /// </summary>
        private static bool ShouldIncludeInAssetBundle(string normalizedAssetPath)
        {
            if (string.IsNullOrEmpty(normalizedAssetPath))
                return false;

            foreach (var segment in normalizedAssetPath.Split('/'))
            {
                if (segment.Length == 0)
                    continue;
                if (string.Equals(segment, "Editor", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            var ext = Path.GetExtension(normalizedAssetPath);
            if (string.Equals(ext, ".cs", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(ext, ".asmdef", StringComparison.OrdinalIgnoreCase))
                return false;

            var mainType = AssetDatabase.GetMainAssetTypeAtPath(normalizedAssetPath);
            if (mainType == typeof(MonoScript))
                return false;

            return true;
        }

        private static bool IsUnderExcludedFolder(string normalizedAssetPath, GameAssetBundleGroup group)
        {
            if (group.excludeFolders == null || group.excludeFolders.Count == 0)
                return false;

            foreach (var da in group.excludeFolders)
            {
                if (da == null)
                    continue;
                var ex = AssetDatabase.GetAssetPath(da).Replace('\\', '/');
                if (string.IsNullOrEmpty(ex) || !ex.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(normalizedAssetPath, ex, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (normalizedAssetPath.StartsWith(ex + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static List<string> CollectRecursiveFileAssetsUnderFolder(string folderPath)
        {
            var list = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { folderPath }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(p))
                    continue;
                list.Add(p.Replace('\\', '/'));
            }

            return list;
        }

        private static List<string> CollectDirectChildFileAssetsUnderFolder(string folderPath)
        {
            var list = new List<string>();
            var normalizedFolder = folderPath.Replace('\\', '/');
            foreach (var guid in AssetDatabase.FindAssets("", new[] { folderPath }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(p))
                    continue;
                var norm = p.Replace('\\', '/');
                var dir = Path.GetDirectoryName(norm)?.Replace('\\', '/');
                if (!string.Equals(dir, normalizedFolder, StringComparison.OrdinalIgnoreCase))
                    continue;
                list.Add(norm);
            }

            return list;
        }

        /// <summary>
        /// Returns true if <paramref name="norm"/> was handled as a glob (including errors). False = treat as single asset path.
        /// </summary>
        private static bool TryExpandExtraPathGlob(string norm, out List<string> expanded, out string warningMessage)
        {
            expanded = null;
            warningMessage = null;

            if (norm.EndsWith("/**", StringComparison.Ordinal))
            {
                var folder = norm.Substring(0, norm.Length - 3);
                if (string.IsNullOrEmpty(folder))
                {
                    warningMessage = $"Invalid glob (empty folder): {norm}";
                    expanded = new List<string>();
                    return true;
                }

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    warningMessage = $"Glob /** target is not a folder: {norm}";
                    expanded = new List<string>();
                    return true;
                }

                expanded = CollectRecursiveFileAssetsUnderFolder(folder);
                return true;
            }

            if (norm.EndsWith("/*", StringComparison.Ordinal))
            {
                var folder = norm.Substring(0, norm.Length - 2);
                if (string.IsNullOrEmpty(folder))
                {
                    warningMessage = $"Invalid glob (empty folder): {norm}";
                    expanded = new List<string>();
                    return true;
                }

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    warningMessage = $"Glob /* target is not a folder: {norm}";
                    expanded = new List<string>();
                    return true;
                }

                expanded = CollectDirectChildFileAssetsUnderFolder(folder);
                return true;
            }

            if (norm.Contains('*'))
            {
                warningMessage = $"Unsupported glob pattern: {norm}";
                expanded = new List<string>();
                return true;
            }

            return false;
        }

        private static HashSet<string> CollectGroupAssets(GameAssetBundleGroup group)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (group.rootFolder != null)
            {
                var folderPath = AssetDatabase.GetAssetPath(group.rootFolder);
                if (AssetDatabase.IsValidFolder(folderPath))
                {
                    var guids = AssetDatabase.FindAssets("", new[] { folderPath });
                    foreach (var guid in guids)
                    {
                        var p = AssetDatabase.GUIDToAssetPath(guid);
                        if (AssetDatabase.IsValidFolder(p))
                            continue;
                        var norm = p.Replace('\\', '/');
                        if (!ShouldIncludeInAssetBundle(norm))
                            continue;
                        if (IsUnderExcludedFolder(norm, group))
                            continue;
                        set.Add(norm);
                    }
                }
                else
                {
                    Debug.LogWarning($"[GameAssetBundleBuilder] rootFolder is not a valid folder: {folderPath}");
                }
            }

            if (group.extraAssetPaths != null)
            {
                foreach (var extra in group.extraAssetPaths)
                {
                    if (string.IsNullOrWhiteSpace(extra))
                        continue;
                    var norm = extra.Trim().Replace('\\', '/');
                    if (!norm.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogWarning($"[GameAssetBundleBuilder] Skipping non-Assets path: {norm}");
                        continue;
                    }

                    if (TryExpandExtraPathGlob(norm, out var expanded, out var globWarning))
                    {
                        if (globWarning != null)
                            Debug.LogWarning($"[GameAssetBundleBuilder] {globWarning}");

                        foreach (var path in expanded)
                        {
                            if (!ShouldIncludeInAssetBundle(path))
                                continue;
                            if (IsUnderExcludedFolder(path, group))
                                continue;
                            set.Add(path);
                        }

                        continue;
                    }

                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(norm) == null)
                    {
                        Debug.LogWarning($"[GameAssetBundleBuilder] Missing asset: {norm}");
                        continue;
                    }

                    if (!ShouldIncludeInAssetBundle(norm))
                    {
                        Debug.LogWarning(
                            $"[GameAssetBundleBuilder] Skipped extra path (Editor folder, script, or asmdef): {norm}");
                        continue;
                    }

                    if (IsUnderExcludedFolder(norm, group))
                    {
                        Debug.LogWarning(
                            $"[GameAssetBundleBuilder] Skipped extra path (under excluded folder): {norm}");
                        continue;
                    }

                    set.Add(norm);
                }
            }

            return set;
        }

        private static string FindBundleFileOnDisk(string tempRoot, string bundleName)
        {
            var direct = Path.Combine(tempRoot, bundleName);
            if (File.Exists(direct))
                return bundleName;

            foreach (var ext in new[] { ".bundle", ".unity3d" })
            {
                var withExt = bundleName + ext;
                if (File.Exists(Path.Combine(tempRoot, withExt)))
                    return withExt;
            }

            foreach (var file in Directory.GetFiles(tempRoot))
            {
                var fn = Path.GetFileName(file);
                if (fn.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(fn, GameAssetPaths.ManifestFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(fn, bundleName, StringComparison.OrdinalIgnoreCase))
                    return fn;
                if (string.Equals(Path.GetFileNameWithoutExtension(fn), bundleName, StringComparison.OrdinalIgnoreCase))
                    return fn;
            }

            return null;
        }

        private static string ComputeShortHash(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var bytes = sha.ComputeHash(stream);
            var sb = new StringBuilder(16);
            for (var i = 0; i < 8; i++)
                sb.AppendFormat("{0:x2}", bytes[i]);
            return sb.ToString();
        }
    }
}
