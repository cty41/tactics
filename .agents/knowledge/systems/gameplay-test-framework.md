---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Tools/gameplay-test-spec
title: Gameplay Test Framework
description: 将 Agent 编写的受控 gameplay spec 编译为 Unity 或 Godot adapters 可执行的确定性计划。
tags: [testing, gameplay, automation, unity, godot]
timestamp: "2026-08-15T17:37:15+08:00"
status: active
catalog_scope: gameplay-test-framework
repo_paths:
  - .agents/docs/gameplay-test-framework.md
  - .agents/skills/gameplay-test-framework/SKILL.md
  - Tools/gameplay-test-spec
  - Assets/Tactics/Scripts/Common/Testing/Gameplay
  - Assets/Tactics/Tests/PlayMode/PlayerInputGameplayPlanTests.cs
  - Assets/Tactics/Tests/PlayMode/GameplayRuntimeBattlePlanTests.cs
  - Assets/Tactics/Tests/PlayMode/HumanPlayerBattleAdapterRegressionTests.cs
  - Assets/Tactics/Tests/PlayMode/HomeSceneInputSmokeTests.cs
  - Tests/gameplay-specs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:c5767934a3fcda4cde009900c9674d436145b264fc2e1410357aee4bd4baf70b
---

# Current State

Agent 编写的 `.gameplay-test.md`/`ScenarioSpec` 经 TypeScript validator 和 compiler 生成 `.plan.json`。未指定 runtime 时继续 byte-identical 生成 Unity v1 plan；显式 `--runtime godot` 生成带 capability、checkpoint、隔离存档和 watchdog 合同的 Godot v2 plan。目标 runtime 不支持的步骤、错误 adapter 声明及被篡改的 capability/probe 合同均 fail-closed。源 Spec 是维护对象，plan 是生成物。

Godot v2 Runtime Runner 通过预 Tree 注入的 `GodotPlayableRunTestContext` 加载正式 `Main.tscn`，每次场景只允许使用 `user://qa-runner/<scenario>/<attempt>/` 下的隔离 Save Store，并在前后校验生产主档与 backup 的长度、时间戳和 SHA-256。PlayerInput action 使用 `Viewport.PushInput` 经过正式 GUI/Input/UnhandledInput 链；Button/Map 点击必须观察真实 production signal，Enter 必须观察 Battle 权威状态变化，表现锁期间的输入 fail-closed。validated checkpoint 只能由受控 catalog 私有构造，并以 canonical V5 hash、plan path/identity、唯一 `loadValidatedCheckpoint` setup 和隔离存档读回值交叉校验。Runner 区分 step timeout、scenario timeout、no-progress、assertion 与 cleanup，并在失败 trace 后释放 Main、隔离目录及临时表现节点。`playBattleThroughInput` 复用 Unity 策略边界：每个玩家单位回合计一次 action，先尝试技能，无合法技能时移动并重试技能，最后经生产 EndTurn 输入推进；独立 `endTurnOnlyUntilTerminal` 仍用于明确只结束回合的失败场景。

首批五个 Godot acceptance spec 已进入统一迁移门禁：Inventory 装备投影进入真实 BattleState、无召唤物 Defeated、Mana 动态数字、确定性 Miss 动态数字，以及 PendingBattle 的 Main 重启/Continue/清理。批量执行生成 `godot-gameplay-spec-result-v1.json`，门禁要求五场景通过、生产 save/backup 证据前后一致且临时节点为零。自动证据关闭规则、事务、scene/process reload 和清理边界；真实 Editor Assembly Reload、文字可读性与动画观感仍保留人工 smoke。

Godot-owned 隔离门禁继续编译并执行全部 Godot v2 spec；`GODOT_OWNED_VERIFY=1` 只跳过对已被物理排除的 Unity runtime plan 的 byte-level deep compare，不跳过 schema、capability、checkpoint、生产输入、Main journey 或清理验证。默认 Unity 编译行为在完整仓库模式仍必须保持 byte-identical。

`GameplayRuntimeRunner` 默认以 `GamePlaybackSpeed.Quadruple`（4×）执行计划；需要真实 1× 语义的调用方可通过显式 speed constructor opt out 到 `Normal`。Runner 只通过 `GameTimeService` 设置 requested speed，并在成功、timeout 或 adapter exception 后恢复进入时速度；它不调用 `ForceResume`，进入时已暂停会 fail-fast，因此 pause ownership 始终属于调用方。`plan.TimeoutMs` 与 cancellation grace 继续使用 realtime `Task.Delay`，不会被 4× 缩短。Runner 的返回也是 runtime scope 生命周期边界：成功、timeout 或异常退出前均先 cancel scope、await `WhenIdleAsync()` 排空所有 tracked task，再 dispose runtime context 和 scope，避免投射物/VFX 等异步 cleanup 泄漏到下一个 fixture 或场景。由于速度状态是进程级全局状态，Runner 调用不得重叠；新增或调整 consumer 时应避免并行执行，并仅在明确验证 1× 行为时使用 `Normal` opt-out。

框架支持真实 Unity 资产，并已有生命、法力、Buff、位置、行动状态、投射物和多阶段等专用断言。Battle adapter 还能创建并推进精确参战顺序，观察朝向、先攻、标准状态、召唤注册、技能可用性原因和有序目标选择；UI adapter 支持点击、右键、悬停、键盘输入，以及文本、样式类、子节点顺序和布局关系断言。UI 玩家流还可绑定指定等级的 Pure Run 技能、构造落地长矛、点击真实战斗单位，并显式刷新当前激活的升级、背包和战斗控制器。adapter 与断言支持面以当前 schema、compiler、Unity 代码和 fixtures 为准，不再按历史 Phase 文档判断。

Map adapter 除 Pure Run 路线与胜场外，现可创建独立消耗品实例、操作角色携带/卸下、一步替换装备、购买通用商店货物，并断言背包、携带引用及商店数量/药水保底/去重。Battle adapter 可对明确单位使用角色携带药水并构造、断言 `CanReceiveHealing`；UI adapter 可验证统一 Inventory popover 和独立战斗消耗品槽。对应维护源位于 `Tests/gameplay-specs/consumables/`、`map/` 与 `ui/`，生成 plan 不手改。

Map adapter 还可断言角色稳定技能 ID 的实际等级、指定 Pure Run 候选是否包含目标等级，以及候选池是否同时含新技能和升级。`pure-run-mixed-levelup-candidates` 源 spec 覆盖 Fireball Lv1 角色在 Lv2 获得 Fireball Lv2 与新技能混合候选；跨存档绑定和真实 SkillGraph 行为由 PlayMode 测试补足。

Map adapter 的节点事务动作支持确定性解析 Mystery 选项、应用 Rest、购买 Store 商品、提交节点事务和重载 Pure Run 存档；对应断言可检查事件分配去重、事件 ID、事务阶段、奖励应用标记、奖励键应用次数和节点消费状态。`Tests/gameplay-specs/map/` 的 Mystery、结果页重入、Rest 重入与 Store 购买重入场景共同验证“先持久化结果、奖励只应用一次、明确提交后才消费”的恢复约束。

Map adapter 还支持 `encounterRecipeContract`、怪物 AI 目录/Heavy Shot 资源、战败零奖励和终局快照断言。`encounter-runtime-contract` 验证 E1/E2/Special 倍率、固定 10×10 中心阻挡格 `(4,4)` 与六类独立 AI；`pure-run-summary-and-defeat` 通过真实奖励事务验证累计金币不受消费影响、已使用药水仍保留在获得列表，以及活动 session 清理后仍可读取战败快照。两份源 spec 均由 TypeScript CLI 校验并生成 compiled plan。

`Tests/gameplay-specs/shared/` 是共享战斗原语的维护源，五个场景分别覆盖朝向/当前轮先攻重排、标准状态回合语义、召唤上限与最早替换、可点击禁用原因，以及可撤销的有序多段选择。对应 plan 必须由 CLI 生成，不手改。

`Tests/gameplay-specs/mage/` 以真实法师 SkillGraph 资产验证等级行为；当前场景覆盖火球术 Lv2 的主目标与正交溅射。运行时上下文区分“加载的项目资产”和“测试创建且由上下文拥有的临时资产”，Dispose 只销毁后者，避免真实资产被误销毁。

`Tests/gameplay-specs/necromancer/` 使用真实死灵法师等级资产验证尸体事务。轻量 Battle 世界中的 `spawnInteractableCorpse` 创建真实 `Corpse` 组件并按格记录实例，消耗与断言读取同一运行时状态；compiler 对尸体动作和断言显式路由到 Battle adapter，避免混合 Skill/Battle 场景被通用回退误分发。当前场景证明 Lv2 骷髅只消耗选中的一具尸体，未选尸体保留。

`Tests/gameplay-specs/amazon/` 使用真实亚马逊等级资产验证毒矛扩散和落矛状态。Skill adapter 在图执行后同步持矛者与落点别名，Battle adapter 可观察长矛持有、落点、诱饵和有序目标结果，使实体长矛规则无需依赖日志或手工 Inspector 验证。

亚马逊/死灵法师 PlayMode 回归还覆盖“毒矛落地 → 跨其他单位回合 → 亚马逊相邻免费拾取”、跨职业不注入亚马逊长矛工具技能，以及骨矛 Lv1–Lv3 在执行前拒绝非横、纵、45° 对角线目标；这些用例防止缓存状态或旧 UI 回调把亚马逊能力泄漏到其他角色，或让投射物飞行后才以无目标失败。

`Tests/gameplay-specs/ui/` 的 Slice 6 场景覆盖混合升级确认、背包只读技能详情、战斗两行布局、可点击禁用原因及连续刺击多段撤销/取消。`pure-run-ui-lifecycle-reentry` 在单个测试内保留同一 Inventory 缓存实例，连续执行三次关闭/重开并验证隐藏期间新增物品、角色信息和操作回调；测试组级清理仍隔离不同用例的旧战斗控制器。

`PlayerInput` adapter 使用由 runtime context 拥有的虚拟 Mouse/Keyboard，通过 Input System 和生产 UI 输入模块驱动状态变化；production `EventSystem`、`InputSystemUIInputModule`、action asset 或 pointer actions 缺失时 fail-closed，不创建替代 module，也不调用 `AssignDefaultActions`。UI 目标按稳定元素名解析，坐标由 `worldBound`、Panel scale 与屏幕 Y 轴转换得到，并在发送事件前用 Panel picking 验证；`Reachable` map resolver 的主候选必须同时满足 reachable、`VisitState.Unvisited` 与未 consumed，transient rendered fallback 可暂不要求 reachability，但仍必须排除 visited/consumed，避免旧 battle node 收到 ClickEvent 后被产品点击权限静默拒绝。地图节点还会按自身 panel 的 viewport、scroll geometry 和最终 pick 结果重新获取目标，不使用固定设备坐标。世界目标由正式 Camera 转为屏幕坐标。战斗策略只选择固定 Camera 内可点击的合法攻击目标和移动格，不会为测试修改生产 Camera。输入状态只入队一次并交给自动 PlayerLoop 消费；同一次 press 不发送二次 re-arm 状态事件，避免 UI Toolkit `ClickEvent` 与 `Button.clicked` 在跨 context 时序下脱节。等待下一帧使用可取消的 `Awaitable.NextFrameAsync`，避免 Unity SynchronizationContext 中的同帧忙循环；动画完成类 readiness（例如 EndTurn 解锁）使用 realtime deadline，避免无焦点 Editor 在动画结束前耗尽 frame budget。EndTurn 必须按 battle inactive、round、current unit 或 current player 的真实变化判定推进；pointer 已投递但无业务效果时才 fallback production `M` hotkey，不能用此前 ability/move 的变化替代 EndTurn 证据。fixture 重建虚拟设备后会仅刷新 controls 或 `activeControl` 陈旧的 production actions；若共享 actions 因前一虚拟设备移除而 disabled，只通过现有 production module 自身的 disable/enable lifecycle 重建，不替换配置。测试期间保存并在结束时恢复 Editor PlayMode input behavior、Input System background behavior 和物理 Mouse/Keyboard enabled 状态，action-failure 与 timeout 回归会逐项断言这些状态。pointer observer 只证明事件投递；普通 readiness 可观察已布局但 non-pickable 的 UI，只有声明 `interactable` 时才要求真实 pick。非 Button 目标可用 `ClickEvent` 作为语义成功边界；Button 必须观察真实 `Button.clicked`、release 前仍 attached 且 release 后发生的 production target transition，或后续业务 observable 闭环，裸 `ClickEvent` 不再视为按钮业务回调成功。PointerDown 时已经 detach 不算成功。`player-input-e2e` 标签禁止 setup 写入捷径和 UI/Map/Battle/Skill runtime action，只允许这些 adapter 做只读断言；Journey 与两条 Mystery 路线都从 Home 经三次初始技能选择和三场真实输入战斗推进，不使用 Map fast-forward/refresh seam。`inventory-reentry-player-input` 从 Home 实际点击创建 Run，并连续三次通过地图按钮打开 Inventory，证明缓存重入、筛选、关闭和地图恢复交互都经过生产输入链。Home Options smoke 使用独立 fixture，真实加载 Home 并等待 UI ready，再以虚拟 Mouse 通过生产 `PlayerInput` 点击 Options、断言 `OptionsRoot` 存在且可见，从而隔离长旅程残留；其 `.gameplay-test.md` source spec 是维护对象，plan 由 compiler 生成。pending roguelike battle 在 `IsBattleActive=false` 后仍须相对入口 baseline 观察新的 phase、reward identity 或 active settlement root，并以 realtime observable-first polling 覆盖产品异步恢复延迟；上一场遗留的 `Complete` 不算本场 settlement transaction。虚拟设备在成功、action 失败与 Runner 超时后均由 context 释放；已有生产输入模块不会在清理时被卸载。

`HomeSceneInputSmokeTests` 还强制扩展动态中文字形 atlas 并经过隐藏/重开，验证 FontAsset、Material 与全部已使用 atlas 的 `DontSave` 生命周期；测试会模拟新增 atlas 标志尚未同步时直接打开另一个 UI，要求修复自有资源图而不替换共享实例。托管静态引用丢失后必须通过带运行时 provenance 的确定性 owner 恢复同一 source、FontAsset、Material 和 atlas 实例；无 provenance owner 及其外部资源图不得被采用、修改或销毁，并逐一断言其 Material、已使用 atlas 身份及候选数量不变。测试还覆盖结构损坏 owner 与保留 owner 共享同一 FontAsset 的恢复/同步顺序，以及独立 FontAsset 部分共享保留 Material、首 atlas 并携带未使用 atlas 尾槽的清理；保留图身份先确定，清理只销毁未受保护的已使用资源。

真实输入层新增 `battle-player-input-smoke` 与 `pure-run-player-input-route`：前者覆盖正式单位选择、移动、右键取消、技能卡和目标输入；后者从 Home 开始完成三场自然战斗、三次显式升级、Inventory、Store 和多次场景重入。战斗策略可只读查询当前回合、合法技能和目标，但所有位置、资源、生命、节点和成长变化必须由鼠标或键盘输入产生，最多执行 100 个单位行动。原 `pure-run-real-player-route` 已重标为 `journey-integration`，继续快速覆盖五场胜利、Boss、RunSummary、失败和事件团灭，不再宣称覆盖真实玩家输入。

长期测试分为四层：逻辑测试验证规则和事务，语义 UI 测试验证元素与布局，`player-input-e2e` 验证生产输入和场景旅程，最终人工测试只判断视觉裁切、动画反馈、可读性与操作手感。

Battle/Map/UI PlayMode 夹具在激活对象前完成序列化依赖注入，避免重复调用 Unity 生命周期；运行时上下文销毁时取消 AI 任务、解绑结算事件并清理战斗作用域状态。`Battle` adapter 在 `BattleEnded` 时只快照当前 `BattleController.CellManager` 上属于已完成战斗的尸体；`restartBattle` 仅消费这批 residue，使下一战复用同一格前恢复 occupancy，同时保留战斗结束后新预置的尸体及其他测试世界的 interactable。测试 adapter 的失败信息包含当前选中单位、能力、节点与 summary 快照，便于区分业务失败和夹具隔离问题。`GameplayRuntimeBattlePlanTests` fixture 在 SetUp 捕获 `LogAssert.ignoreFailingMessages` 进入值，共享异步清理按序 cancel+await controller runtime scope、观察 teardown fault/result 后再销毁 process-global UI，各清理步骤独立记录异常且单步失败不跳过后续恢复；成功、失败和 teardown 异常路径都恢复进入时的 LogAssert 与时间状态，suite 级恢复由 cleanup probe 用例跨运行验证。

# Relationships

- Battle adapter 验证[Monster AI](monster-ai.md)与[Battle System](battle.md)。
- Map adapter 验证[Roguelike Run](roguelike-run.md)。
- Skill adapter 验证[SkillGraph](skill-graph.md)。
- 严格事件顺序、动画完成断言和 CI 接入记录在[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

修改 Spec 工具、adapter 或 fixtures 后运行工具测试、validate/compile 和对应 Unity PlayMode 测试。Home Options smoke 需从 `home-options-player-input-smoke.gameplay-test.md` 编译 plan，并独立运行 `HomeSceneInputSmokeTests` fixture。真实玩家输入场景必须带 `player-input-e2e` 标签，状态变化只能来自 `PlayerInput` action；Map、Battle、Skill、UI adapter 仅可用于只读 assertion。需要证明实际行为时必须加载真实资产，不能用手写结果或日志文本替代。

涉及 UIManager runtime font 生命周期时，验证顺序必须为 `PlayerInputGameplayPlanTests` → 完整 `HomeSceneInputSmokeTests`，以捕获前序大型字形 atlas 对后序 Home 首绘制的测试运行时污染。单个 UnityTest 对静态引用丢失、重复/损坏 owner、动态 atlas 和资源身份的模拟不等同于真实退出并重新进入 Play Mode。

# Citations

[1] [Gameplay spec tool](https://github.com/cty41/tactics/tree/main/Tools/gameplay-test-spec)
[2] [Unity gameplay adapters](https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Testing/Gameplay)
