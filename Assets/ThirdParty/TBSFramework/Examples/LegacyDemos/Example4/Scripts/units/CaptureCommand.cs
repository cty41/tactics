using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TurnBasedStrategyFramework.Common.Controllers;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Common.Units.Abilities;
using TurnBasedStrategyFramework.Common.Utilities;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Units.Abilities
{
    /// <summary>
    /// Command for capturing a structure, reducing its loyalty and giving control to the unit's player.
    /// </summary>
    public readonly struct CaptureCommand : ICommand
    {
        private readonly ICapturable _capturable;
        private readonly int _gridPositionX;
        private readonly int _gridPositionY;
        private readonly int _amount;
        private readonly Color _playerColor;

        private readonly ScriptableObject _structureUnitType;

        public CaptureCommand(ICapturable capturable, int amount, Color playerColor, ScriptableObject structureUnitType, int gridPositionX, int gridPositionY)
        {
            _capturable = capturable;
            _amount = amount;
            _playerColor = playerColor;
            _structureUnitType = structureUnitType;
            _gridPositionX = gridPositionX;
            _gridPositionY = gridPositionY;
        }

        public Task Execute(IUnit unit, IGridController controller)
        {
            _capturable.Capture(unit, _amount, unit.PlayerNumber, _playerColor);
            unit.ActionPoints -= 1;
            return Task.CompletedTask;
        }

        public Task Undo(IUnit unit, IGridController controller)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                { "capturablePosX", _gridPositionX },
                { "capturablePosY", _gridPositionY },
                { "amount", _amount },
                { "colorR", _playerColor.r },
                { "colorG", _playerColor.g },
                { "colorB", _playerColor.b }
            };
        }

        public ICommand Deserialize(Dictionary<string, object> actionParams, IGridController gridController)
        {
            int x = Convert.ToInt32(actionParams["capturablePosX"]);
            int y = Convert.ToInt32(actionParams["capturablePosY"]);

            var structureUnitType = _structureUnitType;
            var capturable = gridController.UnitManager
                .GetUnits()
                .OfType<Unit>()
                .FirstOrDefault(u =>
                    u.CurrentCell.GridCoordinates.Equals(new Vector2IntImpl(x, y)) &&
                    u.GetComponent<ICapturable>() != null)
                ?.GetComponent<ICapturable>();

            float r = Convert.ToSingle(actionParams["colorR"]);
            float g = Convert.ToSingle(actionParams["colorG"]);
            float b = Convert.ToSingle(actionParams["colorB"]);
            Color color = new Color(r, g, b);

            int amount = Convert.ToInt32(actionParams["amount"]);

            return new CaptureCommand(capturable, amount, color, structureUnitType, x, y);
        }
    }
}