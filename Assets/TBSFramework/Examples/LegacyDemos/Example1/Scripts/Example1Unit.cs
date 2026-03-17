using System.Collections.Generic;
using TurnBasedStrategyFramework.Common.Cells;
using TurnBasedStrategyFramework.Common.Units;
using TurnBasedStrategyFramework.Unity.Units;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example1
{
    /// <summary>
    /// Represents a unit in the Example 1 demo.
    /// </summary>
    public class Example1Unit : Unit, ITypedUnit, IColoredUnit, INamedUnit
    {
        [SerializeField] private string _unitName;
        [SerializeField] private ScriptableObject _unitType;
        [SerializeField] private Color _unitColor;
        [SerializeField] private List<ScriptableObject> _weakness;

        public string UnitName => _unitName;
        public ScriptableObject UnitType => _unitType;
        public Color Color { get => _unitColor; set => _unitColor = value; }

        public override float CalculateDamageTaken(IUnit aggressor, float dealtDamage, ICell aggressorCell, ICell defenderCell)
        {
            var enemyType = (aggressor as ITypedUnit).UnitType;
            var realDamage = _weakness.Contains(enemyType) ? dealtDamage * 2 : dealtDamage;
            return realDamage;
        }
    }
}