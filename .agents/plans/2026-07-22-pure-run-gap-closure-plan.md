# Pure Run 实际闭环缺口补全开发计划

## Summary

一句话 Goal：按《Pure Run 九切片逐项验收计划》严格串行自主完成 Slice 1–9 的实现、自动化验证、代码 Review、缺陷修复与独立 Git 提交，并在全部自动化及文档/OKF 收口后停在 Slice 10，仅向用户交付最终人工测试清单并等待手动验收。

本计划补全 2026-07-14 之后 Pure Run 中“数据已经存在，但真实玩家流程没有接通”的剩余缺口。目标不是扩展新的长期玩法，而是让一局 Pure Run 从地图、事件、成长、战斗到结局总结都以同一份持久化状态为真相源，并让三职业首批技能的 Lv1/Lv2/Lv3 设计真实进入下一场战斗。

完成后应满足：

- Pure Run 玩家单位只获得角色实际学会的职业技能及其当前等级；移动、职业普通攻击和额外工具技能按明确规则补入。
- 每次升级都从合法新技能与已学技能下一等级的混合池中确定性生成最多 3 个候选。
- 三职业 18 个正式技能、亚马逊额外技能 `amazon.pickup_spear`、朝向、召唤物、状态和元素分类按本文规则运行。
- RoguelikeMap 的技能区是只读信息区，不提供技能装配、卸载、排序或满槽替换。
- 第 4、6 层 Mystery 正式属于 Pure Run v1；同局不重复、可恢复、不可刷结果，事件致死可结束 Run。
- 遭遇倍率、布局阻挡、怪物能力和 AI 配置真正生效；战败不发奖励。
- RunEndSummary 统计本局累计事实，而不是临时读取结算时余额。
- 至少有一条接近真实玩家操作的自动化路线覆盖战斗、结算、升级、事件、商店、跨场景恢复和 Boss 结局。

交付编排为十个严格串行 Slice：Slice 1–9 由 Codex 自主执行“实现 → 自动化验证 → 代码 Review → 缺陷修复 → 独立 Commit → 下一 Slice”，不等待用户逐项确认；功能与交互验收全部由 Editor Test、PlayMode Test 和 Gameplay Test Framework 自动化完成。Slice 10 是唯一人工门，只由用户最终验证视觉呈现、可读性、操作手感和真实玩家完整流程，不替代任何逻辑断言。

## Status

- 状态：Active
- 范围：Pure Run v1 缺口收口
- 修改权限：运行时代码、共享战斗契约、Unity 资产、UI、测试适配器和必要结构调整均允许
- 实施约束：Unity 序列化资产必须通过 Unity MCP 或 `unity-agentic-tools` 修改；不得直接编辑 YAML
- 验收约束：Slice 1–9 必须先让测试框架能够表达并断言该 Slice 的玩家可观察行为，再以自动化结果进入 Review；缺少 adapter/action/assertion 时必须在当前 Slice 补齐，不能用“最终人工测试”代替
- 执行授权：Slice 1–9 的实现、测试、Review、修复、精确暂存和独立提交均由 Codex 自动完成，不设置中途用户确认门
- 人工边界：Codex 只在 Slice 10 交付最终测试清单并等待用户手动验收；常规测试失败和 Review 缺陷不构成暂停理由

## Current State

### 已接通且只需回归保护

- 消耗品定义、独立实例、角色单槽携带、统一背包交互、战斗目标选择、商店与掉落链已经形成闭环。
- 生命药剂、魔法药剂、净化药水均为独立 `1/1` 实例；角色死亡后装备和携带药水自动退回背包。
- 标准 HP 恢复对复活类召唤物结算为 0，但目标仍合法；该规则应继续覆盖后续新增的骷髅和骷髅法师资产。
- `LearnedSkill` 已保存稳定 `SkillId`、`SkillType` 和 `Level`；`SkillSystem.UpgradeSkill` 已能递增同一记录。
- `Unit.ApplyAbilityConfigs` 可在 `InitializeGame` 前覆盖 Prefab 预挂能力。
- SkillGraph、Buff、尸体、召唤、AI Pattern 和 Gameplay Test adapter 已具备部分基础能力。

### 已验证断链

- `CharacterStatsApplicator` 只复制属性；Pure Run 战斗没有根据 `LearnedSkills` 绑定能力。
- `Unit.InitializeGame` 仍可继承 `_roleConfig.Abilities`，并且仅凭 Amazon 职业无条件启用战斗技巧。
- `LevelUpPanelController` 部分等级显示读取 `SkillDefinition.Level`，没有使用角色实际 `LearnedSkill.Level`。
- `SkillDatabase` 仍有 `_1 -> _2` 的旧独立 ID 推导路径，不适合新的稳定 ID 模型。
- `BattleUIController` 对置灰卡片移除点击处理，无法支持“置灰但可点击并说明原因”。
- 伤害只有 Physical/Magic 大类，元素没有 Lightning；SkillGraph 还会把所有 Magical 错标为 Fire。
- 战斗单位没有持久四方向朝向；现有选择上下文也不支持有序多段目标队列。
- `EncounterRecipe` 的生命/输出倍率和 `BattleLayout.BlockedCells` 没有完整运行时消费者。
- 首批怪物共用同一 BasicMelee AI；Ranged 配置的 Heavy Shot 高于其可用 MP。
- 升级结算使用 `Task.Run + Task.Yield` 伪装超时，可能未确认就推进。
- 战败仍可能计算金币/经验；RunSummary 没有持续累计流水线。
- Mystery/Rest 进入时过早设置 `IsConsumed`；当前重入标记不足以恢复已选择、已结算或未提交状态。
- 路线 smoke test 通过 `completeNode` 快进，不能证明真实玩家流程。

## Scope

### In Scope

- Pure Run 动态技能绑定、等级资产解析、候选生成、只读技能 UI 和旧存档幂等补全。
- 三职业首批技能及共享战斗机制：元素、朝向、即时先攻重排、点燃、中毒、眩晕、恐惧、护盾、召唤集合、实体长矛、多段选目标、分身诱敌。
- 当前出战队伍职业唯一约束。
- Mystery 三事件、确定性检定、结果反馈、事件致死和分阶段重入。
- 遭遇倍率、阻挡格、基础怪物差异化 AI 与配置可执行性校验。
- 升级确认、战败奖励、RunSummary 持久累计和结局快照。
- Gameplay Test schema/adapter 扩展、单机制测试、真实 Pure Run E2E、Unity 编译与 PlayMode 回归。
- 权威设计文档、Known Gaps 和 OKF 同步。

### Out of Scope

- Treasure 节点进入 Pure Run 路线；仅保留“完善奖励逻辑、交互流程和地图出现规则”的后续需求。
- 技能槽已满后的新技能替换；满槽只过滤新技能，保留升级候选。
- RoguelikeMap 技能装配、卸载、排序和装备数值比较。
- 通用背刺增伤、斜向持久朝向、电抗或雷电附加机制。
- 新职业、角色仓库 UI、重复职业角色删除或旧存档自动改编队。
- 专用火魔美术和分身独立美术。
- 长矛命中后反弹到落点的抛物线动画，以及召唤长矛飞回角色的表现；本期均瞬时结算。
- BG3 风格投骰动画；本期只显示检定角色、属性、概率和结果。

## Locked Product Rules

### 1. 技能持久化、等级与候选

- 角色数据使用稳定逻辑技能 ID 和 `LearnedSkill.Level`；等级不是独立技能 ID，也不是独立已学卡片。
- 战斗资产按等级拆分，运行时目录解析 `(skillId, level) -> AbilityConfig/SkillGraph`。
- 缺少目标等级资产时，运行时回退到该技能最高可用的较低等级并记录错误；编辑器校验和自动化测试必须失败。
- 基础技能最高 Lv3，高级技能最高 Lv2；主动与被动槽各 3 个。
- 每次角色升级都从“合法未学技能 + 已学技能下一等级”生成最多 3 个确定性候选；两类都存在时至少各保留 1 个位置。
- 角色 Lv2 即可看到起始技能 Lv2；取消“Lv2–7 学技能、Lv8–12 升级技能”的旧分段。
- 对应类型槽位满时过滤该类型新技能，不过滤已学技能升级；所有合法候选为空时只完成属性成长。
- 候选存在时必须选择一个才能确认升级。
- 本次分配的属性立即重建候选；属性从 6 加到 7 后，同一面板可出现刚满足门槛的高级技能。
- 起始分支高级技能在首次满足条件时固定显示于槽 0；这是一次展示保底，即使未选也消费。
- 升级面板标题为“选择技能”；卡片标识“新技能”或“升级至 LvX”，描述展示升级后的完整效果。
- `LearnedSkills` 是 Pure Run 职业主动/被动的唯一真相源；移动和职业普通攻击是基础动作。
- 动态覆盖仅在 `IsPureRun=true` 启用；非 Pure Run 和独立测试继续使用 Prefab/遭遇能力。

### 2. 基础动作、队伍与地图技能区

- 法师普通攻击：零消耗、射程 1–3、每回合最多成功使用一次。
- 死灵法师普通攻击：显示名“灵魂弹”，零消耗、射程 1–3、每回合最多成功使用一次。
- 亚马逊普通攻击：零消耗近战、射程 1、每回合最多成功使用一次；实体长矛掉落时不可用。
- 当前出战队伍中每个正式职业最多一名；召唤物、分身和敌人不参与检查。未来候补角色可重复职业，编队时阻止加入；不自动删除或判废旧存档。
- Inventory 的 `SkillSlots` 保留为只读列表：主动在前、被动在后，同类按学习顺序，剩余显示“空”。
- 点击技能格只显示名称、等级和功能描述 tooltip，不显示操作按钮。
- 正常学习的高级技能“召唤长矛”显示；额外工具技能“拾取长矛”在地图与升级面板均隐藏。
- 隐藏规则使用通用配置标记，不硬编码单一技能 ID。

### 3. 共享战斗规则

#### 伤害与元素

- DamageCategory 继续区分 Physical/Magic；Element 独立区分 None/Fire/Ice/Water/Earth/Wind/Lightning。
- `Magic + None` 是合法组合；Magical 不再默认映射 Fire。
- Lightning 本期只用于分类、反馈和未来扩展，不新增抗性。

#### 朝向

- 所有战斗单位持有 North/East/South/West 四方向状态；不保存到地图或下一场战斗。
- 出生配置可显式指定；缺省玩家朝东、敌人朝西。召唤物复制召唤者施法时朝向。
- 当前行动单位在没有进入移动、技能或消耗品目标选择时，可点击上下左右相邻格免费转向；格子占用与通行不影响转向。
- 主动移动结束后采用路径最后一步方向；零格移动不改变。
- 成功的目标/方向型行动在效果结算前转向；取消或失败不改变。
- 斜向目标按偏移更大的轴换算；横纵相等时优先保留当前有效方向，否则选水平轴。
- 击退、拉拽、抛掷和被动传送等瞬时强制位移保留朝向；恐惧等沿路径行走采用最后一步；主动瞬移按目标方向转向。
- AI 不在回合结束时自动面向最近敌人。
- 不显示常驻箭头或正前方高亮；单位视觉必须表现四方向，只有方向技能瞄准时显示范围。
- 本期不加入通用背刺规则。

#### 状态与回合

- 点燃使用递减层数：施加 N 时累加；目标回合开始受到当前层数伤害，然后层数减 1，降到 0 时移除；无持续回合字段、无层数上限。
- 中毒固定每个目标回合开始造成 2 点伤害；伤害不叠加，每次成功施加增加 3 个目标回合，持续时间无上限。
- Slow 固定 `Speed -2`，最低 1；不叠加数值，重复施加刷新剩余回合。Speed 同时重新计算先攻和移动范围。
- Slow 生效后立即重排当前轮尚未行动单位；已行动单位不重复行动。解除后同样重排尚未行动单位。
- Stun 跳过目标下一次行动；如果目标本轮尚未行动则跳过本轮，否则跳过下一轮；重复施加只刷新。
- 闪避整次即时命中时，该次伤害及附带状态都失败；已经附着的持续伤害不可闪避。

#### 目标选择与失败原子性

- 目标、资源、生成位置、尸体和实体落点必须在提交前完成验证。
- 取消、目标非法、生成失败均不扣 MP、不消耗尸体、不替换旧召唤物、不清除旧状态。
- 需要“置灰但可点击”的卡片通过统一 Availability 结果返回可用性和原因，而不是把点击监听移除。

### 4. 法师技能矩阵

| 技能 | 等级 | 完整效果 |
|---|---:|---|
| 火球术 | Lv1 | 保持现有射程、消耗和单体直接伤害；弹道命中第一个敌人，只伤主目标并施加点燃 2，无 AOE。 |
| 火球术 | Lv2 | 主目标直接伤害在 Lv1 基础上 +2；解锁命中格正交相邻溅射，溅射直接伤害为主目标本次基础直接伤害的 50%、向下取整且至少 1；主目标和溅射目标施加点燃 3，不伤友军。 |
| 火球术 | Lv3 | 主目标若已有点燃，先造成等于当前点燃层数的额外伤害并清空旧点燃，再正常结算 Lv2 火球并施加点燃 3；溅射目标不引爆。 |
| 寒冰箭 | Lv1 | 射程 5、现有单体魔法伤害、6 MP；命中施加 1 回合 Slow。 |
| 寒冰箭 | Lv2 | 消耗降至 4 MP；Slow 延长为 2 回合。 |
| 寒冰箭 | Lv3 | 主目标命中后反弹至其 3 格内最近的另一敌人，造成 50% 伤害并施加 1 回合 Slow；最多一次，同距按稳定单位 ID。 |
| 霹雳闪电 | Lv1 | 5 格内指定敌人，瞬时直击，不使用 projectile，不受中间单位、墙体或 LOS 阻挡；9 魔法伤害、6 MP。 |
| 霹雳闪电 | Lv2 | 直接伤害不变，25% 概率施加 1 次行动 Stun。 |
| 霹雳闪电 | Lv3 | 直接伤害 +2，Stun 概率提升到 50%。 |
| 召唤火魔 | Lv1 | 保持现有消耗；重新施放时替换该法师全部旧火魔，最多生成 1 个。 |
| 召唤火魔 | Lv2 | 重新施放时替换旧火魔，尝试生成 2 个；从曼哈顿距离 1 向外搜索到 3，至少一格合法即可施放，允许只生成 1 个。 |
| 冰甲 | Lv1 | 自身 2 回合受到伤害降低 25%；重复施放只刷新为 2 回合。 |
| 冰甲 | Lv2 | 减伤不变；每次遭受近战攻击后，对攻击者施加 2 回合 Slow。 |
| 瞬移术 | Lv1 | 6 格内选择可见的合法空格，8 MP；无视路径、单位和障碍位移，不消耗普通移动机会，行动继续。 |
| 瞬移术 | Lv2 | 消耗降至 5 MP，并允许选择不可见但合法的空格。 |

火魔配置：独立 `FireDemon` 单位/Prefab，视觉暂复用 MageBlue 并使用火焰色调；生命 12、Speed 4、移动 4、无 MP；1–3 格火焰攻击造成 4 点伤害并施加点燃 1；每回合最多攻击一次，AI 尽量保持 2–3 格。火魔可接受普通治疗。每个火魔在完成第 5 次自身行动后死亡，被眩晕跳过的行动也计时；战斗结束清除。

### 5. 死灵法师技能矩阵

| 技能 | 等级 | 完整效果 |
|---|---:|---|
| 召唤骷髅 | Lv1 | 选择并消耗 1 具合法尸体，在尸体原格生成 1 个骷髅；上限 1；骷髅 HP 8、近战伤害 2。 |
| 召唤骷髅 | Lv2 | 同上；上限 2；骷髅 HP 10、伤害 3。 |
| 召唤骷髅 | Lv3 | 同上；上限 3；骷髅 HP 12、伤害 4。 |
| 伤害加深诅咒 | Lv1 | 5 格内单个敌人，3 MP；受到所有伤害 +30%，持续 5 个目标回合。 |
| 伤害加深诅咒 | Lv2 | 5 格内任意格为中心，十字 5 格敌人受到同一诅咒。 |
| 伤害加深诅咒 | Lv3 | 5 格内任意格为中心，3x3 九格敌人受到同一诅咒。 |
| 骨矛 | Lv1 | 射程 5、7 点无元素魔法伤害、6 MP，命中第一个敌人停止。 |
| 骨矛 | Lv2 | 消耗降至 4 MP，其余不变。 |
| 骨矛 | Lv3 | 4 MP；可选同一直线敌人或空格为终点，对路径所有敌人各造成 7 点伤害；友军不受伤也不阻挡，墙体阻挡，到终点结束。 |
| 骷髅法师 | Lv1 | 7 MP；消耗 1 具合法尸体，在原格生成 1 个；上限 1；HP 6、Speed 4、移动 3；零 MP 使用火球术 Lv1。 |
| 骷髅法师 | Lv2 | 上限 2，满员替换最早者；单体 HP 8；零 MP 使用火球术 Lv2；其余本体属性不变。 |
| 恐惧诅咒 | Lv1 | 射程 5、单个敌人、7 MP；施加下一次行动的恐惧。 |
| 恐惧诅咒 | Lv2 | 射程 5，可选敌人或空格为中心，对十字 5 格敌人施加恐惧。 |
| 骨盾 | Lv1 | 自身、8 MP；护盾值为施法时魅力总值 x2，无回合期限；吸收所有物理伤害。 |
| 骨盾 | Lv2 | 数值与消耗不变；吸收所有战斗伤害，包括魔法和已附着持续伤害。 |

召唤公共规则：普通单位、友军/敌军非召唤单位和预置尸体可用；召唤物不产生可用尸体。普通骷髅和骷髅法师分别计数，满员成功重召时先移除该类别最早召唤者。所有校验成功后才消耗尸体、MP 和旧召唤物。召唤物无固定寿命，存活到被击杀、被替换或战斗结束。

恐惧规则：目标行动开始时强制使用其普通移动机会，移动到本回合可达且距离施法者最远的格；同距按稳定坐标。无更远格时原地消耗移动机会。逃跑后仍可普通攻击或释放技能。恐惧不叠加，重复施加刷新。

骨盾重复施放：允许消耗 8 MP 将剩余护盾重置为当前魅力总值 x2，不累加。

复活类召唤物继续是治疗合法目标，但所有标准 HP 恢复为 0；魔法恢复和净化不受该规则影响。

### 6. 亚马逊技能、长矛与分身

| 技能 | 等级 | 完整效果 |
|---|---:|---|
| 突刺 | Lv1 | 3 MP；选择正交 2 格内敌人确定方向，攻击前方 2 格，每名敌人 6 物理伤害、可暴击；空格可穿过，友军和墙阻挡。 |
| 突刺 | Lv2 | 长度增至 3 格，伤害和消耗不变。 |
| 突刺 | Lv3 | 保持 3 格；每目标伤害为 `6 + 本回合主动移动格数`，无上限；成功施放清零，取消/失败不清零，回合结束清零。 |
| 连续刺击 | Lv1 | 8 MP、3 段、每段 4 物理伤害并独立暴击。 |
| 连续刺击 | Lv2 | 8 MP、4 段，其余规则不变。 |
| 毒矛 | Lv1 | 射程 6、6 MP、主目标 8 物理直接伤害且可暴击；主目标增加 3 回合中毒。 |
| 毒矛 | Lv2 | 主目标直接伤害 10；主目标及正交相邻十字 5 格敌人增加 3 回合中毒，其他敌人无直接伤害。 |
| 毒矛 | Lv3 | 主目标直接伤害 10；主目标周围 3x3 九格敌人增加 3 回合中毒，其他敌人无直接伤害。 |
| 战斗技巧 | Lv1 | 原有闪避率 +30 个百分点，统一进行一次闪避判定。可闪避敌方单体或范围即时物理/魔法伤害；无攻击者环境伤害和既有 DoT 不可闪避。 |
| 战斗技巧 | Lv2 | 玩家主动普通近战攻击命中且目标存活后，30% 概率免费再次攻击同一目标；重新独立命中/闪避/暴击/伤害，不递归，不由技能或反击触发。 |
| 战斗技巧 | Lv3 | 亚马逊所有可暴击直接伤害 +20 个百分点暴击率，包括普通/追加攻击、突刺、连续刺击、毒矛和召唤长矛 Lv2；不影响 DoT。 |
| 召唤长矛 | Lv1 | 显示名“召唤长矛”，内部 ID 保留 `amazon.recover_spear`；敏捷 7 + 已学毒矛；4 MP、6 格，进入目标选择，仅掉落长矛格合法；无视 LOS/墙/单位，召回唯一实体长矛，不结束行动。 |
| 召唤长矛 | Lv2 | 范围和消耗不变；召回成功后，以亚马逊为中心对正交相邻四格敌人各造成 6 点 `Magic + Lightning` 伤害，各自独立暴击；无敌人也可召回。 |
| 分身 | Lv1 | 显示名“分身”，内部 ID `amazon.decoy`；6 MP；选择合法后撤空格，亚马逊技能位移，原格生成静止诱饵。 |
| 分身 | Lv2 | 消耗与分身属性不变；完整施放成功后清除亚马逊全部 Harmful Buff，保留 Beneficial。 |

连续刺击交互：

- 激活时锁定朝向，选区为前方深度 3、宽度 1/3/5 的九格扇形。
- 墙和不可穿越地形阻挡选择，单位不阻挡。
- 每段分别选择，允许重复目标；提示“第 n/N 段”，目标格显示有序编号。
- 右键或 Esc 采用栈式撤销；队列为空时再次取消才退出技能。
- 最后一段有效选择后立即按顺序执行，无二次确认。
- 每段完成或死亡跳过后移除编号，不做短暂高亮。
- 后续段绑定具体单位；目标被反应位移后仍命中，目标已死亡则消耗该段并跳过，不自动改目标。

实体长矛公共状态：

- 亚马逊只有一支实体长矛；所有带“投掷系”标签的技能共用。当前毒矛和召唤长矛带该标签，拾取长矛不带。
- 毒矛投出后生成唯一落地长矛；长矛掉落时，亚马逊普通攻击、突刺、连续刺击和毒矛置灰但可点击，提示“需要先回收长矛”。
- `amazon.pickup_spear` 显示名“拾取长矛”，固定 Lv1、0 MP、ExtraUtility；学习任意投掷系技能时幂等自动授予并持久化，不占槽、不进候选，只在战斗技能栏排于正常主动技能之后。
- 拾取需要亚马逊位于长矛八方向相邻格，点击后直接回收，不进入目标选择；无掉落提示“当前没有需要回收的长矛”，距离不足提示“需要移动到长矛相邻格”。
- 召唤长矛与拾取长矛在相邻时都可用，玩家可主动花 4 MP 触发 Lv2 电击。
- 落地长矛占用格子，任何单位不能站入；不阻挡 LOS，不能被攻击、推动或销毁。
- 投掷前预检半径 3 内最近合法落点，主目标格排除；同距优先目标背离施法者方向。
- 合法落点必须为空、可行走，且八方向至少一格是地形上可到达的可行走格；可达性忽略临时单位占位，但不跨永久墙体或断区。
- 无合法落点时目标非法，不扣 MP；战斗胜负、中断或亚马逊死亡时清除落地对象并恢复下一战持矛，不提示。

分身完整规则：

- 首圈八格有合法空格时只开放首圈；首圈完全无合法格时开放距离 2 的外圈。只检查目标格可行走且为空，不检查路径，可越过墙和单位。
- 后撤不累计突刺移动增伤、不改变朝向、不消耗普通移动机会；施放后行动继续。
- 分身 Max/Current HP 为施法者最大生命的 50% 向下取整，至少 1；快照施法时防御和闪避，之后不同步。
- 分身无回合、不能移动/攻击/施法；占格，只能被敌方攻击，玩家友军技能和消耗品不能选它。
- 分身免疫全部 Buff/Debuff，只承受敌方直接单体/AOE 伤害。
- 持续 3 个亚马逊回合周期：施放回合计第 1 回合，在亚马逊第 4 回合开始移除。
- 每名亚马逊最多一个；新分身完整确认后才替换旧分身，取消/失败不影响旧分身。
- 敌方 AI 在能通过正常移动接近或攻击分身时强制优先处理；否则使用普通选目标。
- 击杀、到期、替换、亚马逊死亡和战斗结束均直接移除，不留尸体、奖励或死灵素材。
- 视觉复用亚马逊当前外观和施法时朝向，使用半透明冷色调，仅需待机、受击和消失表现。

## Mystery v1 Rules

### 地图、池与检定

- 第 4、6 层竞争层继续包含 Mystery；Treasure 不加入 Pure Run。
- 事件池保留诅咒宝箱、堕落祭坛、迷路村民三个主题，修复配置，不重做主题。
- `runSeed` 对事件池做确定性洗牌；同局两个 Mystery 分配不同事件，读档不重抽。池耗尽后才允许重复。
- 选项自动使用对应总属性最高的存活角色；同值按固定队伍顺序。`Self` 指检定者，`RandomAlly` 指其他随机存活队友，`All` 指全体存活角色。
- 总属性为基础属性 + 当前装备加成，不含只在战斗生效的临时 Buff。
- `最终成功率 = clamp(基础成功率 + (总属性 - 5) x 5%, 5%, 95%)`。
- 检定随机源为 `runSeed + nodeId + stableOptionId`；同一节点同一选项不可通过重开刷结果。
- 选择前显示检定角色、总属性和最终成功率；结算后显示成功/失败、叙述和实际数值变化/获得物。
- 正式事件禁止引用未定义 ID；`holy_symbol` 只能作为文案元素，不能作为奖励数据。
- 本期事件结果只使用金币、HP、已定义消耗品，以及仅带入下一场战斗的现有机制 Buff/Debuff；不发装备、不改永久属性、不做事件链。

### 三个事件结果

| 事件 | 选项 | 成功 | 失败 |
|---|---|---|---|
| 诅咒宝箱 | 力量 60% | +50 金币 | 检定者下一战受到伤害 +30%，持续 3 回合 |
| 诅咒宝箱 | 敏捷 70% | +30 金币 | 检定者立即 -15 HP |
| 诅咒宝箱 | 放弃 | 无效果 | 无失败分支 |
| 堕落祭坛 | 魅力 50% | 检定者下一战受到伤害 -20%，持续 3 回合 | 检定者下一战受到伤害 +30%，持续 3 回合 |
| 堕落祭坛 | 智力 65% | 获得 1 瓶净化药水，进入共享背包 | 检定者立即 -20 HP |
| 堕落祭坛 | 绕道 | 无效果 | 无失败分支 |
| 迷路村民 | 体质 75% | +40 金币 | 检定者立即 -10 HP |
| 迷路村民 | 智力 60% | +20 金币 | 无效果 |
| 迷路村民 | 无视 | 无效果 | 无失败分支 |

### 重入和死亡

- 点击 Mystery 即锁定本层路线，不能关闭后改选同层节点。
- 保存阶段为 `Entered -> Resolved -> Completed`：未选择时恢复同一事件；已结算未继续时恢复同一结果页且不重复应用；点击继续后才消费节点、提交路径并清理事务。
- 事件 HP 变化可杀死角色。结果页仍先完整展示；点击继续后若全队死亡，进入 Run 失败总结，不返回地图。
- Rest 和 Store 使用同一节点事务基础：进入不提前写 `IsConsumed`；结果/购买写回幂等；关闭或继续时原子提交节点。Store 重入恢复同一商品与购买状态。

## Encounter, Settlement and Summary Rules

### 遭遇执行

- 保留现有 N1–N6、E1–E2、Special 配方和怪物能力配置，不恢复旧文档中已经不再权威的 Expose 等描述。
- E1/E2 使用 Health x1.3、Output x1.15；Special 使用 Health x1.8、Output x1.25。
- Health 倍率在单位完成基础属性初始化后应用到 MaxHealth，并以新上限满血出生；新上限使用 `Mathf.CeilToInt(baseMaxHealth x multiplier)`，最低 1。
- Output 倍率在统一伤害入口应用于该单位发起的直接伤害和可追溯来源的 DoT；不影响治疗、护盾、环境伤害或事件伤害。
- `center_blocker` 的永久阻挡格在单位生成前写入 Cell 状态，影响站立、寻路、技能落点和 LOS，并在战斗清理时恢复。
- 每种怪物使用独立 Brain/Profile 资产并围绕现有能力形成差异：Charger 接近并优先 ChargeStrike；Ranged 保持射程并优先 HeavyShot；AOE 最大化敌方覆盖且不主动选择会伤害友军的中心；Support 优先对未受诅咒目标施加 Curse；EliteCharger 和 ElitePoisonCaster 使用可断言的 Pattern 顺序与合法 fallback。
- Encounter 校验必须保证每个配置能力在单位初始资源下至少可使用一次；Ranged 的最大/初始 MP 提升到不低于 HeavyShot 成本，而不是让已配置技能永久不可用。
- AI 资产缺失、Pattern 引用不存在能力、布局格不存在或倍率非法时，加载失败并给出明确错误，不静默退回错误内容。

### 升级与战败

- LevelUp 只等待 `OnConfirm`；移除伪帧超时。候选存在但玩家未选，或属性点未合法分配时，确认按钮不可用。
- 面板确认后先持久化角色成长，再继续结算；控制器缺失或保存失败时停止结算并记录可见错误，不自动跳过成长。
- 只有玩家方胜利才计算和应用金币、经验、物品与 Pure Run 成长；玩家战败返回全零奖励，败北 UI 不显示 `+金币`。

### RunSummary

- 在 Pure Run session 中持久保存进行中的 `RunSummary`，所有提交操作使用稳定 transaction key 去重。
- `totalGold` 表示本局累计获得的正向金币，不因购买扣款减少，也不等于结束时余额。
- `acquiredEquipment` 和 `acquiredItems` 在奖励实际写入状态时记录稳定 ID；装备后来被装备仍计入，消耗品后来被使用仍计入。
- `enemiesDefeated` 记录胜利战斗中真实死亡的敌方正式单位；不统计召唤物、分身或测试对象。
- `nodesVisited` 在节点完成提交时每节点加一；`eventsCompleted` 仅在 Mystery 完成时加一；Boss 胜利设置 `bossDefeated`。
- Run 结束时先生成不可变结局快照，再清理活动 session；RunEndSummary UI 消费快照后删除快照。败北、Boss 胜利和事件全灭共用该链。
- UI 将稳定 ID 解析为显示名，并分别展示累计金币、击败敌人、访问节点、完成事件、装备和消耗品。

## Relevant Context and Primary Files

| 领域 | 现有入口 | 计划中的职责 |
|---|---|---|
| 角色状态 | `Assets/Tactics/Scripts/Common/Roster/CharacterDefinition.cs`、`PlayerAdventureState.cs`、`PlayerAdventureStateStore.cs` | 技能类别/标记、旧存档补全、职业唯一、RunSummary 持久化 |
| 技能目录与成长 | `FirstSliceSkillCatalog.cs`、`SkillSystem.cs`、`SkillDatabase.cs`、`BattleSettlementCoordinator.cs` | 稳定 ID + 等级目录、混合候选、保底和槽位过滤 |
| 战斗装载 | `CharacterStatsApplicator.cs`、`BattleController.cs`、`Unit.cs` | Pure Run 能力覆盖、被动按等级启用、基础动作和额外技能 |
| SkillGraph | `Assets/Tactics/Scripts/Common/Skills/Graph/`、`SkillGraphAbilityImpl.cs` | 元素、状态、召唤集合、方向范围、多段选择、长矛、分身 |
| 回合与伤害 | `CombatComponent.cs`、Buff 目录、回合控制器 | 即时重排、闪避、护盾、DoT、Output 倍率、恐惧 |
| UI | `BattleUIController.cs`、`LevelUpPanelController.cs`、`InventoryUIController.cs` 及对应 UXML/USS | 两行战斗布局、可点击禁用原因、多段标记、候选等级、只读技能 tooltip |
| 事件 | `NodeInteractionManager.cs`、`EventUIController.cs`、`AttributeCheckSystem.cs`、`EventResult.cs` | Mystery 结果、确定性检定、分阶段事务与致死结局 |
| 地图与会话 | `RoguelikeMapGenerator.cs`、`RoguelikeMapRuntimeState.cs`、`PureRunSessionStore.cs` | 不重复事件、节点提交、重入和结局快照 |
| 遭遇 | `EncounterConfig.cs`、`BattleController.cs`、Monster AI 资产 | 倍率、阻挡格、资源可执行性和差异化 AI |
| 结算 | `BattleRewardSystem.cs`、`BattleSettlementFlow.cs`、`RunSummary.cs` | 胜负门禁、显式升级确认、累计统计 |
| 自动化 | `Assets/Tactics/Scripts/Common/Testing/Gameplay/`、`Tests/gameplay-specs/`、PlayMode tests | adapter 扩展、机制测试、真实玩家流 E2E |

新增运行时类型应按职责拆分，避免继续堆入 `Unit` 或 `BattleUIController`：

- `PureRunAbilityCatalog`：解析 `(skillId, level)`、技能标签、UI 可见性和额外工具技能。
- `PureRunAbilityBinder`：把角色存档转换为战斗 AbilityConfig 与被动等级。
- `AbilityAvailability`：统一表达 Enabled/DisabledClickable/Hidden 与原因。
- `FacingState`/`FacingResolver`：四方向状态、坐标换算和更新规则。
- `BattleInitiativeService`：当前轮未行动单位的安全重排。
- `SummonRegistry`：按召唤者与召唤类别保存有序集合、上限和最早替换。
- `AmazonSpearState`：唯一实体长矛、落点、持有状态与战斗清理。
- `OrderedTargetSelectionState`：连续刺击的有序段队列与栈式撤销。
- `RoguelikeNodeTransaction`：节点阶段、结果快照、幂等应用 key 和恢复入口。
- `PureRunSummaryRecorder`：统一记录奖励、节点、事件、击杀和结局快照。

## Implementation Plan

### 串行交付与验收协议

Slice 1–9 由 Codex 无人值守地统一执行以下闭环：

1. **实现范围核对**：只实现当前 Slice 及其必要测试能力，不提前实现下一 Slice。
2. **自动化验收**：C# 修改后编译；运行当前 Slice 的 Editor Test、PlayMode Test 和 `*.gameplay-test.md`；源 Spec 必须经过 validate/compile，Unity Runner 必须消费生成 plan；真实资产行为不得由测试替身代替。
3. **Codex Review 与修复**：生成可复核 Review 记录，检查修改文件与 `.meta` 配对、需求到断言的映射、测试结果、控制台错误、延期项、无关脏改动及代码风险；发现当前 Slice 缺陷后立即修复，并重复自动化验收和 Review，直到没有未处理的可执行问题。
4. **自动独立 Commit**：无需用户确认，仅暂存当前 Slice 路径或混合文件中的当前 hunk，完成 `.meta`、GUID、`git diff --cached --check`、OKF 影响检测与必要校验后创建独立提交。
5. **自动进入下一 Slice**：记录 Commit hash、提交文件数和剩余风险后立即开始下一 Slice，不等待中途回复。只有缺少新授权、需要显著扩大已批准范围或外部状态连续阻断且没有安全替代方案时才暂停；普通编译失败、测试失败和 Review 缺陷均由 Codex 自行处理。

若玩家可观察行为暂时无法自动断言，当前 Slice 必须扩展 Gameplay Test Framework 后再验收。允许留到 Slice 10 的只有视觉观感和难以稳定量化的体验判断；功能正确性、交互状态、按钮行为、文本、顺序、布局关系和跨场景状态都必须先自动化。

### Slice 1 — 成长数据真实进入下一场战斗

1. 建立 `PureRunAbilityCatalog`，为 18 个正式技能和 `amazon.pickup_spear` 注册稳定 ID、角色、类型、最大等级、前置、属性门槛、标签、地图/升级可见性和每级 AbilityConfig 路径。
2. 移除首批技能升级对 `_1/_2` ID 推导的依赖；兼容旧 ID 时只在加载迁移层转换为稳定 ID。
3. 扩展存档修复：有投掷系技能的 Amazon 幂等补发 `amazon.pickup_spear`；不复制、不提示、不计槽。
4. 实现 `PureRunAbilityBinder`，在玩家 Unit 初始化前调用 `ApplyAbilityConfigs`；只注入基础普通攻击、正常已学主动技能和额外工具技能，并按实际等级启用被动。
5. 删除 Amazon 按职业无条件启用战斗技巧的路径，改为读取该被动是否已学及等级。
6. 重写技能候选为混合池，保留新技能/升级的确定性配额、属性即时刷新、一次展示保底和无候选属性-only 分支。
7. 增加目录完整性校验：每个 1..MaxLevel 都必须有对应资产；运行时回退仅作为保护。

自动验收：`pure-run-mixed-levelup-candidates` 证明 Lv2 混合候选；`pure-run-learned-skill-binding`/PlayMode 证明选择、存档重载和下一战只绑定火球 Lv2；真实 Fireball Lv1/Lv2 资产测试证明等级行为；非 Pure Run RoleConfig 回归通过。

### Slice 2 — 共享战斗原语与测试能力

1. 拆分伤害大类和 Element，新增 Lightning，修正 Magical 默认 Fire。
2. 新增四方向状态和所有更新入口；完成四方向视觉适配，不增加箭头。
3. 实现 Slow 后当前轮安全重排、Stun 下一行动、递减点燃、固定毒伤/累加时长和统一状态刷新策略。
4. 增加按召唤者/类别的有序 `SummonRegistry`，支持数量上限、最早替换、主人死亡与战斗清理。
5. 增加 `AbilityAvailability`，让禁用卡片保留点击并显示原因。
6. 扩展 SkillGraph/Ability 目标协议：任意格中心、方向扇形、有序多段选择、实体对象格、回收动作和无路径技能位移。
7. 扩展 Gameplay adapters 和 assertions，至少支持：技能实际等级、单位能力列表、禁用原因、Facing、当前轮顺序、状态层数/剩余行动、召唤类别/顺序、长矛持有与落点、多段选择队列、分身生命周期和 AI 目标。
8. 扩展真实交互测试输入与 UI 可观察面：增加 `hoverElement`、`rightClickElement`、`pressKey`，以及 `elementClassContains`、`elementChildOrderEquals`、`elementRectRelationEquals`、`abilityCardAvailabilityEquals`、`targetMarkerOrderEquals` 和 `selectionStageEquals`；validator、compiler、Unity adapter 与源 Spec 回归必须同步更新。

自动验收：`facing-and-initiative`、`status-turn-semantics`、`summon-registry-order`、`ability-availability-reason` 和 `ordered-target-selection-state` 分别证明状态、重排、召唤顺序、可点击禁用原因与多段队列；所有 Spec validate/compile/runner 通过后才允许职业 Slice 复用。

### Slice 3 — 法师完整等级链

1. 按法师矩阵创建每级 AbilityConfig/SkillGraph，补齐点燃、Slow、即时闪电、反弹和可见性规则。
2. 创建 FireDemon 独立单位、Prefab、角色配置、攻击能力和 AI；复用 MageBlue 视觉资源并做色调区分。
3. 通过 `SummonRegistry` 实现火魔替换、部分生成和 5 次自身行动寿命。
4. 冰甲使用统一伤害入口减伤，并在近战命中后施加 Slow；瞬移复用朝向和技能位移规则。

自动验收：`mage-skill-levels` 为六项技能分别覆盖等级差异；断言火球 Lv1 无 AOE、Lv2 十字溅射、Lv3 只引爆主目标，Slow 当轮重排，火魔 Lv2 仅一格可用时只生成一只且原子替换旧火魔；加载真实 AbilityConfig/SkillGraph/Prefab。

### Slice 4 — 死灵法师完整等级链

1. 统一尸体合法性和原子消耗，召唤物死亡不产尸体。
2. 用 `SummonRegistry` 分开管理普通骷髅和骷髅法师，按等级限制数量并替换最早者。
3. 创建/更新骷髅等级配置；骷髅法师复用对应等级火球但成本为 0。
4. 实现任意格诅咒范围、不可叠加只刷新、骨矛直线贯穿和恐惧强制移动后继续行动。
5. 将骨盾接入统一伤害拦截顺序，Lv1 仅 Physical，Lv2 吸收全部战斗伤害，重施重置不叠加。
6. 将 `CanReceiveHealing` 应用于新召唤资产，保持可选中但 HP 恢复为 0。

自动验收：`necromancer-skill-levels` 与尸体场景 Spec 断言尸体/MP/旧召唤替换原子性、两类召唤上限和顺序、恐惧后仍可攻击、骨盾伤害分类以及复活类召唤物治疗结果为 0；全部使用真实尸体和召唤资产。

### Slice 5 — 亚马逊、实体长矛与分身

1. 实现突刺方向线、主动移动计数和成功清零规则。
2. 实现连续刺击 `OrderedTargetSelectionState`、扇形选区、编号标记、栈式撤销和立即有序结算。
3. 实现 `AmazonSpearState`、落点预检、占格、可回收性约束、持矛依赖和全战斗清理。
4. 创建 `amazon.pickup_spear` 额外 Ability；改造 `amazon.recover_spear` 为显示名“召唤长矛”的两级正式技能。
5. 实现毒矛各级中毒范围，以及 Lv2 召唤长矛的 `Magic + Lightning` 四格电击。
6. 战斗技巧按已学等级接入统一闪避、追加普通攻击和暴击修正链。
7. 创建分身运行时/Prefab、无回合生命周期、后撤选择、快照属性、AI 强制优先和 Lv2 净化。
8. 在队伍变更服务层增加正式角色职业唯一校验；Pure Run 固定队伍测试保持通过。

自动验收：`amazon-spear-cycle`、`amazon-multi-stab-selection` 和 `amazon-decoy` 通过真实动作输入证明一支长矛锁定、相邻拾取/召唤并存、右键/Esc 栈式撤销、重复目标、死亡跳过、编号顺序、分身不可被友方选择及敌方 AI 目标改变。

### Slice 6 — LevelUp、Inventory 与 Battle UI 闭环

1. 修正等级角标和候选来源，使用角色实际 `LearnedSkill.Level`。
2. 更新 LevelUp 文案、卡片标签和完整效果描述；候选/属性变化后同步刷新确认状态。
3. Inventory `SkillSlots` 显示正常已学技能，隐藏 ExtraUtility；实现只读 tooltip，移除任何装卸暗示。
4. Battle UI 保持两行：移动和独立消耗品按钮在上排，技能卡组在下排；额外拾取技能排在正常主动技能之后。
5. 接入可点击禁用原因、多段选择提示/标记、方向技能范围和长矛对象表现。
6. 移除升级结算伪超时，以显式确认事件驱动继续流程。
7. 为本 Slice 编写 `levelup-mixed-candidate-confirmation`、`inventory-readonly-skill-tooltip`、`battle-ui-two-row-layout`、`battle-disabled-ability-reason` 和 `amazon-multi-stab-ui-flow` 源 Spec；通过 UI/Battle adapter 驱动真实点击、hover、右键和 Esc，并断言层级顺序、矩形相对位置、文本、启用状态、禁用原因和标记队列。

自动验收：地图技能 tooltip 不存在操作按钮；ExtraUtility 不出现在地图但按序出现在战斗；上下两排元素矩形不重叠且子项顺序正确；点击禁用毒矛显示“需要先回收长矛”；连续刺击标记/撤销状态正确；未选择技能时确认不可用且不能返回地图。

### Slice 7 — Mystery 与非战斗节点事务

1. 修正三个正式事件 JSON，移除数字枚举错配、`holy_symbol` 和不存在 Buff ID；新增/复用下一战减伤与承伤增加配置。
2. 在地图生成/首次初始化时确定性分配两个不同事件并持久化 `eventId`。
3. 修正属性检定总属性、概率公式、稳定 option ID 和确定性随机。
4. 实现 `RoguelikeNodeTransaction` 三阶段持久化；事件结果保存可恢复的展示文本和结构化效果快照。
5. `RewardResult` 使用 transaction key 幂等应用；继续按钮原子提交节点、统计和路径。
6. 事件致死先恢复/展示结果，再进入统一失败总结。
7. Rest/Store 接入同一事务基础，消除提前 `IsConsumed` 和地图锁死；Treasure 维持非 Pure Run 范围。

自动验收：`mystery-determinism-and-reentry`、`mystery-result-page-reentry`、`rest-transaction-reentry` 和 `store-purchase-reentry` 覆盖选择前、结算后继续前、继续提交后三个时点；断言 UI 结果文本、按钮状态、奖励/伤害幂等、同 seed 结果稳定和两个 Mystery 不重复。

### Slice 8 — 遭遇、奖励和 Run 总结

1. 在敌人初始化和统一伤害链消费 Health/Output 倍率；应用并清理布局阻挡格。
2. 创建六类怪物的独立 Brain/Profile/Pattern 资产，修复资源不可执行问题并增加加载校验。
3. 在奖励系统首入口增加玩家胜利门禁；战败跳过所有奖励、掉落与成长。
4. 以 `PureRunSummaryRecorder` 替换零散/未调用的 `ApplyToSummary`，从实际提交点记录累计数据。
5. 将进行中 summary 与 session 一起保存；Run 结束生成结局快照后清理活动状态，UI 消费后再清快照。
6. 统一 Battle Defeat、Boss Victory、Mystery 全灭三种结局入口。

自动验收：`encounter-runtime-contract` 和 `pure-run-summary-and-defeat` 断言 E1/E2/Special 倍率、center_blocker、HeavyShot 可用性、六类 AI 差异、战败零奖励、购买后累计金币不下降、已使用药水仍保留在本局获得物列表，并覆盖三种统一结局入口。

### Slice 9 — 全量自动化真实玩家流 E2E

1. 为上述每个复杂机制编写或更新 `*.gameplay-test.md`；使用 gameplay-test-spec validate/compile 生成 plan，不手写 `.plan.json`。
2. 新增真实 Pure Run 路线：从 Start 进入真实战斗，玩家操作获胜，完成升级并验证下一战技能等级；经过 Store 或 Mystery；执行场景返回；完成 Elite 与 Boss；断言 RunSummary。
3. 增加失败路线：战斗失败奖励为零；事件导致全灭；两者均进入同一结局快照。
4. 增加三个中断恢复测试：Mystery 未选、已结算未继续、Store 购买后未关闭。
5. 增加 UI 驱动成功路线和允许自然战败的真实路线；不得用 `completeNode`、直接写结局或状态快进代替玩家动作，状态设置仅允许建立不可由 UI 到达的测试前置。
6. 运行 Editor/PlayMode 相关测试、全部 Gameplay specs、全量编译、控制台检查和静态校验；修改 `.cs` 后显式 `refresh_unity`。
7. 更新 `three-class-skill-design.md`、Pure Run 设计、事件设计和 `project-known-gaps.md`；Treasure、两项长矛动画和投骰表现继续保留为 deferred/idea。
8. 运行 OKF `report --worktree`，核对并同步受影响的 roguelike-run、battle、skill-graph、monster-ai、gameplay-test-framework 和 project-documentation scopes，完成 Slice 1–9 的文档与知识收口。

自动验收：成功/失败/重入路线都由真实 Map、UI、Battle、Skill adapter 驱动；至少一条路线从 Home/New Run 开始，经真实战斗、升级确认、Store 或 Mystery、跨场景恢复到 Boss/失败总结；所有源 Spec validate、compile 与 Unity Runner 通过，且没有未解释的 Error 日志；权威文档、Known Gaps 和相关 OKF scopes 已同步。

### Slice 10 — 用户最终人工测试

1. Slice 1–9 全部独立提交且自动化、文档和 OKF 收口通过后，Codex 生成一份可直接执行的最终人工测试清单并停止自动推进，等待用户测试。
2. 用户在干净 Unity Editor 会话中只使用玩家可见鼠标/键盘流程，从 Home 创建或继续 Run；至少走完一次升级、Inventory 查看、Battle 技能交互、Mystery/Store 和结局。成功击败 Boss 不是强制条件，自然战败同样是合法路线。
3. 用户集中检查无法稳定数值化的表现：Battle 两行布局在目标分辨率下无裁切/遮挡，tooltip 锚点和层级正确，四方向视觉与技能范围易读，多段编号辨识清楚，长矛/分身反馈可理解，LevelUp/Inventory/Event 文案没有溢出或歧义。
4. 用户只需回报通过，或提供失败项与复现步骤。若失败，Codex 自动回到所属 Slice 修复、补 Gameplay/PlayMode 回归、重新 Review 并独立提交，再重新交付人工测试清单。
5. 用户确认最终人工测试通过后，Codex 自动执行计划生命周期清理：复核长期文档，删除本计划文件，保留 `.agents/plans/.gitkeep`，同步 project-documentation scope 并创建最终收口提交。

人工验收：这是唯一需要用户执行的验收，只判断视觉、可读性和整体操作体验；所有功能结果必须已经由 Slice 1–9 自动化证明。若人工流程发现逻辑问题，Slice 10 不通过并触发 Codex 自动修复闭环。

## Test Plan

### Editor / data validation

- 稳定技能 ID、等级资产完整性、最大等级和前置关系。
- ExtraUtility 不计槽、不进候选、地图隐藏、战斗显示。
- Mystery 正式 ID、Buff/Consumable 引用、option stable ID 和事件池去重。
- Encounter 倍率、布局格、AI 资产、Pattern 能力名和初始资源可执行性。
- Unity 资产及 `.meta` 配对、SkillGraph 结构校验、禁止测试 Prefab 进入正式目录。

### PlayMode / gameplay specs

- `pure-run-learned-skill-binding`：存档技能与等级决定下一战卡组。
- `pure-run-mixed-levelup-candidates`：Lv2 同时出现 Fireball Lv2 与新 Lv1，属性 7 当场刷新高级技能。
- `facing-and-initiative`：免费转向、移动/技能转向、瞬时位移保留、Slow 当轮重排。
- `mage-skill-levels`：点燃、反弹、Stun、火魔替换/寿命、冰甲、瞬移。
- `necromancer-skill-levels`：尸体原子性、召唤上限、诅咒范围、骨矛、恐惧、骨盾。
- `amazon-spear-cycle`：落点预检、占格、禁用原因、八邻拾取、远程召回、Lv2 电击、清理。
- `amazon-multi-stab-selection`：重复目标、撤销、立即执行、死亡跳过、位移后不重验。
- `amazon-decoy`：两圈目标、三回合、替换、AI 优先、不可友方选择、Lv2 净化。
- `mystery-determinism-and-reentry`：不重复、概率、结果恢复、幂等和全灭。
- `encounter-runtime-contract`：倍率、blocked cell、AI 选择与 HeavyShot 可用。
- `pure-run-summary-and-defeat`：累计统计、战败零奖励、结局快照。
- `pure-run-real-player-route`：真实 UI/战斗/结算/升级/事件或商店/跨场景/Boss。

### 自动化交互替代矩阵

| 原中途手动项 | 自动化替代 |
|---|---|
| Battle 两行布局与卡片顺序 | `battle-ui-two-row-layout` + `elementChildOrderEquals` + `elementRectRelationEquals` |
| 连续刺击编号、右键/Esc 撤销 | `amazon-multi-stab-ui-flow` + `rightClickElement`/`pressKey` + `targetMarkerOrderEquals` |
| 置灰但可点击提示 | `battle-disabled-ability-reason` + `abilityCardAvailabilityEquals` + tooltip 文本断言 |
| 四方向状态与视觉资源切换 | `facing-and-initiative` 断言 Facing 与 renderer/animation key；最终只人工判断视觉易读性 |
| Inventory 只读技能 tooltip | `inventory-readonly-skill-tooltip` 断言技能顺序、等级、文本和操作按钮不存在 |
| LevelUp 混合候选与确认门禁 | `levelup-mixed-candidate-confirmation` 驱动属性分配、候选刷新、选择和确认状态 |
| Event 结果页与重入 | `mystery-result-page-reentry` 断言结果文本、按钮、事务阶段和幂等效果 |

### Slice 10 集中人工验证

- 只在 Slice 1–9 独立提交、全量自动化、权威文档和 OKF 收口全部完成后，由 Codex 交付一次集中人工测试清单并等待用户。
- 用户仅检查视觉裁切、动画/朝向可读性、tooltip 锚点、标记辨识度、文案溢出和整体操作手感，并回报通过或失败复现。
- 人工验收前不得删除计划；人工失败由 Codex 回到所属 Slice 增加自动回归、修复、Review 和独立提交，用户无需参与中间处理。

## Risks and Mitigations

| 风险 | 缓解措施 |
|---|---|
| 18 个技能一次改动面过大 | 按共享原语、职业纵向切片实施；每个切片完成后编译和运行本职业回归 |
| 每级独立资产数量增加 | 以目录校验保证完整性，稳定 ID 保持存档和 UI 简单 |
| Slow 即时重排导致重复/丢失行动 | 只重排当前轮尚未行动集合，已行动集合独立保存并测试 |
| 召唤/尸体/长矛失败造成资源丢失 | 所有资源和替换都放到成功提交阶段，先做完整预检 |
| UI 禁用模型改造影响普通技能 | Availability 保持默认 Enabled/Disabled 行为，只对 DisabledClickable 增加点击说明 |
| UI 自动化只检查状态却遗漏布局/交互 | 扩展 UI/Battle adapter 的输入、层级、矩形关系和标记断言；Slice 10 再集中检查视觉与手感 |
| 旧存档技能 ID 或缺级资产 | 加载层幂等迁移；运行时低级回退；校验和测试阻止缺级发布 |
| 节点事务和旧 `IsConsumed` 并存 | 以 transaction 为写入入口，迁移时识别旧状态并只做一次安全修复 |
| Dirty worktree 覆盖用户修改 | 实施和提交均按路径审查，只修改本计划列出的相关文件；Codex 在每个 Slice 的自动 Review 中核对精确 staged scope |

## Handoff

实施者从 Slice 1 开始，不应直接批量制作 18 个等级资产。先完成稳定 ID/等级解析和一条 Fireball Lv1→Lv2 的真实跨战斗闭环，再扩展共享原语和三个职业。

任何 Unity `.asset`、`.prefab`、`.unity` 或 `.meta` 操作必须走 Unity MCP/agentic workflow；C# 修改前加载项目编码规范，修改后显式编译。每个切片结束都要保留可运行状态，不把未完成的职业资产注册到正式目录。Slice 1–9 不要求手动进入 Unity 验收，也不等待用户审核自动化证据；Codex 必须自行完成可复现 Review、修复和独立提交后继续。

完成定义：Slice 1–9 全部通过自动化、代码 Review 和缺陷修复并各自独立提交，权威文档/OKF 已同步；用户只完成 Slice 10 集中人工测试并确认通过；随后 Codex 自动删除本计划文件、同步计划生命周期并提交最终收口。
