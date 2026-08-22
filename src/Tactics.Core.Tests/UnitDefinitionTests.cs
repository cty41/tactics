using System.Text.Json;
using NUnit.Framework;
using Tactics.Core.Board;
using Tactics.Core.Content;
using Tactics.Core.Units;

namespace Tactics.Core.Tests;

public sealed class UnitDefinitionTests
{
    [Test]
    public void DerivedFormulaUsesConstitutionAndAgilityInsteadOfSpeed()
    {
        using JsonDocument golden = LoadGolden();

        UnitDerivedStats result = UnitDerivedStatRules.Calculate(new UnitAttributes(5, 4, 6, 5, 5, 5));
        Assert.That(result.MoveRange, Is.EqualTo(5));
        Assert.That(result.Initiative, Is.EqualTo(8));
    }

    [Test]
    public void GoldenDefinitions_ExposeExplicitDerivedValues()
    {
        using JsonDocument golden = LoadGolden();

        foreach (JsonElement unit in golden.RootElement.GetProperty("units").EnumerateArray())
        {
            UnitDefinition definition = CreateDefinition(unit);
            JsonElement derived = unit.GetProperty("derived");

            Assert.Multiple(() =>
            {
                Assert.That(definition.DerivedStats.MaxHealth, Is.EqualTo(derived.GetProperty("maxHealth").GetInt32()));
                Assert.That(definition.DerivedStats.MaxMana, Is.EqualTo(derived.GetProperty("maxMana").GetInt32()));
                Assert.That(definition.DerivedStats.StartingMana, Is.EqualTo(derived.GetProperty("startingMana").GetInt32()));
                Assert.That(definition.DerivedStats.MoveRange, Is.EqualTo(derived.GetProperty("moveRange").GetInt32()));
                Assert.That(definition.DerivedStats.Initiative, Is.EqualTo(derived.GetProperty("initiative").GetSingle()));
            });
        }
    }

    [Test]
    public void CreateBattleState_SeparatesDefinitionAndRuntimeIdentity()
    {
        using JsonDocument golden = LoadGolden();
        UnitDefinition definition = CreateDefinition(golden.RootElement.GetProperty("units")[0]);

        var first = definition.CreateBattleState(
            new UnitInstanceId("party.mage.0"),
            new GridPoint(1, 2),
            playerNumber: 0,
            spawnOrdinal: 0);
        var second = definition.CreateBattleState(
            new UnitInstanceId("party.mage.1"),
            new GridPoint(2, 2),
            playerNumber: 0,
            spawnOrdinal: 1);

        Assert.Multiple(() =>
        {
            Assert.That(first.Unit.DefinitionId, Is.EqualTo(definition.ContentId));
            Assert.That(second.Unit.DefinitionId, Is.EqualTo(definition.ContentId));
            Assert.That(first.Unit.InstanceId, Is.Not.EqualTo(second.Unit.InstanceId));
            Assert.That(first.Unit.Position, Is.EqualTo(new GridPoint(1, 2)));
            Assert.That(second.Unit.SpawnOrdinal, Is.EqualTo(1));
            Assert.That(first.CurrentHealth, Is.EqualTo(definition.DerivedStats.MaxHealth));
            Assert.That(first.CurrentMana, Is.EqualTo(definition.DerivedStats.StartingMana));
        });
    }

    [Test]
    public void InvalidAttributesNumbersAndDerivedMismatch_FailClosed()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UnitAttributes(-1, 0, 0, 0, 0, 0));
            Assert.Throws<ArgumentException>(() => new UnitDefinition(
                new ContentId("unit.invalid"),
                "source",
                "Invalid",
                "test",
                "test",
                new UnitAttributes(1, 1, 1, 1, 1, 1),
                5,
                new UnitDerivedStats(4, 3, 1, 4, 10),
                1,
                1,
                1,
                UnitMovementKind.Land,
                true));
        });
    }

    [Test]
    public void ExplicitDerivedMode_AllowsAuthoredClassMovement()
    {
        var definition = new UnitDefinition(
            new ContentId("unit.pure-run.demonbound"), "godot.demonbound", "Demonbound", "player", "demonbound",
            new UnitAttributes(5, 5, 5, 5, 6, 5), 4,
            new UnitDerivedStats(20, 18, 6, 4, 8), 1, 1, 1,
            UnitMovementKind.Land, true, UnitDerivedStatMode.Explicit);

        Assert.Multiple(() =>
        {
            Assert.That(definition.DerivedStatMode, Is.EqualTo(UnitDerivedStatMode.Explicit));
            Assert.That(definition.DerivedStats.MoveRange, Is.EqualTo(4));
            Assert.That(UnitDerivedStatRules.Calculate(definition.Attributes).MoveRange, Is.EqualTo(4));
        });
    }

    private static UnitDefinition CreateDefinition(JsonElement unit)
    {
        JsonElement attributes = unit.GetProperty("attributes");
        JsonElement derived = unit.GetProperty("derived");
        JsonElement combat = unit.GetProperty("combat");
        return new UnitDefinition(
            new ContentId(unit.GetProperty("contentId").GetString()!),
            unit.GetProperty("sourceId").GetString()!,
            unit.GetProperty("displayName").GetString()!,
            unit.GetProperty("familyId").GetString()!,
            unit.GetProperty("roleId").GetString()!,
            new UnitAttributes(
                attributes.GetProperty("strength").GetInt32(),
                attributes.GetProperty("agility").GetInt32(),
                attributes.GetProperty("constitution").GetInt32(),
                attributes.GetProperty("intelligence").GetInt32(),
                attributes.GetProperty("charisma").GetInt32(),
                attributes.GetProperty("luck").GetInt32()),
            unit.GetProperty("speed").GetSingle(),
            new UnitDerivedStats(
                derived.GetProperty("maxHealth").GetInt32(),
                derived.GetProperty("maxMana").GetInt32(),
                derived.GetProperty("startingMana").GetInt32(),
                derived.GetProperty("moveRange").GetInt32(),
                derived.GetProperty("initiative").GetSingle()),
            combat.GetProperty("attackRange").GetInt32(),
            combat.GetProperty("attackFactor").GetSingle(),
            combat.GetProperty("defenceFactor").GetSingle(),
            UnitMovementKind.Land,
            unit.GetProperty("canProduceCorpse").GetBoolean(),
            UnitDerivedStatMode.Explicit);
    }

    private static JsonDocument LoadGolden() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Golden", "unit-batch-v1.json")));
}
