using System.Threading.Tasks;
using Tactics.Tbsf.Unity.Cells;
using Tactics.Tbsf.Unity.Units;
using UnityEngine;

namespace Tactics.Tbsf.Unity.Highlighters
{
    /// <summary>
    /// Rotates a Transform to face a destination specified in MoveHighlightParams.
    /// </summary>
    public class FaceDestinationHighlighter : BaseRotationHighlighter
    {
        public override async Task Apply(IHighlightParams @params)
        {
            var moveHighlightParams = (MoveHighlightParams)@params;

            Vector3 directionToFace = ((moveHighlightParams.Destination as Cell).transform.position - _transform.position).normalized;
            await RotateTowards(directionToFace);
        }
    }
}
