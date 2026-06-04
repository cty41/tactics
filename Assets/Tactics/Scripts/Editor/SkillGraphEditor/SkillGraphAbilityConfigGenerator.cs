using System.IO;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.SkillGraphEditor
{
    /// <summary>
    /// Editor-side generator/sync helper for SkillGraphAbilityConfig assets.
    /// Uses SerializedObject to populate inherited private AbilityConfig fields safely.
    /// </summary>
    public static class SkillGraphAbilityConfigGenerator
    {
        public const string SkillGraphDir = "Assets/Tactics/Battle/Abilities/SkillGraphs";
        public const string AbilityConfigDir = "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs";

        public static string BuildAbilityConfigPath(SkillGraphAsset graph, string overridePath = null)
        {
            if (!string.IsNullOrEmpty(overridePath))
                return overridePath;

            string baseName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(graph));
            return $"{AbilityConfigDir}/{baseName}_Ability.asset";
        }

        public static SkillGraphAbilityConfig FindAbilityConfigForGraph(SkillGraphAsset graph)
        {
            if (graph == null)
                return null;

            string expectedPath = BuildAbilityConfigPath(graph);
            var expected = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(expectedPath);
            if (expected != null && expected.SkillGraph == graph)
                return expected;

            string[] guids = AssetDatabase.FindAssets("t:SkillGraphAbilityConfig", new[] { AbilityConfigDir });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(path);
                if (config != null && config.SkillGraph == graph)
                    return config;
            }

            return null;
        }

        public static SkillGraphAbilityConfig CreateOrSync(
            SkillGraphAsset graph,
            string configPath = null,
            int manaCost = 0,
            int? targetRangeOverride = null,
            string iconAssetPath = null,
            bool overwriteExisting = true)
        {
            if (graph == null)
                return null;

            EnsureDirectoryExists(AbilityConfigDir);

            var existing = FindAbilityConfigForGraph(graph);
            bool created = false;

            if (existing == null)
            {
                string finalPath = BuildAbilityConfigPath(graph, configPath);
                existing = ScriptableObject.CreateInstance<SkillGraphAbilityConfig>();
                AssetDatabase.CreateAsset(existing, finalPath);
                created = true;
            }
            else if (!overwriteExisting)
            {
                return existing;
            }

            ApplySerializedFields(existing, graph, manaCost, targetRangeOverride, iconAssetPath, created);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();

            TLog.Info($"[SkillGraphAbilityConfigGenerator] {(created ? "Created" : "Synced")} '{AssetDatabase.GetAssetPath(existing)}' for graph '{graph.name}'.");
            return existing;
        }

        private static void ApplySerializedFields(
            SkillGraphAbilityConfig config,
            SkillGraphAsset graph,
            int manaCost,
            int? targetRangeOverride,
            string iconAssetPath,
            bool isCreate)
        {
            var serialized = new SerializedObject(config);

            SetString(serialized, "_displayName", graph.DisplayName);
            SetInt(serialized, "_manaCost", manaCost);
            SetFloat(serialized, "_cooldown", 0f);
            SetBool(serialized, "_isBasicAbility", false);

            var descriptionProp = serialized.FindProperty("_description");
            if (descriptionProp != null && (isCreate || string.IsNullOrEmpty(descriptionProp.stringValue)))
                descriptionProp.stringValue = graph.DisplayName;

            var iconProp = serialized.FindProperty("_icon");
            if (iconProp != null && !string.IsNullOrEmpty(iconAssetPath))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconAssetPath);
                if (sprite != null)
                    iconProp.objectReferenceValue = sprite;
            }

            var skillGraphProp = serialized.FindProperty("_skillGraph");
            if (skillGraphProp != null)
                skillGraphProp.objectReferenceValue = graph;

            SetInt(serialized, "_targetRange", targetRangeOverride ?? InferTargetRange(graph));

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static int InferTargetRange(SkillGraphAsset graph)
        {
            if (graph == null)
                return 1;

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i] is SelectPrimaryTargetNodeRecord selectPrimary)
                    return Mathf.Max(1, selectPrimary.MaxRange);
            }

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i] is SelectTargetPointNodeRecord selectPoint)
                    return Mathf.Max(1, selectPoint.MaxRange);
            }

            return 1;
        }

        private static void EnsureDirectoryExists(string assetDir)
        {
            if (AssetDatabase.IsValidFolder(assetDir))
                return;

            Directory.CreateDirectory(assetDir);
            AssetDatabase.Refresh();
        }

        private static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value;
        }

        private static void SetInt(SerializedObject serialized, string propertyName, int value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetBool(SerializedObject serialized, string propertyName, bool value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }
    }
}
