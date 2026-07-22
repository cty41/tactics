using System.Collections.Generic;
using System.Linq;

namespace Tactics.Common.Battle
{
    /// <summary>
    /// Compatibility facade for callers that still use the first-slice name.
    /// The canonical metadata and runtime paths now live in <see cref="PureRunAbilityCatalog"/>.
    /// </summary>
    public static class FirstSliceSkillCatalog
    {
        public static IEnumerable<SkillDefinition> All =>
            PureRunAbilityCatalog.FormalSkills.Select(definition => definition.Skill);

        public static bool TryGet(string skillId, out SkillDefinition definition)
        {
            if (PureRunAbilityCatalog.TryGet(skillId, out var pureRunDefinition) &&
                pureRunDefinition.IsUpgradeVisible)
            {
                definition = pureRunDefinition.Skill;
                return true;
            }

            definition = null;
            return false;
        }
    }
}
