# Pure Run 当前战斗数值总览

更新时间：2026-08-05。本文是下一轮平衡讨论的只读基线。数据来自当前 `D:\codes\tactics` 工作树中的真实类型、Ability/SkillGraph、正式怪物 Prefab、Brain/Profile/Pattern，并与执行器、伤害链代码及固定棋盘 Editor 测试交叉核对。强制刷新与编译后已核对三人状态、18 个正式技能的 42 个已实现等级资产、6 类怪物和全部配方；未将旧 revision 的一次 Editor 导出描述成当前工作树证据。新增 `VisualAction` 属于表现字段，不计入平衡数值。

“原始伤害”不等于最终扣血；`range = 999` 是当前尸体选择资产的实际无界哨兵，不应解释成设计上的无限射程承诺。

## 初始三人队：已修正旧冲突

`PlayerAdventureStateStore.CreatePureRunState(12345)` 的只读运行时结果证明：**不是六维均 5**。每个职业的主属性为 6，其余五项为 5；旧文档“六维均 5”已过期。起始分支由 seed 决定，下面列出的起始技能只用于证明本次样本，不是固定分支。

| 角色 | 力 / 敏 / 体 / 智 / 魅 / 运 | HP | MP（当前 / 最大） | 移动 / Speed | 样本起始技能 |
|---|---|---:|---:|---:|---|
| 法师 | 5 / 5 / 5 / **6** / 5 / 5 | 20 | 5 / 15 | 5 / 5 | 火球术 Lv1 |
| 死灵法师 | 5 / 5 / 5 / 5 / **6** / 5 | 20 | 6 / 18 | 5 / 5 | 骨矛 Lv1 |
| 亚马逊 | 5 / **6** / 5 / 5 / 5 / 5 | 20 | 5 / 15 | 5 / 5 | 突刺 Lv1 |

通用派生与资源规则：

| 项目 | 当前规则 |
|---|---|
| 最大 HP | `max(1, Constitution × 4)` |
| 最大 MP | `max(0, Charisma × 3)` |
| 最大移动 | `max(1, Speed)`；先攻为 `Speed × 2` |
| 新角色初始 MP | `Charisma`，不是满 MP；首战从持久化 `CurrentMp` 覆盖单位初始化值 |
| 回合回蓝 | 单位自身回合**结束**恢复 `max(0, Intelligence)`，不超过最大 MP |
| 战后恢复 | 存活单位 HP `+ Constitution × 2`，MP `+ Charisma`，均不超过上限 |
| 近战普攻公式 | `max(AttackFactor + Strength - 5, 1)` |
| 远程普攻公式 | `max(AttackFactor + floor((Agility - 5)/2), 1)` |
| 基础暴击 | `clamp(10% + (Luck - 5) × 2%, 0, 100%)`；暴击伤害 ×2 |
| 属性闪避 | Agility 高于 5 时每点 +2%；再与单位 Dodge、命中惩罚组合 |

## 18 个正式技能：每级有效值

### 法师

| 技能 | 等级 | MP / 射程 / 每回合次数 | 直接伤害、DoT、控制、召唤 |
|---|---:|---|---|
| 火球术 | Lv1 | 7 / 4 / 不限 | 主目标 Fire Magic 2；命中后点燃 2 层 |
|  | Lv2 | 7 / 4 / 不限 | 主目标 4；正交相邻敌人各 2；命中目标点燃 3 层 |
|  | Lv3 | 7 / 4 / 不限 | 先以旧点燃层数造成绕防御、不可暴击的额外伤害并清旧层，再按 Lv2 结算 |
| 寒冰箭 | Lv1 | 6 / 4 / 不限 | Ice Magic 8；命中后 Slow 1 个目标行动 |
|  | Lv2 | 4 / 4 / 不限 | 伤害 8；Slow 2 个目标行动 |
|  | Lv3 | 4 / 4 / 不限 | 主目标 8；其 3 格内最近另一敌人反弹 4；反弹 Slow 1 |
| 霹雳闪电 | Lv1 | 6 / 4 / 不限 | Lightning Magic 9；瞬时指定目标 |
|  | Lv2 | 6 / 4 / 不限 | 9；命中后 25% Stun 1 次行动 |
|  | Lv3 | 6 / 4 / 不限 | 11；命中后 50% Stun 1 次行动 |
| 召唤火魔 | Lv1 | 7 / 3 / 不限 | 替换旧火魔，生成 1；火魔 HP 12、Speed 4、移动 2，攻击射程 1–3、Fire Magic 4 + 点燃 1；第 5 次自身行动后退场 |
|  | Lv2 | 7 / 3 / 不限 | 替换旧火魔，半径 1–3 内最多生成 2；允许只有 1 个合法落点时部分成功 |
| 冰甲 | Lv1 | 5 / 自身 / 不限 | 2 个行动周期，所受伤害 -25%；重复施放刷新 |
|  | Lv2 | 5 / 自身 / 不限 | 同 Lv1；遭相邻近战后对攻击者施加 Slow 2 |
| 瞬移术 | Lv1 | 8 / 4 / 不限 | 无伤害；合法空格，要求可见，不耗普通移动 |
|  | Lv2 | 5 / 4 / 不限 | 同 Lv1，但不要求可见 |

### 死灵法师

| 技能 | 等级 | MP / 射程 / 每回合次数 | 直接伤害、DoT、控制、召唤 |
|---|---:|---|---|
| 召唤骷髅 | Lv1 | 3 / 尸体（资产 999） / 不限 | 消耗 1 尸体召唤 1；上限 1；HP 8，近战攻击 2 |
|  | Lv2 | 3 / 尸体（999） / 不限 | 上限 2；HP 10，攻击 3 |
|  | Lv3 | 3 / 尸体（999） / 不限 | 上限 3；HP 12，攻击 4 |
| 召唤骷髅法师 | Lv1 | 7 / 尸体（999） / 不限 | 上限 1；HP 6、Speed 4、移动 2；使用零 MP 火球 Lv1 |
|  | Lv2 | 7 / 尸体（999） / 不限 | 上限 2；HP 8；使用零 MP 火球 Lv2 |
| 伤害加深诅咒 | Lv1 | 3 / 4 / 不限 | 单体承受所有伤害 ×1.3，持续 5 个目标行动 |
|  | Lv2 | 3 / 4 / 不限 | 指定格十字 5 格敌人，同效果 |
|  | Lv3 | 3 / 4 / 不限 | 指定格 3×3 九格敌人，同效果 |
| 恐惧诅咒 | Lv1 | 7 / 4 / 不限 | 单体 Fear 1 次行动 |
|  | Lv2 | 7 / 4 / 不限 | 指定格十字 5 格敌人 Fear 1 次行动 |
| 骨矛 | Lv1 | 6 / 4 / 不限 | 直线首敌 None-element Magic 7，不可暴击 |
|  | Lv2 | 4 / 4 / 不限 | 同 Lv1 |
|  | Lv3 | 4 / 4 / 不限 | 仅横/纵直线端点，路径所有敌人各 7；墙阻挡 |
| 骨盾 | Lv1 | 8 / 自身 / 不限 | 护盾 `Charisma × 2`，吸收 Physical；重施重置 |
|  | Lv2 | 8 / 自身 / 不限 | 同数值，吸收所有战斗伤害，包括 Magic/已附着 DoT |

骷髅与骷髅法师按类别独立计上限，满员时替换最早者；召唤物不产可用尸体，标准 HP 治疗目标合法但实际恢复为 0。恐惧在目标下次行动开始强制使用普通移动到远离施法者的稳定可达格，之后仍可攻击或施法。

### 亚马逊

| 技能 | 等级 | MP / 射程 / 每回合次数 | 直接伤害、DoT、控制、召唤 |
|---|---:|---|---|
| 突刺 | Lv1 | 3 / 2 / 不限 | 朝向线每敌 Physical 6，可暴击 |
|  | Lv2 | 3 / 3 / 不限 | 同伤害，线长 3 |
|  | Lv3 | 3 / 3 / 不限 | 每敌 `6 + 本回合主动移动格数`；成功后清移动计数 |
| 连续刺击 | Lv1 | 8 / 3 / 不限 | 有序 3 段，每段 Physical 4，独立暴击 |
|  | Lv2 | 8 / 3 / 不限 | 有序 4 段，每段 4 |
| 毒矛 | Lv1 | 6 / 5 / 不限 | 主目标 Physical 8；命中后 Poison 每目标行动 2，增加 3 个行动周期；投矛落地 |
|  | Lv2 | 6 / 5 / 不限 | 主目标 10；十字 5 格敌人延长/获得 Poison，其他敌人无直接伤害 |
|  | Lv3 | 6 / 5 / 不限 | 主目标 10；3×3 九格敌人延长/获得 Poison |
| 召唤长矛 | Lv1 | 4 / 5 / 不限 | 只选择自己的落矛格并召回；无伤害 |
|  | Lv2 | 4 / 5 / 不限 | 召回后自身正交相邻敌人各 Lightning Magic 6，独立暴击 |
| 战斗技巧（被动） | Lv1 | — / — / — | 闪避 +30 个百分点 |
|  | Lv2 | — / — / — | 另：主动普通近战命中后 30% 免费再攻击一次，不递归 |
|  | Lv3 | — / — / — | 另：所有可暴击直接伤害暴击率 +20 个百分点 |
| 分身 | Lv1 | 6 / 2 / 不限 | 免费后撤换位；原格生成诱饵；HP=floor(施法者 MaxHP×50%)，至少 1；持续 3 个亚马逊回合周期 |
|  | Lv2 | 6 / 2 / 不限 | 同 Lv1；成功后移除自身全部 Harmful Buff |

隐藏额外技能“拾取长矛”不是 18 个正式技能之一：0 MP，八方向相邻时免费回收，每回合次数资产为不限。上述“不限”均对应 `MaxUsesPerTurn = 0`；实际仍受 MP、目标合法性、普通基础行动、长矛状态及选择流程约束。

## 正式怪物：Prefab、资源与 AI

Prefab 只读快照显示六维均 5、最大 MP 派生为 15；`Unit.Initialize` 后生命按 Constitution 派生为 20、MP 从 Charisma=5 开始，再应用遭遇最低起始 MP 与生命倍率。表中移动取 Speed 派生上限；Prefab 序列化的旧 `_health/_mana/_movementPoints` 不是生成后的权威值。

| 怪物 | 派生 HP / MP / 移动 / Speed | AttackRange / Factor / Defence | 能力 | 起始资源门槛 | Brain / Profile / Pattern |
|---|---|---|---|---|---|
| Charger | 20 / 5 / 4 / 8 | 1 / 1 / 0 | Melee Attack；Charge Strike | 0 | ChargerBrain / Aggressive Charger；偏好 1 格；无 Pattern |
| Ranged | 20 / **15** / 4 / 12 | 3 / 2 / 0 | Ranged Attack；Heavy Shot | 最低 MP 15 | RangedBrain / Ranged Skirmisher；偏好 2–4 格；无 Pattern |
| AOE | 20 / 5 / 3 / 6 | 5 / 2 / 0 | Melee Attack；Area Blast | 0 | AOEBrain / Area Controller；偏好 2–3 格；无 Pattern |
| Support | 20 / 5 / 4 / 8 | 1 / 1 / 0 | Melee Attack；伤害加深 Lv1 | 0 | SupportBrain / Curse Support；偏好 2–3 格；无 Pattern |
| EliteCharger | 20 / 5 / 4 / 8 | 1 / 1 / 0 | Melee Attack；Charge Strike | 0 | EliteChargerBrain / Elite Charge Pattern；`Charge Strike Lv1 → Melee Attack` |
| ElitePoisonCaster | 20 / 5 / 3 / 6 | 5 / 2 / 0 | Melee Attack；**Area Blast** | 0 | ElitePoisonCasterBrain / Elite Caster Pattern；`Area Blast Lv1 → Melee Attack` |

名称“ElitePoisonCaster”目前没有从只读资产确认到 Poison 能力；实际配置是 Area Blast，因此本文不把“毒素施法”写成已实现效果。

怪物能力资产：

| 能力 | MP / 射程 / 每回合次数 | 当前效果 |
|---|---|---|
| Melee Attack | 0 / 1 / 基础动作规则 | Physical 2，可暴击 |
| Ranged Attack | 0 / 2–4 / 基础动作规则 | Physical 2，可暴击，要求 LOS |
| Charge Strike Lv1 | 0 / 3 / 1 | 冲锋；Physical 8，可暴击；击退 1（Dash 节点另有 CollisionDamage=1） |
| Heavy Shot | 8 / 4 / 1 | Ranged Physical 6，可暴击；命中惩罚 0.5，要求 LOS |
| Area Blast Lv1 | 0 / 3，半径 2 / 1 | 范围内 All 阵营 Magical 6，可暴击；AI 候选会拒绝伤友中心 |
| 伤害加深 Lv1 | 3 / 4 / 资产不限 | 单体承伤 ×1.3，持续 5 个目标行动 |

所有六个正式 Brain 当前都引用 `BasicMeleeGraph`，但各自绑定独立 Profile；两个 Elite 另有固定 Pattern。Profile 权重的完整数值没有在本文展开，避免复制易过期资产细节。

## 遭遇配方与倍率

| 配方 | 布局 | 组成 | HP / 输出倍率 |
|---|---|---|---|
| N1 | open | 2 Charger + 1 Ranged | 1.0 / 1.0 |
| N2 | open | 2 Ranged + 1 Support | 1.0 / 1.0 |
| N3 | center_blocker | 1 AOE + 2 Charger + 1 Support | 1.0 / 1.0 |
| N4 | split_flank | 2 Ranged + 1 AOE + 1 Charger | 1.0 / 1.0 |
| N5 | center_blocker | 2 Support + 1 Charger + 1 AOE | 1.0 / 1.0 |
| N6 | split_flank | 2 Charger + 1 Ranged + 1 AOE | 1.0 / 1.0 |
| E1 | center_blocker | 复用 N3 | 1.3 / 1.15 |
| E2 | split_flank | 复用 N4 | 1.3 / 1.15 |
| Special | open | EliteCharger **或** ElitePoisonCaster | 1.8 / 1.25 |

生命倍率在派生最大 HP 后向上取整并满血出生：当前 E1/E2 基础 20 → 26，Special 20 → 36。输出倍率在统一伤害入口应用于有来源直接伤害及可追溯 DoT；不影响治疗、护盾与无来源环境效果。固定 10×10 棋盘中的 `center_blocker` 是一个 2×2 永久阻挡区，由 `(4,4)`、`(4,5)`、`(5,4)`、`(5,5)` 四格组成；战斗清理时恢复这四格。

## 实际直接伤害链

有来源直接伤害按当前代码顺序：

1. 目标有效性；
2. 属性闪避、单位 Dodge、命中惩罚与战斗技巧闪避合并判定；
3. Frozen：Fire 解除冻结，其他元素被阻断；
4. 遭遇输出倍率；
5. 暴击判定；
6. `OnBeforeAttacked` 钩子（可强制暴击/改伤害）；
7. 暴击 ×2；
8. 防御减法并保证该步骤最低 1（除非绕过防御）；
9. 伤害加深 ×1.3；
10. 所有减伤 Buff 乘法；
11. 护盾吸收；
12. 扣血、受击事件、日志与 `OnDamageTaken`。

持续伤害不重新走直接命中的闪避/暴击；已附着 DoT 以固定原始值、绕过防御结算。带施法来源的 DoT 仍消费来源单位的 Encounter 输出倍率。范围直接伤害仍可对每个目标独立闪避；要求成功命中的状态只在首次命中成功后附加。

## 尚未由本次只读导出确认

- 没有执行手动试玩，因而没有当前胜率、回合长度、技能选择率、减员位置或可读性评分；
- 没有进入 Play Mode 实例化一场完整正式遭遇；表中生成后 HP/MP/移动来自已验证初始化公式、Prefab 字段与 Encounter modifier 的组合，而非战斗现场截图；
- 没有确认 Special 具备额外行动、阶段或召唤；当前只确认单实体、倍率、Profile 与两步 Pattern；
- 没有把 Profile 每个评分权重或所有测试 seed 的起始技能逐项导出；这些不是本数值基线的必要重复表。

## 可复核来源

`PlayerAdventureStateStore.CreatePureRunState`、`CharacterDefinition`、`Unit`、`CombatComponent`、`EncounterConfig`/`EncounterCatalog`、`EncounterUnitRuntimeModifiers`、`PureRunAbilityCatalog`、三职业 SkillNodeExecutor、正式 `Test1` 地图与相关 BattleTest 配置、`Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/`、`Assets/Tactics/Arts/PureRun/Prefabs/Units/`、`Assets/Tactics/AI/Encounters/`。
