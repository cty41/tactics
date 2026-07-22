using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.AssetPipeline;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using Tactics.Common.Units.Buffs;
using Tactics.Runtime.Utilities;
using Tactics.Common.Utilities;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>
    /// Resolves Mage level semantics at the graph boundary. Assets choose the level and
    /// referenced status assets; this executor owns cross-target and summon atomicity.
    /// </summary>
    public sealed class MageSkillNodeExecutor : ISkillNodeExecutor
    {
        private const string FireDemonCategory = "FireDemon";
        private const int FireDemonLifetimeActions = 5;

        public SkillGraphNodeType NodeType => SkillGraphNodeType.MageSkill;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (MageSkillNodeRecord)node;
            return record.SkillKind switch
            {
                MageSkillKind.Fireball => Task.FromResult(ExecuteFireball(record, context)),
                MageSkillKind.IceBolt => Task.FromResult(ExecuteIceBolt(record, context)),
                MageSkillKind.Lightning => Task.FromResult(ExecuteLightning(record, context)),
                MageSkillKind.SummonFireDemon => ExecuteSummonFireDemon(record, context),
                MageSkillKind.IceArmor => Task.FromResult(ExecuteIceArmor(record, context)),
                _ => Task.FromResult(SkillNodeExecutionResult.Failed("Unsupported Mage skill kind."))
            };
        }

        private static SkillNodeExecutionResult ExecuteFireball(MageSkillNodeRecord record, SkillExecutionContext context)
        {
            var target = ResolveFirstEnemyOnPath(context) ?? context.PrimaryTarget;
            if (target == null)
                return SkillNodeExecutionResult.Failed("No target for Fireball.");

            context.PrimaryTarget = target;
            if (record.Level >= 3)
            {
                var oldBurning = target.GetActiveBuffs()
                    .FirstOrDefault(buff => buff.Config.EffectType == BuffEffectType.Burning);
                if (oldBurning != null)
                {
                    int detonationDamage = oldBurning.StackCount;
                    target.RemoveBuff(oldBurning);
                    if (detonationDamage > 0)
                    {
                        CombatComponent.ApplyDamage(
                            context.Caster, target, detonationDamage, true, DamageCategory.Magic, ElementType.Fire,
                            canTriggerBeforeAttacked: false, canCrit: false, canTriggerDamageTaken: true,
                            logSourceName: "Fireball Detonation", bypassDefense: true);
                    }
                }
            }

            float directDamage = record.Level >= 2 ? 4f : 2f;
            var primaryResolution = ApplyMagicDamage(context.Caster, target, directDamage, ElementType.Fire);
            if (!primaryResolution.WasHit)
                return SkillNodeExecutionResult.Success();

            ApplyStatus(target, context.Caster, record.BurningBuff, record.Level >= 2 ? 3 : 2);
            if (record.Level < 2)
                return SkillNodeExecutionResult.Success();

            float splashDamage = Mathf.Max(1f, Mathf.Floor(directDamage * 0.5f));
            foreach (var splashTarget in GetOrthogonalEnemies(context, target))
            {
                var splashResolution = ApplyMagicDamage(context.Caster, splashTarget, splashDamage, ElementType.Fire);
                if (splashResolution.WasHit)
                    ApplyStatus(splashTarget, context.Caster, record.BurningBuff, 3);
            }

            context.PrimaryTarget = target;
            return SkillNodeExecutionResult.Success();
        }

        private static SkillNodeExecutionResult ExecuteIceBolt(MageSkillNodeRecord record, SkillExecutionContext context)
        {
            var target = ResolveFirstEnemyOnPath(context) ?? context.PrimaryTarget;
            if (target == null)
                return SkillNodeExecutionResult.Failed("No target for Ice Bolt.");

            context.PrimaryTarget = target;
            var primaryResolution = ApplyMagicDamage(context.Caster, target, 8f, ElementType.Ice);
            if (!primaryResolution.WasHit)
                return SkillNodeExecutionResult.Success();

            ApplyStatus(target, context.Caster, record.SlowBuff, record.Level >= 2 ? 2 : 1);
            if (record.Level < 3)
                return SkillNodeExecutionResult.Success();

            var bounceTarget = context.GridController.UnitManager.GetUnits()
                .Where(unit => unit != null && !unit.IsDowned && !ReferenceEquals(unit, target)
                    && unit.PlayerNumber != context.Caster.PlayerNumber && unit.CurrentCell != null
                    && target.CurrentCell.GetDistance(unit.CurrentCell) <= 3)
                .OrderBy(unit => target.CurrentCell.GetDistance(unit.CurrentCell))
                .ThenBy(unit => unit.UnitID)
                .ThenBy(GetStableUnitName, StringComparer.Ordinal)
                .FirstOrDefault();
            if (bounceTarget != null)
            {
                var bounceResolution = ApplyMagicDamage(context.Caster, bounceTarget, 4f, ElementType.Ice);
                if (bounceResolution.WasHit)
                    ApplyStatus(bounceTarget, context.Caster, record.SlowBuff, 1);
            }

            context.PrimaryTarget = target;
            return SkillNodeExecutionResult.Success();
        }

        private static SkillNodeExecutionResult ExecuteLightning(MageSkillNodeRecord record, SkillExecutionContext context)
        {
            var target = context.PrimaryTarget;
            if (target == null)
                return SkillNodeExecutionResult.Failed("No target for Lightning.");

            float damage = record.Level >= 3 ? 11f : 9f;
            var resolution = ApplyMagicDamage(context.Caster, target, damage, ElementType.Lightning);
            float stunChance = record.Level switch { 2 => 0.25f, >= 3 => 0.5f, _ => 0f };
            if (resolution.WasHit && stunChance > 0f && MageSkillRandom.NextDouble() < stunChance)
                ApplyStatus(target, context.Caster, record.StunBuff, 1);
            return SkillNodeExecutionResult.Success();
        }

        private static async Task<SkillNodeExecutionResult> ExecuteSummonFireDemon(
            MageSkillNodeRecord record,
            SkillExecutionContext context)
        {
            var caster = context.Caster;
            var grid = context.GridController;
            if (caster?.CurrentCell == null || grid?.CellManager == null || grid.UnitManager == null)
                return SkillNodeExecutionResult.Failed("Invalid Fire Demon summon context.");

            int desiredCount = record.Level >= 2 ? 2 : 1;
            var registry = SummonRegistry.For(grid);
            var oldSummons = registry.GetOrdered(caster, FireDemonCategory).ToList();
            var spawnCells = FindFireDemonSpawnCells(caster.CurrentCell, grid, desiredCount, oldSummons);
            TLog.Info($"[MageSkill] Fire Demon Lv{record.Level}: old={oldSummons.Count}, legalSpawns={spawnCells.Count}, desired={desiredCount}.");
            if (spawnCells.Count == 0)
                return SkillNodeExecutionResult.Failed("No legal Fire Demon spawn cell within range 3.");

            var manager = GameAssetManager.Instance;
            var prefab = manager?.Load<GameObject>(GameAssetManager.NormalizeAssetPath(record.FireDemonPrefabPath));
            if (prefab == null)
                return SkillNodeExecutionResult.Failed($"Fire Demon prefab not found: {record.FireDemonPrefabPath}");

            var prepared = new List<(GameObject gameObject, IUnit unit, ICell cell)>();
            try
            {
                foreach (var cell in spawnCells)
                {
                    var gameObject = UnityEngine.Object.Instantiate(
                        prefab, cell.WorldPosition.ToVector3(), Quaternion.identity, grid.UnitManager.ContainerTransform);
                    gameObject.SetActive(false);
                    var unit = gameObject.GetComponent<IUnit>();
                    if (unit == null)
                        throw new InvalidOperationException("Fire Demon prefab has no IUnit component.");
                    prepared.Add((gameObject, unit, cell));
                }
            }
            catch (Exception exception)
            {
                foreach (var candidate in prepared)
                    UnityEngine.Object.Destroy(candidate.gameObject);
                TLog.Error($"[MageSkill] Failed to prepare Fire Demon summon: {exception.Message}");
                return SkillNodeExecutionResult.Failed("Fire Demon preparation failed.");
            }

            foreach (var oldSummon in oldSummons)
                registry.Despawn(oldSummon);

            foreach (var candidate in prepared)
            {
                var unit = candidate.unit;
                unit.OwnerUnitId = caster.UnitID;
                unit.PlayerNumber = caster.PlayerNumber;
                unit.CurrentCell = candidate.cell;
                candidate.cell.CurrentUnits.Add(unit);
                candidate.cell.IsTaken = true;
                candidate.gameObject.SetActive(true);
                grid.UnitManager.AddUnit(unit);
                try
                {
                    unit.Initialize(grid);
                }
                catch (UnassignedReferenceException) when (grid is Testing.SkillGraphTestGridController)
                {
                    TLog.Warning("[MageSkill] Skipped scene-only Fire Demon initialization in graph test world.");
                }

                unit.Facing = caster.Facing;
            }

            registry.RegisterBatch(caster, FireDemonCategory, prepared.Select(candidate => candidate.unit).ToList(), FireDemonLifetimeActions);
            foreach (var candidate in prepared)
                context.RecordEvent("FireDemonSummoned", record.NodeId, candidate.unit);

            await Task.CompletedTask;
            return SkillNodeExecutionResult.Success();
        }

        private static SkillNodeExecutionResult ExecuteIceArmor(MageSkillNodeRecord record, SkillExecutionContext context)
        {
            if (context.Caster == null || record.IceArmorBuff == null)
                return SkillNodeExecutionResult.Failed("Ice Armor config is missing.");
            ApplyStatus(context.Caster, context.Caster, record.IceArmorBuff, 2);
            return SkillNodeExecutionResult.Success();
        }

        private static DamageResolution ApplyMagicDamage(IUnit caster, IUnit target, float damage, ElementType element)
        {
            return CombatComponent.ApplyDamage(
                caster, target, damage, true, DamageCategory.Magic, element,
                canTriggerBeforeAttacked: true, canCrit: false, canTriggerDamageTaken: true);
        }

        private static void ApplyStatus(IUnit target, IUnit source, BuffConfig config, int duration)
        {
            if (target != null && config != null)
                target.AddBuff(new Buff(config, source, duration));
        }

        private static IUnit ResolveFirstEnemyOnPath(SkillExecutionContext context)
        {
            var caster = context.Caster;
            var selected = context.PrimaryTarget;
            if (caster?.CurrentCell == null || selected?.CurrentCell == null || context.GridController?.CellManager == null)
                return selected;

            int ax = caster.CurrentCell.GridCoordinates.x;
            int ay = caster.CurrentCell.GridCoordinates.y;
            int bx = selected.CurrentCell.GridCoordinates.x;
            int by = selected.CurrentCell.GridCoordinates.y;
            int dx = bx - ax;
            int dy = by - ay;
            return context.GridController.CellManager.GetCells()
                .Where(cell => cell != null && !ReferenceEquals(cell, caster.CurrentCell))
                .Where(cell =>
                {
                    int cx = cell.GridCoordinates.x - ax;
                    int cy = cell.GridCoordinates.y - ay;
                    return cx * dy == cy * dx && cx * dx + cy * dy > 0
                        && cx * cx + cy * cy <= dx * dx + dy * dy;
                })
                .OrderBy(cell => caster.CurrentCell.GetDistance(cell))
                .SelectMany(cell => cell.CurrentUnits)
                .FirstOrDefault(unit => unit != null && !unit.IsDowned && unit.PlayerNumber != caster.PlayerNumber)
                ?? selected;
        }

        private static IEnumerable<IUnit> GetOrthogonalEnemies(SkillExecutionContext context, IUnit primary)
        {
            if (primary?.CurrentCell == null)
                return Array.Empty<IUnit>();
            int x = primary.CurrentCell.GridCoordinates.x;
            int y = primary.CurrentCell.GridCoordinates.y;
            return context.GridController.CellManager.GetCells()
                .Where(cell => cell != null && Math.Abs(cell.GridCoordinates.x - x) + Math.Abs(cell.GridCoordinates.y - y) == 1)
                .SelectMany(cell => cell.CurrentUnits)
                .Where(unit => unit != null && !unit.IsDowned && unit.PlayerNumber != context.Caster.PlayerNumber)
                .Distinct()
                .OrderBy(unit => unit.UnitID)
                .ThenBy(GetStableUnitName, StringComparer.Ordinal)
                .ToList();
        }

        private static List<ICell> FindFireDemonSpawnCells(
            ICell origin,
            IGridController grid,
            int maximumCount,
            IReadOnlyCollection<IUnit> replaceableSummons)
        {
            return grid.CellManager.GetCells()
                .Where(cell => cell != null && !ReferenceEquals(cell, origin))
                .Where(cell =>
                {
                    int distance = origin.GetDistance(cell);
                    bool isEmptyOrReplaced = cell.CurrentUnits.Count == 0
                        || cell.CurrentUnits.All(unit => replaceableSummons.Contains(unit));
                    bool isWalkableAfterReplacement = grid.CellManager.IsCellWalkable(cell)
                        || cell.CurrentUnits.All(unit => replaceableSummons.Contains(unit));
                    return distance >= 1 && distance <= 3 && isEmptyOrReplaced
                        && isWalkableAfterReplacement;
                })
                .OrderBy(cell => origin.GetDistance(cell))
                .ThenBy(cell => cell.GridCoordinates.x)
                .ThenBy(cell => cell.GridCoordinates.y)
                .Take(Mathf.Max(1, maximumCount))
                .ToList();
        }

        private static string GetStableUnitName(IUnit unit) => unit is INamedUnit named ? named.UnitName : string.Empty;
    }

    /// <summary>
    /// Injectable random boundary keeps percentage effects deterministic in gameplay tests.
    /// </summary>
    public static class MageSkillRandom
    {
        private static Func<double> _provider = new System.Random().NextDouble;

        public static double NextDouble() => _provider();
        public static void SetProviderForTests(Func<double> provider) => _provider = provider ?? new System.Random().NextDouble;
        public static void Reset() => _provider = new System.Random().NextDouble;
    }
}
