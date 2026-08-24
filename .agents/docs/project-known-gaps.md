# 项目已知缺口

## 用途

本文集中记录已经从代码、资产或测试中确认，但尚未转成活跃执行计划的缺口。它不是承诺清单；只有满足“激活条件”并另行建立范围明确的开发计划后，项目才开始实现。

人工视觉、输入手感、Editor Reload 和完整游玩验收不在本文重复维护；其权威状态与可执行步骤统一记录在 [人工验收账本](manual-acceptance.md)。本文只保留需要继续开发、决策或工程收尾的事项。

状态定义：

- `verified-gap`：已确认当前实现缺失或不完整。
- `needs-decision`：需要产品或技术决策后才能排期。
- `deferred`：明确延后，当前切片不依赖。
- `idea`：值得保留的探索方向，尚未验证价值。

## Monster AI

| 状态 | 缺口与证据 | 未激活原因 | 激活条件 | 历史来源 |
|---|---|---|---|---|
| `deferred` | AI `Rule` 节点字段为历史 round-trip 数据，但当前 `AiDecisionService` 不消费；Workbench 已改为只读并稳定诊断。`ScoreNode.Parameter` 实际映射为运行时消费的 Score weight，不再列为缺口。 | 不改变当前 AI 决策规则。 | 只有批准新的 Rule 运行语义并建立 Golden 回归时才重新开放编辑。 | Workbench 最终收口 2026-08-17 |
| `verified-gap` | 首批六类遭遇 Brain/Profile、Pattern 引用和资源可执行性已有自动校验；跨第二批怪物的行为退化基线尚未建立。 | 当前只有首批遭遇资产进入正式目录。 | 出现第二批 Brain/Profile 或批量生成需求。 | 怪物 AI 旧计划 |
| `idea` | 用热力图展示候选行动及评分，辅助调试。 | 非运行必需。 | 评分系统稳定且调试成本明显增加。 | Monster System 旧计划 |

## Roguelike Event Editor

| 状态 | 缺口与证据 | 未激活原因 | 激活条件 | 历史来源 |
|---|---|---|---|---|
| `verified-gap` | 缺少 Undo/Redo、模板、自动布局、拖入 JSON、版本迁移和批量导出。 | 当前编辑器已满足基础创建、连线、检查与 JSON 导入导出。 | 内容生产开始依赖高频批量编辑。 | Event Editor 旧计划 |
| `verified-gap` | 数据模型预留 `Branch`，但创建菜单、节点渲染和 Inspector 尚无完整 Branch 编辑链路。 | 当前首批事件可由 Start/Option/Check/Success/Failure/End 表达。 | 出现需要多分支端口的实际事件设计。 | Event Editor 代码审计 |
| `verified-gap` | `SaveSessionState()` 为空，编辑会话状态没有持久化。 | 不影响资产本身保存。 | 明确要恢复的视图/选择状态。 | Event Editor 代码审计 |
| `verified-gap` | 图级语义校验和编辑器专用自动化测试不足。 | 当前只覆盖基础序列化与校验。 | 编辑器进入稳定内容生产链路。 | Event Editor 旧计划 |
| `verified-gap` | 编辑器代码仍存在直接 `Debug.Log`，不符合项目日志约束。 | 不影响运行时首切片。 | 下次修改该编辑器代码时一并清理。 | 当前代码审计 |

## Gameplay Test 与 SkillGraph

| 状态 | 缺口与证据 | 未激活原因 | 激活条件 | 历史来源 |
|---|---|---|---|---|
| `verified-gap` | Gameplay Test 尚不能严格断言完整事件先后序列。 | 当前结果断言已覆盖首切片核心行为。 | 出现“结果相同但阶段顺序错误”的回归风险。 | Framework Roadmap 旧计划 |
| `verified-gap` | 动画完成/表现时序缺少稳定的跨帧断言契约。 | 战斗逻辑验证不依赖表现资产。 | 动画进入自动验收范围。 | Framework Roadmap 旧计划 |
| `deferred` | Gameplay Test 的完整 Godot runtime batch 尚未作为独立 CI 质量面板展示。 | 当前统一 verifier 已执行该批次，但 CI 可发现性仍有限。 | 需要独立质量趋势或失败分类时，为 Godot runtime report 建立专门汇总。 | Framework Roadmap |
| `needs-decision` | SkillGraph 更强的循环、阶段与目标语义静态检查仍可扩展。 | 静态校验与运行测试的职责边界需先确定。 | 收集真实误配案例并定义可判定规则。 | SkillGraph 旧计划 |

## Godot Content Workbench

| 状态 | 缺口与证据 | 未激活原因 | 激活条件 | 历史来源 |
|---|---|---|---|---|
| `deferred` | 自动作者闭环已完成；真实 Godot Editor 中逐页 Apply All→Undo→Redo→Save→C# Reload、Graph 拖动手感与 Preview cleanup 尚未人工验收。自动套件中的 PlayableRunUi 仍报告 4 个 orphan-node runner warning，独立 page-replacement/preview cleanup 断言通过。 | 前台交互、视觉观感和 Assembly Reload 不能由后台门禁替代；不修改第三方 GdUnit adapter 掩盖 warning。 | 用户在 canonical Editor 按 `MQA-GODOT-CONTENT-WORKBENCH` 完成并反馈。 | Workbench 人工验收账本 |

## Adventure Tile 与发布整合

| 状态 | 缺口与证据 | 未激活原因 | 激活条件 | 历史来源 |
|---|---|---|---|---|
| `verified-gap` | Adventure 节点已实现 10×10 等距 Board、正式输入寻路、领队切换、交互物和仅指向直接后继节点的即时出口；营地、商人、NPC、火堆、出口、宝箱和祭坛仍主要使用功能性占位表现，缺少按节点定制的场景构图、正式状态变化、转场和音频。 | 当前垂直切片先验证玩法闭环，正式内容预算和视觉基准尚未批准。 | 确认首批节点的美术清单、复用规则与表现验收标准后建立内容计划。 | Tile Adventure review；`GodotPlayableRunMain` |
| `verified-gap` | 多出口已经替代开局 Route Overview/两组路线提交，只显示当前节点直接可达的下一层节点；出口类型、危险度、目的地提示和近距离误触保护仍未形成正式表现规范。 | 拓扑合法性已有自动回归，但可读性和点击风险只能先由人工验收暴露真实问题。 | 完成 `MQA-GODOT-TILE-ROUTE-MISCLICK`；若失败，再按具体误触或信息缺口立项。 | `781fd5fd`；Adventure exit Gameplay Tests |
| `needs-decision` | 旧活跃 Run 按既定兼容策略要求重新开局；当前 V10 存档与事件上下文可恢复，但旧存档失效时的玩家提示、终局摘要保留说明和备份恢复入口尚未形成产品文案契约。 | 不影响新 Run 的状态正确性，需要先决定提示层级和是否提供恢复入口。 | 确认旧存档面向玩家的处理方式后实现并加入迁移验收。 | V9/V10 Save review |
| `verified-gap` | `migration/godot` 到公开 `main` 的本地整合可无文本冲突重放，但公开候选门禁仍报告 200 个未登记 artwork state、4 个未登记 asset 和 1 个本地 `godot_ai` 用户路径；因此尚不能创建可合并的公开候选。 | 资产需逐项登记或排除，本地注入插件不得进入公开根，不能用放宽门禁代替处理。 | 收口资产 provenance/发布范围，剔除或改造本地注入内容，随后重跑 public candidate validator 与 `Verify-GodotProject.ps1`。 | 2026-08-21 public-main integration audit |

## Pure Run、奖励与内容扩展

| 状态 | 缺口与证据 | 未激活原因 | 激活条件 | 历史来源 |
|---|---|---|---|---|
| `deferred` | 魔剑士非大师内容、腐化/冥想、附身 AI、Run/Resource/Workbench 与自动测试已进入实现；恶魔失控形态规格（六维+5 重构、已学技能临时满级、敌友统一目标池、幸运修正永久死亡、墓碑记录、缺员继续）已实现并验证，`DEMONBOUND-POSSESSED-*` 六份合同全部升级为 `verified_current`；三个大师技能、正式角色美术、完整 VFX 和音频仍未设计或制作。 | 当前活跃计划排除大师技能与正式表现资产；死亡来源存档字段变更与三局人工 Run/30 固定样本批测仍未排期。 | 分别确认大师技能规则、死亡来源存档迁移或正式美术/表现资源预算。 | 2026-08-18 魔剑士实现循环；2026-08-22 grilling 收束；2026-08-22 P1–P3 实现 |
| `deferred` | [Cast Focus FX 延期设计](demonbound-cast-focus-fx-deferred-design.md)已固定剑尖挂点、朝向转换与 charge/release/recover 职责；尚未创建 Profile、Resource 或运行时控制器。 | 当前 Cast Sprite 只制作无光效举剑姿态，避免把可复用 FX 烘焙进角色图。 | 确认首个需要聚能表现的技能、遮挡层和性能预算后建立独立表现计划。 | 2026-08-18 Cast DR 重制 |
| `needs-decision` | 职业精通、通用被动、更多技能分支尚未定义为当前系统。 | 首批 18 技能已满足垂直切片。 | 明确下一阶段成长目标和资源预算。 | 职业技能旧计划 |
| `needs-decision` | 战后第三类奖励槽位与装备战斗掉落仍未形成规则；装备/消耗品统一背包、角色携带、药水战斗使用、药水掉落和商店购买已实现。 | 当前胜利后直接成长，药水掉落独立结算，不提供战后奖励选择。 | 决定是否改变“无战后选择”的核心节奏，以及装备是否进入战斗掉落。 | 战斗奖励/商店旧计划 |
| `verified-gap` | 部分奖励提示仍为临时 UI，商店存在 fallback 路径。 | 首切片优先保证流程闭环。 | UI 交互规范和商店内容稳定。 | Map/Reward 旧计划 |
| `needs-decision` | 难度池、装备掉落与当前固定层级遭遇如何组合尚未定义。 | 会影响 Run 节奏和配置结构。 | 确定长期遭遇生成模型。 | 战斗奖励旧计划 |
| `resolved` | Pure Run 的 Layer 4/6 已各生成一个 Treasure 节点；2–5 Gold、weighted Equipment/Consumable/Buff、一次性确认与 Reload 不重掷均已收口。该规则最初在 Save V6 落地，当前由 V10 round-trip 与幂等回归继续保护。 | — | 若未来改变奖励预算，建立新版本 Treasure Definition，不修改 v1 语义。 | Godot ownership closure 2026-08-15；V10 regression |
| `idea` | 为事件检定结果增加类似桌面角色扮演投骰子的表现结算。 | 当前确定性检定与结果事务已完成，表现形式尚未确定。 | 确定骰面、加值展示、跳过规则和美术资源。 | Pure Run 事件设定 |
| `idea` | 无道德值的选择后果框架。 | 尚无当前玩法需求验证。 | 出现至少一组需要跨节点追踪的选择后果。 | Morality Framework 旧计划 |

## 战斗反馈

| 状态 | 缺口与证据 | 未激活原因 | 激活条件 | 历史来源 |
|---|---|---|---|---|
| `deferred` | 镜头震动、hit stop、状态动画和战斗音效尚未作为统一反馈层落地。 | 当前已有伤害数字、Buff 图标和屏幕战斗日志；美术/音频资产未齐。 | 对应资产和表现验收标准到位。 | 战斗反馈旧计划 |
| `deferred` | 毒矛最终反弹落地的抛物线动画尚未实现。 | 当前实体长矛的命中、落点搜索、阻挡与回收逻辑已可验证，动画效果未定。 | 确定轨迹、时长和遮挡表现。 | 亚马逊技能设定 |
| `deferred` | “召唤长矛”把落地长矛召回目标格的表现动画尚未实现。 | 当前效果与目标选择状态已完成，表现效果明确留作 TODO。 | 确定召回路径与落点反馈。 | 亚马逊技能设定 |

## 配置硬编码

以下路径仍由代码默认值或 fallback 提供：`RoguelikeMapUIController` 的场景/资产路径、`Unit` 的移动 Ability、`BattleController` 的尸体 Prefab、`EquipmentDatabase` 的 JSON、`EncounterCatalog` 的基础 Brain/Unit/Ability，以及 Event Editor 的资源路径。

| 状态 | 缺口与证据 | 未激活原因 | 激活条件 | 历史来源 |
|---|---|---|---|---|
| `needs-decision` | 上述路径应迁入统一配置、保留 fallback，还是由资产引用替代，尚未逐项决定。 | 一次性迁移会扩大首切片风险。 | 某路径需要多套配置、改名或打包验证时，逐项建立小计划。 | Hardcoded Config Audit |

## 明确不再追踪

以下内容与当前架构冲突或已经完成，不应重新变成缺口：

- 旧 5×4 自由探索地图、回溯和节点重入；当前是 7 层只前进结构。
- 将 AbilityConfig 全量迁移到 SkillGraph；当前桥接边界已经存在，是否替换需由新问题驱动。
- “虚弱诅咒”；基础技能是伤害加深诅咒。
- 回收长矛嵌入敌人的状态；当前规则是落到最后命中单位附近空格。
- 将装备与消耗品分成两套背包交互，或让全队共享全部战斗药水能力；当前已统一为角色各携带 1 个独立实例。

相关当前设计见 [Pure Run 小队原型](2026-06-24-pure-run-squad-prototype-design.md)、[三职业首批技能](three-class-skill-design.md) 与 [Gameplay Test Framework](gameplay-test-framework.md)；SkillGraph 当前状态由 OKF 系统页导航到代码和 Godot Resource。
