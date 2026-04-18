using Tactics.Common.Controllers.TurnResolvers;

namespace Tactics.Common.Controllers
{
    /// <summary>
    /// A concrete implementation of <see cref="ITurnResolver"/> that delegates turn resolution to <see cref="SubsequentTurnResolverImpl"/>.
    /// This resolver handles turns sequentially for all players, selecting the first player at the start and moving through players in order.
    /// </summary>
    public class SubsequentTurnResolver : ITurnResolver
    {
        private readonly SubsequentTurnResolverImpl _impl = new SubsequentTurnResolverImpl();

        public TurnContext ResolveStart(GridController gridController)
        {
            return _impl.ResolveStart(gridController);
        }

        public TurnContext ResolveTurn(GridController gridController)
        {
            return _impl.ResolveTurn(gridController);
        }
    }
}
