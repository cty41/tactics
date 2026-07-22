using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Tactics.Common.Controllers;
using Tactics.Common.Units;

namespace Tactics.Common.Battle
{
    public sealed class SummonEntry
    {
        public long Sequence { get; }
        public IUnit Owner { get; }
        public string Category { get; }
        public IUnit Unit { get; }

        internal SummonEntry(long sequence, IUnit owner, string category, IUnit unit)
        {
            Sequence = sequence;
            Owner = owner;
            Category = category;
            Unit = unit;
        }
    }

    /// <summary>
    /// Battle-scoped ordered summon ownership. Registration records the validated new unit
    /// before detaching the oldest entries that the caller must despawn.
    /// </summary>
    public sealed class SummonRegistry
    {
        private static readonly ConditionalWeakTable<IGridController, SummonRegistry> Registries = new();
        private readonly List<SummonEntry> _entries = new();
        private long _nextSequence;

        public static SummonRegistry For(IGridController gridController)
        {
            return gridController == null ? null : Registries.GetValue(gridController, _ => new SummonRegistry());
        }

        public IReadOnlyList<SummonEntry> Entries
        {
            get
            {
                RemoveInvalidEntries();
                return _entries.OrderBy(entry => entry.Sequence).ToList();
            }
        }

        public IReadOnlyList<IUnit> Register(IUnit owner, string category, IUnit unit, int maximumActive)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (unit == null) throw new ArgumentNullException(nameof(unit));

            RemoveInvalidEntries();
            var previousOwner = _entries.FirstOrDefault(entry => ReferenceEquals(entry.Unit, unit))?.Owner;
            if (previousOwner != null)
            {
                RemoveEntry(unit);
                RefreshLegacyOwnerReference(previousOwner);
            }

            string normalizedCategory = NormalizeCategory(category);
            int limit = Math.Max(1, maximumActive);
            var sameGroup = _entries
                .Where(entry => ReferenceEquals(entry.Owner, owner) && entry.Category == normalizedCategory)
                .OrderBy(entry => entry.Sequence)
                .ToList();
            int replacementCount = Math.Max(0, sameGroup.Count - limit + 1);
            var replacements = sameGroup.Take(replacementCount).Select(entry => entry.Unit).ToList();

            unit.OwnerUnit = owner;
            unit.OwnerUnitId = owner.UnitID;
            _entries.Add(new SummonEntry(++_nextSequence, owner, normalizedCategory, unit));
            foreach (var replacement in replacements)
                RemoveEntry(replacement);

            RefreshLegacyOwnerReference(owner);
            return replacements;
        }

        public IReadOnlyList<IUnit> GetOrdered(IUnit owner, string category)
        {
            RemoveInvalidEntries();
            string normalizedCategory = NormalizeCategory(category);
            return _entries
                .Where(entry => ReferenceEquals(entry.Owner, owner) && entry.Category == normalizedCategory)
                .OrderBy(entry => entry.Sequence)
                .Select(entry => entry.Unit)
                .ToList();
        }

        public string GetCategory(IUnit unit)
        {
            RemoveInvalidEntries();
            return _entries.FirstOrDefault(entry => ReferenceEquals(entry.Unit, unit))?.Category;
        }

        public void Unregister(IUnit unit)
        {
            var owner = _entries.FirstOrDefault(entry => ReferenceEquals(entry.Unit, unit))?.Owner;
            RemoveEntry(unit);
            if (owner != null)
                RefreshLegacyOwnerReference(owner);
        }

        public void Despawn(IUnit unit)
        {
            if (unit == null)
                return;
            Unregister(unit);
            unit.OwnerUnit = null;
            unit.OwnerUnitId = -1;
            if (!unit.IsDowned && unit.Health > 0)
                unit.ModifyHealth(-unit.Health - 1f, null);
        }

        public void HandleUnitDeath(IUnit unit)
        {
            if (unit == null)
                return;

            var owned = _entries
                .Where(entry => ReferenceEquals(entry.Owner, unit))
                .OrderBy(entry => entry.Sequence)
                .Select(entry => entry.Unit)
                .ToList();
            foreach (var summon in owned)
                Despawn(summon);

            Unregister(unit);
        }

        public void Clear(bool despawnSummons)
        {
            var units = _entries.OrderBy(entry => entry.Sequence).Select(entry => entry.Unit).ToList();
            var owners = _entries.Select(entry => entry.Owner).Where(owner => owner != null).Distinct().ToList();
            _entries.Clear();
            _nextSequence = 0;
            foreach (var owner in owners)
            {
                if (units.Any(unit => ReferenceEquals(owner.SummonedUnit, unit)))
                    owner.SummonedUnit = null;
            }

            foreach (var unit in units)
            {
                if (unit == null)
                    continue;
                unit.OwnerUnit = null;
                unit.OwnerUnitId = -1;
                if (despawnSummons && !unit.IsDowned && unit.Health > 0)
                    unit.ModifyHealth(-unit.Health - 1f, null);
            }
        }

        private void RemoveInvalidEntries()
        {
            var affectedOwners = _entries
                .Where(entry => entry.Unit == null || entry.Unit.IsDowned || entry.Unit.Health <= 0)
                .Select(entry => entry.Owner)
                .Where(owner => owner != null)
                .Distinct()
                .ToList();
            _entries.RemoveAll(entry => entry.Unit == null || entry.Unit.IsDowned || entry.Unit.Health <= 0);
            foreach (var owner in affectedOwners)
                RefreshLegacyOwnerReference(owner);
        }

        private void RemoveEntry(IUnit unit)
        {
            _entries.RemoveAll(entry => ReferenceEquals(entry.Unit, unit));
        }

        private void RefreshLegacyOwnerReference(IUnit owner)
        {
            if (owner == null)
                return;
            owner.SummonedUnit = _entries
                .Where(entry => ReferenceEquals(entry.Owner, owner))
                .OrderByDescending(entry => entry.Sequence)
                .Select(entry => entry.Unit)
                .FirstOrDefault();
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? "Default" : category.Trim();
        }
    }
}
