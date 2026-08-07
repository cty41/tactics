using System.Reflection;
using NUnit.Framework;
using Tactics.Common.Units;
using Tactics.Units;
using UnityEngine;

namespace Tactics.Tests.Editor
{
    public sealed class UnitDerivedStatRulesTests
    {
        private static readonly PropertyInfo PreviewMovementProperty = typeof(TilemapUnit).GetProperty(
            "PreviewMaxMovementPoints",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [TestCase(1f, 1f, 2f)]
        [TestCase(4f, 2f, 8f)]
        [TestCase(5f, 3f, 10f)]
        [TestCase(6f, 3f, 12f)]
        [TestCase(8f, 4f, 16f)]
        [TestCase(12f, 4f, 24f)]
        [TestCase(99f, 4f, 198f)]
        public void Speed_UsesCappedHalfStepMovement_WithoutChangingInitiative(
            float speed,
            float expectedMovement,
            float expectedInitiative)
        {
            var gameObject = new GameObject($"DerivedStats_{speed}");
            try
            {
                var unit = gameObject.AddComponent<TilemapUnit>();

                unit.Speed = speed;

                Assert.That(UnitDerivedStatRules.CalculateMovement(speed), Is.EqualTo(expectedMovement));
                Assert.That(unit.MaxMovementPoints, Is.EqualTo(expectedMovement));
                Assert.That(unit.Initiative, Is.EqualTo(expectedInitiative));
                Assert.That(PreviewMovementProperty, Is.Not.Null,
                    "TilemapUnit must expose an Inspector preview for derived movement.");
                Assert.That((float)PreviewMovementProperty.GetValue(unit), Is.EqualTo(expectedMovement),
                    "The Inspector preview must use the same movement rule as runtime derived stats.");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
