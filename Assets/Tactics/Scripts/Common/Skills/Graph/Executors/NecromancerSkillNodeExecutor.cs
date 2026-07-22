using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Interactables;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Resolves Necromancer level semantics and keeps corpse replacement transactional.
    /// </summary>
    public sealed class NecromancerSkillNodeExecutor : ISkillNodeExecutor
    {
        private const string SkeletonCategory = "Skeleton";
        private const string SkeletonMageCategory = "SkeletonMage";

        public SkillGraphNodeType NodeType => SkillGraphNodeType.NecromancerSkill;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (NecromancerSkillNodeRecord)node;
            return record.SkillKind switch
            {
                NecromancerSkillKind.SummonSkeleton => ExecuteSummon(record, context, false),
                NecromancerSkillKind.SummonSkeletonMage => ExecuteSummon(record, context, true),
                NecromancerSkillKind.AmplifyDamage => Task.FromResult(ExecuteCurse(record, context, record.AmplifyDamageBuff)),
                NecromancerSkillKind.FearCurse => Task.FromResult(ExecuteCurse(record, context, record.FearBuff)),
                NecromancerSkillKind.BoneSpear => Task.FromResult(ExecuteBoneSpear(record, context)),
                NecromancerSkillKind.BoneShield => Task.FromResult(ExecuteBoneShield(record, context)),
                _ => Task.FromResult(SkillNodeExecutionResult.Failed("Unsupported Necromancer skill kind."))
            };
        }

        private static async Task<SkillNodeExecutionResult> ExecuteSummon(
            NecromancerSkillNodeRecord record,
            SkillExecutionContext context,
            bool isMage)
        {
            var caster = context.Caster;
            var grid = context.GridController;
            var corpse = ResolveCorpse(context);
            var spawnCell = corpse?.CurrentCell;
            if (caster == null || grid?.UnitManager == null || corpse == null || spawnCell == null)
                return SkillNodeExecutionResult.Failed("A valid corpse is required.");

            var manager = GameAssetManager.Instance;
            var prefab = manager?.Load<GameObject>(GameAssetManager.NormalizeAssetPath(record.SummonPrefabPath));
            if (prefab == null || record.SummonAttack == null)
                return SkillNodeExecutionResult.Failed("Summon dependencies are missing.");

            GameObject gameObject = null;
            IUnit unit = null;
            try
            {
                gameObject = UnityEngine.Object.Instantiate(
                    prefab, spawnCell.WorldPosition.ToVector3(), Quaternion.identity, grid.UnitManager.ContainerTransform);
                gameObject.SetActive(false);
                gameObject.name = isMage ? "SkeletonMage" : "Skeleton";
                unit = gameObject.GetComponent<IUnit>();
                if (unit == null)
                    throw new InvalidOperationException("Summon prefab has no IUnit component.");

                unit.CanReceiveHealing = false;
                unit.OwnerUnitId = caster.UnitID;
                unit.OwnerUnit = caster;
                unit.PlayerNumber = caster.PlayerNumber;
                unit.CurrentCell = spawnCell;
                if (unit is Unit concrete)
                {
                    concrete.ApplyAbilityConfigs(new[] { record.SummonAttack });
                    concrete.ApplyAiBrain(record.SummonBrain);
                }
                unit.Initialize(grid);
                ConfigureSummonStats(unit, record.Level, isMage);
                unit.Facing = caster.Facing;
            }
            catch (UnassignedReferenceException) when (grid is Testing.SkillGraphTestGridController && unit != null)
            {
                ConfigureSummonStats(unit, record.Level, isMage);
                unit.Facing = caster.Facing;
                TLog.Warning($"[NecromancerSkill] Skipped scene-only initialization for '{gameObject.name}'.");
            }
            catch (Exception exception)
            {
                if (gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
                TLog.Error($"[NecromancerSkill] Summon preparation failed: {exception.Message}");
                return SkillNodeExecutionResult.Failed("Summon preparation failed.");
            }

            // All fallible preparation is complete before the corpse or an old summon changes.
            corpse.Consume();
            spawnCell.CurrentUnits.Add(unit);
            spawnCell.IsTaken = true;
            gameObject.SetActive(true);
            grid.UnitManager.AddUnit(unit);

            string category = isMage ? SkeletonMageCategory : SkeletonCategory;
            int maximum = isMage ? Math.Min(2, record.Level) : Math.Min(3, record.Level);
            var registry = SummonRegistry.For(grid);
            var replacements = registry.Register(caster, category, unit, maximum);
            foreach (var replacement in replacements)
                registry.Despawn(replacement);

            context.TargetCorpses = new List<Corpse> { corpse };
            context.RecordEvent(isMage ? "SkeletonMageSummoned" : "SkeletonSummoned", record.NodeId, unit);
            await Task.CompletedTask;
            return SkillNodeExecutionResult.Success();
        }

        private static Corpse ResolveCorpse(SkillExecutionContext context)
        {
            return context.TargetCorpses?
                .Where(corpse => corpse != null && !corpse.IsDestroyed && corpse.CurrentCell != null)
                .OrderBy(corpse => context.Caster?.CurrentCell?.GetDistance(corpse.CurrentCell) ?? int.MaxValue)
                .ThenBy(corpse => corpse.CurrentCell.GridCoordinates.x)
                .ThenBy(corpse => corpse.CurrentCell.GridCoordinates.y)
                .FirstOrDefault();
        }

        private static void ConfigureSummonStats(IUnit unit, int level, bool isMage)
        {
            if (unit is not Unit concrete)
                return;

            if (isMage)
            {
                concrete.Speed = 4f;
                concrete.MaxMovementPoints = 3f;
                concrete.MovementPoints = 3f;
                concrete.MaxHealth = level >= 2 ? 8f : 6f;
            }
            else
            {
                concrete.MaxHealth = level switch { >= 3 => 12f, 2 => 10f, _ => 8f };
            }

            concrete.Health = concrete.MaxHealth;
        }

        private static SkillNodeExecutionResult ExecuteCurse(
            NecromancerSkillNodeRecord record,
            SkillExecutionContext context,
            BuffConfig config)
        {
            if (config == null)
                return SkillNodeExecutionResult.Failed("Curse config is missing.");

            var targets = ResolveCurseTargets(record, context).ToList();
            if (targets.Count == 0)
                return SkillNodeExecutionResult.Failed("No enemy is affected by the curse.");

            int duration = record.SkillKind == NecromancerSkillKind.AmplifyDamage ? 5 : 1;
            foreach (var target in targets)
                target.AddBuff(new Buff(config, context.Caster, duration));
            context.TargetSet = targets;
            return SkillNodeExecutionResult.Success();
        }

        private static IEnumerable<IUnit> ResolveCurseTargets(
            NecromancerSkillNodeRecord record,
            SkillExecutionContext context)
        {
            if (record.Level <= 1)
            {
                if (context.PrimaryTarget != null && context.PrimaryTarget.PlayerNumber != context.Caster.PlayerNumber)
                    yield return context.PrimaryTarget;
                yield break;
            }

            var center = context.TargetPoint ?? context.PrimaryTarget?.CurrentCell;
            if (center == null)
                yield break;

            int cx = center.GridCoordinates.x;
            int cy = center.GridCoordinates.y;
            foreach (var unit in context.GridController.UnitManager.GetUnits()
                .Where(unit => unit != null && !unit.IsDowned && unit.PlayerNumber != context.Caster.PlayerNumber
                    && unit.CurrentCell != null)
                .OrderBy(unit => unit.UnitID))
            {
                int dx = Math.Abs(unit.CurrentCell.GridCoordinates.x - cx);
                int dy = Math.Abs(unit.CurrentCell.GridCoordinates.y - cy);
                bool included = record.Level == 2 ? dx + dy <= 1 : dx <= 1 && dy <= 1;
                if (included)
                    yield return unit;
            }
        }

        private static SkillNodeExecutionResult ExecuteBoneSpear(
            NecromancerSkillNodeRecord record,
            SkillExecutionContext context)
        {
            if (record.Level < 3)
            {
                var target = ResolveFirstEnemyOnLine(context, context.PrimaryTarget?.CurrentCell);
                if (target == null)
                    return SkillNodeExecutionResult.Failed("No enemy for Bone Spear.");
                ApplyBoneSpearDamage(context.Caster, target);
                context.PrimaryTarget = target;
                return SkillNodeExecutionResult.Success();
            }

            var endpoint = context.TargetPoint ?? context.PrimaryTarget?.CurrentCell;
            var path = BuildStraightPath(context.Caster?.CurrentCell, endpoint, context);
            if (path == null)
                return SkillNodeExecutionResult.Failed("Bone Spear requires a straight-line endpoint.");

            var targets = new List<IUnit>();
            foreach (var cell in path)
            {
                foreach (var target in cell.CurrentUnits
                    .Where(unit => unit != null && !unit.IsDowned && unit.PlayerNumber != context.Caster.PlayerNumber)
                    .OrderBy(unit => unit.UnitID))
                {
                    ApplyBoneSpearDamage(context.Caster, target);
                    targets.Add(target);
                }
            }

            context.TargetSet = targets;
            return SkillNodeExecutionResult.Success();
        }

        private static IUnit ResolveFirstEnemyOnLine(SkillExecutionContext context, ICell endpoint)
        {
            var path = BuildStraightPath(context.Caster?.CurrentCell, endpoint, context);
            return path?.SelectMany(cell => cell.CurrentUnits)
                .FirstOrDefault(unit => unit != null && !unit.IsDowned
                    && unit.PlayerNumber != context.Caster.PlayerNumber);
        }

        private static List<ICell> BuildStraightPath(ICell start, ICell end, SkillExecutionContext context)
        {
            if (start == null || end == null || context.GridController?.CellManager == null)
                return null;
            int dx = end.GridCoordinates.x - start.GridCoordinates.x;
            int dy = end.GridCoordinates.y - start.GridCoordinates.y;
            if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy))
                return null;

            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps == 0)
                return null;
            int stepX = Math.Sign(dx);
            int stepY = Math.Sign(dy);
            var path = new List<ICell>(steps);
            for (int index = 1; index <= steps; index++)
            {
                var coordinate = new Vector2IntImpl(
                    start.GridCoordinates.x + stepX * index,
                    start.GridCoordinates.y + stepY * index);
                var cell = context.GridController.CellManager.GetCellAt(coordinate);
                if (cell == null)
                    break;
                if (cell.IsTaken && cell.CurrentUnits.Count == 0)
                    break;
                path.Add(cell);
            }
            return path;
        }

        private static void ApplyBoneSpearDamage(IUnit caster, IUnit target)
        {
            CombatComponent.ApplyDamage(
                caster, target, 7f, true, DamageCategory.Magic, ElementType.None,
                canTriggerBeforeAttacked: true, canCrit: false, canTriggerDamageTaken: true,
                logSourceName: "Bone Spear");
        }

        private static SkillNodeExecutionResult ExecuteBoneShield(
            NecromancerSkillNodeRecord record,
            SkillExecutionContext context)
        {
            if (context.Caster == null)
                return SkillNodeExecutionResult.Failed("Bone Shield has no caster.");
            CombatComponent.ApplyDamageShield(
                context.Caster,
                context.Caster.Charisma * 2f,
                absorbsAllDamage: record.Level >= 2);
            return SkillNodeExecutionResult.Success();
        }
    }
}
