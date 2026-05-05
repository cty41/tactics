using UnityEditor;
using Tactics.UI;
using UnityEngine;

namespace Tactics.Editor
{
    public static class DamageNumberConfigGenerator
    {
        private const string SettingsPath = "Assets/Tactics/ScriptableObjects/DamageNumberSettings.asset";

        [MenuItem("Tactics/Generate Damage Number Settings")]
        public static void GenerateSettings()
        {
            // Delete old individual configs
            var oldFiles = new[]
            {
                "Assets/Tactics/ScriptableObjects/DamageNumber_Normal.asset",
                "Assets/Tactics/ScriptableObjects/DamageNumber_Crit.asset",
                "Assets/Tactics/ScriptableObjects/DamageNumber_Heal.asset",
                "Assets/Tactics/ScriptableObjects/DamageNumber_Miss.asset",
            };
            foreach (var f in oldFiles)
            {
                if (AssetDatabase.AssetPathExists(f))
                    AssetDatabase.DeleteAsset(f);
            }

            // Create new settings
            var settings = ScriptableObject.CreateInstance<DamageNumberSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[DamageNumberConfigGenerator] Generated DamageNumberSettings at " + SettingsPath);
        }
    }
}
