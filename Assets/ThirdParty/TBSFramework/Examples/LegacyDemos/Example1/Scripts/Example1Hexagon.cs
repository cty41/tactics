using TurnBasedStrategyFramework.Unity.Cells;
using TurnBasedStrategyFramework.Unity.Highlighters;
using UnityEngine;

namespace TurnBasedStrategyFramework.Unity.Examples.Legacy.Example1.Cells
{
    /// <summary>
    /// Represents a hexagonal cell in the Example 1 demo.
    /// </summary>
    public class Example1Hexagon : Hexagon
    {
        [SerializeField] private RendererHighlighter _aiDebugHighlighter;

        public override void SetColor(float r, float g, float b, float a)
        {
            _aiDebugHighlighter.SetColor(new Color(r, g, b, a));
            _aiDebugHighlighter.Apply(NoParam.Instance);
        }
    }
}