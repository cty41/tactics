using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Tactics.Common.Units.Classes;
using Tactics.Common.Units.Buffs;
using Tactics.Equipment;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tactics.Roster
{
    /// <summary>Base attribute types matching <see cref="CharacterDefinition"/> fields.</summary>
    public enum AttributeType
    {
        Strength,
        Agility,
        Constitution,
        Intelligence,
        Charisma,
        Luck,
        Speed
    }

    /// <summary>Skill activation type.</summary>
    public enum SkillType
    {
        Active,
        Passive,
        ExtraUtility
    }

    /// <summary>Serializable character data aligned with <see cref="Tactics.Common.Units.Unit"/> combat fields.</summary>
    [Serializable]
    public class CharacterDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public int Level { get; set; }
        public int Experience { get; set; }
        public int CurrentHp { get; set; }
        public int? CurrentMp { get; set; }
        public bool IsDead { get; set; }

        /// <summary>最大HP，基于体质计算（与 Unit.MaxHealth 公式一致：Constitution × 4）。</summary>
        public int MaxHp => System.Math.Max(1, Constitution * 4);

        /// <summary>最大MP，基于魅力计算（与 Unit.MaxMana 公式一致：Charisma × 3）。</summary>
        public int MaxMp => System.Math.Max(0, Charisma * 3);

        public int AttributePoints { get; set; }
        public Dictionary<AttributeType, int> AllocatedAttributes { get; set; }
        public List<LearnedSkill> LearnedSkills { get; set; }
        public int Gold { get; set; }

        /// <summary>The basic skill that defines the character's starting first-slice branch.</summary>
        public string StartingBranchSkillId { get; set; }

        /// <summary>Whether this branch's one-time guaranteed advanced offer has been consumed.</summary>
        public bool HasConsumedStartingAdvancedGuarantee { get; set; }

        public int Strength { get; set; }
        public int Agility { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Charisma { get; set; }
        public int Luck { get; set; }

        public float Speed { get; set; }
        public int AttackRange { get; set; }
        public int AttackFactor { get; set; }
        public int DefenceFactor { get; set; }
        public RoleType RoleType { get; set; }
        public string PrefabPath { get; set; }

        public const string PrefabPathPrefix = "Assets/Tactics/Arts/Prefabs/Units/";
        private const string PrefabExtension = ".prefab";

        public static string ResolvePrefabPath(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (name.StartsWith("Assets/", System.StringComparison.Ordinal))
                return name;
            return PrefabPathPrefix + name + PrefabExtension;
        }

        public Dictionary<EquipmentSlot, string> Equipment { get; set; } = new Dictionary<EquipmentSlot, string>();

        /// <summary>
        /// Instance ID of the single consumable carried by this character.
        /// </summary>
        /// <remarks>
        /// Carried instances remain in PlayerAdventureState.ConsumableInstances and are
        /// excluded from the shared backpack by CharacterLoadoutService.
        /// </remarks>
        public string CarriedConsumableInstanceId { get; set; }

        /// <summary>
        /// 地图层待生效 Buff 列表。战斗开始时应用到角色，然后清空。
        /// </summary>
        [JsonIgnore]
        public List<BuffConfig> PendingBuffs = new List<BuffConfig>();

        /// <summary>
        /// PendingBuffs 的轻量可持久化表示，避免直接序列化 ScriptableObject。
        /// </summary>
        public List<PendingBuffSnapshot> PendingBuffSnapshots { get; set; } = new List<PendingBuffSnapshot>();

        /// <summary>
        /// 添加待生效 Buff（防止重复添加相同名称的 Buff）。
        /// </summary>
        public void AddPendingBuff(BuffConfig config)
        {
            if (config == null) return;

            PendingBuffSnapshots ??= new List<PendingBuffSnapshot>();
            PendingBuffs ??= new List<BuffConfig>();

            var snapshot = PendingBuffSnapshot.FromConfig(config);
            if (snapshot == null)
                return;

            if (!PendingBuffSnapshots.Exists(b => b.BuffName == snapshot.BuffName))
                PendingBuffSnapshots.Add(snapshot);

            if (!PendingBuffs.Exists(b => b.BuffName == snapshot.BuffName))
                PendingBuffs.Add(snapshot.ToRuntimeConfig());
        }

        /// <summary>
        /// 移除指定名称的待生效 Buff。
        /// </summary>
        public void RemovePendingBuff(string buffName)
        {
            PendingBuffSnapshots?.RemoveAll(b => b.BuffName == buffName);
            PendingBuffs.RemoveAll(b => b.BuffName == buffName);
        }

        /// <summary>
        /// 清空所有待生效 Buff（战斗开始后调用）。
        /// </summary>
        public void ClearPendingBuffs()
        {
            PendingBuffSnapshots?.Clear();
            PendingBuffs.Clear();
        }

        /// <summary>
        /// 检查是否有指定名称的待生效 Buff。
        /// </summary>
        public bool HasPendingBuff(string buffName)
        {
            return (PendingBuffSnapshots?.Exists(b => b.BuffName == buffName) == true)
                   || PendingBuffs.Exists(b => b.BuffName == buffName);
        }

        public void HydratePendingBuffs()
        {
            PendingBuffs ??= new List<BuffConfig>();
            PendingBuffSnapshots ??= new List<PendingBuffSnapshot>();

            PendingBuffs.Clear();
            foreach (var snapshot in PendingBuffSnapshots)
            {
                var config = snapshot?.ToRuntimeConfig();
                if (config != null)
                    PendingBuffs.Add(config);
            }
        }

        public int GetTotalStrength()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.StrengthBonus;
            }
            return Strength + bonus;
        }

        public int GetTotalAgility()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.AgilityBonus;
            }
            return Agility + bonus;
        }

        public int GetTotalConstitution()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.ConstitutionBonus;
            }
            return Constitution + bonus;
        }

        public int GetTotalIntelligence()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.IntelligenceBonus;
            }
            return Intelligence + bonus;
        }

        public int GetTotalCharisma()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.CharismaBonus;
            }
            return Charisma + bonus;
        }

        public int GetTotalLuck()
        {
            int bonus = 0;
            foreach (var kvp in Equipment)
            {
                var def = EquipmentDatabase.GetById(kvp.Value);
                if (def != null) bonus += def.LuckBonus;
            }
            return Luck + bonus;
        }

        public static CharacterDefinition CreateDefault(string id, string displayName, int strengthBonus = 0, int intelligenceBonus = 0, int agilityBonus = 0, RoleType roleType = RoleType.Barbarian)
        {
            return new CharacterDefinition
            {
                Id = id,
                DisplayName = displayName,
                Level = 1,
                Strength = 5 + strengthBonus,
                Agility = 5 + agilityBonus,
                Constitution = 5,
                Intelligence = 5 + intelligenceBonus,
                Charisma = 5,
                Luck = 5,
                Speed = 5f,
                AttackRange = 1,
                AttackFactor = 1,
                DefenceFactor = 1,
                RoleType = roleType,
                Experience = 0,
                CurrentHp = 5 * 4,
                CurrentMp = 5 * 3,
                IsDead = false,
                AttributePoints = 0,
                AllocatedAttributes = new Dictionary<AttributeType, int>(),
                LearnedSkills = new List<LearnedSkill>(),
                Gold = 0
            };
        }

        /// <summary>A skill learned by the character.</summary>
        [Serializable]
        public class LearnedSkill
        {
            public string SkillId { get; set; }
            public SkillType SkillType { get; set; }
            public int Level { get; set; }
        }

        [Serializable]
        public class PendingBuffSnapshot
        {
            public string BuffName { get; set; }
            public string BuffAssetPath { get; set; }
            public int DefaultDuration { get; set; }
            public bool CanAct { get; set; }
            public BuffPolarity Polarity { get; set; }
            public BuffEffectType EffectType { get; set; }
            public BuffTriggerTiming TriggerTiming { get; set; }
            public string CurseCategory { get; set; }
            public float DamagePerTurn { get; set; }
            public ElementType ElementType { get; set; }
            public DamageCategory DamageCategory { get; set; } = DamageCategory.Magic;
            public BuffRefreshStrategy RefreshStrategy { get; set; }
            public float SpeedModifier { get; set; }
            public float DamageReductionPercent { get; set; }

            public static PendingBuffSnapshot FromConfig(BuffConfig config)
            {
                if (config == null || string.IsNullOrWhiteSpace(config.BuffName))
                    return null;

                return new PendingBuffSnapshot
                {
                    BuffName = config.BuffName,
                    BuffAssetPath = ResolveBuffAssetPath(config),
                    DefaultDuration = config.DefaultDuration,
                    CanAct = config.CanAct,
                    Polarity = config.Polarity,
                    EffectType = config.EffectType,
                    TriggerTiming = config.TriggerTiming,
                    CurseCategory = config.CurseCategory,
                    DamagePerTurn = config.DamagePerTurn,
                    ElementType = config.ElementType,
                    DamageCategory = config.DamageCategory,
                    RefreshStrategy = config.RefreshStrategy,
                    SpeedModifier = config.SpeedModifier,
                    DamageReductionPercent = config.DamageReductionPercent
                };
            }

            public BuffConfig ToRuntimeConfig()
            {
                if (string.IsNullOrWhiteSpace(BuffName))
                    return null;

                if (!string.IsNullOrWhiteSpace(BuffAssetPath))
                {
                    var loadedConfig = Tactics.AssetPipeline.GameAssetManager.Instance?.Load<BuffConfig>(BuffAssetPath);
                    if (loadedConfig == null)
                    {
#if UNITY_EDITOR
                        loadedConfig = AssetDatabase.LoadAssetAtPath<BuffConfig>(BuffAssetPath);
#endif
                    }

                    if (loadedConfig != null)
                    {
                        loadedConfig.RuntimeSourceAssetPath = BuffAssetPath;
                        return loadedConfig;
                    }
                }

                var config = ScriptableObject.CreateInstance<BuffConfig>();
                SetPrivateField(config, "_buffName", BuffName);
                SetPrivateField(config, "_defaultDuration", DefaultDuration);
                SetPrivateField(config, "_canAct", CanAct);
                SetPrivateField(config, "_polarity", Polarity);
                SetPrivateField(config, "_effectType", EffectType);
                SetPrivateField(config, "_triggerTiming", TriggerTiming);
                SetPrivateField(config, "_curseCategory", CurseCategory ?? string.Empty);
                SetPrivateField(config, "_damagePerTurn", DamagePerTurn);
                SetPrivateField(config, "_elementType", ElementType);
                SetPrivateField(config, "_damageCategory", DamageCategory);
                SetPrivateField(config, "_refreshStrategy", RefreshStrategy);
                SetPrivateField(config, "_speedModifier", SpeedModifier);
                SetPrivateField(config, "_damageReductionPercent", DamageReductionPercent);
                config.RuntimeSourceAssetPath = BuffAssetPath;
                return config;
            }

            private static string ResolveBuffAssetPath(BuffConfig config)
            {
                if (!string.IsNullOrWhiteSpace(config.RuntimeSourceAssetPath))
                    return config.RuntimeSourceAssetPath;

#if UNITY_EDITOR
                string assetPath = AssetDatabase.GetAssetPath(config);
                if (!string.IsNullOrWhiteSpace(assetPath))
                    return assetPath;
#endif

                return null;
            }

            private static void SetPrivateField<T>(BuffConfig config, string fieldName, T value)
            {
                typeof(BuffConfig).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(config, value);
            }
        }
    }
}
