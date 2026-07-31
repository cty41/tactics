using System.IO;
using Tactics.Common.Skills.Graph;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.SkillGraphEditor
{
    /// <summary>
    /// Rebuilds the published Necromancer level chain while retaining existing Lv1 GUIDs.
    /// </summary>
    public static class NecromancerSliceAssetBuilder
    {
        private const string GraphDirectory = "Assets/Tactics/Battle/Abilities/SkillGraphs";
        private const string ConfigDirectory = "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs";
        private const string SkeletonPrefabPath = "Assets/Tactics/Arts/Prefabs/Units/Skeleton.prefab";
        private const string BoneSpearProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/BoneSpear.asset";

        [MenuItem("Tactics/Tools/Pure Run/Rebuild Necromancer Slice Assets")]
        public static void RebuildAll()
        {
            Directory.CreateDirectory(GraphDirectory);
            Directory.CreateDirectory(ConfigDirectory);

            var amplify = AssetDatabase.LoadAssetAtPath<BuffConfig>(
                "Assets/Tactics/ScriptableObjects/Buffs/CurseDamageAmplifier.asset");
            var fear = AssetDatabase.LoadAssetAtPath<BuffConfig>(
                "Assets/Tactics/ScriptableObjects/Buffs/Fear.asset");
            ConfigureCurse(amplify, "CurseDamageAmplifier", BuffEffectType.CurseDamageAmplifier, 5);
            ConfigureCurse(fear, "Fear", BuffEffectType.Fear, 1);

            var skeletonAttack1 = BuildAttack("SkeletonAttack_Lv1_Graph", 2f);
            var skeletonAttack2 = BuildAttack("SkeletonAttack_Lv2_Graph", 3f);
            var skeletonAttack3 = BuildAttack("SkeletonAttack_Lv3_Graph", 4f);
            var skeletonAttackConfig1 = CreateConfig("SkeletonAttack_Lv1_Ability", skeletonAttack1, 0, 1,
                "对相邻敌人造成2点物理伤害。", true);
            var skeletonAttackConfig2 = CreateConfig("SkeletonAttack_Lv2_Ability", skeletonAttack2, 0, 1,
                "对相邻敌人造成3点物理伤害。", true);
            var skeletonAttackConfig3 = CreateConfig("SkeletonAttack_Lv3_Ability", skeletonAttack3, 0, 1,
                "对相邻敌人造成4点物理伤害。", true);

            var fireball1 = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>($"{GraphDirectory}/Fireball_Lv1_Graph.asset");
            var fireball2 = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>($"{GraphDirectory}/Fireball_Lv2_Graph.asset");
            if (fireball1 == null || fireball2 == null)
                throw new FileNotFoundException("Mage Fireball level graphs must be built before Skeleton Mage assets.");
            var mageAttack1 = CreateIndependentConfig("SkeletonMageFireball_Lv1_Ability", fireball1, 0, 4,
                "零消耗施放火球术 Lv1。", true);
            var mageAttack2 = CreateIndependentConfig("SkeletonMageFireball_Lv2_Ability", fireball2, 0, 4,
                "零消耗施放火球术 Lv2。", true);

            BuildSummon("SummonSkeleton_Graph", "召唤骷髅 Lv1", NecromancerSkillKind.SummonSkeleton, 1, skeletonAttackConfig1);
            BuildSummon("SummonSkeleton_Lv2_Graph", "召唤骷髅 Lv2", NecromancerSkillKind.SummonSkeleton, 2, skeletonAttackConfig2);
            BuildSummon("SummonSkeleton_Lv3_Graph", "召唤骷髅 Lv3", NecromancerSkillKind.SummonSkeleton, 3, skeletonAttackConfig3);
            BuildCurse("Curse_Graph", "伤害加深诅咒 Lv1", NecromancerSkillKind.AmplifyDamage, 1, amplify, fear);
            BuildCurse("Curse_Lv2_Graph", "伤害加深诅咒 Lv2", NecromancerSkillKind.AmplifyDamage, 2, amplify, fear);
            BuildCurse("Curse_Lv3_Graph", "伤害加深诅咒 Lv3", NecromancerSkillKind.AmplifyDamage, 3, amplify, fear);
            BuildBoneSpear("BoneSpear_Graph", "骨矛 Lv1", 1);
            BuildBoneSpear("BoneSpear_Lv2_Graph", "骨矛 Lv2", 2);
            BuildBoneSpear("BoneSpear_Lv3_Graph", "骨矛 Lv3", 3);
            BuildSummon("SkeletonMage_Graph", "骷髅法师 Lv1", NecromancerSkillKind.SummonSkeletonMage, 1, mageAttack1);
            BuildSummon("SkeletonMage_Lv2_Graph", "骷髅法师 Lv2", NecromancerSkillKind.SummonSkeletonMage, 2, mageAttack2);
            BuildCurse("FearCurse_Graph", "恐惧诅咒 Lv1", NecromancerSkillKind.FearCurse, 1, amplify, fear);
            BuildCurse("FearCurse_Lv2_Graph", "恐惧诅咒 Lv2", NecromancerSkillKind.FearCurse, 2, amplify, fear);
            BuildSelf("BoneShield_Graph", "骨盾 Lv1", 1, amplify, fear);
            BuildSelf("BoneShield_Lv2_Graph", "骨盾 Lv2", 2, amplify, fear);

            CreateConfig("SummonSkeleton_Graph_Ability", Graph("SummonSkeleton_Graph"), 3, 999,
                "消耗1具尸体召唤1个骷髅，上限1。", false);
            CreateConfig("SummonSkeleton_Lv2_Graph_Ability", Graph("SummonSkeleton_Lv2_Graph"), 3, 999,
                "消耗1具尸体召唤强化骷髅，上限2。", false);
            CreateConfig("SummonSkeleton_Lv3_Graph_Ability", Graph("SummonSkeleton_Lv3_Graph"), 3, 999,
                "消耗1具尸体召唤强化骷髅，上限3。", false);
            CreateConfig("Curse_Graph_Ability", Graph("Curse_Graph"), 3, 5,
                "令单个敌人受到的所有伤害提高30%，持续5次行动。", false);
            CreateConfig("Curse_Lv2_Graph_Ability", Graph("Curse_Lv2_Graph"), 3, 5,
                "令十字5格内敌人受到的所有伤害提高30%。", false);
            CreateConfig("Curse_Lv3_Graph_Ability", Graph("Curse_Lv3_Graph"), 3, 5,
                "令3x3范围内敌人受到的所有伤害提高30%。", false);
            CreateConfig("BoneSpear_Graph_Ability", Graph("BoneSpear_Graph"), 6, 5,
                "对直线首个敌人造成7点魔法伤害。", false);
            CreateConfig("BoneSpear_Lv2_Graph_Ability", Graph("BoneSpear_Lv2_Graph"), 4, 5,
                "以更低消耗对直线首个敌人造成7点魔法伤害。", false);
            CreateConfig("BoneSpear_Lv3_Graph_Ability", Graph("BoneSpear_Lv3_Graph"), 4, 5,
                "对终点前直线上的所有敌人造成7点魔法伤害。", false);
            CreateConfig("SkeletonMage_Graph_Ability", Graph("SkeletonMage_Graph"), 7, 999,
                "消耗1具尸体召唤使用火球术 Lv1 的骷髅法师。", false);
            CreateConfig("SkeletonMage_Lv2_Graph_Ability", Graph("SkeletonMage_Lv2_Graph"), 7, 999,
                "消耗1具尸体召唤使用火球术 Lv2 的骷髅法师，上限2。", false);
            CreateConfig("FearCurse_Graph_Ability", Graph("FearCurse_Graph"), 7, 5,
                "令单个敌人下一次行动开始时远离施法者。", false);
            CreateConfig("FearCurse_Lv2_Graph_Ability", Graph("FearCurse_Lv2_Graph"), 7, 5,
                "令十字5格内敌人下一次行动开始时远离施法者。", false);
            CreateConfig("BoneShield_Graph_Ability", Graph("BoneShield_Graph"), 8, 0,
                "获得魅力两倍的物理伤害护盾。", false);
            CreateConfig("BoneShield_Lv2_Graph_Ability", Graph("BoneShield_Lv2_Graph"), 8, 0,
                "获得魅力两倍的全伤害护盾。", false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TLog.Info("[NecromancerSliceAssetBuilder] Necromancer level assets rebuilt.");
        }

        private static SkillGraphAsset BuildSummon(
            string name, string displayName, NecromancerSkillKind kind, int level, AbilityConfig attack)
        {
            // PrimaryUnit is intentionally retained so ResolveTargetMode can infer the
            // physical-object protocol from SelectCorpseTargetNodeRecord.
            var graph = ResetGraph(name, displayName, SkillTargetMode.PrimaryUnit);
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            var select = Add<SelectCorpseTargetNodeRecord>(graph, SkillGraphNodeType.SelectCorpseTarget);
            select.MinRange = 0;
            select.MaxRange = 999;
            var effect = AddNecromancerNode(graph, kind, level, null, null, attack);
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, select, effect, finish);
            Save(graph);
            return graph;
        }

        private static SkillGraphAsset BuildCurse(
            string name, string displayName, NecromancerSkillKind kind, int level,
            BuffConfig amplify, BuffConfig fear)
        {
            var mode = level == 1 ? SkillTargetMode.PrimaryUnit : SkillTargetMode.AnyCellCenter;
            var graph = ResetGraph(name, displayName, mode);
            graph.Targeting.AllowsEmptyCell = level > 1;
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            SkillGraphNodeRecord select;
            if (level == 1)
            {
                var primary = Add<SelectPrimaryTargetNodeRecord>(graph, SkillGraphNodeType.SelectPrimaryTarget);
                primary.MinRange = 1;
                primary.MaxRange = 5;
                select = primary;
            }
            else
            {
                var point = Add<SelectTargetPointNodeRecord>(graph, SkillGraphNodeType.SelectTargetPoint);
                point.MaxRange = 5;
                select = point;
            }
            var effect = AddNecromancerNode(graph, kind, level, amplify, fear, null);
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, select, effect, finish);
            Save(graph);
            return graph;
        }

        private static SkillGraphAsset BuildBoneSpear(string name, string displayName, int level)
        {
            var mode = level >= 3 ? SkillTargetMode.AnyCellCenter : SkillTargetMode.PrimaryUnit;
            var graph = ResetGraph(name, displayName, mode);
            graph.Targeting.AllowsEmptyCell = level >= 3;
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            SkillGraphNodeRecord select;
            if (level >= 3)
            {
                var point = Add<SelectTargetPointNodeRecord>(graph, SkillGraphNodeType.SelectTargetPoint);
                point.MaxRange = 5;
                select = point;
            }
            else
            {
                var primary = Add<SelectPrimaryTargetNodeRecord>(graph, SkillGraphNodeType.SelectPrimaryTarget);
                primary.MinRange = 1;
                primary.MaxRange = 5;
                select = primary;
            }
            var projectile = Add<ProjectileLaunchNodeRecord>(graph, SkillGraphNodeType.ProjectileLaunch);
            projectile.TravelTime = 0.25f;
            projectile.Speed = 12f;
            projectile.VisualProfile = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                BoneSpearProfilePath);
            // Bone Spear owns unit piercing and wall stopping itself. Generic projectile
            // line-of-sight would incorrectly reject a farther endpoint behind the first enemy.
            projectile.RequiresLineOfSight = false;
            var onHit = Add<OnHitNodeRecord>(graph, SkillGraphNodeType.OnHit);
            var effect = AddNecromancerNode(graph, NecromancerSkillKind.BoneSpear, level, null, null, null);
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, select, projectile, onHit, effect, finish);
            Save(graph);
            return graph;
        }

        private static SkillGraphAsset BuildSelf(
            string name, string displayName, int level, BuffConfig amplify, BuffConfig fear)
        {
            var graph = ResetGraph(name, displayName, SkillTargetMode.PrimaryUnit);
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            var self = Add<SelectSelfNodeRecord>(graph, SkillGraphNodeType.SelectSelf);
            var effect = AddNecromancerNode(graph, NecromancerSkillKind.BoneShield, level, amplify, fear, null);
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, self, effect, finish);
            Save(graph);
            return graph;
        }

        private static SkillGraphAsset BuildAttack(string name, float damage)
        {
            var graph = ResetGraph(name, name, SkillTargetMode.PrimaryUnit);
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            var select = Add<SelectPrimaryTargetNodeRecord>(graph, SkillGraphNodeType.SelectPrimaryTarget);
            select.MinRange = 1;
            select.MaxRange = 1;
            var hit = Add<ApplyDamageNodeRecord>(graph, SkillGraphNodeType.ApplyDamage);
            hit.BaseDamage = damage;
            hit.DamageType = SkillGraphDamageType.Physical;
            hit.ElementType = ElementType.None;
            hit.CanCrit = false;
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, select, hit, finish);
            Save(graph);
            return graph;
        }

        private static NecromancerSkillNodeRecord AddNecromancerNode(
            SkillGraphAsset graph, NecromancerSkillKind kind, int level,
            BuffConfig amplify, BuffConfig fear, AbilityConfig attack)
        {
            var node = Add<NecromancerSkillNodeRecord>(graph, SkillGraphNodeType.NecromancerSkill);
            node.SkillKind = kind;
            node.Level = level;
            node.AmplifyDamageBuff = amplify;
            node.FearBuff = fear;
            node.SummonPrefabPath = SkeletonPrefabPath;
            node.SummonAttack = attack;
            node.SummonBrain = AssetDatabase.LoadAssetAtPath<AiBrainAsset>(
                kind == NecromancerSkillKind.SummonSkeletonMage
                    ? "Assets/Tactics/AI/FireDemonBrain.asset"
                    : "Assets/Tactics/AI/BasicMeleeBrain.asset");
            return node;
        }

        private static void ConfigureCurse(BuffConfig config, string name, BuffEffectType effect, int duration)
        {
            if (config == null)
                throw new FileNotFoundException($"Required buff '{name}' is missing.");
            var serialized = new SerializedObject(config);
            serialized.FindProperty("_buffName").stringValue = name;
            serialized.FindProperty("_defaultDuration").intValue = duration;
            serialized.FindProperty("_canAct").boolValue = true;
            serialized.FindProperty("_polarity").enumValueIndex = (int)BuffPolarity.Harmful;
            serialized.FindProperty("_effectType").enumValueIndex = (int)effect;
            serialized.FindProperty("_curseCategory").stringValue = "Curse";
            serialized.FindProperty("_refreshStrategy").enumValueIndex = (int)BuffRefreshStrategy.RefreshDuration;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        private static SkillGraphAbilityConfig CreateConfig(
            string name, SkillGraphAsset graph, int mana, int range, string description, bool basic)
        {
            var config = SkillGraphAbilityConfigGenerator.CreateOrSync(
                graph, $"{ConfigDirectory}/{name}.asset", mana, range, overwriteExisting: true);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("_description").stringValue = description;
            serialized.FindProperty("_isBasicAbility").boolValue = basic;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static SkillGraphAbilityConfig CreateIndependentConfig(
            string name, SkillGraphAsset graph, int mana, int range, string description, bool basic)
        {
            string path = $"{ConfigDirectory}/{name}.asset";
            var config = AssetDatabase.LoadAssetAtPath<SkillGraphAbilityConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SkillGraphAbilityConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            config.name = name;
            var serialized = new SerializedObject(config);
            serialized.FindProperty("_displayName").stringValue = graph.DisplayName;
            serialized.FindProperty("_description").stringValue = description;
            serialized.FindProperty("_manaCost").intValue = mana;
            serialized.FindProperty("_targetRange").intValue = range;
            serialized.FindProperty("_isBasicAbility").boolValue = basic;
            serialized.FindProperty("_skillGraph").objectReferenceValue = graph;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static SkillGraphAsset ResetGraph(string name, string displayName, SkillTargetMode mode)
        {
            string path = $"{GraphDirectory}/{name}.asset";
            var graph = AssetDatabase.LoadAssetAtPath<SkillGraphAsset>(path);
            if (graph == null)
            {
                graph = ScriptableObject.CreateInstance<SkillGraphAsset>();
                AssetDatabase.CreateAsset(graph, path);
            }
            graph.name = name;
            graph.DisplayName = displayName;
            graph.Version = 1;
            graph.Nodes.Clear();
            graph.Edges.Clear();
            graph.Targeting.Mode = mode;
            graph.Targeting.MinimumSelections = mode == SkillTargetMode.PrimaryUnit ? 1 : 0;
            graph.Targeting.MaximumSelections = mode == SkillTargetMode.PrimaryUnit ? 1 : 0;
            graph.Targeting.AllowsEmptyCell = false;
            graph.Targeting.UsesPathfinding = false;
            return graph;
        }

        private static T Add<T>(SkillGraphAsset graph, SkillGraphNodeType type) where T : SkillGraphNodeRecord =>
            (T)graph.AddNode(type, Vector2.zero);

        private static void Link(SkillGraphAsset graph, params SkillGraphNodeRecord[] nodes)
        {
            for (int index = 0; index + 1 < nodes.Length; index++)
                graph.AddEdge(nodes[index].NodeId, nodes[index + 1].NodeId);
        }

        private static void Save(SkillGraphAsset graph) => EditorUtility.SetDirty(graph);
        private static SkillGraphAsset Graph(string name) =>
            AssetDatabase.LoadAssetAtPath<SkillGraphAsset>($"{GraphDirectory}/{name}.asset");
    }
}
