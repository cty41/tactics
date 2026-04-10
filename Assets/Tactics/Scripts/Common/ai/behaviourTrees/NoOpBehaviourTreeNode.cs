using System.Collections.Generic;
using Tactics.Common.AI.BehaviourTrees;
using Tactics.Common.Controllers;
using Tactics.Common.Units;

namespace Tactics.Common.AI.BehaviourTrees
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