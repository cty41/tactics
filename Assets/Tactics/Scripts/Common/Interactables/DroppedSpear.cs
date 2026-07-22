using Tactics.Common.Units;

namespace Tactics.Common.Interactables
{
    /// <summary>The Amazon's unique dropped spear. It occupies a cell but is not a combat target.</summary>
    public sealed class DroppedSpear : Interactable
    {
        public IUnit Owner { get; set; }
        public override bool OccupiesCell => true;
        public override bool Selectable => true;

        public override void Interact()
        {
            // AmazonBattleState commits pickup after ownership and distance validation.
        }

        public void RemoveFromBattle() => Destroy();
    }
}
