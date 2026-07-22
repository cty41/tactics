using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Battle;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using Tactics.Common.Units.Abilities;
using Tactics.Common.Units.Buffs;
using Tactics.Common.Utilities;
using Tactics.Roster;
using Tactics.Runtime.Utilities;
using UnityEngine;

namespace Tactics.Common.Skills.Graph
{
    /// <summary>Executes Amazon level semantics against the shared battle-scoped spear state.</summary>
    public sealed class AmazonSkillNodeExecutor : ISkillNodeExecutor
    {
        public SkillGraphNodeType NodeType => SkillGraphNodeType.AmazonSkill;

        public Task<SkillNodeExecutionResult> Execute(SkillGraphNodeRecord node, SkillExecutionContext context)
        {
            var record = (AmazonSkillNodeRecord)node;
            return record.SkillKind switch
            {
                AmazonSkillKind.Thrust => Task.FromResult(ExecuteThrust(record, context)),
                AmazonSkillKind.MultiStab => Task.FromResult(ExecuteMultiStab(record, context)),
                AmazonSkillKind.PoisonSpear => Task.FromResult(ExecutePoisonSpear(record, context)),
                AmazonSkillKind.RecoverSpear => Task.FromResult(ExecuteRecoverSpear(record, context)),
                AmazonSkillKind.PickupSpear => Task.FromResult(ExecutePickupSpear(context)),
                AmazonSkillKind.Decoy => Task.FromResult(ExecuteDecoy(record, context)),
                _ => Task.FromResult(SkillNodeExecutionResult.Failed("Unsupported Amazon skill kind."))
            };
        }

        private static SkillNodeExecutionResult ExecuteThrust(AmazonSkillNodeRecord record, SkillExecutionContext context)
        {
            var caster = context.Caster;
            var selected = context.PrimaryTarget;
            if (caster?.CurrentCell == null || selected?.CurrentCell == null)
                return SkillNodeExecutionResult.Failed("Thrust requires a target direction.");
            int dx = selected.CurrentCell.GridCoordinates.x - caster.CurrentCell.GridCoordinates.x;
            int dy = selected.CurrentCell.GridCoordinates.y - caster.CurrentCell.GridCoordinates.y;
            if (dx != 0 && dy != 0)
                return SkillNodeExecutionResult.Failed("Thrust requires a cardinal direction.");

            int stepX = Math.Sign(dx);
            int stepY = Math.Sign(dy);
            int length = record.Level >= 2 ? 3 : 2;
            float damage = 6f + (record.Level >= 3
                ? AmazonBattleState.For(context.GridController).GetActiveMovement(caster)
                : 0f);
            var targets = new List<IUnit>();
            for (int index = 1; index <= length; index++)
            {
                var cell = context.GridController.CellManager.GetCellAt(new Vector2IntImpl(
                    caster.CurrentCell.GridCoordinates.x + stepX * index,
                    caster.CurrentCell.GridCoordinates.y + stepY * index));
                if (cell == null || cell.IsTaken && cell.CurrentUnits.Count == 0)
                    break;
                var liveUnits = cell.CurrentUnits.Where(unit => unit != null && !unit.IsDowned).ToList();
                if (liveUnits.Any(unit => unit.PlayerNumber == caster.PlayerNumber))
                    break;
                foreach (var target in liveUnits.Where(unit => unit.PlayerNumber != caster.PlayerNumber))
                {
                    CombatComponent.ApplyDamage(caster, target, damage, false, DamageCategory.Physical, ElementType.None,
                        true, true, true, "Thrust");
                    targets.Add(target);
                }
            }
            if (record.Level >= 3)
                AmazonBattleState.For(context.GridController).ResetActiveMovement(caster);
            context.TargetSet = targets;
            return SkillNodeExecutionResult.Success();
        }

        private static SkillNodeExecutionResult ExecuteMultiStab(AmazonSkillNodeRecord record, SkillExecutionContext context)
        {
            int expected = record.Level >= 2 ? 4 : 3;
            var targets = context.TargetSet?.Take(expected).ToList() ?? new List<IUnit>();
            if (targets.Count != expected)
                return SkillNodeExecutionResult.Failed($"Multi Stab requires {expected} ordered targets.");
            foreach (var target in targets)
            {
                if (target == null || target.IsDowned || target.Health <= 0f)
                    continue;
                CombatComponent.ApplyDamage(context.Caster, target, 4f, false, DamageCategory.Physical, ElementType.None,
                    true, true, true, "Multi Stab");
                context.RecordEvent("MultiStabHit", record.NodeId, target);
            }
            return SkillNodeExecutionResult.Success();
        }

        private static SkillNodeExecutionResult ExecutePoisonSpear(AmazonSkillNodeRecord record, SkillExecutionContext context)
        {
            var caster = context.Caster;
            var target = context.PrimaryTarget;
            var state = AmazonBattleState.For(context.GridController);
            var dropCell = state.FindDropCell(caster, target?.CurrentCell, 3);
            if (caster == null || target == null || record.PoisonBuff == null || dropCell == null || !state.IsSpearHeld(caster))
                return SkillNodeExecutionResult.Failed("Poison Spear has no legal drop cell.");
            float damage = record.Level >= 2 ? 10f : 8f;
            var resolution = CombatComponent.ApplyDamage(caster, target, damage, true,
                DamageCategory.Physical, ElementType.None, true, true, true, "Poison Spear");
            if (resolution.WasHit)
            {
                foreach (var affected in ResolvePoisonTargets(record.Level, context, target.CurrentCell))
                    affected.AddBuff(new Buff(record.PoisonBuff, caster, 3));
            }
            if (!state.DropSpear(caster, dropCell))
                return SkillNodeExecutionResult.Failed("Poison Spear failed to commit its drop cell.");
            context.RecordEventAtCell("SpearDropped", record.NodeId, dropCell);
            return SkillNodeExecutionResult.Success();
        }

        private static IEnumerable<IUnit> ResolvePoisonTargets(int level, SkillExecutionContext context, ICell center)
        {
            foreach (var unit in context.GridController.UnitManager.GetUnits()
                .Where(unit => unit != null && !unit.IsDowned && unit.PlayerNumber != context.Caster.PlayerNumber && unit.CurrentCell != null)
                .OrderBy(unit => unit.UnitID))
            {
                int dx = Math.Abs(unit.CurrentCell.GridCoordinates.x - center.GridCoordinates.x);
                int dy = Math.Abs(unit.CurrentCell.GridCoordinates.y - center.GridCoordinates.y);
                bool included = level <= 1 ? dx == 0 && dy == 0 : level == 2 ? dx + dy <= 1 : dx <= 1 && dy <= 1;
                if (included)
                    yield return unit;
            }
        }

        private static SkillNodeExecutionResult ExecuteRecoverSpear(AmazonSkillNodeRecord record, SkillExecutionContext context)
        {
            var state = AmazonBattleState.For(context.GridController);
            var spearCell = state.GetSpearCell(context.Caster);
            if (spearCell == null || !ReferenceEquals(spearCell, context.TargetPoint) || !state.RecoverSpear(context.Caster))
                return SkillNodeExecutionResult.Failed("Select the dropped spear.");
            if (record.Level >= 2)
            {
                foreach (var unit in context.GridController.UnitManager.GetUnits()
                    .Where(unit => unit != null && !unit.IsDowned && unit.PlayerNumber != context.Caster.PlayerNumber &&
                        unit.CurrentCell != null && unit.CurrentCell.GetDistance(context.Caster.CurrentCell) == 1)
                    .OrderBy(unit => unit.UnitID))
                {
                    CombatComponent.ApplyDamage(context.Caster, unit, 6f, true,
                        DamageCategory.Magic, ElementType.Lightning, true, true, true, "Summon Spear");
                }
            }
            return SkillNodeExecutionResult.Success();
        }

        private static SkillNodeExecutionResult ExecutePickupSpear(SkillExecutionContext context)
        {
            var state = AmazonBattleState.For(context.GridController);
            var spearCell = state.GetSpearCell(context.Caster);
            if (spearCell == null)
                return SkillNodeExecutionResult.Failed("No dropped spear.");
            int dx = Math.Abs(spearCell.GridCoordinates.x - context.Caster.CurrentCell.GridCoordinates.x);
            int dy = Math.Abs(spearCell.GridCoordinates.y - context.Caster.CurrentCell.GridCoordinates.y);
            if (Math.Max(dx, dy) != 1)
                return SkillNodeExecutionResult.Failed("Move adjacent to the spear.");
            return state.RecoverSpear(context.Caster) ? SkillNodeExecutionResult.Success() :
                SkillNodeExecutionResult.Failed("Spear pickup failed.");
        }

        private static SkillNodeExecutionResult ExecuteDecoy(AmazonSkillNodeRecord record, SkillExecutionContext context)
        {
            if (context.Caster is not MonoBehaviour casterBehaviour || context.Caster.CurrentCell == null ||
                context.TargetPoint == null || context.TargetPoint.IsTaken)
                return SkillNodeExecutionResult.Failed("Decoy requires an empty retreat cell.");
            var origin = context.Caster.CurrentCell;
            var destination = context.TargetPoint;
            GameObject gameObject = null;
            Unit decoy = null;
            try
            {
                gameObject = UnityEngine.Object.Instantiate(casterBehaviour.gameObject,
                    origin.WorldPosition.ToVector3(), casterBehaviour.transform.rotation,
                    context.GridController.UnitManager?.ContainerTransform);
                gameObject.SetActive(false);
                gameObject.name = "AmazonDecoy";
                decoy = gameObject.GetComponent<Unit>();
                if (decoy == null)
                    throw new InvalidOperationException("Amazon prefab has no Unit component.");
                decoy.ApplyAbilityConfigs(Array.Empty<AbilityConfig>());
                decoy.ApplyLearnedSkillLevels(Array.Empty<CharacterDefinition.LearnedSkill>());
                decoy.PlayerNumber = context.Caster.PlayerNumber;
                decoy.CurrentCell = origin;
                decoy.Initialize(context.GridController);
                decoy.MaxHealth = Math.Max(1f, Mathf.Floor(context.Caster.MaxHealth * 0.5f));
                decoy.Health = decoy.MaxHealth;
                decoy.DefenceFactor = context.Caster.DefenceFactor;
                decoy.DodgeRate = context.Caster.DodgeRate;
                decoy.Facing = context.Caster.Facing;
                foreach (var renderer in gameObject.GetComponentsInChildren<SpriteRenderer>())
                    renderer.color = new Color(0.45f, 0.8f, 1f, 0.55f);
            }
            catch (Exception exception)
            {
                if (gameObject != null)
                    UnityEngine.Object.Destroy(gameObject);
                TLog.Error($"[AmazonSkill] Decoy preparation failed: {exception.Message}");
                return SkillNodeExecutionResult.Failed("Decoy preparation failed.");
            }

            origin.CurrentUnits.Remove(context.Caster);
            origin.IsTaken = false;
            destination.CurrentUnits.Add(context.Caster);
            destination.IsTaken = true;
            context.Caster.CurrentCell = destination;
            context.Caster.WorldPosition = destination.WorldPosition;
            origin.CurrentUnits.Add(decoy);
            origin.IsTaken = true;
            gameObject.SetActive(true);
            context.GridController.UnitManager.AddUnit(decoy);
            AmazonBattleState.For(context.GridController).RegisterDecoy(context.Caster, decoy, 3);

            if (record.Level >= 2)
            {
                foreach (var buff in context.Caster.GetActiveBuffs()
                    .Where(buff => buff.Config.Polarity == BuffPolarity.Harmful).ToList())
                    context.Caster.RemoveBuff(buff);
            }
            context.RecordEvent("DecoyCreated", record.NodeId, decoy);
            return SkillNodeExecutionResult.Success();
        }
    }
}
