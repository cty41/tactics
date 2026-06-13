#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Tactics.Runtime.Utilities;
using UnityEditor;
using UnityEngine;

namespace Tactics.Common.Units.Abilities.Editor
{
    /// <summary>
    /// Creates 6 default ability configs using the new Asset-based system.
    /// Menu: Tools > Ability System > Create Default Ability Configs
    /// </summary>
    public class CreateDefaultAbilityConfigs : EditorWindow
    {
        [MenuItem("Tactics/Ability System/Create Default Ability Configs")]
        public static void CreateConfigs()
        {
            var logs = new List<string>();
            string dir = "Assets/Tactics/Battle/Abilities";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // 1. Melee_Attack: SingleTargetEnemy(min=0, max=1) + DamageEffect
            string meleePath = $"{dir}/Melee_Attack.asset";
            DeleteIfExists(meleePath);
            var melee = ScriptableObject.CreateInstance<AbilityConfig>();
            melee.name = "Melee_Attack";
            SetField(melee, "_displayName", "Melee Attack");
            SetField(melee, "_description", "Attack adjacent enemy unit.");
            var meleeTargeting = new SingleTargetEnemy();
            SetField(meleeTargeting, "_minRange", 0);
            SetField(meleeTargeting, "_maxRange", 1);
            SetField(melee, "_targetingStrategy", meleeTargeting);
            SetField(melee, "_effects", new List<AbilityEffect> { new DamageEffect() });
            AssetDatabase.CreateAsset(melee, meleePath);
            logs.Add($"Created: {meleePath}");

            // 2. Ranged_Attack: SingleTargetEnemy(min=2, max=5) + DamageEffect(isRanged)
            string rangedPath = $"{dir}/Ranged_Attack.asset";
            DeleteIfExists(rangedPath);
            var ranged = ScriptableObject.CreateInstance<AbilityConfig>();
            ranged.name = "Ranged_Attack";
            SetField(ranged, "_displayName", "Ranged Attack");
            SetField(ranged, "_description", "Attack enemy unit at range 2-5.");
            var rangedTargeting = new SingleTargetEnemy();
            SetField(rangedTargeting, "_minRange", 2);
            SetField(rangedTargeting, "_maxRange", 5);
            SetField(ranged, "_targetingStrategy", rangedTargeting);
            var rangedEffect = new DamageEffect();
            SetField(rangedEffect, "_isRangedDamage", true);
            SetField(ranged, "_effects", new List<AbilityEffect> { rangedEffect });
            AssetDatabase.CreateAsset(ranged, rangedPath);
            logs.Add($"Created: {rangedPath}");

            // 3. Melee_Heal: SingleTargetAlly(max=1) + HealEffect
            string healPath = $"{dir}/Melee_Heal.asset";
            DeleteIfExists(healPath);
            var heal = ScriptableObject.CreateInstance<AbilityConfig>();
            heal.name = "Melee_Heal";
            SetField(heal, "_displayName", "Heal");
            SetField(heal, "_description", "Heal adjacent ally unit.");
            var healTargeting = new SingleTargetAlly();
            SetField(healTargeting, "_maxRange", 1);
            SetField(heal, "_targetingStrategy", healTargeting);
            var healEffect = new HealEffect();
            SetField(healEffect, "_healAmount", 3f);
            SetField(heal, "_effects", new List<AbilityEffect> { healEffect });
            AssetDatabase.CreateAsset(heal, healPath);
            logs.Add($"Created: {healPath}");

            // 4. Fireball: AoETargeting(radius=1, maxRange=4) + DamageEffect
            string fireballPath = $"{dir}/Fireball.asset";
            DeleteIfExists(fireballPath);
            var fireball = ScriptableObject.CreateInstance<AbilityConfig>();
            fireball.name = "Fireball";
            SetField(fireball, "_displayName", "Fireball");
            SetField(fireball, "_description", "Deal AoE damage in a cross pattern.");
            SetField(fireball, "_manaCost", 5);
            var aoeTargeting = new AoETargeting();
            SetField(aoeTargeting, "_radius", 1);
            SetField(aoeTargeting, "_maxRange", 4);
            SetField(aoeTargeting, "_shape", AoeShape.Cross);
            SetField(fireball, "_targetingStrategy", aoeTargeting);
            var fireballEffect = new DamageEffect();
            SetField(fireballEffect, "_baseDamage", 5f);
            SetField(fireball, "_effects", new List<AbilityEffect> { fireballEffect });
            AssetDatabase.CreateAsset(fireball, fireballPath);
            logs.Add($"Created: {fireballPath}");

            // 5. Charge_Attack: MoveThenAttackTargeting + DamageEffect
            string chargeAtkPath = $"{dir}/Charge_Attack.asset";
            DeleteIfExists(chargeAtkPath);
            var chargeAtk = ScriptableObject.CreateInstance<AbilityConfig>();
            chargeAtk.name = "Charge_Attack";
            SetField(chargeAtk, "_displayName", "Charge Attack");
            SetField(chargeAtk, "_description", "Move then attack an enemy.");
            var chargeAtkTargeting = new MoveThenAttackTargeting();
            SetField(chargeAtkTargeting, "_moveRange", 5);
            SetField(chargeAtk, "_targetingStrategy", chargeAtkTargeting);
            SetField(chargeAtk, "_effects", new List<AbilityEffect> { new DamageEffect() });
            AssetDatabase.CreateAsset(chargeAtk, chargeAtkPath);
            logs.Add($"Created: {chargeAtkPath}");

            // 6. Charge_Heal: MoveThenHealTargeting + HealEffect
            string chargeHealPath = $"{dir}/Charge_Heal.asset";
            DeleteIfExists(chargeHealPath);
            var chargeHeal = ScriptableObject.CreateInstance<AbilityConfig>();
            chargeHeal.name = "Charge_Heal";
            SetField(chargeHeal, "_displayName", "Charge Heal");
            SetField(chargeHeal, "_description", "Move then heal an adjacent ally.");
            var chargeHealTargeting = new MoveThenHealTargeting();
            SetField(chargeHealTargeting, "_moveRange", 5);
            SetField(chargeHealTargeting, "_healRange", 1);
            SetField(chargeHeal, "_targetingStrategy", chargeHealTargeting);
            var chargeHealEffect = new HealEffect();
            SetField(chargeHealEffect, "_healAmount", 5f);
            SetField(chargeHeal, "_effects", new List<AbilityEffect> { chargeHealEffect });
            AssetDatabase.CreateAsset(chargeHeal, chargeHealPath);
            logs.Add($"Created: {chargeHealPath}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var log in logs) TLog.Info($"[AbilityConfig] {log}");
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
            var meta = path + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
        }

        private static void SetField<T>(object obj, string name, T val)
        {
            var f = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            f?.SetValue(obj, val);
        }
    }
}