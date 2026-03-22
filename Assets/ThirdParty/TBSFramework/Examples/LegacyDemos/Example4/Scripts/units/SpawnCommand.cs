using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Common.Units.Abilities;
using TurnBasedStrategyFramework.Common.Utilities;
using TurnBasedStrategyFramework.Unity.Cells;
using TurnBasedStrategyFramework.Unity.Units;
using TurnBasedStrategyFramework.Unity.Utilities;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units.Abilities
{
    /// <summary>
    /// Represents a command to spawn a new unit at a specific location, deducting the cost from the player's resources.
    /// </summary>
    public readonly struct SpawnCommand : ICommand
    {
        private readonly GameObject unitPrefab;
        private readonly ICell spawnCell;

        private readonly Color color;
        private readonly EconomyController economyController;
        private readonly int cost;

        public SpawnCommand(GameObject unitPrefab, ICell spawnCell, Color color, EconomyController economyController, int cost)
        {
            this.unitPrefab = unitPrefab;
            this.spawnCell = spawnCell;
            this.color = color;
            this.economyController = economyController;
            this.cost = cost;
        }

        public async Task Execute(IUnit unit, IGridController controller)
        {
            var unitGO = GameObject.Instantiate(unitPrefab, spawnCell.WorldPosition.ToVector3(), Quaternion.identity);
            var spawnedUnit = unitGO.GetComponent<Unit>();

            // Set color for Mask or main material
            var maskRenderer = unitGO.transform.Find("Mask")?.GetComponent<Renderer>();
            if (maskRenderer != null)
            {
                maskRenderer.material.color = color;
            }

            (spawnedUnit as IColoredUnit).Color = color;

            spawnedUnit.PlayerNumber = unit.PlayerNumber;
            spawnedUnit.CurrentCell = spawnCell;
            spawnedUnit.WorldPosition = spawnCell.WorldPosition;
            (spawnedUnit as Unit).transform.localPosition = (spawnCell as Cell).transform.localPosition;

            spawnCell.IsTaken = true;
            spawnCell.CurrentUnits.Add(spawnedUnit);

            controller.UnitManager.AddUnit(spawnedUnit);
            economyController.UpdateValue(unit.PlayerNumber, -cost);

            spawnedUnit.ActionPoints = 0;
            await spawnedUnit.MarkAsFinished();
        }

        public Task Undo(IUnit unit, IGridController controller)
        {
            return Task.CompletedTask;
        }

        public Dictionary<string, object> Serialize()
        {
            string resourcePath = $"Units/{unitPrefab.name}";
            return new Dictionary<string, object>
            {
                { "unitPrefabPath", resourcePath },
                { "cellX", spawnCell.GridCoordinates.x },
                { "cellY", spawnCell.GridCoordinates.y },
                { "colorR", color.r },
                { "colorG", color.g },
                { "colorB", color.b },
                { "cost", cost }
            };
        }

        public ICommand Deserialize(Dictionary<string, object> actionParams, IGridController gridController)
        {
            string prefabPath = actionParams["unitPrefabPath"].ToString();
            GameObject resolvedPrefab = Resources.Load<GameObject>(prefabPath);

            if (resolvedPrefab == null)
                throw new Exception($"Could not load prefab at Resources/{prefabPath}");

            int x = Convert.ToInt32(actionParams["cellX"]);
            int y = Convert.ToInt32(actionParams["cellY"]);
            ICell resolvedCell = gridController.CellManager.GetCellAt(new Vector2IntImpl(x, y));

            float r = Convert.ToSingle(actionParams["colorR"]);
            float g = Convert.ToSingle(actionParams["colorG"]);
            float b = Convert.ToSingle(actionParams["colorB"]);
            Color resolvedColor = new Color(r, g, b);

            int resolvedCost = Convert.ToInt32(actionParams["cost"]);

            var economy = UnityEngine.Object.FindFirstObjectByType<EconomyController>();

            return new SpawnCommand(resolvedPrefab, resolvedCell, resolvedColor, economy, resolvedCost);
        }
    }
}
