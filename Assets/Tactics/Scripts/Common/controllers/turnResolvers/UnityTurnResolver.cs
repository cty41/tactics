using Tactics.Common.Controllers;
using Tactics.Common.Controllers.TurnResolvers;
using UnityEngine;

namespace Tactics.Common.Controllers
{
    /// <summary>
    /// An abstract Unity-specific implementation of <see cref="ITurnResolver"/>.
    /// </summary>
    public abstract class UnityTurnResolver : MonoBehaviour, ITurnResolver
    {
        public abstract TurnContext ResolveStart(GridController gridController);
        public abstract TurnContext ResolveTurn(GridController gridController);
    }
}