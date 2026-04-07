using System.Collections.Generic;
using Tactics.Tbsf.Common.AI.BehaviourTrees;
using Tactics.Tbsf.Common.Controllers;
using Tactics.Tbsf.Common.Units;

namespace Tactics.Tbsf.Unity.AI.BehaviourTrees
{
    /// <summary>
    /// A behavior tree node that performs no operations.
    /// </summary>
    public partial class NoOpBehaviourTreeNode : BehaviourTreeResource
    {
        public override void Initialize(IUnit unit, IGridController gridController)
        {
            BehaviourTree = new SelectorNode(new List<ITreeNode>());
        }
    }
}