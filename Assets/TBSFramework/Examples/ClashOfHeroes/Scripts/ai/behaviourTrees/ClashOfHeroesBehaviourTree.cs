using System.Collections.Generic;
using TurnBasedStrategyFramework.Common.AI.BehaviourTrees;
using TurnBasedStrategyFramework.Common.AI.Evaluators;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.AI.BehaviourTrees;
using TurnBasedStrategyFramework.Unity.Examples.ClashOfHeroes.AI.Evaluators;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.ClashOfHeroes.AI.BehaviourTrees
{
    /// <summary>
    /// A basic behavior tree that evaluates positions and enemies to guide unit movement and attack actions.
    /// </summary>
    public class ClashOfHeroesBehaviourTree : BehaviourTreeResource
    {
        [Space]
        [Header("General")]
        /// <summary>
        /// Delay between actions in milliseconds
        /// </summary>
        [SerializeField] private int _delay = 100;

        [Space]
        [Header("Damage Dealt Position Evaluator")]
        [SerializeField] private float _damageDealtPositionEvaluatorWeight = 1f;
        [SerializeField] private float _damageDealtPositionEvaluatorDecayValue = 0.5f;

        [Space]
        [Header("Damage Received Position Evaluator")]
        [SerializeField] private float _damageReceivedPositionEvaluatorWeight = -0.1f;
        [SerializeField] private float _damageReceivedPositionEvaluatorDecayValue = 0.5f;

        [Space]
        [Header("Distance Position Evaluator")]
        [SerializeField] private float _distancePositionEvaluatorWeight = -0.1f;
        [SerializeField] private int _distancePositionEvaluatorThreshold = 10;

        [Space]
        [Header("Height Position Evaluator")]
        [SerializeField] private float _heightPositionEvaluatorWeight = 0.2f;

        [Space]
        [Header("Health Target Evaluator")]
        [SerializeField] private float _healthTargetEvaluatorWeight = 1f;

        [Space]
        [Header("Damage Given Target Evaluator")]
        [SerializeField] private float _damageGivenTargetEvaluatorWeight = 1f;

        public override void Initialize(IUnit unit, IGridController gridController)
        {
            BehaviourTree = new SequenceNode(new List<ITreeNode>
            {
                new SelectorNode(new List<ITreeNode>
                {
                    new SequenceNode(new List<ITreeNode>
                    {
                        new DebugMoveAction(unit, gridController, new List<IPositionEvaluator>
                            {
                                new DamageDealtPositionEvaluator(_damageDealtPositionEvaluatorWeight, _damageDealtPositionEvaluatorDecayValue),
                                new DamageReceivedPositionEvaluator(_damageReceivedPositionEvaluatorWeight, _damageReceivedPositionEvaluatorDecayValue),
                                new DistancePositionEvaluator(_distancePositionEvaluatorWeight, _distancePositionEvaluatorThreshold),
                                new HeightPositionEvaluator(_heightPositionEvaluatorWeight),
                            }),
                        new SuccederNode(
                            new MoveActionNode(unit, gridController, new List<IPositionEvaluator>
                            {
                                new DamageDealtPositionEvaluator(_damageDealtPositionEvaluatorWeight, _damageDealtPositionEvaluatorDecayValue),
                                new DamageReceivedPositionEvaluator(_damageReceivedPositionEvaluatorWeight, _damageReceivedPositionEvaluatorDecayValue),
                                new DistancePositionEvaluator(_distancePositionEvaluatorWeight, _distancePositionEvaluatorThreshold),
                                new HeightPositionEvaluator(_heightPositionEvaluatorWeight),
                            })),
                    }),
                }),
                new AttackSequenceNode(unit, gridController, new List<ITargetEvaluator>
                {
                    new HealthTargetEvaluator(_healthTargetEvaluatorWeight),
                    new DamageDealtTargetEvaluator(_damageGivenTargetEvaluatorWeight)
                })
            }); ;
        }
    }
}