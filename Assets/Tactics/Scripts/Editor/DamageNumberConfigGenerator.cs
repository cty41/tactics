using UnityEngine;
using UnityEditor;
using Tactics.UI;

namespace Tactics.Editor
{
    public static class DamageNumberConfigGenerator
    {
        [MenuItem("Tactics/Generate Damage Number Configs")]
        public static void GenerateConfigs()
        {
            string path = "Assets/Tactics/ScriptableObjects/";
            
            // Normal
            var normal = ScriptableObject.CreateInstance<DamageNumberConfig>();
            normal.lifetime = 1.5f;
            normal.fadeInDuration = 0.2f;
            normal.fadeOutDuration = 0.3f;
            normal.moveSpeed = 60f;
            normal.startScale = 0.5f;
            normal.peakScale = 1.2f;
            normal.endScale = 1.0f;
            normal.textColor = Color.white;
            normal.fontSize = 24;
            normal.ussClassName = "damage-number-normal";
            AssetDatabase.CreateAsset(normal, path + "DamageNumber_Normal.asset");
            
            // Critical
            var crit = ScriptableObject.CreateInstance<DamageNumberConfig>();
            crit.lifetime = 1.5f;
            crit.fadeInDuration = 0.2f;
            crit.fadeOutDuration = 0.3f;
            crit.moveSpeed = 60f;
            crit.startScale = 0.5f;
            crit.peakScale = 1.5f;
            crit.endScale = 1.0f;
            crit.textColor = new Color(1f, 0.86f, 0.2f);
            crit.fontSize = 32;
            crit.ussClassName = "damage-number-crit";
            AssetDatabase.CreateAsset(crit, path + "DamageNumber_Crit.asset");
            
            // Heal
            var heal = ScriptableObject.CreateInstance<DamageNumberConfig>();
            heal.lifetime = 1.5f;
            heal.fadeInDuration = 0.2f;
            heal.fadeOutDuration = 0.3f;
            heal.moveSpeed = 60f;
            heal.startScale = 0.5f;
            heal.peakScale = 1.2f;
            heal.endScale = 1.0f;
            heal.textColor = new Color(0.31f, 1f, 0.47f);
            heal.fontSize = 24;
            heal.ussClassName = "damage-number-heal";
            AssetDatabase.CreateAsset(heal, path + "DamageNumber_Heal.asset");
            
            // Miss
            var miss = ScriptableObject.CreateInstance<DamageNumberConfig>();
            miss.lifetime = 1.0f;
            miss.fadeInDuration = 0.2f;
            miss.fadeOutDuration = 0.3f;
            miss.moveSpeed = 60f;
            miss.startScale = 0.5f;
            miss.peakScale = 1.2f;
            miss.endScale = 1.0f;
            miss.textColor = new Color(0.59f, 0.59f, 0.59f);
            miss.fontSize = 20;
            miss.ussClassName = "damage-number-miss";
            AssetDatabase.CreateAsset(miss, path + "DamageNumber_Miss.asset");
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("[DamageNumberConfigGenerator] Generated 4 damage number configs at " + path);
        }
    }
}
