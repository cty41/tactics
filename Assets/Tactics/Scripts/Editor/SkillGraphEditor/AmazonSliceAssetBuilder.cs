using System.IO;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Editor.PresentationGraph;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Editor.SkillGraphEditor
{
    /// <summary>Rebuilds the published Amazon level chain while retaining existing Lv1 GUIDs.</summary>
    public static class AmazonSliceAssetBuilder
    {
        private const string GraphDirectory = "Assets/Tactics/Battle/Abilities/SkillGraphs";
        private const string ConfigDirectory = "Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs";
        private const string PoisonSpearProfilePath =
            "Assets/Tactics/Arts/PureRun/Tween/Projectiles/AmazonPoisonSpear.asset";

        [MenuItem("Tactics/Tools/Pure Run/Rebuild Amazon Slice Assets")]
        public static void RebuildAll()
        {
            Directory.CreateDirectory(GraphDirectory);
            Directory.CreateDirectory(ConfigDirectory);
            var poison = AssetDatabase.LoadAssetAtPath<BuffConfig>("Assets/Tactics/ScriptableObjects/Buffs/Poison.asset");
            if (poison == null)
                throw new FileNotFoundException("Poison buff asset is required.");

            BuildPrimary("Thrust_Graph", "突刺 Lv1", AmazonSkillKind.Thrust, 1, 2, null);
            BuildPrimary("Thrust_Lv2_Graph", "突刺 Lv2", AmazonSkillKind.Thrust, 2, 3, null);
            BuildPrimary("Thrust_Lv3_Graph", "突刺 Lv3", AmazonSkillKind.Thrust, 3, 3, null);
            BuildOrdered("MultiStab_Graph", "连续刺击 Lv1", 1, 3);
            BuildOrdered("MultiStab_Lv2_Graph", "连续刺击 Lv2", 2, 4);
            BuildPrimary("PoisonSpear_Graph", "毒矛 Lv1", AmazonSkillKind.PoisonSpear, 1,
                PureRunRangeCalibrationAssetBuilder.ExtendedPlayerRange, poison);
            BuildPrimary("PoisonSpear_Lv2_Graph", "毒矛 Lv2", AmazonSkillKind.PoisonSpear, 2,
                PureRunRangeCalibrationAssetBuilder.ExtendedPlayerRange, poison);
            BuildPrimary("PoisonSpear_Lv3_Graph", "毒矛 Lv3", AmazonSkillKind.PoisonSpear, 3,
                PureRunRangeCalibrationAssetBuilder.ExtendedPlayerRange, poison);
            BuildPoint("RecoverSpear_Graph", "召唤长矛 Lv1", AmazonSkillKind.RecoverSpear, 1,
                PureRunRangeCalibrationAssetBuilder.ExtendedPlayerRange);
            BuildPoint("RecoverSpear_Lv2_Graph", "召唤长矛 Lv2", AmazonSkillKind.RecoverSpear, 2,
                PureRunRangeCalibrationAssetBuilder.ExtendedPlayerRange);
            BuildSelf("PickupSpear_Graph", "拾取长矛", AmazonSkillKind.PickupSpear, 1);
            BuildPoint("Decoy_Graph", "分身 Lv1", AmazonSkillKind.Decoy, 1, 2);
            BuildPoint("Decoy_Lv2_Graph", "分身 Lv2", AmazonSkillKind.Decoy, 2, 2);

            CreateConfig("Thrust_Graph_Ability", Graph("Thrust_Graph"), 3, 2, "攻击前方2格内所有敌人。", false);
            CreateConfig("Thrust_Lv2_Graph_Ability", Graph("Thrust_Lv2_Graph"), 3, 3, "攻击前方3格内所有敌人。", false);
            CreateConfig("Thrust_Lv3_Graph_Ability", Graph("Thrust_Lv3_Graph"), 3, 3, "移动距离会提高本次突刺伤害。", false);
            CreateConfig("MultiStab_Graph_Ability", Graph("MultiStab_Graph"), 8, 3, "依次选择3段刺击目标。", false);
            CreateConfig("MultiStab_Lv2_Graph_Ability", Graph("MultiStab_Lv2_Graph"), 8, 3, "依次选择4段刺击目标。", false);
            CreateConfig("PoisonSpear_Graph_Ability", Graph("PoisonSpear_Graph"), 6,
                PureRunRangeCalibrationAssetBuilder.ExtendedPlayerRange, "造成8点物理伤害并使目标中毒。", false);
            CreateConfig("PoisonSpear_Lv2_Graph_Ability", Graph("PoisonSpear_Lv2_Graph"), 6,
                PureRunRangeCalibrationAssetBuilder.ExtendedPlayerRange, "造成10点伤害并使十字5格敌人中毒。", false);
            CreateConfig("PoisonSpear_Lv3_Graph_Ability", Graph("PoisonSpear_Lv3_Graph"), 6,
                PureRunRangeCalibrationAssetBuilder.ExtendedPlayerRange, "造成10点伤害并使九宫格敌人中毒。", false);
            CreateConfig("RecoverSpear_Graph_Ability", Graph("RecoverSpear_Graph"), 4,
                PureRunRangeCalibrationAssetBuilder.ExtendedPlayerRange, "召回5格内的落地长矛。", false);
            CreateConfig("RecoverSpear_Lv2_Graph_Ability", Graph("RecoverSpear_Lv2_Graph"), 4,
                PureRunRangeCalibrationAssetBuilder.ExtendedPlayerRange, "召回长矛并电击相邻敌人。", false);
            CreateConfig("PickupSpear_Graph_Ability", Graph("PickupSpear_Graph"), 0, 0, "免费拾取相邻落地长矛。", false);
            CreateConfig("Decoy_Graph_Ability", Graph("Decoy_Graph"), 6, 2, "后撤并在原地留下分身。", false);
            CreateConfig("Decoy_Lv2_Graph_Ability", Graph("Decoy_Lv2_Graph"), 6, 2, "后撤、留下分身并净化自身。", false);

            PureRunPresentationGraphAssetBuilder.RebuildThrustSamples();
            PureRunPresentationGraphAssetBuilder.RebuildPoisonSpearSamples();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            TLog.Info("[AmazonSliceAssetBuilder] Amazon level assets rebuilt.");
        }

        private static void BuildPrimary(
            string name, string displayName, AmazonSkillKind kind, int level, int range, BuffConfig poison)
        {
            var graph = ResetGraph(name, displayName, SkillTargetMode.PrimaryUnit);
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            var select = Add<SelectPrimaryTargetNodeRecord>(graph, SkillGraphNodeType.SelectPrimaryTarget);
            select.MinRange = 1;
            select.MaxRange = range;
            var effect = AddAmazon(graph, kind, level, poison);
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            if (kind == AmazonSkillKind.PoisonSpear)
            {
                var projectile = Add<ProjectileLaunchNodeRecord>(graph, SkillGraphNodeType.ProjectileLaunch);
                projectile.Speed = 7f;
                projectile.TravelTime = 0.3f;
                projectile.DropOnHit = false;
                projectile.RequiresLineOfSight = true;
                projectile.VisualProfile = AssetDatabase.LoadAssetAtPath<ProjectileVisualProfile>(
                    PoisonSpearProfilePath);
                var onHit = Add<OnHitNodeRecord>(graph, SkillGraphNodeType.OnHit);
                Link(graph, start, select, projectile, onHit, effect, finish);
            }
            else
            {
                Link(graph, start, select, effect, finish);
            }
            Save(graph);
        }

        private static void BuildOrdered(string name, string displayName, int level, int count)
        {
            var graph = ResetGraph(name, displayName, SkillTargetMode.OrderedMultiTarget);
            graph.Targeting.MinimumSelections = count;
            graph.Targeting.MaximumSelections = count;
            graph.Targeting.ConeDepth = 3;
            graph.Targeting.ConeWidth = 5;
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            var select = Add<SelectPrimaryTargetNodeRecord>(graph, SkillGraphNodeType.SelectPrimaryTarget);
            select.MinRange = 1;
            select.MaxRange = 3;
            var effect = AddAmazon(graph, AmazonSkillKind.MultiStab, level, null);
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, select, effect, finish);
            Save(graph);
        }

        private static void BuildPoint(string name, string displayName, AmazonSkillKind kind, int level, int range)
        {
            var graph = ResetGraph(name, displayName,
                kind == AmazonSkillKind.RecoverSpear ? SkillTargetMode.PhysicalObjectCell : SkillTargetMode.PathlessMove);
            graph.Targeting.AllowsEmptyCell = kind == AmazonSkillKind.Decoy;
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            var select = Add<SelectTargetPointNodeRecord>(graph, SkillGraphNodeType.SelectTargetPoint);
            select.MaxRange = range;
            var effect = AddAmazon(graph, kind, level, null);
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, select, effect, finish);
            Save(graph);
        }

        private static void BuildSelf(string name, string displayName, AmazonSkillKind kind, int level)
        {
            var graph = ResetGraph(name, displayName, SkillTargetMode.RecoveryAction);
            var start = Add<StartNodeRecord>(graph, SkillGraphNodeType.Start);
            var self = Add<SelectSelfNodeRecord>(graph, SkillGraphNodeType.SelectSelf);
            var effect = AddAmazon(graph, kind, level, null);
            var finish = Add<FinishNodeRecord>(graph, SkillGraphNodeType.Finish);
            Link(graph, start, self, effect, finish);
            Save(graph);
        }

        private static AmazonSkillNodeRecord AddAmazon(
            SkillGraphAsset graph, AmazonSkillKind kind, int level, BuffConfig poison)
        {
            var node = Add<AmazonSkillNodeRecord>(graph, SkillGraphNodeType.AmazonSkill);
            node.SkillKind = kind;
            node.Level = level;
            node.PoisonBuff = poison;
            return node;
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
            graph.Targeting.MinimumSelections = 1;
            graph.Targeting.MaximumSelections = 1;
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
