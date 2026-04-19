using Tactics.Common.Units.Highlight;

namespace Tactics.Common.Highlighters
{
    /// <summary>
    /// Legacy interface for highlight parameters. Now inherits from the new interface.
    /// Any type implementing the new interface can be cast to this one.
    /// </summary>
    public interface IHighlightParams : Tactics.Common.Units.Highlight.IHighlightParams
    {
    }

    /// <summary>
    /// Legacy NoParam struct for backward compatibility.
    /// </summary>
    public readonly struct NoParam : IHighlightParams
    {
        public static readonly IHighlightParams Instance = new NoParam();
    }
}
