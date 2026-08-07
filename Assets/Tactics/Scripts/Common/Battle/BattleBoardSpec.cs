using UnityEngine;

namespace Tactics.Common.Battle
{
    public static class BattleBoardSpec
    {
        public const int Width = 10;
        public const int Height = 10;
        public const int CellCount = Width * Height;

        public static readonly Vector2Int Minimum = Vector2Int.zero;
        public static readonly Vector2Int Maximum = new Vector2Int(Width - 1, Height - 1);

        public static bool Contains(int x, int y)
        {
            return x >= Minimum.x && x <= Maximum.x && y >= Minimum.y && y <= Maximum.y;
        }

        public static bool Contains(Vector2Int cell)
        {
            return Contains(cell.x, cell.y);
        }
    }
}
