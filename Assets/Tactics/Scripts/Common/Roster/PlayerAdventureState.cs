using System;
using System.Collections.Generic;

namespace Tactics.Roster
{
    [Serializable]
    public class PlayerAdventureState
    {
        public int Version { get; set; } = 1;
        public List<CharacterDefinition> Roster { get; set; } = new List<CharacterDefinition>();
        public List<string> ActivePartyCharacterIds { get; set; } = new List<string>();
        public List<string> Inventory { get; set; } = new List<string>();
    }
}
