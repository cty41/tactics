using System.Reflection;
using Tactics.AssetPipeline;
using Tactics.Common.AI.MonsterAI;
using Tactics.Common.Cells;
using Tactics.Common.Units;
using Tactics.Common.Units.Classes;
using UnityEngine;

namespace Tactics.Common.Testing.Gameplay
{
    public static class TestUnitFactory
    {
        public static Unit CreateUnit(
            Transform parent,
            string name,
            int playerNumber,
            ICell cell,
            RoleConfig roleConfig = null,
            AiBrainAsset brainAsset = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var unit = go.AddComponent<Unit>();

            // Set player number
            unit.PlayerNumber = playerNumber;

            // Set cell
            unit.CurrentCell = cell;

            // Set RoleConfig via reflection
            if (roleConfig != null)
            {
                var roleConfigField = typeof(Unit).GetField("_roleConfig",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                roleConfigField?.SetValue(unit, roleConfig);
            }

            // Set AiBrainAsset
            if (brainAsset != null)
            {
                unit.ApplyAiBrain(brainAsset);
            }

            return unit;
        }

        public static Unit CreateBarbarian(Transform parent, string name, int playerNumber, ICell cell, AiBrainAsset brainAsset = null)
        {
            var config = GameAssetManager.Instance?.Load<RoleConfig>("Assets/Tactics/Battle/Classes/Barbarian.asset");
            return CreateUnit(parent, name, playerNumber, cell, config, brainAsset);
        }

        public static Unit CreateMage(Transform parent, string name, int playerNumber, ICell cell, AiBrainAsset brainAsset = null)
        {
            var config = GameAssetManager.Instance?.Load<RoleConfig>("Assets/Tactics/Battle/Classes/Mage.asset");
            return CreateUnit(parent, name, playerNumber, cell, config, brainAsset);
        }

        public static Unit CreateHunter(Transform parent, string name, int playerNumber, ICell cell, AiBrainAsset brainAsset = null)
        {
            var config = GameAssetManager.Instance?.Load<RoleConfig>("Assets/Tactics/Battle/Classes/Hunter.asset");
            return CreateUnit(parent, name, playerNumber, cell, config, brainAsset);
        }

        public static AiBrainAsset LoadBasicMeleeBrain()
        {
            return GameAssetManager.Instance?.Load<AiBrainAsset>("Assets/Tactics/AI/BasicMeleeBrain.asset");
        }
    }
}
