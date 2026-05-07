using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tactics.Common.Controllers;

namespace Tactics.Common.Units.Abilities
{
    /// <summary>
    /// Command that wraps ability effect execution for the Command pattern.
    /// </summary>
    public readonly struct AbilityCommand : ICommand
    {
        private readonly GenericAbilityImpl _ability;
        private readonly IEnumerable<IUnit> _targets;

        public AbilityCommand(GenericAbilityImpl ability, IEnumerable<IUnit> targets)
        {
            _ability = ability;
            _targets = targets;
        }

        public async Task Execute(IUnit unit, IGridController controller)
        {
            await _ability.ExecuteEffectsAsync(_targets, controller);
        }

        public Task Undo(IUnit unit, IGridController controller)
        {
            return Task.CompletedTask;
        }

        public Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>();
        }

        public ICommand Deserialize(Dictionary<string, object> actionParams, IGridController gridController)
        {
            throw new System.NotImplementedException();
        }
    }
}
