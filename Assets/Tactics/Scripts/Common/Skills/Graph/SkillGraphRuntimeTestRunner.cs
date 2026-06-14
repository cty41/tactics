using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Cells;
using Tactics.Common.Controllers;
using Tactics.Common.Players;
using Tactics.Common.Units;
using Tactics.Common.Utilities;
using UnityEngine;

namespace Tactics.Common.Skills.Graph.Testing
{
    /// <summary>
    /// 运行时技能图测试请求。
    /// </summary>
    public sealed class SkillGraphRuntimeTestRequest
    {
        public string Name { get; set; }
        public SkillGraphAsset Graph { get; set; }
        public IGridController GridController { get; set; }
        public IUnit Caster { get; set; }
        public IUnit PrimaryTarget { get; set; }
        public ICell TargetPoint { get; set; }
        public int MaxSteps { get; set; } = 200;
        public bool ValidateGraph { get; set; } = true;
    }

    /// <summary>
    /// 运行时技能图测试结果。
    /// </summary>
    public sealed class SkillGraphRuntimeTestResult
    {
        public string Name { get; set; }
        public string GraphName { get; set; }
        public bool ValidationRan { get; set; }
        public List<SkillGraphDiagnostic> ValidationErrors { get; } = new();
        public List<SkillGraphDiagnostic> ValidationWarnings { get; } = new();
        public SkillGraphExecutionState ExecutionState { get; set; } = SkillGraphExecutionState.Aborted;
        public string LastError { get; set; }
        public int StepCount { get; set; }
        public SkillGraphTestUnitSnapshot Caster { get; set; }
        public SkillGraphTestUnitSnapshot PrimaryTarget { get; set; }
        public List<SkillGraphExecutionEvent> ExecutionEvents { get; } = new();
        public List<SkillStageResult> StageResults { get; } = new();

        public bool Passed => ValidationErrors.Count == 0 && ExecutionState == SkillGraphExecutionState.Completed;

        public string Summary
            => $"{Name ?? GraphName ?? "Unnamed"} | Passed={Passed} | State={ExecutionState} | Steps={StepCount} | Error={LastError ?? "none"}";

        public override string ToString()
        {
            return Summary;
        }
    }

    /// <summary>
    /// 技能图执行前后单位快照。
    /// </summary>
    public sealed class SkillGraphTestUnitSnapshot
    {
        public string UnitName { get; set; }
        public int PlayerNumber { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public float Mana { get; set; }
        public bool IsDowned { get; set; }
        public string CellCoordinates { get; set; }

        public static SkillGraphTestUnitSnapshot Capture(IUnit unit)
        {
            if (unit == null)
                return null;

            string unitName = unit is MonoBehaviour monoBehaviour
                ? monoBehaviour.gameObject.name
                : unit.GetType().Name;

            return new SkillGraphTestUnitSnapshot
            {
                UnitName = unitName,
                PlayerNumber = unit.PlayerNumber,
                Health = unit.Health,
                MaxHealth = unit.MaxHealth,
                Mana = unit.Mana,
                IsDowned = unit.IsDowned,
                CellCoordinates = unit.CurrentCell?.GridCoordinates.ToString()
            };
        }

        public override string ToString()
        {
            return $"{UnitName} P{PlayerNumber} HP={Health}/{MaxHealth} Mana={Mana} Downed={IsDowned} Cell={CellCoordinates ?? "null"}";
        }
    }

    /// <summary>
    /// 运行时技能图测试执行器。
    /// </summary>
    public sealed class SkillGraphRuntimeTestRunner
    {
        private readonly SkillGraphRunner _runner = new();

        public async Task<SkillGraphRuntimeTestResult> ExecuteAsync(SkillGraphRuntimeTestRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (request.Graph == null)
                throw new ArgumentNullException(nameof(request.Graph));
            if (request.GridController == null)
                throw new ArgumentNullException(nameof(request.GridController));
            if (request.Caster == null)
                throw new ArgumentNullException(nameof(request.Caster));

            var result = new SkillGraphRuntimeTestResult
            {
                Name = request.Name,
                GraphName = request.Graph.DisplayName
            };

            if (request.ValidateGraph)
            {
                result.ValidationRan = true;

                if (!SkillGraphValidation.Validate(request.Graph, out var errors, out var warnings))
                {
                    result.ValidationErrors.AddRange(errors);
                    result.ValidationWarnings.AddRange(warnings);
                    result.ExecutionState = SkillGraphExecutionState.Aborted;
                    result.LastError = errors.Count > 0
                        ? $"Graph validation failed: {errors[0].Message}"
                        : "Graph validation failed.";
                    result.Caster = SkillGraphTestUnitSnapshot.Capture(request.Caster);
                    result.PrimaryTarget = SkillGraphTestUnitSnapshot.Capture(request.PrimaryTarget);
                    return result;
                }

                result.ValidationWarnings.AddRange(warnings);
            }

            var runtimeDef = SkillGraphRuntimeDefinition.FromAsset(request.Graph);
            var context = new SkillExecutionContext(request.Caster, request.Graph, runtimeDef, request.GridController)
            {
                MaxSteps = request.MaxSteps,
                PrimaryTarget = request.PrimaryTarget,
                TargetPoint = request.TargetPoint
            };

            try
            {
                result.ExecutionState = await _runner.Execute(context);
                result.LastError = context.LastError;
                result.StepCount = context.StepCount;
            }
            catch (Exception ex)
            {
                result.ExecutionState = SkillGraphExecutionState.Aborted;
                result.LastError = ex.Message;
            }

            result.ExecutionEvents.AddRange(context.ExecutionEvents);
            result.StageResults.AddRange(context.StageResults);
            result.Caster = SkillGraphTestUnitSnapshot.Capture(request.Caster);
            result.PrimaryTarget = SkillGraphTestUnitSnapshot.Capture(context.PrimaryTarget);
            return result;
        }
    }
}
