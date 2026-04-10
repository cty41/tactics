using System.Threading.Tasks;
using Tactics.Tbsf.Common.Units;
using UnityEngine;

namespace Tactics.Tbsf.Common.Highlighters
{
    /// <summary>
    /// Rotates a Transform to face an enemy unit during combat.
    /// </summary>
    public class FaceEnemyHighlighter : BaseRotationHighlighter
    {
        public override async Task Apply(IHighlightParams @params)
        {
            var combatHighlightParams = (CombatHighlightParams)@params;
            Vector3 directionToFace = (combatHighlightParams.SecondaryUnit.transform.position - _transform.position).normalized;
            await RotateTowards(directionToFace);
        }
    }
}
