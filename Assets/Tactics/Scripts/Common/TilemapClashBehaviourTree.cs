using System.Collections.Generic;
using Tactics.Common.AI.BehaviourTrees;
using Tactics.Common.AI.Evaluators;
using Tactics.Common.Controllers;
using Tactics.Common.Units;
using UnityEngine;

namespace Tactics.AI.BehaviourTrees
{
    /// <summary>
    /// A behavior tree adapted from ClashOfHeroes for tilemap-based tactics games.
    /// Evaluates positions and enemies to guide unit movement and attack actions.
    /// </summary>
    public class TilemapClashBehaviourTree : BehaviourTreeResource
    {
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
                        new MoveActionNode(unit, gridController, new List<IPositionEvaluator>
                        {
                            new DamageDealtPositionEvaluator(_damageDealtPositionEvaluatorWeight, _damageDealtPositionEvaluatorDecayValue),
                            new DamageReceivedPositionEvaluator(_damageReceivedPositionEvaluatorWeight, _damageReceivedPositionEvaluatorDecayValue),
                            new DistancePositionEvaluator(_distancePositionEvaluatorWeight, _distancePositionEvaluatorThreshold),
                        }),
                        new SuccederNode(
                            new MoveActionNode(unit, gridController, new List<IPositionEvaluator>
                            {
                                new DamageDealtPositionEvaluator(_damageDealtPositionEvaluatorWeight, _damageDealtPositionEvaluatorDecayValue),
                                new DamageReceivedPositionEvaluator(_damageReceivedPositionEvaluatorWeight, _damageReceivedPositionEvaluatorDecayValue),
                                new DistancePositionEvaluator(_distancePositionEvaluatorWeight, _distancePositionEvaluatorThreshold),
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