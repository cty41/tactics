# 项目已知缺口

## 用途

本文集中记录已经从代码、资产或测试中确认，但尚未转成活跃执行计划的缺口。它不是承诺清单；只有满足“激活条件”并另行建立范围明确的开发计划后，项目才开始实现。

状态定义：

- `verified-gap`：已确认当前实现缺失或不完整。
- `needs-decision`：需要产品或技术决策后才能排期。
- `deferred`：明确延后，当前切片不依赖。
- `idea`：值得保留的探索方向，尚未验证价值。

## Monster AI

| 状态 | 缺口与证据 | 未激活原因 | 激活条件 | 历史来源 |
|---|---|---|---|---|
| `verified-gap` | `ScoreNode.Parameter` 已序列化，但 `IntentScorer` 尚未消费该参数。 | 参数语义尚未统一。 | 定义每种 scorer 的参数契约与回归测试。 | 怪物 AI 旧计划 |
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
| `deferred` | Gameplay Test 未接入 CI。 | 项目当前没有 CI 流程。 | 建立可稳定启动 Unity 的 CI 环境。 | Framework Roadmap 旧计划 |
| `needs-decision` | SkillGraph 更强的循环、阶段与目标语义静态检查仍可扩展。 | 静态校验与运行测试的职责边界需先确定。 | 收集真实误配案例并定义可判定规则。 | SkillGraph 旧计划 |

## Unity MCP 可靠性

| 状态 | 缺口与证据 | 未激活原因 | 激活条件 | 历史来源 |
|---|---|---|---|---|
| `verified-gap` | MCPForUnity 10.1.0/10.1.2 在 `registered` 处理期间同步等待 Unity 主线程工具发现，receive loop 无法同时处理 ping；10.1.2 的相关三个源码文件没有变化。 | 当前任务只允许稳定版升级，禁止 fork；尚无满足源码门的发布版。 | 上游 stable 同时发布匹配 Unity package/server，并让工具发现 pending 时 receive loop 仍可响应、旧 session 结果不会写入新连接。 | Unity MCP 重连调查 2026-08-06 |
| `verified-gap` | 后台测试轮询的上游 focus nudge 可能调用 Windows 前台激活，与项目焦点保护规则冲突，当前 10.1.x 未提供正式 opt-out。 | 项目不允许用窗口激活推进后台测试；本地不能安全补丁 PackageCache。 | 上游提供可配置关闭开关，或新稳定版移除对前台焦点的依赖并通过后台测试。 | Unity MCP 测试合批调查 2026-08-06 |
| `verified-gap` | 自动恢复仍为 `0/5`、`blocked_upstream`。项目 bootstrap 已收缩为 batch/import-worker guard 后显式 no-op，5 个定向 EditMode case 通过；项目不再覆盖 manual Disconnect、不再写 endpoint/preference，也不再 start/stop/connect/verify/retry。包层 10.1.0 仍可能创建并发 reconnect continuation 和 session eviction，10.1.2 相关源码未修复。 | 开发期优先消除项目层副作用，不在当前切片建设新的自动恢复 owner；定向 guard 测试不能替代真实 reload 稳定性。 | 先等待上游 stable 通过 receive-loop/tool-discovery 源码门；若未来确需自动恢复，再设计可被用户接管的 per-Editor owner，并在同一 Editor、零手动 Connect/重启/前台自动化条件下从 0 完成连续 5 次 reload。 | Unity MCP 安全收缩 2026-08-07 |
| `verified-gap` | MCPForUnity 10.1.x 的 transport、scope、local endpoint 与 auto-start 公共 API 最终写入机器级 `EditorPrefs`；并行打开其他 Unity 项目时仍可能观察到本项目配置。项目 bootstrap 已停止每 Domain 重写 endpoint，因此只保证项目层不再扩大竞争，不提供跨项目存储隔离。 | 当前只允许上游稳定版升级，不能 fork 包并改写配置存储。 | 上游提供 project/editor-instance scoped 配置 API，或稳定版实现不依赖机器级偏好，并通过多 Editor/worktree 隔离验证。 | Unity MCP 生命周期审查 2026-08-07 |
| `deferred` | `Manage-UnityTestGate.ps1` 仍是本地 draft helper，尚未完成无歧义 canonical commitment、attempt-local result、单次 StateRoot snapshot、全根 fail-closed、严格 migration/replay 与确定性并发，因此不能作为 CI、发布或审计事实源。 | 当前开发只要求单 Editor、单执行者、单 test job；完善 final v3 的收益低于玩法回归。 | 建立 CI/发布认证、多执行者并发或正式审计需求，并为 final-v3 契约建立独立计划。 | Unity Test Gate closing review 2026-08-06 |
| `deferred` | 历史 Git 提交曾包含 Context7 凭据；当前 tracked tree 与 Phase A 新 blob 已移除凭据形态，但服务端撤销/轮换状态未知，Git 历史未重写。 | 用户明确接受开发期剩余风险，先完成仓库侧防复发与逻辑开发。 | 在 Context7 供应商侧撤销/轮换旧 key，并只记录完成确认，不记录凭据值。 | Unity MCP 配置迁移 2026-08-07 |

## Pure Run、奖励与内容扩展

| 状态 | 缺口与证据 | 未激活原因 | 激活条件 | 历史来源 |
|---|---|---|---|---|
| `needs-decision` | 职业精通、通用被动、更多技能分支尚未定义为当前系统。 | 首批 18 技能已满足垂直切片。 | 明确下一阶段成长目标和资源预算。 | 职业技能旧计划 |
| `needs-decision` | 战后第三类奖励槽位与装备战斗掉落仍未形成规则；装备/消耗品统一背包、角色携带、药水战斗使用、药水掉落和商店购买已实现。 | 当前胜利后直接成长，药水掉落独立结算，不提供战后奖励选择。 | 决定是否改变“无战后选择”的核心节奏，以及装备是否进入战斗掉落。 | 战斗奖励/商店旧计划 |
| `verified-gap` | 部分奖励提示仍为临时 UI，商店存在 fallback 路径。 | 首切片优先保证流程闭环。 | UI 交互规范和商店内容稳定。 | Map/Reward 旧计划 |
| `needs-decision` | 难度池、装备掉落与当前固定层级遭遇如何组合尚未定义。 | 会影响 Run 节奏和配置结构。 | 确定长期遭遇生成模型。 | 战斗奖励旧计划 |
| `deferred` | Pure Run 尚未生成 Treasure 节点，宝箱开启事务、奖励规则和地图出现规则也未收口。 | 当前 Mystery、战斗、休息和商店已覆盖首个真实路线闭环。 | 确定宝箱与 Mystery 的节奏差异及奖励预算。 | Pure Run 九切片收口 |
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

相关当前设计见 [Pure Run 小队原型](2026-06-24-pure-run-squad-prototype-design.md)、[三职业首批技能](three-class-skill-design.md)、[SkillGraph 系统](skill-graph-system.md) 与 [Gameplay Test Framework](gameplay-test-framework.md)。
