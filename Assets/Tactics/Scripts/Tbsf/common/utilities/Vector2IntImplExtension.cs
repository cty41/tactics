using Tactics.Tbsf.Common.Utilities;
using UnityEngine;

namespace Tactics.Tbsf.Common.Utilities
{
    /// <summary>
    /// Extension methods for converting Vector2IntImpl to UnityEngine.Vector2Int.
    /// </summary>
    public static class Vector2IntImplExtension
    {
        public static Vector2Int ToVector2Int(this Vector2IntImpl vector)
        {
            return new Vector2Int(vector.x, vector.y);
        }
    }
}