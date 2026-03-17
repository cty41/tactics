using System.Collections.Generic;
using TurnBasedStrategyFramework.Common.AI.BehaviourTrees;
using TurnBasedStrategyFramework.Common.AI.Evaluators;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.AI.BehaviourTrees;
using TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI.Evaluators;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI
{
    /// <summary>
    /// A behavior tree used in Example 4 that includes defending capturable structures.
    /// </summary>
    public class Example4UnitBehaviourTreeResource : BehaviourTreeResource
    {
        [Space]
        [Header("General")]
        [SerializeField] private int _actionDelay = 100;
        [SerializeField] private int _turnFinishDelay = 100;

        [Space]
        [Header("Unit Types")]
        [SerializeField] private ScriptableObject _structureUnitType;
        [SerializeField] private ScriptableObject _scoutUnitType;

        [Space]
        [Header("Position Evaluator Weights")]
        [SerializeField] private float _captureDefenceEvaluatorWeight = 1f;

        [SerializeField] private float _damageDealtEvaluatorWeight = 1f;
        [SerializeField] private float _damageDealtEvaluatorDecay = 20f;

        [SerializeField] private float _damageReceivedEvaluatorWeight = -0.8f;
        [SerializeField] private float _damageReceivedEvaluatorDecay = 0.5f;

        [SerializeField] private float _distanceEvaluatorWeight = -1f;
        [SerializeField] private int _distanceEvaluatorThreshold = 10;

        [Space]
        [Header("Target Evaluator Weights")]
        [SerializeField] private float _healthTargetEvaluatorWeight = 1f;
        [SerializeField] private float _damageDealtTargetEvaluatorWeight = 1f;
        [SerializeField] private float _captureDefenceTargetEvaluatorWeight = 1f;

        public override void Initialize(IUnit unit, IGridController gridController)
        {
            BehaviourTree = new SequenceNode(new List<ITreeNode>
            {
                new SequenceNode(new List<ITreeNode>
                {
                    new DebugMoveAction(unit, gridController, new List<IPositionEvaluator>
                        {
                            new CaptureDefencePositionEvaluator(_captureDefenceEvaluatorWeight, _structureUnitType, _scoutUnitType),
                            new DamageDealtPositionEvaluator(_damageDealtEvaluatorWeight, _damageDealtEvaluatorDecay),
                            new DamageReceivedPositionEvaluator(_damageReceivedEvaluatorWeight, _damageReceivedEvaluatorDecay),
                            new DistancePositionEvaluator(_distanceEvaluatorWeight, _distanceEvaluatorThreshold),
                        }),
                    new SuccederNode(
                        new MoveActionNode(unit, gridController, new List<IPositionEvaluator>
                        {
                            new CaptureDefencePositionEvaluator(_captureDefenceEvaluatorWeight, _structureUnitType, _scoutUnitType),
                            new DamageDealtPositionEvaluator(_damageDealtEvaluatorWeight, _damageDealtEvaluatorDecay),
                            new DamageReceivedPositionEvaluator(_damageReceivedEvaluatorWeight, _damageReceivedEvaluatorDecay),
                            new DistancePositionEvaluator(_distanceEvaluatorWeight, _distanceEvaluatorThreshold),
                        }))
                }),
                new RealtimeDelayNode(_actionDelay),
                new SuccederNode(new AttackSequenceNode(unit, gridController, new List<ITargetEvaluator>
                {
                    new HealthTargetEvaluator(_healthTargetEvaluatorWeight),
                    new DamageDealtTargetEvaluator(_damageDealtTargetEvaluatorWeight),
                    new CaptureDefenceTargetEvaluator(_captureDefenceTargetEvaluatorWeight, _structureUnitType)
                })),
                new RealtimeDelayNode(_turnFinishDelay)
            });
        }
    }
}
