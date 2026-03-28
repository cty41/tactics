using System.Threading.Tasks;
using Tactics.Tbsf.Unity.Units;
using Tactics.Tbsf.Unity.Utilities;
using UnityEngine;

namespace Tactics.Tbsf.Unity.Highlighters
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