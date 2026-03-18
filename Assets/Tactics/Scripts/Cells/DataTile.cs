using UnityEngine;
using UnityEngine.Tilemaps;

namespace TurnBasedStrategyFramework.Unity.Examples.TilemapExample.Cells
{
    [CreateAssetMenu(fileName = "NewDataTile", menuName = "TBS Framework/DataTile")]
    public class DataTile : Tile
    {
        [Header("Gameplay Data")]
        [Tooltip("How many movement points this tile costs.")]
        public int movementCost = 1;

        [Tooltip("Can units enter this tile?")]
        public bool isWalkable = true;
        public ScriptableObject cellType;
    }
}