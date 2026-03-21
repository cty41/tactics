using UnityEngine;

namespace Tactics.Roster
{
    /// <summary>Links a battle <see cref="Tactics.Units.TilemapUnit"/> to a roster character id for future persistence.</summary>
    public class RosterCharacterLink : MonoBehaviour
    {
        [SerializeField] private string _characterId;

        public string CharacterId
        {
            get => _characterId;
            set => _characterId = value;
        }
    }
}
