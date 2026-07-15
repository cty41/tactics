# Roguelike地图玩法设计文档 v3 — FTL风格自由探索 + BG3事件系统

> **版本**: v3.0  
> **日期**: 2026-05-21  
> **状态**: 设计完成  
> **关联计划**: [战斗系统演进计划](../plans/战斗系统演进计划.md)
> **关联算法**: [FTL风格地图生成算法](ftl-style-map-generation-algorithm.md)

---

## TL;DR

**核心玩法**: FTL风格自由探索（网格布局 + 有限视野迷雾） + 博德之门3属性判定事件系统。

**地图结构**: 单张FTL风格自由星图（5×4网格，20个节点），非线性可往返探索。

**关键设计**:
- **FTL式自由探索**: 网格布局、距离约束连接、有限视野迷雾、可重复往返
- **BG3式属性事件**: 每个事件选项基于属性（力量/敏捷/智力/魅力）计算成功率，可预览
- **事件编辑器**: 独立工具，详见 [roguelike-event-editor-design.md](roguelike-event-editor-design.md)
- **低金币经济**: 单局总金币控制在50以内，每个铜板都有分量
- **Boss胜利结算**: 击败Boss后进入胜利结算，展示本次Run成果

---

## 设计决策汇总

| 决策项 | v3.0 设计 |
|--------|-----------|
| **地图结构** | 单张FTL风格自由星图（5×4网格，20节点） |
| **探索模式** | 自由探索，可重复往返，有限视野迷雾 |
| **事件系统** | BG3式属性判定（力量/敏捷/智力/体质/魅力 + 成功率%） |
| **事件编辑器** | 独立工具 — 详见[事件编辑器设计](roguelike-event-editor-design.md) |
| **Boss** | 单个最终Boss（地图最右侧），击败后胜利结算 |
| **金币总量** | ≤50金币/局 |
| **单局时长** | 15-25分钟（原型目标） |
| **失败惩罚** | 完全重来 |

---

## 地图架构

### FTL风格自由星图

采用FTL: Faster Than Light的信标地图系统，实现非线性自由探索。

**核心特征**:
- 网格布局（5列×4行，共20个节点）
- 距离约束的网状连接（前向+侧向+后向）
- 有限视野迷雾（只能看到当前节点的连接邻居 + 已访问节点）
- 可重复往返已访问节点

```
                    [Boss]
                      ↑
    ○──○──○──○──○    最右侧列
    │╲ │ ╱│ ╲│ ╱│
    ○──○──○──○──○    ← 可往返
    │╱ │ ╲│ ╱│ ╲│    网状连接
    ○──◎──○──○──○    ← ◎ 当前位置
    │╲ │ ╱│ ╲│ ╱│
   [起点]──○──○──○    最左侧列
```

**生成参数**:

| 参数 | 值 | 说明 |
|------|-----|------|
| gridColumns | 5 | 网格列数 |
| gridRows | 4 | 网格行数 |
| nodeCount | 20 | 节点总数 |
| maxReachableDistance | 3.0 | 最大可达距离（欧几里得） |
| visionRange | 5.0 | 视野范围距离 |
| minDistanceBetweenNodes | 1.0 | 节点间最小距离 |
| storeMinDistance | 2.0 | 商店间最小距离 |

**详细算法**: [FTL风格地图生成算法](ftl-style-map-generation-algorithm.md)

### 节点分布（统一）

| 节点类型 | 概率 | 数量（20节点） | 说明 |
|----------|------|---------------|------|
| MinorEnemy | 30% | ~6 | 普通战斗 |
| EliteEnemy | 15% | ~3 | 精英战斗 |
| Mystery | 25% | ~5 | 事件节点（BG3属性判定） |
| Store | 10% | ~2 | 商店 |
| Treasure | 10% | ~2 | 宝藏 |
| RestSite | 10% | ~2 | 休息站 |
| Boss | 固定1个 | 1 | 最终Boss（最右侧列） |

### 视野系统

- **已访问节点**: 永久可见，显示完整信息
- **当前节点的连接邻居**: 可见（Reachable状态），可点击前往
- **视野范围内未访问节点**: 可见但不可交互（Revealed状态）
- **视野范围外未访问节点**: 不可见（Unrevealed状态）

---

## 地图生成算法

地图生成采用 FTL 风格自由星图算法（v3.0），已提取为独立设计文档。

详见：[FTL 风格地图生成算法](ftl-style-map-generation-algorithm.md)

---

## 节点类型详细设计

### 1. MinorEnemy (普通战斗)

**胜利奖励**:
- 金币: **1-3** （低金币经济）
- 经验值
- 小概率掉落消耗品

### 2. EliteEnemy (精英战斗)

**胜利奖励**:
- 金币: **3-6**
- 经验值（大量）
- 大概率掉落消耗品 + 小概率装备

### 3. Mystery (事件节点) ⭐ 核心玩法 — BG3式属性判定

**触发**: 点击节点 → 弹出事件UI

**事件结构**:
```
[事件标题]
[事件描述文本 — 沉浸式叙事]

选项A: [动作描述]
  → 属性: 力量  |  成功率: 65%
  → 成功: [奖励描述]
  → 失败: [后果描述]

选项B: [动作描述]
  → 属性: 敏捷  |  成功率: 45%
  → 成功: [奖励描述]
  → 失败: [后果描述]

选项C: [默认选项 — 无需属性判定]
  → 结果: [安全但奖励较少]
```

#### 属性判定系统 — 博德之门3风格

**核心公式**:
```
成功率 = 基础成功率 + (属性值 - 10) × 5%
```

| 属性 | 值 | 力量类选项成功率 | 说明 |
|------|-----|----------------|------|
| 极低 | 6 | 20% | 几乎没有成功可能 |
| 低 | 8 | 30% | 很困难 |
| 普通 | 10 | 40% | 有一半不到的把握 |
| 较高 | 14 | 60% | 大概率成功 |
| 高 | 18 | 80% | 几乎稳了 |
| 巅峰 | 20 | 90% | 信手拈来 |

**属性与动作类型映射**:

| 属性 | 适用动作 | 示例 |
|------|---------|------|
| **力量** | 破坏障碍、推开石门、强行制服 | "猛力砸开腐朽的木门" |
| **敏捷** | 解除陷阱、攀爬、潜行、偷窃 | "灵巧地解除魔法陷阱" |
| **体质** | 抵抗毒素、忍耐恶劣环境、持续施法 | "屏住呼吸穿过毒气" |
| **智力** | 破解符文、解读文献、识别魔法 | "破译墙上的古代符文" |
| **魅力** | 说服、威吓、交易、迷惑 | "用恶魔的语言命令它退下" |

#### 事件类型

##### 类型A: 单属性抉择
```
事件: "一道厚重的石门挡住了去路"
→ 选项A: 猛力撞开 [力量 成功率60%]
   — 成功: 发现密室，获得宝藏
   — 失败: 撞伤肩膀，损失5%HP
→ 选项B: 寻找机关 [智力 成功率45%]
   — 成功: 优雅打开，安全通过
   — 失败: 触发毒箭，损失10%HP
→ 选项C: 绕路而行 [自动成功]
   — 无风险但也无额外奖励
```

##### 类型B: 多属性分工
```
事件: "一个被诅咒的宝箱"
→ 选项A: 暴力撬开 [力量 成功率50%]
   — 成功: 获得金币  |  失败: 触发爆炸
→ 选项B: 解除陷阱 [敏捷 成功率65%]  
   — 成功: 安全开箱并获得额外奖励
   — 失败: 触发毒气
→ 选项C: 分析诅咒 [智力 成功率40%]
   — 成功: 解除诅咒并获得魔法装备
   — 失败: 被诅咒（负面Buff）
→ 选项D: 不管它 [自动成功]
   — 无奖励
```

##### 类型C: 团队协作
```
事件: "一座断桥阻断了去路"
→ 选项A: 搭桥通过 [力量 成功率55%]
   — 成功: 全员通过 | 失败: 一名队员受伤
→ 选项B: 跳跃过去 [敏捷 成功率50%]
   — 成功: 快速通过 | 失败: 跌入深渊损失HP
→ 选项C: 用法术修复 [智力 70% — 需队伍有Mage]
   — 成功: 完美通过获得经验
   — 失败: 法术反噬
```

**事件JSON数据结构**:
```json
{
  "eventId": "cursed_chest_001",
  "title": "被诅咒的宝箱",
  "description": "房间中央摆着一个精致的宝箱，上面刻着诡异的符文...",
  "region": "DarkForest",
  "options": [
    {
      "optionId": "smash",
      "text": "暴力撬开",
      "attribute": "Strength",
      "successRate": 50,
      "success": {"type": "gold", "amount": 5},
      "failure": {"type": "damage", "amount": 10, "target": "self"}
    },
    {
      "optionId": "disarm",
      "text": "解除陷阱",
      "attribute": "Dexterity",
      "successRate": 65,
      "success": {"type": "item", "itemId": "health_potion"},
      "failure": {"type": "damage_all", "amount": 5}
    },
    {
      "optionId": "analyze",
      "text": "分析诅咒",
      "attribute": "Intelligence",
      "successRate": 40,
      "success": {"type": "equip", "equipId": "ring_of_protection"},
      "failure": {"type": "buff", "buffId": "cursed"}
    },
    {
      "optionId": "leave",
      "text": "不管它",
      "attribute": null,
      "successRate": 100,
      "success": {"type": "nothing"},
      "failure": null
    }
  ]
}
```

### 4. Store (商店)

**触发**: 点击节点 → 弹出商店UI

**低金币经济下的商店设计**:
- 商品数量: 2-3个（精简）
- 价格范围: **3-15金币**
- 消耗品: 3-5金币
- 普通装备: 8-12金币
- 稀有装备: 15金币（贵但买得起1-2件）

**设计理念**: 因为单局只有40-50金币，每个购买决策都很重要。买药水还是攒钱买装备？这是核心策略点。

### 5. Treasure (宝藏)

**触发**: 点击节点 → 直接获得奖励

**奖励池**:
- 金币: **2-5**
- 消耗品: 概率获得
- 装备: 小概率

**节点配置接口**:
```csharp
// RoguelikeMapNode.treasureConfig
public class TreasureNodeConfig
{
    public int goldMin = 2;
    public int goldMax = 5;
    public List<BuffConfigEntry> buffEntries;      // Buff奖励池
    public List<EquipmentEntry> equipmentEntries;   // 装备奖励池
}
```

### 6. RestSite (休息站)

**触发**: 点击节点 → 弹出选择UI

**选项**:
- **休息**: 恢复队伍30%最大HP

**节点配置接口**: 无特殊配置，使用默认 RestSite 行为

### 7. Store (商店)

**触发**: 点击节点 → 弹出商店UI

**低金币经济下的商店设计**:
- 商品数量: 2-3个（精简）
- 价格范围: **3-15金币**
- 消耗品: 3-5金币
- 普通装备: 8-12金币
- 稀有装备: 15金币（贵但买得起1-2件）

**设计理念**: 因为单局只有40-50金币，每个购买决策都很重要。买药水还是攒钱买装备？这是核心策略点。

**节点配置接口**:
```csharp
// RoguelikeMapNode.storeConfig
public class StoreNodeConfig
{
    public List<StoreGoodEntry> goods;  // 商品列表
}

public class StoreGoodEntry
{
    public string equipmentId;  // 装备ID
    public int price = 5;       // 价格
}
```

**位置**: 地图最右侧列，固定1个Boss节点

**触发**: 点击节点 → 进入Boss战斗场景

**胜利后**: 进入胜利结算界面，展示本次Run成果（金币/装备/经验汇总）

---

## 事件编辑器 — 独立工具

事件编辑器已分离为**独立的开发计划**，详见：

- **设计文档**: [roguelike-event-editor-design.md](roguelike-event-editor-design.md)
- **开发计划**: [roguelike-event-editor-开发计划.md](../plans/roguelike-event-editor-开发计划.md)

### 与本计划的关系

事件编辑器是**独立工具**，与主Roguelike地图玩法开发可**完全并行**。

- 事件系统开发（Task 6-8）前期可使用手工编写JSON先行开发
- 事件编辑器完成后，事件内容切换为编辑器导出
- 对接接口为 `RoguelikeMapConfig.eventFiles`（TextAsset 列表），数据结构已固定

### 依赖关系

```
主计划 (当前文档)          事件编辑器 (独立计划)
  Task 1-2: 地图/节点状态    Task 1-2: 编辑器窗口/事件列表
  Task 5: 非战斗节点           Task 3-4: 节点图/属性面板
  Task 6-8: 事件系统 ← JSON→  Task 7-8: JSON导入导出
  Task 9-13: 商店/平衡/Boss    Task 9-13: 撤销/布局/模板/验证
```

两个计划可**同步并行开发**，无阻塞依赖。

---

## 资源系统

### 低金币经济模型

**设计原则**: 每个金币都有价值，每场战斗的奖励都是"小确幸"。

**单局总金币: ≤50**

**金币获取**:

| 来源 | 金额 | 单局预估次数 | 小计 |
|------|------|-------------|------|
| 普通战斗 | 1-3 | ~6次 | 6-18 |
| 精英战斗 | 3-6 | ~3次 | 9-18 |
| 宝藏节点 | 2-5 | ~2次 | 4-10 |
| 事件奖励 | 0-5 | ~5次 | 0-25 |
| Boss奖励 | 5-10 | 1次 | 5-10 |
| **总计** | | | **~40-50** |

**金币消耗**:

| 项目 | 价格 | 说明 |
|------|------|------|
| 药水 | 3-5 | 消耗品，回血 |
| 普通装备 | 8-12 | 白/蓝装 |
| 稀有装备 | 15 | 金装 |
| 事件支付 | 2-5 | 某些事件选项需要金币 |

**策略影响**:
- 买2瓶药水(6-10金) vs 攒钱买稀有装备(15金)
- 每个金币都是资源管理决策

---

## 与战斗系统的衔接

（保留原设计中的BattleContext和BattleResult数据结构，**但移除区域→地形影响逻辑**）

**战斗前传递**: BattleContext (nodeType, party, enemyLevel, enemyGroupId)
**战斗后返回**: BattleResult (isVictory, goldEarned, loot, party)

---

## 数据文件结构 — 按新规范

```
.agents/docs/
├── design/
│   └── roguelike-map-gameplay-design.md    ← 本文档
├── plans/
│   └── roguelike-map-gameplay-开发计划.md   ← 开发计划
├── usage/
│   └── CheatCodeGuide.md
└── screenshots/

Assets/Tactics/
├── Scripts/
│   └── RoguelikeMap/
│       ├── Events/          ← 事件系统
│       ├── Economy/         ← 经济系统
│       ├── Interaction/     ← 节点交互
│       └── UI/              ← 事件/商店/休息UI
├── Editor/
│   └── RoguelikeEventEditor/ ← 事件编辑器（Editor Only）
├── GameData/
│   └── Events/              ← JSON事件配置文件（统一目录）
└── Arts/
    └── ScriptableObjects/
        └── MapConfigs/      ← RoguelikeMapConfig 资产
```

---

## 与v1.0的差异总结

| 项目 | v1.0 | v3.0（当前） |
|------|------|-------------|
| 地图结构 | 层级推进 | FTL风格自由星图（网格布局） |
| 探索模式 | 线性单向 | 自由往返，有限视野迷雾 |
| 事件系统 | 条件判定（有/无） | BG3式属性成功率（百分比） |
| 事件编辑器 | 无 | UI Toolkit图编辑器 |
| Boss | 多Boss | 单个最终Boss |
| 金币总量 | 500-800 | ≤50 |
| 连接规则 | 层间固定 | 距离约束 + 前向/后向混合 |

---

## 附录: BG3属性判定事件示例（完整）

### 示例1: 被诅咒的宝箱

```json
{
  "eventId": "cursed_chest_001",
  "title": "被诅咒的宝箱",
  "description": "房间中央摆着一个精致的宝箱，上面刻着诡异的符文，散发着微弱的紫色光芒。空气中有硫磺的味道。",
  "options": [
    {
      "optionId": "smash",
      "text": "暴力撬开",
      "attribute": "Strength",
      "successRate": 50,
      "success": {"type": "gold", "amount": 5, "text": "你猛力一砸，锁扣碎裂！里面有几枚金币。"},
      "failure": {"type": "damage", "amount": 8, "text": "宝箱爆炸！碎片划伤了你的脸。"}
    },
    {
      "optionId": "disarm",
      "text": "灵巧解除",
      "attribute": "Dexterity",
      "successRate": 65,
      "success": {"type": "item", "itemId": "health_potion", "text": "你的手指如手术般精准，陷阱被安全解除。"},
      "failure": {"type": "damage_all", "amount": 5, "text": "触发了毒气陷阱！全员吸入毒雾。"}
    },
    {
      "optionId": "analyze",
      "text": "破译诅咒",
      "attribute": "Intelligence",
      "successRate": 40,
      "success": {"type": "equip", "equipId": "ring_of_protection", "text": "你念出反咒，宝箱应声而开！里面是一枚古老的戒指。"},
      "failure": {"type": "buff", "buffId": "weakened", "text": "反咒失败！一股虚弱感侵袭了你的身体。"}
    },
    {
      "optionId": "leave",
      "text": "不管它",
      "attribute": null,
      "successRate": 100,
      "success": {"type": "nothing", "text": "你决定还是不要冒险。"}
    }
  ]
}
```

### 示例2: 断桥

```json
{
  "eventId": "broken_bridge_001",
  "title": "断裂的石桥",
  "description": "一座古老的石桥从中断裂，下方是深不见底的深渊。对面隐约能看到一个发光的宝箱。",
  "options": [
    {
      "optionId": "jump",
      "text": "助跑跳跃",
      "attribute": "Dexterity",
      "successRate": 55,
      "success": {"type": "loot", "text": "你稳稳落在对面！宝箱里有金币和一瓶药水。"},
      "failure": {"type": "damage", "amount": 12, "text": "你差一点没抓住边缘，狠狠摔在崖壁上。"}
    },
    {
      "optionId": "rebuild",
      "text": "搬运石块搭桥",
      "attribute": "Strength",
      "successRate": 60,
      "success": {"type": "heal", "amount": 0, "text": "你搬来大石块搭出一条通路，全员安全通过。"},
      "failure": {"type": "nothing", "text": "石块不够，你只能绕路。白白消耗了体力。"}
    },
    {
      "optionId": "magic",
      "text": "用法术修复",
      "attribute": "Intelligence",
      "successRate": 45,
      "condition": {"class": "Mage"},
      "success": {"type": "exp", "amount": 50, "text": "你用法术重塑了石桥！这为你带来了宝贵的经验。"},
      "failure": {"type": "damage", "amount": 10, "text": "法术失控！碎石飞溅砸中了你。"}
    },
    {
      "optionId": "detour",
      "text": "绕路",
      "attribute": null,
      "successRate": 100,
      "success": {"type": "nothing", "text": "你选择了安全的绕路，虽然什么也没得到。"}
    }
  ]
}
```

### 示例3: 恶魔契约（Boss领地）

```json
{
  "eventId": "demon_pact_001",
  "title": "恶魔的契约",
  "description": "一个火焰环绕的恶魔出现在你面前，它伸出利爪，掌心浮现一份发光的契约。",
  "options": [
    {
      "optionId": "accept",
      "text": "接受契约",
      "attribute": "Charisma",
      "successRate": 35,
      "success": {"type": "buff", "buffId": "demon_power", "text": "你与恶魔达成交易，获得了强大的力量！（攻击力+50%，但每场战斗损失5%HP）"},
      "failure": {"type": "battle", "enemyGroupId": "hell_knights", "text": "恶魔觉得你不够格，愤怒地召唤了地狱骑士！"}
    },
    {
      "optionId": "refuse",
      "text": "拒绝并战斗",
      "attribute": "Strength",
      "successRate": 40,
      "success": {"type": "equip", "equipId": "demon_blade", "text": "你击败了恶魔！它化为灰烬，留下了一把燃烧的剑。"},
      "failure": {"type": "damage_all", "amount": 15, "text": "恶魔在消失前释放了最后的火焰冲击。"}
    },
    {
      "optionId": "bargain",
      "text": "谈判交涉",
      "attribute": "Intelligence",
      "successRate": 50,
      "condition": {"class": "Mage"},
      "success": {"type": "gold", "amount": 10, "text": "你用学识折服了恶魔，它给了你金币作为投名状。"},
      "failure": {"type": "buff", "buffId": "cursed", "text": "恶魔戏弄了你，给你下了一个恶咒。"}
    },
    {
      "optionId": "ignore",
      "text": "转身离开",
      "attribute": null,
      "successRate": 100,
      "success": {"type": "nothing", "text": "恶魔的嘲笑声在你身后回荡..."}
    }
  ]
}
```
# 2026-07-14 Pure Run v1 实现基线

首个 demo 不再使用本文件早期章节描述的 5×4 可往返自由图；Pure Run v1 采用 7 层、只沿 outgoing 前进的结构，已访问节点不会重新变为可选。早期自由图仍可作为通用 Roguelike 模式参考，但不是首个 demo 的运行规则。

- 第 1–3 层：必经普通战斗。
- 第 4 层：补给、可选普通混编战或随机事件三选一。
- 第 5 层：必经战斗。
- 第 6 层：商店、可选精英混编战或随机事件三选一。
- 第 7 层：单一 Special 终战。

因此一局稳定产生 5、6 或 7 场战斗。地图使用固定 run seed 生成，运行时保存当前层、胜场和已完成战斗节点，便于重放和自动化断言。
