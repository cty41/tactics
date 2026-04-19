#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Tactics.Common.Units;
using Tactics.Common.Highlighters;

namespace Tactics.Common.Units.Highlight.Editor
{
    /// <summary>
    /// Editor tool to migrate old Highlighter MonoBehaviour configurations to the new HighlightConfig system.
    /// Menu: Tactics > Migration > Migrate Highlighter Configs
    /// </summary>
    public static class HighlighterMigrationTool
    {
        [MenuItem("Tactics/Migration/Migrate Highlighter Configs On Selected Units")]
        public static void MigrateSelectedUnits()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select Unit GameObject(s) first.", "OK");
                return;
            }

            int migratedCount = 0;
            int skippedCount = 0;

            foreach (var go in selected)
            {
                var unit = go.GetComponent<Unit>();
                if (unit == null)
                {
                    skippedCount++;
                    Debug.LogWarning($"[HighlighterMigration] No Unit component on: {go.name}");
                    continue;
                }

                try
                {
                    Undo.RecordObject(unit, "Migrate Highlighter Configs");
                    MigrateUnitDirect(unit);
                    EditorUtility.SetDirty(unit);
                    migratedCount++;
                    Debug.Log($"[HighlighterMigration] Migrated: {go.name}");
                }
                catch (Exception e)
                {
                    skippedCount++;
                    Debug.LogError($"[HighlighterMigration] Failed to migrate {go.name}: {e.Message}");
                }
            }

            EditorUtility.DisplayDialog("Migration Complete", 
                $"Migrated: {migratedCount}\nSkipped: {skippedCount}", "OK");
        }

        /// <summary>
        /// Migrate highlighter configs by directly accessing Unit's old Highlighter list fields.
        /// </summary>
        private static void MigrateUnitDirect(Unit unit)
        {
            var unitType = typeof(Unit);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            // Define mapping from old field names to new config fields
            var fieldMappings = new (string oldField, HighlightConfig newConfig)[]
            {
                ("_unMarkFn", GetConfig(unit, "unMarkConfig")),
                ("_markAsSelectedFn", GetConfig(unit, "selectedConfig")),
                ("_markAsFriendlyFn", GetConfig(unit, "friendlyConfig")),
                ("_markAsFinishedFn", GetConfig(unit, "finishedConfig")),
                ("_markAsTargetable", GetConfig(unit, "targetableConfig")),
                ("_markAsAttackingFn", GetConfig(unit, "attackingConfig")),
                ("_markAsDefendingFn", GetConfig(unit, "defendingConfig")),
                ("_markAsMoving", GetConfig(unit, "movingConfig")),
                ("_unMarkAsMoving", GetConfig(unit, "unMovingConfig")),
                ("_markAsDestroyedFn", GetConfig(unit, "destroyedConfig")),
            };

            foreach (var (oldFieldName, config) in fieldMappings)
            {
                if (config == null) continue;

                var field = unitType.GetField(oldFieldName, flags);
                if (field == null)
                {
                    Debug.Log($"[HighlighterMigration] Field {oldFieldName} not found on Unit");
                    continue;
                }

                var highlighterList = field.GetValue(unit) as IEnumerable<MonoBehaviour>;
                if (highlighterList == null)
                {
                    Debug.Log($"[HighlighterMigration] Field {oldFieldName} value is null");
                    continue;
                }

                var list = highlighterList.ToList();
                Debug.Log($"[HighlighterMigration] Field {oldFieldName} has {list.Count} highlighters");

                foreach (var highlighter in list)
                {
                    if (highlighter == null)
                    {
                        Debug.Log($"[HighlighterMigration] Found null highlighter in {oldFieldName}");
                        continue;
                    }
                    Debug.Log($"[HighlighterMigration] Processing highlighter type: {highlighter.GetType().Name} in {oldFieldName}");
                    MigrateSingleHighlighter(highlighter, config);
                }
            }
        }

        private static HighlightConfig GetConfig(Unit unit, string configFieldName)
        {
            var configsField = typeof(Unit).GetField("_highlightConfigs",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var configs = configsField?.GetValue(unit) as UnitHighlightConfigs;
            if (configs == null) return null;

            var field = typeof(UnitHighlightConfigs).GetField(configFieldName);
            return field?.GetValue(configs) as HighlightConfig;
        }

        private static void MigrateSingleHighlighter(MonoBehaviour highlighter, HighlightConfig config)
        {
            var type = highlighter.GetType();

            switch (type.Name)
            {
                case "ScalingHighlighter":
                    if (!config.scaling)
                    {
                        config.scaling = true;
                        config.target = GetFieldValue<Transform>(highlighter, "_targetTransform");
                        config.duration = GetFieldValue(highlighter, "_duration", 1f);
                        config.targetValue = GetFieldValue(highlighter, "_targetScale", Vector3.one);
                    }
                    break;

                case "SpriteRendererHighlighter":
                    var spriteColor = GetFieldValue(highlighter, "_color", Color.white);
                    // Force re-migrate if color is white (previous migration might have failed)
                    if (!config.color || config.colorValue == Color.white)
                    {
                        config.color = true;
                        config.targetSprite = GetFieldValue<SpriteRenderer>(highlighter, "_sprite");
                        config.colorValue = spriteColor;
                        Debug.Log($"[HighlighterMigration] Migrated SpriteRendererHighlighter: color={config.colorValue}, target={config.targetSprite?.name}");
                    }
                    break;

                case "AnimationHighlighter":
                    if (!config.animation)
                    {
                        config.animation = true;
                        config.animator = GetFieldValue<Animator>(highlighter, "_animator");
                        config.parameter = GetFieldValue(highlighter, "_parameter", "");
                        config.delay = GetFieldValue(highlighter, "_delaySeconds", 0f);
                    }
                    break;

                case "DelayHighlighter":
                    if (!config.delayEffect)
                    {
                        config.delayEffect = true;
                        config.delaySeconds = GetFieldValue(highlighter, "_delaySeconds", 0f);
                    }
                    break;

                case "GameObjectActivatorHighlighter":
                    if (!config.activate)
                    {
                        config.activate = true;
                        config.targetObj = GetFieldValue<GameObject>(highlighter, "_target");
                        config.status = GetFieldValue(highlighter, "_activationStatus", true);
                    }
                    break;

                case "GameObjectsActivatorHighlighter":
                    if (!config.activateMulti)
                    {
                        config.activateMulti = true;
                        var targets = GetFieldValue<List<GameObject>>(highlighter, "_targets", new List<GameObject>());
                        config.targets.Clear();
                        config.targets.AddRange(targets);
                        config.multiStatus = GetFieldValue(highlighter, "_activationStatus", true);
                    }
                    break;

                case "SwayHighlighter":
                    if (!config.sway)
                    {
                        config.sway = true;
                        config.swayTarget = GetFieldValue<Transform>(highlighter, "_targetTransform");
                        config.swayDuration = GetFieldValue(highlighter, "_duration", 1f);
                        config.swayAmplitude = GetFieldValue(highlighter, "_amplitude", 0.1f);
                        config.swayFrequency = GetFieldValue(highlighter, "_frequency", 2f);
                    }
                    break;

                case "SpinningHighlighter":
                    if (!config.spinning)
                    {
                        config.spinning = true;
                        config.spinTarget = GetFieldValue<Transform>(highlighter, "_targetTransform");
                        config.spinDuration = GetFieldValue(highlighter, "_duration", 1f);
                        config.spinSpeed = GetFieldValue(highlighter, "_spinSpeed", 360f);
                        config.spinAxis = GetFieldValue(highlighter, "_spinAxis", Vector3.up);
                    }
                    break;

                case "RendererHighlighter":
                    if (!config.rendererColor)
                    {
                        config.rendererColor = true;
                        config.renderer = GetFieldValue<Renderer>(highlighter, "_renderer");
                        config.rendererColorValue = GetFieldValue(highlighter, "_color", Color.white);
                    }
                    break;

                case "SetSpriteOrderHighlighter":
                    if (!config.spriteOrder)
                    {
                        config.spriteOrder = true;
                        config.orderSprite = GetFieldValue<SpriteRenderer>(highlighter, "_sprite");
                        config.orderValue = GetFieldValue(highlighter, "_sortingOrder", 0);
                    }
                    break;
            }
        }

        /// <summary>
        /// Gets the value of a field from a MonoBehaviour instance using reflection.
        /// </summary>
        private static T GetFieldValue<T>(MonoBehaviour mb, string fieldName, T defaultValue = default)
        {
            var field = mb.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field == null) return defaultValue;
            var value = field.GetValue(mb);
            return value is T typed ? typed : defaultValue;
        }
    }
}
#endif