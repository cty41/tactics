using System.Collections.Generic;
using TurnBasedStrategyFramework.Common.AI.BehaviourTrees;
using TurnBasedStrategyFramework.Common.AI.Evaluators;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.AI.BehaviourTrees;
using TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI.Evaluators;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI.BehaviourTrees
{
    /// <summary>
    /// A behavior tree for the Scout unit that includes defending and capturing structures.
    /// </summary>
    public class ScoutBehaviourTreeResource : BehaviourTreeResource
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
        [SerializeField] private float _capturableStructureEvaluatorWeight = 1f;
        [SerializeField] private float _damageDealtEvaluatorWeight = 1f;
        [SerializeField] private float _damageDealtEvaluatorDecay = 0.5f;
        [SerializeField] private float _damageReceivedEvaluatorWeight = -0.8f;
        [SerializeField] private float _damageRecievedEvaluatorDecay = 0.5f;
        [SerializeField] private float _distanceEvaluatorWeight = -1f;
        [SerializeField] private int _distanceEvaluatorThreshold = 4;

        [Space]
        [Header("Target Evaluator Weights")]
        [SerializeField] private float _healthTargetEvaluatorWeight = 1f;
        [SerializeField] private float _damageDealtTargetEvaluatorWeight = 1f;
        [SerializeField] private float _captureDefenceTargetEvaluatorWeight = 1f;

        public override void Initialize(IUnit unit, IGridController gridController)
        {
            BehaviourTree = new SequenceNode(new List<ITreeNode>
            {
                new SelectorNode(new List<ITreeNode>
                {
                    new CaptureActionNode(unit, _structureUnitType, gridController),
                    new SequenceNode(new List<ITreeNode>
                    {
                        new DebugMoveAction(unit, gridController, new List<IPositionEvaluator>
                        {
                            new CaptureDefencePositionEvaluator(_captureDefenceEvaluatorWeight, _structureUnitType, _scoutUnitType),
                            new CapturableStructurePositionEvaluator(_capturableStructureEvaluatorWeight, _structureUnitType),
                            new DamageDealtPositionEvaluator(_damageDealtEvaluatorWeight, _damageDealtEvaluatorDecay),
                            new DamageReceivedPositionEvaluator(_damageReceivedEvaluatorWeight, _damageRecievedEvaluatorDecay),
                            new DistancePositionEvaluator(_distanceEvaluatorWeight, _distanceEvaluatorThreshold),
                        }),
                        new SuccederNode(
                            new MoveActionNode(unit, gridController, new List<IPositionEvaluator>
                            {
                                new CaptureDefencePositionEvaluator(_captureDefenceEvaluatorWeight, _structureUnitType, _scoutUnitType),
                                new CapturableStructurePositionEvaluator(_capturableStructureEvaluatorWeight, _structureUnitType),
                                new DamageDealtPositionEvaluator(_damageDealtEvaluatorWeight),
                                new DamageReceivedPositionEvaluator(_damageReceivedEvaluatorWeight),
                                new DistancePositionEvaluator(_distanceEvaluatorWeight, _distanceEvaluatorThreshold),
                            }))
                    }),
                }),
                new RealtimeDelayNode(_actionDelay),
                new SuccederNode(new CaptureActionNode(unit, _structureUnitType, gridController)),
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
