using System.Threading.Tasks;
using Tactics.Tbsf.Common.Units;
using Tactics.Tbsf.Common.Utilities;
using UnityEngine;

namespace Tactics.Tbsf.Common.Highlighters
{
    /// <summary>
    /// A highlighter that repositions a transform to a unit's destination cell, defined by MoveHighlightParams.
    /// </summary>
    public class RelocateToDestinationHighlighter : Highlighter
    {
        [SerializeField] private Transform _transform;

        public override Task Apply(IHighlightParams @params)
        {
            var moveHighlightParams = (MoveHighlightParams)@params;
            _transform.position = moveHighlightParams.Destination.WorldPosition.ToVector3();
            return Task.CompletedTask;
        }
    }
}