using TurnBasedStrategyFramework.Unity.Cells;
using TurnBasedStrategyFramework.Unity.Highlighters;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example4.Cells
{
    /// <summary>
    /// Represents a square cell in the Example 4 demo.
    /// </summary>
    public class Example4Square : Square, IDefenceAffectingCell, INamedCell
    {
        [SerializeField] private SpriteRendererHighlighter _debugHighlighter;
        [SerializeField] private int _defenceModifier;
        [SerializeField] private string _cellName;

        public int DefenceModifier { get { return _defenceModifier; } }
        public string CellName { get { return _cellName; } }

        public override void SetColor(float r, float g, float b, float a)
        {
            _debugHighlighter.SetColor(new Color(r, g, b, a));
            _debugHighlighter.Apply(NoParam.Instance);
        }
    }
}