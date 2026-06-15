# 商店商品池动态化 — 设计文档

> **版本**: v1.0
> **日期**: 2026-06-15
> **状态**: 设计完成
> **关联设计**: [roguelike-map-gameplay-design.md](roguelike-map-gameplay-design.md)

---

## TL;DR

将 `ShopManager` 从硬编码 5 件商品改为从 `EquipmentDatabase` 动态读取，支持 Common/Rare 稀有度加权随机选取 2-4 件商品，价格来自 `Equipment.json` 新增的 Price 字段。

---

## 设计决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 价格来源 | Equipment.json 增加 Price 字段 | 数据集中管理，新增装备时只改一个文件 |
| 稀有度定义 | Equipment.json 增加 Rarity 字段，手动标记 Common/Rare | 可控性强，策划可精确调整 |
| 商品筛选 | 按 Rarity 加权随机（Common 70% / Rare 30%），2-4 件不重复 | 每次访问有惊喜但不会全是稀有 |
| 装备池扩充 | 补充 5 件新装备（含 2 件 Rare） | 让商店有足够变化 |

---

## 现状分析

### 当前代码

- `Equipment.json`：7 件装备，**无 Price/Rarity 字段**
- `EquipmentDefinition.cs`：仅有 Id/DisplayName/Slot/属性加成字段
- `EquipmentSlot` 枚举：Weapon/Armor/Helmet/Boots/Accessory
- `ShopManager.cs`：硬编码 5 件商品 + 固定价格（铁剑12/皮甲8/铁盔7/短弓10/幸运戒指15）
- `StoreNodeHandler.BuildGoods()`：优先用节点配置 `storeConfig.goods`，回退到 `ShopManager.GenerateGoods()`

### 问题

1. 商品池写死，每次访问内容相同
2. 价格与装备数据分离，维护成本高
3. 无稀有度概念，缺少"抽到好东西"的惊喜感
4. 装备池太小（7件），商店缺乏变化

---

## 数据模型改动

### EquipmentDefinition 扩展

```csharp
[Serializable]
public class EquipmentDefinition
{
    public string Id;
    public string DisplayName;
    public EquipmentSlot Slot;
    public EquipmentRarity Rarity;  // 新增
    public int Price;               // 新增
    public int StrengthBonus;
    public int AgilityBonus;
    public int ConstitutionBonus;
    public int IntelligenceBonus;
    public int CharismaBonus;
    public int LuckBonus;
}
```

### EquipmentRarity 枚举（新增）

```csharp
public enum EquipmentRarity { Common = 0, Rare = 1 }
```

### EquipmentSlot 扩展

```csharp
public enum EquipmentSlot
{
    Weapon, Armor, Helmet, Boots, Accessory, Shield  // 新增 Shield
}
```

---

## Equipment.json 数据

### 现有装备补充 Price/Rarity

| Id | DisplayName | Rarity | Price | 属性加成总和 |
|----|------------|--------|-------|-------------|
| sword_01 | 铁剑 | Common | 10 | STR+5 |
| leather_armor_01 | 皮甲 | Common | 8 | AGI+2, CON+3 |
| iron_helmet_01 | 铁盔 | Common | 7 | CON+2 |
| leather_boots_01 | 皮靴 | Common | 7 | AGI+3 |
| lucky_ring_01 | 幸运戒指 | Rare | 15 | CHA+1, LUCK+5 |
| staff_01 | 橡木法杖 | Common | 9 | INT+5 |
| bow_01 | 短弓 | Common | 10 | STR+1, AGI+4 |

### 新增装备

| Id | DisplayName | Slot | Rarity | Price | 属性加成 |
|----|------------|------|--------|-------|---------|
| shield_01 | 铁盾 | Shield | Common | 9 | CON+3 |
| wizard_hat_01 | 法师帽 | Helmet | Common | 8 | INT+3, CHA+1 |
| silver_ring_01 | 银戒指 | Accessory | Common | 7 | LUCK+3 |
| steel_sword_01 | 精钢剑 | Weapon | Rare | 15 | STR+7, AGI+1 |
| shadow_cloak_01 | 暗影斗篷 | Armor | Rare | 15 | AGI+5, LUCK+2 |

**定价逻辑**：Common 装备 7-10 金，Rare 装备 15 金。符合设计文档要求（消耗品 3-5、普通 8-12、稀有 15）。

---

## ShopManager 改动

### 删除

硬编码的四个静态数组：`GoodIds` / `GoodNames` / `GoodIcons` / `GoodPrices`

### 重写 GenerateGoods

```csharp
public List<ShopGood> GenerateGoods(int count)
{
    count = Mathf.Clamp(count, 2, 4);
    EquipmentDatabase.Load();
    var allDefs = EquipmentDatabase.GetAll();

    var commonPool = allDefs.Where(d => d.Rarity == EquipmentRarity.Common).ToList();
    var rarePool = allDefs.Where(d => d.Rarity == EquipmentRarity.Rare).ToList();

    var selectedIds = new HashSet<string>();
    var goods = new List<ShopGood>();

    for (int i = 0; i < count; i++)
    {
        bool pickRare = rarePool.Count > 0
            && Random.value < 0.3f
            && !goods.Any(g => EquipmentDatabase.GetById(g.EquipmentId)?.Rarity == EquipmentRarity.Rare);

        var pool = pickRare ? rarePool : commonPool;

        // 从未选过的池中随机选取
        var available = pool.Where(d => !selectedIds.Contains(d.Id)).ToList();
        if (available.Count == 0)
            available = pool; // 允许重复

        var picked = available[Random.Range(0, available.Count)];
        selectedIds.Add(picked.Id);

        goods.Add(new ShopGood
        {
            EquipmentId = picked.Id,
            Name = picked.DisplayName,
            Price = picked.Price,
            IconHint = string.Empty
        });
    }

    TLog.Info($"[ShopManager] 生成 {goods.Count} 件商品");
    return goods;
}
```

**关键规则**：
- Rare 装备最多出现 1 件（30% 概率）
- 不重复选取同一装备
- 如果装备池不够，允许重复

---

## EquipmentDatabase 改动

新增 `GetAll()` 方法：

```csharp
public static IReadOnlyList<EquipmentDefinition> GetAll()
{
    if (!_isLoaded) Load();
    return _definitions.Values.ToList();
}
```

---

## 不改动的部分

- `StoreNodeConfig` / `StoreGoodEntry` — 节点级配置保持不变
- `ShopGood` 数据类 — 字段不变
- `StoreNodeHandler` 的购买/持久化/UI 逻辑 — 不变
- `ShopPanel.uxml` — 不变

---

## 文件清单

| 文件 | 改动类型 |
|------|---------|
| `Assets/Tactics/Scripts/Common/Equipment/EquipmentDefinition.cs` | 修改：增加 Price/Rarity 字段 |
| `Assets/Tactics/Scripts/Common/Equipment/EquipmentSlot.cs` | 修改：新增 Shield 值 |
| `Assets/Tactics/Scripts/Common/Equipment/EquipmentDatabase.cs` | 修改：增加 GetAll() 方法 |
| `Assets/Tactics/GameData/Equipment.json` | 修改：增加 Price/Rarity + 5 件新装备 |
| `Assets/Tactics/Scripts/RoguelikeMap/Interaction/ShopManager.cs` | 重写：动态生成替代硬编码 |

---

## 验收标准

1. `EquipmentDatabase.Load()` 成功加载 12 件装备（7旧+5新）
2. `ShopManager.GenerateGoods(3)` 返回 2-4 件不重复商品
3. 价格来自 Equipment.json（非硬编码）
4. Rare 装备最多出现 1 件
5. 每次访问商品不同（随机性）
6. 购买流程正常（金币扣除、装备入库）

---

*文档生成时间：2026-06-15*
