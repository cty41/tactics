using System;
using System.Collections.Generic;
using Tactics.Consumables;

namespace Tactics.Roster
{
    [Serializable]
    public class PlayerAdventureState
    {
        public int Version { get; set; } = 4;
        public bool IsPureRun { get; set; }
        public int RunSeed { get; set; }
        public int Gold { get; set; } = 0;
        public List<CharacterDefinition> Roster { get; set; } = new List<CharacterDefinition>();
        public List<string> ActivePartyCharacterIds { get; set; } = new List<string>();
        public List<string> Inventory { get; set; } = new List<string>();
        public List<ConsumableInstance> ConsumableInstances { get; set; } = new List<ConsumableInstance>();
    }
}
