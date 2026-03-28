namespace Tactics.Tbsf.Unity.Units
{
    /// <summary>
    /// Represents a unit with a name, providing an interface to access the unit's name.
    /// </summary>
    public interface INamedUnit
    {
        string UnitName { get; }
    }
}