using System;
using Tactics.Common.Utilities;

namespace Tactics.Common.Units
{
    public enum FacingDirection
    {
        North,
        East,
        South,
        West
    }

    public readonly struct FacingChangedEventArgs
    {
        public FacingDirection Previous { get; }
        public FacingDirection Current { get; }

        public FacingChangedEventArgs(FacingDirection previous, FacingDirection current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>
    /// Resolves four-direction facing from grid offsets. Diagonal ties preserve the
    /// current direction when it lies on either tied axis, otherwise horizontal wins.
    /// </summary>
    public static class FacingResolver
    {
        public static bool TryResolve(
            Vector2IntImpl from,
            Vector2IntImpl to,
            FacingDirection current,
            out FacingDirection resolved)
        {
            int deltaX = to.x - from.x;
            int deltaY = to.y - from.y;
            if (deltaX == 0 && deltaY == 0)
            {
                resolved = current;
                return false;
            }

            int absoluteX = Math.Abs(deltaX);
            int absoluteY = Math.Abs(deltaY);
            if (absoluteX > absoluteY)
            {
                resolved = deltaX > 0 ? FacingDirection.East : FacingDirection.West;
                return true;
            }

            if (absoluteY > absoluteX)
            {
                resolved = deltaY > 0 ? FacingDirection.North : FacingDirection.South;
                return true;
            }

            bool currentIsHorizontal = current is FacingDirection.East or FacingDirection.West;
            bool currentIsVertical = current is FacingDirection.North or FacingDirection.South;
            if (currentIsHorizontal && MatchesHorizontalSign(current, deltaX))
            {
                resolved = current;
                return true;
            }

            if (currentIsVertical && MatchesVerticalSign(current, deltaY))
            {
                resolved = current;
                return true;
            }

            resolved = deltaX > 0 ? FacingDirection.East : FacingDirection.West;
            return true;
        }

        public static bool IsOrthogonallyAdjacent(Vector2IntImpl from, Vector2IntImpl to)
        {
            return Math.Abs(to.x - from.x) + Math.Abs(to.y - from.y) == 1;
        }

        private static bool MatchesHorizontalSign(FacingDirection current, int deltaX)
        {
            return (current == FacingDirection.East && deltaX > 0) ||
                   (current == FacingDirection.West && deltaX < 0);
        }

        private static bool MatchesVerticalSign(FacingDirection current, int deltaY)
        {
            return (current == FacingDirection.North && deltaY > 0) ||
                   (current == FacingDirection.South && deltaY < 0);
        }
    }
}
