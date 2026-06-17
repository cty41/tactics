namespace Tactics.Common.Units.Buffs
{
    public enum BuffChangeType
    {
        Added,
        Removed,
        Refreshed,
        TurnChanged
    }

    public class BuffChangedEventArgs
    {
        public BuffChangeType ChangeType { get; }
        public Buff Buff { get; }

        public BuffChangedEventArgs(BuffChangeType changeType, Buff buff)
        {
            ChangeType = changeType;
            Buff = buff;
        }
    }
}
