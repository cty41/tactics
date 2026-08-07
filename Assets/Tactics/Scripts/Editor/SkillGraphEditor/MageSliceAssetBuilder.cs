using System.Collections.Generic;
using System.IO;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Units.Classes;
using Tactics.Editor.PresentationGraph;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.SkillGraphEditor
{
    /// <summary>
    /// Deterministically creates the published Mage level assets used by Pure Run.
    /// Existing level-one assets are updated in place so their GUID references remain valid.
    /// </summary>
    public static class MageSliceAssetBuilder
    {
        private const string GraphDirectory = "Assets/Tactics/Battle/Abilities/SkillGraphs";
        private const string ConfigDirectory = "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs";
        private const string BuffDirectory = "Assets/Tactics/ScriptableObjects/Buffs";
        private const string FireDemonPrefabPath = "Assets/Tactics/Arts/Prefabs/Units/FireDemon.prefab";
        private const string MageBluePrefabPath = "Assets/Tactics/Arts/Prefabs/Units/MageBlue.prefab";
        private const string ProjectileProfileRoot = "Assets/Tactics/Arts/PureRun/Tween/Projectiles";
        private const string VisualCueProfileRoot =
            "Assets/Tactics/Arts/PureRun/VFX/PilotoAdapted/Profiles";
        private const string PresentationGraphRoot =
            "Assets/Tactics/Arts/PureRun/Presentation";
        private const string FireDemonRolePath = "Assets/Tactics/Battle/Classes/FireDemon.asset";
        private const string FireDemonBrainPath = "Assets/Tactics/AI/FireDemonBrain.asset";

        [MenuItem("Tactics/Tools/Pure Run/Rebuild Mage Slice Assets")]
        public static void RebuildAll()
        {
            EnsureFolders();
            var ignite = AssetDatabase.LoadAssetAtPath<BuffConfig>("Assets/Tactics/Battle/Buffs/Ignite.asset");
            ConfigureBuff(ignite, "Ignite", BuffEffectType.Burning, BuffPolarity.Harmful, 2,
                canAct: true, refreshStrategy: BuffRefreshStrategy.AddStacks,
                triggerTiming: BuffTriggerTiming.TurnStart, elementType: ElementType.Fire);
            var slow = CreateOrUpdateBuff("Slow", BuffEffectType.Slow, BuffPolarity.Harmful, 2);
            var stun = CreateOrUpdateBuff("Stun", BuffEffectType.Stun, BuffPolarity.Harmful, 1, canAct: false);
            var iceArmorLv1 = CreateOrUpdateBuff("IceArmor_Lv1", BuffEffectType.DamageReduction, BuffPolarity.Beneficial, 2, reduction: 0.25f);
            var iceArmorLv2 = CreateOrUpdateBuff("IceArmor_Lv2", BuffEffectType.DamageReduction, BuffPolarity.Beneficial, 2,
                reduction: 0.25f, retaliationBuff: slow, retaliationDuration: 2);

            BuildProjectileMageGraph("Fireball_Lv1_Graph", "火球术 Lv1", MageSkillKind.Fireball, 1, 4, ignite, slow, stun, null);
            BuildProjectileMageGraph("Fireball_Lv2_Graph", "火球术 Lv2", MageSkillKind.Fireball, 2, 4, ignite, slow, stun, null);
            BuildProjectileMageGraph("Fireball_Lv3_Graph", "火球术 Lv3", MageSkillKind.Fireball, 3, 4, ignite, slow, stun, null);

            BuildProjectileMageGraph("IceBolt_Graph", "寒冰箭 Lv1", MageSkillKind.IceBolt, 1,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, ignite, slow, stun, null);
            BuildProjectileMageGraph("IceBolt_Lv2_Graph", "寒冰箭 Lv2", MageSkillKind.IceBolt, 2,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, ignite, slow, stun, null);
            BuildProjectileMageGraph("IceBolt_Lv3_Graph", "寒冰箭 Lv3", MageSkillKind.IceBolt, 3,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, ignite, slow, stun, null);

            BuildDirectMageGraph("Lightning_Graph", "霹雳闪电 Lv1", MageSkillKind.Lightning, 1,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, ignite, slow, stun, null);
            BuildDirectMageGraph("Lightning_Lv2_Graph", "霹雳闪电 Lv2", MageSkillKind.Lightning, 2,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, ignite, slow, stun, null);
            BuildDirectMageGraph("Lightning_Lv3_Graph", "霹雳闪电 Lv3", MageSkillKind.Lightning, 3,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, ignite, slow, stun, null);

            var summonLv1 = BuildSelfMageGraph("SummonFireDemon_Graph", "召唤火魔 Lv1", MageSkillKind.SummonFireDemon, 1,
                ignite, slow, stun, null, includeSelfSelection: false);
            var summonLv2 = BuildSelfMageGraph("SummonFireDemon_Lv2_Graph", "召唤火魔 Lv2", MageSkillKind.SummonFireDemon, 2,
                ignite, slow, stun, null, includeSelfSelection: false);

            var armorLv1 = BuildSelfMageGraph("IceArmor_Graph", "冰甲 Lv1", MageSkillKind.IceArmor, 1,
                ignite, slow, stun, iceArmorLv1, includeSelfSelection: true);
            var armorLv2 = BuildSelfMageGraph("IceArmor_Lv2_Graph", "冰甲 Lv2", MageSkillKind.IceArmor, 2,
                ignite, slow, stun, iceArmorLv2, includeSelfSelection: true);

            var teleportLv1 = BuildTeleportGraph("Teleport_Graph", "瞬移术 Lv1", true);
            var teleportLv2 = BuildTeleportGraph("Teleport_Lv2_Graph", "瞬移术 Lv2", false);
            var fireDemonAttack = BuildFireDemonAttackGraph(ignite);

            CreateConfig("Fireball_Lv1_Ability", Graph("Fireball_Lv1_Graph"), 7, 4, "对首个敌人造成2点魔法伤害并施加2层点燃。");
            CreateConfig("Fireball_Lv2_Ability", Graph("Fireball_Lv2_Graph"), 7, 4, "主目标伤害提高，并对正交相邻敌人造成溅射与3层点燃。");
            CreateConfig("Fireball_Lv3_Ability", Graph("Fireball_Lv3_Graph"), 7, 4, "引爆主目标已有点燃后，结算二级火球效果。");
            CreateConfig("IceBolt_Graph_Ability", Graph("IceBolt_Graph"), 6,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, "造成8点冰霜魔法伤害并减速1回合。");
            CreateConfig("IceBolt_Lv2_Graph_Ability", Graph("IceBolt_Lv2_Graph"), 4,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, "造成8点冰霜魔法伤害并减速2回合。");
            CreateConfig("IceBolt_Lv3_Graph_Ability", Graph("IceBolt_Lv3_Graph"), 4,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, "命中后反弹至3格内最近的另一敌人。");
            CreateConfig("Lightning_Graph_Ability", Graph("Lightning_Graph"), 6,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, "无视路径与视线直接造成9点闪电魔法伤害。");
            CreateConfig("Lightning_Lv2_Graph_Ability", Graph("Lightning_Lv2_Graph"), 6,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, "直接造成9点闪电魔法伤害，并有25%概率眩晕。");
            CreateConfig("Lightning_Lv3_Graph_Ability", Graph("Lightning_Lv3_Graph"), 6,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, "直接造成11点闪电魔法伤害，并有50%概率眩晕。");
            CreateConfig("SummonFireDemon_Graph_Ability", summonLv1, 7,
                PureRunRangeCalibrationAssetBuilder.FireDemonSummonRange, "替换旧火魔并在附近召唤1只火魔。");
            CreateConfig("SummonFireDemon_Lv2_Graph_Ability", summonLv2, 7,
                PureRunRangeCalibrationAssetBuilder.FireDemonSummonRange, "替换旧火魔并在3格内尝试召唤2只火魔。");
            CreateConfig("IceArmor_Graph_Ability", armorLv1, 5, 0, "2回合内受到的伤害降低25%。");
            CreateConfig("IceArmor_Lv2_Graph_Ability", armorLv2, 5, 0, "冰甲减伤期间，近战攻击者会被减速2回合。");
            CreateConfig("Teleport_Graph_Ability", teleportLv1, 8,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, "瞬移到4格内可见的合法空格。");
            CreateConfig("Teleport_Lv2_Graph_Ability", teleportLv2, 5,
                PureRunRangeCalibrationAssetBuilder.StandardPlayerRange, "瞬移到4格内任意合法空格，无需视线。");
            var fireAttackConfig = CreateConfig("FireDemonAttack_Ability", fireDemonAttack, 0, 3,
                "对1至3格内敌人造成4点火焰魔法伤害并施加1层点燃。", isBasic: true);

            var role = CreateOrUpdateFireDemonRole(fireAttackConfig);
            var brain = CreateOrUpdateFireDemonBrain();
            CreateOrUpdateFireDemonPrefab(role, brain);
            PureRunPresentationGraphAssetBuilder.RebuildFireballSamples();
            PureRunPresentationGraphAssetBuilder.RebuildLightningSamples();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TLog.Info("[MageSliceAssetBuilder] Mage level assets and Fire Demon prefab rebuilt.");
        }

        /// <summary>
        /// Rebuilds only the three Lightning graphs after their visual cue profiles change.
        /// </summary>
        public static void RebuildLightningVisualSample()
        {
            PatchVisualCueProfile("Lightning_Graph", "LightningLv1");
            PatchVisualCueProfile("Lightning_Lv2_Graph", "LightningLv2");
            PatchVisualCueProfile("Lightning_Lv3_Graph", "LightningLv3");
        }

        private static void PatchVisualCueProfile(string assetName, string profileName)
        {
            string graphPath = $"{GraphDirectory}/{assetName}.asset";
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(graphPath);
            if (graph == null)
                throw new FileNotFoundException($"Lightning graph is missing: {graphPath}");

            PlayVisualCueNodeRecord legacyCue = null;
            foreach (SkillGraphNodeRecord node in graph.Nodes)
            {
                if (node is not PlayVisualCueNodeRecord candidate)
                    continue;
                if (legacyCue != null)
                    throw new InvalidDataException($"Lightning graph has multiple visual cues: {graphPath}");
                legacyCue = candidate;
            }

            string profilePath = $"{VisualCueProfileRoot}/{profileName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(profilePath);
            if (profile == null)
                throw new FileNotFoundException($"Lightning visual cue profile is missing: {profilePath}");

            if (legacyCue != null)
            {
                if (legacyCue.Profile == profile)
                    return;
                legacyCue.Profile = profile;
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssetIfDirty(graph);
                return;
            }

            string presentationName = assetName.EndsWith("_Graph", System.StringComparison.Ordinal)
                ? assetName[..^6]
                : assetName;
            string presentationPath = $"{PresentationGraphRoot}/{presentationName}_Presentation.asset";
            var presentation = AssetDatabase.LoadAssetAtPath<BattlePresentationGraph>(presentationPath);
            if (presentation == null)
                throw new FileNotFoundException($"Lightning presentation graph is missing: {presentationPath}");
            PresentationPrefabFxNodeRecord cue = null;
            foreach (PresentationNodeRecord node in presentation.Nodes)
            {
                if (node is not PresentationPrefabFxNodeRecord candidate)
                    continue;
                if (cue != null)
                    throw new InvalidDataException($"Lightning presentation graph has multiple prefab FX nodes: {presentationPath}");
                cue = candidate;
            }
            if (cue == null)
                throw new InvalidDataException($"Lightning presentation graph has no prefab FX node: {presentationPath}");
            if (cue.Profile == profile)
                return;
            cue.Profile = profile;
            EditorUtility.SetDirty(presentation);
            AssetDatabase.SaveAssetIfDirty(presentation);
        }

        private static SkillGraphAsset BuildProjectileMageGraph(
            string assetName, string displayName, MageSkillKind kind, int level, int range,
            BuffConfig ignite, BuffConfig slow, BuffConfig stun, BuffConfig armor)
        {
            var graph = ResetGraph(assetName, displayName, SkillTargetMode.PrimaryUnit);
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            var select = Add<SelectPrimaryTargetNodeRecord>(graph, SkillGraphNodeType.SelectPrimaryTarget);
            select.MinRange = 1;
            select.MaxRange = range;
            var projectile = Add<ProjectileLaunchNodeRecord>(graph, SkillGraphNodeType.ProjectileLaunch);
            projectile.TravelTime = kind == MageSkillKind.IceBolt ? 0.25f : 0.5f;
            projectile.Speed = kind == MageSkillKind.IceBolt ? 12f : 8f;
            string profileName = kind == MageSkillKind.IceBolt ? "Ice" : "Fire";
            projectile.VisualProfile = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                $"{ProjectileProfileRoot}/{profileName}.asset");
            var onHit = Add<OnHitNodeRecord>(graph, SkillGraphNodeType.OnHit);
            var effect = AddMageNode(graph, kind, level, ignite, slow, stun, armor);
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, select, projectile, onHit, effect, finish);
            Save(graph);
            return graph;
        }

        private static SkillGraphAsset BuildDirectMageGraph(
            string assetName, string displayName, MageSkillKind kind, int level, int range,
            BuffConfig ignite, BuffConfig slow, BuffConfig stun, BuffConfig armor,
            string fireDemonPrefabPath = FireDemonPrefabPath)
        {
            var graph = ResetGraph(assetName, displayName, SkillTargetMode.PrimaryUnit);
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            var select = Add<SelectPrimaryTargetNodeRecord>(graph, SkillGraphNodeType.SelectPrimaryTarget);
            select.MinRange = 1;
            select.MaxRange = range;
            var cue = Add<PlayVisualCueNodeRecord>(graph, SkillGraphNodeType.PlayVisualCue);
            cue.Profile = AssetDatabase.LoadAssetAtPath<VisualCueProfile>(
                $"{VisualCueProfileRoot}/LightningLv{level}.asset");
            var effect = AddMageNode(
                graph, kind, level, ignite, slow, stun, armor, fireDemonPrefabPath);
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, select, cue, effect, finish);
            Save(graph);
            return graph;
        }

        private static SkillGraphAsset BuildSelfMageGraph(
            string assetName, string displayName, MageSkillKind kind, int level,
            BuffConfig ignite, BuffConfig slow, BuffConfig stun, BuffConfig armor, bool includeSelfSelection)
        {
            var targetMode = includeSelfSelection ? SkillTargetMode.PrimaryUnit : SkillTargetMode.AnyCellCenter;
            var graph = ResetGraph(assetName, displayName, targetMode);
            if (!includeSelfSelection)
                graph.Targeting.AllowsEmptyCell = true;
            var chain = new List<SkillGraphNodeRecord> { Add<StartNodeRecord>(graph, SkillGraphNodeType.Start) };
            if (includeSelfSelection)
                chain.Add(Add<SelectSelfNodeRecord>(graph, SkillGraphNodeType.SelectSelf));
            chain.Add(AddMageNode(graph, kind, level, ignite, slow, stun, armor));
            chain.Add(Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish));
            Link(graph, chain.ToArray());
            Save(graph);
            return graph;
        }

        private static SkillGraphAsset BuildTeleportGraph(string assetName, string displayName, bool requiresLineOfSight)
        {
            var graph = ResetGraph(assetName, displayName, SkillTargetMode.PathlessMove);
            graph.Targeting.AllowsEmptyCell = true;
            graph.Targeting.UsesPathfinding = false;
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            var teleport = Add<TeleportNodeRecord>(graph, SkillGraphNodeType.Teleport);
            teleport.MaxRange = PureRunRangeCalibrationAssetBuilder.StandardPlayerRange;
            teleport.RequiresLineOfSight = requiresLineOfSight;
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, teleport, finish);
            Save(graph);
            return graph;
        }

        private static SkillGraphAsset BuildFireDemonAttackGraph(BuffConfig ignite)
        {
            var graph = ResetGraph("FireDemonAttack_Graph", "火魔攻击", SkillTargetMode.PrimaryUnit);
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            var select = Add<SelectPrimaryTargetNodeRecord>(graph, SkillGraphNodeType.SelectPrimaryTarget);
            select.MinRange = 1;
            select.MaxRange = 3;
            var damage = Add<ApplyDamageNodeRecord>(graph, SkillGraphNodeType.ApplyDamage);
            damage.BaseDamage = 4f;
            damage.DamageType = SkillGraphDamageType.Magical;
            damage.ElementType = ElementType.Fire;
            damage.IsRanged = true;
            damage.CanCrit = false;
            var projectile = Add<ProjectileLaunchNodeRecord>(graph, SkillGraphNodeType.ProjectileLaunch);
            projectile.Speed = 8f;
            projectile.TravelTime = 0.3f;
            projectile.VisualProfile = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                $"{ProjectileProfileRoot}/Fire.asset");
            var onHit = Add<OnHitNodeRecord>(graph, SkillGraphNodeType.OnHit);
            var buff = Add<ApplyBuffNodeRecord>(graph, SkillGraphNodeType.ApplyBuff);
            buff.BuffConfig = ignite;
            buff.Duration = 1;
            buff.RequiresSuccessfulHit = true;
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, select, projectile, onHit, damage, buff, finish);
            Save(graph);
            return graph;
        }

        private static MageSkillNodeRecord AddMageNode(
            SkillGraphAsset graph, MageSkillKind kind, int level,
            BuffConfig ignite, BuffConfig slow, BuffConfig stun, BuffConfig armor,
            string fireDemonPrefabPath = FireDemonPrefabPath)
        {
            var node = Add<MageSkillNodeRecord>(graph, SkillGraphNodeType.MageSkill);
            node.SkillKind = kind;
            node.Level = level;
            node.BurningBuff = ignite;
            node.SlowBuff = slow;
            node.StunBuff = stun;
            node.IceArmorBuff = armor;
            node.FireDemonPrefabPath = fireDemonPrefabPath;
            return node;
        }

        private static SkillGraphAbilityConfig CreateConfig(
            string assetName, SkillGraphAsset graph, int mana, int range, string description, bool isBasic = false)
        {
            string path = $"{ConfigDirectory}/{assetName}.asset";
            var config = SkillGraphAbilityConfigGenerator.CreateOrSync(graph, path, mana, range, overwriteExisting: true);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("_description").stringValue = description;
            serialized.FindProperty("_isBasicAbility").boolValue = isBasic;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static BuffConfig CreateOrUpdateBuff(
            string name, BuffEffectType type, BuffPolarity polarity, int duration,
            bool canAct = true, float reduction = 0f, BuffConfig retaliationBuff = null, int retaliationDuration = 0)
        {
            string path = $"{BuffDirectory}/{name}.asset";
            var config = AssetDatabase.LoadAssetAtPath<BuffConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BuffConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            var serialized = new SerializedObject(config);
            serialized.FindProperty("_buffName").stringValue = name;
            serialized.FindProperty("_defaultDuration").intValue = duration;
            serialized.FindProperty("_canAct").boolValue = canAct;
            serialized.FindProperty("_polarity").enumValueIndex = (int)polarity;
            serialized.FindProperty("_effectType").enumValueIndex = (int)type;
            serialized.FindProperty("_refreshStrategy").enumValueIndex = (int)BuffRefreshStrategy.RefreshDuration;
            serialized.FindProperty("_damageReductionPercent").floatValue = reduction;
            serialized.FindProperty("_meleeRetaliationBuff").objectReferenceValue = retaliationBuff;
            serialized.FindProperty("_meleeRetaliationDuration").intValue = retaliationDuration;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static void ConfigureBuff(
            BuffConfig config,
            string name,
            BuffEffectType type,
            BuffPolarity polarity,
            int duration,
            bool canAct,
            BuffRefreshStrategy refreshStrategy,
            BuffTriggerTiming triggerTiming,
            ElementType elementType)
        {
            if (config == null)
                throw new FileNotFoundException($"Required buff asset '{name}' is missing.");

            var serialized = new SerializedObject(config);
            serialized.FindProperty("_buffName").stringValue = name;
            serialized.FindProperty("_defaultDuration").intValue = duration;
            serialized.FindProperty("_canAct").boolValue = canAct;
            serialized.FindProperty("_polarity").enumValueIndex = (int)polarity;
            serialized.FindProperty("_effectType").enumValueIndex = (int)type;
            serialized.FindProperty("_refreshStrategy").enumValueIndex = (int)refreshStrategy;
            serialized.FindProperty("_triggerTiming").enumValueIndex = (int)triggerTiming;
            serialized.FindProperty("_elementType").enumValueIndex = (int)elementType;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static RoleConfig CreateOrUpdateFireDemonRole(AbilityConfig attack)
        {
            var role = AssetDatabase.LoadAssetAtPath<RoleConfig>(FireDemonRolePath);
            if (role == null)
            {
                role = ScriptableObject.CreateInstance<RoleConfig>();
                AssetDatabase.CreateAsset(role, FireDemonRolePath);
            }

            var serialized = new SerializedObject(role);
            serialized.FindProperty("_displayName").stringValue = "火魔";
            serialized.FindProperty("_roleType").enumValueIndex = (int)RoleType.Mage;
            var abilities = serialized.FindProperty("_abilities");
            abilities.arraySize = 1;
            abilities.GetArrayElementAtIndex(0).objectReferenceValue = attack;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(role);
            return role;
        }

        private static AiBrainAsset CreateOrUpdateFireDemonBrain()
        {
            var source = AssetDatabase.LoadAssetAtPath<AiBrainAsset>("Assets/Tactics/AI/BasicMeleeBrain.asset");
            if (source == null)
                throw new FileNotFoundException("BasicMeleeBrain is required for Fire Demon AI.");

            var brain = AssetDatabase.LoadAssetAtPath<AiBrainAsset>(FireDemonBrainPath);
            if (brain == null)
            {
                brain = Object.Instantiate(source);
                brain.name = "FireDemonBrain";
                AssetDatabase.CreateAsset(brain, FireDemonBrainPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, brain);
            }

            var serialized = new SerializedObject(brain);
            serialized.FindProperty("_preferredMinimumRange").intValue = 2;
            serialized.FindProperty("_preferredMaximumRange").intValue = 3;
            serialized.FindProperty("_preferredRangeRepositionBonus").floatValue = 100f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(brain);
            return brain;
        }

        private static void CreateOrUpdateFireDemonPrefab(RoleConfig role, AiBrainAsset brain)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(MageBluePrefabPath);
            if (source == null)
                throw new FileNotFoundException("MageBlue prefab is required for Fire Demon visuals.", MageBluePrefabPath);

            var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null)
                throw new System.InvalidOperationException("Failed to instantiate MageBlue for Fire Demon.");

            try
            {
                instance.name = "FireDemon";
                var unit = instance.GetComponent<Unit>();
                if (unit == null)
                    throw new System.InvalidOperationException("MageBlue prefab has no Unit component.");

                var serialized = new SerializedObject(unit);
                serialized.FindProperty("_roleConfig").objectReferenceValue = role;
                serialized.FindProperty("_health").floatValue = 12f;
                serialized.FindProperty("_constitution").intValue = 3;
                serialized.FindProperty("_charisma").intValue = 0;
                serialized.FindProperty("_speed").floatValue = 4f;
                serialized.FindProperty("_movementPoints").floatValue = 4f;
                serialized.FindProperty("_attackRange").intValue = 3;
                serialized.FindProperty("_attackFactor").intValue = 4;
                serialized.FindProperty("_aiBrainAsset").objectReferenceValue = brain;
                serialized.FindProperty("_canReceiveHealing").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.gameObject.name == "Sprite")
                        renderer.color = new Color(1f, 0.35f, 0.08f, 1f);
                }

                PrefabUtility.SaveAsPrefabAsset(instance, FireDemonPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static SkillGraphAsset ResetGraph(string assetName, string displayName, SkillTargetMode targetMode)
        {
            string path = $"{GraphDirectory}/{assetName}.asset";
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
            if (graph == null)
            {
                graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
                AssetDatabase.CreateAsset(graph, path);
            }

            graph.name = assetName;
            graph.DisplayName = displayName;
            graph.Version = 1;
            graph.Nodes.Clear();
            graph.Edges.Clear();
            graph.Targeting.Mode = targetMode;
            graph.Targeting.MinimumSelections = targetMode == SkillTargetMode.PrimaryUnit ? 1 : 0;
            graph.Targeting.MaximumSelections = targetMode == SkillTargetMode.PrimaryUnit ? 1 : 0;
            graph.Targeting.AllowsEmptyCell = false;
            graph.Targeting.UsesPathfinding = true;
            return graph;
        }

        private static T Add<T>(SkillGraphAsset graph, SkillGraphNodeType type) where T : SkillGraphNodeRecord
        {
            return (T)graph.AddNode(type, Vector2.zero);
        }

        private static void Link(SkillGraphAsset graph, params SkillGraphNodeRecord[] nodes)
        {
            for (int index = 0; index + 1 < nodes.Length; index++)
                graph.AddEdge(nodes[index].NodeId, nodes[index + 1].NodeId);
        }

        private static void Save(SkillGraphAsset graph)
        {
            EditorUtility.SetDirty(graph);
        }

        private static SkillGraphAsset Graph(string assetName) =>
            AssetDatabase.LoadAssetAtPath<SkillGraphAsset>($"{GraphDirectory}/{assetName}.asset");

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(GraphDirectory);
            Directory.CreateDirectory(ConfigDirectory);
            Directory.CreateDirectory(BuffDirectory);
        }
    }
}
