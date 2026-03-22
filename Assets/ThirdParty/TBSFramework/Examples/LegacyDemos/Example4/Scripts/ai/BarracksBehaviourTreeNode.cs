using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TurnBasedStrategyFramework.Common.AI.BehaviourTrees;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.AI.BehaviourTrees;
using TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.AI.BehaviourTrees
{
    /// <summary>
    /// A behavior tree node for barracks in Example 4, responsible for spawning units like scouts, knights, and wizards based on thresholds and available resources.
    /// </summary>
    public class BarracksBehaviourTreeNode : BehaviourTreeResource
    {
        [SerializeField] private ScriptableObject _scoutUnitType;
        [SerializeField] private GameObject _scoutUnitData;
        [SerializeField] private int _scoutThreshold;

        [SerializeField] private ScriptableObject _knightUnitType;
        [SerializeField] private GameObject _knightUnitData;
        [SerializeField] private int _knightThreshold;

        [SerializeField] private ScriptableObject _wizardUnitType;
        [SerializeField] private GameObject _wizardUnitData;
        [SerializeField] private int _wizardThreshold;

        [SerializeField] private EconomyController _economyController;

        /// <summary>
        /// Delay before finishing unit's turn in milliseconds
        /// </summary>
        [SerializeField] private int _turnFinishDelay = 100;

        public override void Initialize(IUnit unit, IGridController gridController)
        {
            var player = unit.PlayerNumber;

            var scoutPrice = _scoutUnitData.GetComponent<UnitDetails>().GetPrice();
            var knightPrice = _knightUnitData.GetComponent<UnitDetails>().GetPrice();
            var wizardPrice = _wizardUnitData.GetComponent<UnitDetails>().GetPrice();

            BehaviourTree = new SequenceNode(new List<ITreeNode>
            {
                new SuccederNode(new SequenceNode(new List<ITreeNode>
                {
                    new InverterNode(
                        new CellTakenNode(unit.CurrentCell)
                    ),
                    new SelectorNode(new List<ITreeNode>
                    {
                        // Knight Sequence
                        new SequenceNode(new List<ITreeNode>
                        {
                            new CheckCostNode(_economyController, knightPrice, player),
                            new SelectorNode(new List<ITreeNode>
                            {
                                new InverterNode(
                                    new UnitCounterNode(_knightUnitType, _knightThreshold, gridController.UnitManager, player)
                                ),
                                new FuncNode(() => Task.FromResult(
                                    gridController.UnitManager.GetEnemyUnits(player)
                                        .Count(u => ((ITypedUnit)u).UnitType == _knightUnitType) >=
                                    gridController.UnitManager.GetFriendlyUnits(player)
                                        .Count(u => ((ITypedUnit)u).UnitType == _knightUnitType))
                                ),
                            }),
                            new SpawnActionNode(unit, _knightUnitData, knightPrice, _economyController, gridController)
                        }),

                        // Wizard Sequence
                        new SequenceNode(new List<ITreeNode>
                        {
                            new CheckCostNode(_economyController, wizardPrice, player),
                            new SelectorNode(new List<ITreeNode>
                            {
                                new InverterNode(
                                    new UnitCounterNode(_wizardUnitType, _wizardThreshold, gridController.UnitManager, player)
                                ),
                                new FuncNode(() => Task.FromResult(
                                    gridController.UnitManager.GetEnemyUnits(player)
                                        .Count(u => ((ITypedUnit)u).UnitType == _wizardUnitType) >=
                                    gridController.UnitManager.GetFriendlyUnits(player)
                                        .Count(u => ((ITypedUnit)u).UnitType == _wizardUnitType))
                                ),
                            }),
                            new SpawnActionNode(unit, _wizardUnitData, wizardPrice, _economyController, gridController)
                        }),

                        // Scout Sequence
                        new SequenceNode(new List<ITreeNode>
                        {
                            new CheckCostNode(_economyController, scoutPrice, player),
                            new SelectorNode(new List<ITreeNode>
                            {
                                new InverterNode(
                                    new UnitCounterNode(_scoutUnitType, _scoutThreshold, gridController.UnitManager, player)
                                ),
                                new FuncNode(() => Task.FromResult(
                                    gridController.UnitManager.GetEnemyUnits(player)
                                        .Count(u => ((ITypedUnit)u).UnitType == _scoutUnitType) >=
                                    gridController.UnitManager.GetFriendlyUnits(player)
                                        .Count(u => ((ITypedUnit)u).UnitType == _scoutUnitType))
                                ),
                            }),
                            new SpawnActionNode(unit, _scoutUnitData, scoutPrice, _economyController, gridController)
                        }),
                    })
                })),
                new RealtimeDelayNode(_turnFinishDelay)
            });
        }
    }
}