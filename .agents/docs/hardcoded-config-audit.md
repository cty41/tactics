# 运行时代码硬配置审查报告

> 审查日期：2026-05-24
> 审查范围：`Assets/Tactics/Scripts/` 下的运行时代码（排除 Editor 脚本）
> 触发点：`TreasureNodeHandler.cs:44` 的 Buff 路径硬编码

## 问题概述

项目运行时代码中存在大量直接写死的配置数据，包括资产路径、数值参数、字符串列表等。这些硬编码导致：

- 配置变更需要修改代码并重新编译
- 相同配置在多处重复（如金币范围）
- 无法通过 Inspector 或配置文件灵活调整

---

## 一、资产路径硬编码

### 1.1 Buff 路径数组

**文件**: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/TreasureNodeHandler.cs:42-48`

```csharp
string[] buffPaths =
{
    "Assets/Tactics/Arts/ScriptableObjects/Buffs/Mark.asset",
    "Assets/Tactics/Arts/ScriptableObjects/Buffs/Ignite.asset",
    "Assets/Tactics/Arts/ScriptableObjects/Buffs/Frozen.asset",
    "Assets/Tactics/Arts/ScriptableObjects/Buffs/Counter.asset"
};
```

### 1.2 地图配置路径

**文件**: `Assets/Tactics/Scripts/UI/RoguelikeMapUIController.cs:83,86`

```csharp
mapConfig = mgr.Load<RoguelikeMapConfig>("Assets/Tactics/Arts/ScriptableObjects/MapConfigs/DarkForestPrototypeConfig.asset");
mapConfig = mgr.Load<RoguelikeMapConfig>("Assets/Tactics/Arts/ScriptableObjects/MapConfigs/DefaultRogueLikeMapConfig.asset");
```

### 1.3 UI 资产路径

**文件**: `Assets/Tactics/Scripts/UI/RoguelikeMapUIController.cs:429,443`

```csharp
_nodeTemplate = mgr.Load<VisualTreeAsset>("Assets/Tactics/Arts/UI/RoguelikeMapNode.uxml");
_mapBackgroundSprite = mgr.Load<Sprite>("Assets/Tactics/Arts/Sprites/Kenney RPG Pack panels/panel_beige.png");
```

### 1.4 战斗 UI 配置路径

**文件**: `Assets/Tactics/Scripts/UI/BattleUIController.cs:55`

```csharp
private const string DamageNumberSettingsPath = "Assets/Tactics/ScriptableObjects/DamageNumberSettings.asset";
```

### 1.5 PanelSettings 路径

**文件**: `Assets/Tactics/Scripts/Common/UIManager.cs:121`

```csharp
private const string PanelSettingsPath = "Assets/Tactics/UIToolkit/PanelSettings.asset";
```

### 1.6 角色 Prefab 路径前缀

**文件**: `Assets/Tactics/Scripts/Common/Roster/CharacterDefinition.cs:61`

```csharp
public const string PrefabPathPrefix = "Assets/Tactics/Arts/Prefabs/Units/";
```

### 1.7 冒烟测试资产路径

**文件**: `Assets/Tactics/Scripts/AssetPipeline/Runtime/BundleLoadSmokeTest.cs:14`

```csharp
[SerializeField]
private string _assetPath = "Assets/Tactics/AssetPipeline/Sample/BundleTestCube.prefab";
```

---

## 二、数值配置硬编码

### 2.1 金币范围（重复）

**文件 A**: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/TreasureNodeHandler.cs:36`

```csharp
int goldAmount = Random.Range(2, 6);  // 2-5 金币
```

**文件 B**: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/NodeInteractionManager.cs:192`

```csharp
int goldAmount = Random.Range(2, 6);  // 回退逻辑，与上面重复
```

### 2.2 商品数量范围

**文件**: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/StoreNodeHandler.cs:75`

```csharp
int goodCount = Random.Range(2, 4); // 2-3 件
```

### 2.3 金币上限

**文件**: `Assets/Tactics/Scripts/RoguelikeMap/Economy/RunGoldManager.cs:30`

```csharp
public const int MaxGold = 50;
```

### 2.4 战斗公式参数

**文件**: `Assets/Tactics/Scripts/Common/Units/CombatComponent.cs:14-17`

```csharp
private const int NeutralAttributeValue = 5;
private const float BaseCritChance = 0.10f;
private const float CritChancePerLuckPoint = 0.02f;
private const float CritDamageMultiplier = 2f;
```

### 2.5 属性判定公式参数

**文件**: `Assets/Tactics/Scripts/RoguelikeMap/Events/AttributeCheckSystem.cs:21,41-42`

```csharp
// 公式系数
int rate = baseSuccessRate + (attributeValue - 10) * 5;

// 成功率上下限
if (rate < 5) return 5;
if (rate > 95) return 95;
```

### 2.6 商品数量硬限制

**文件**: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/ShopManager.cs:36-37`

```csharp
if (count < 2) count = 2;
if (count > 3) count = 3;
```

---

## 三、字符串列表硬编码（配置型数据）

### 3.1 商品名称

**文件**: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/ShopManager.cs:25`

```csharp
private static readonly string[] GoodNames = { "治疗药水", "铁剑", "皮甲", "魔法卷轴", "力量戒指" };
```

### 3.2 商品图标

**文件**: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/ShopManager.cs:26`

```csharp
private static readonly string[] GoodIcons = { "💊", "⚔️", "🛡️", "📜", "💍" };
```

### 3.3 商品价格

**文件**: `Assets/Tactics/Scripts/RoguelikeMap/Interaction/ShopManager.cs:27`

```csharp
private static readonly int[] GoodPrices = { 5, 12, 8, 10, 15 };
```

> **注**: ShopManager.cs 已有 `TODO: 对接物品系统，替换占位商品` 注释，属于已知的临时方案。

---

## 四、汇总统计

| 类别 | 涉及文件数 | 问题点数 |
|------|-----------|---------|
| 资产路径硬编码 | 6 | 10 |
| 数值配置硬编码 | 6 | 8 |
| 字符串列表硬编码 | 1 | 3 |
| **合计** | **10** | **21** |

### 高优先级文件（问题最集中）

1. **`ShopManager.cs`** — 3处硬编码（商品名/图标/价格），已有 TODO 标记
2. **`TreasureNodeHandler.cs`** — 2处硬编码（Buff路径 + 金币范围）
3. **`RoguelikeMapUIController.cs`** — 4处硬编码（资产路径）
4. **`CombatComponent.cs`** — 4处硬编码（战斗公式参数）

---

## 五、建议改进方向

1. **资产路径** → 统一使用 ScriptableObject 配置或 `[SerializeField]` 暴露
2. **数值参数** → 提取到 ScriptableObject 配置表
3. **商品数据** → 对接物品系统（按 ShopManager TODO 指示）
4. **金币范围** → 合并重复定义，统一到 RunGoldManager 或配置表
