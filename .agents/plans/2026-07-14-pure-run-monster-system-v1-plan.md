# Pure Run 怪物系统 v1 实施计划

## Background

首个 demo 采用固定三人小队、单张不可回头的 Run 地图，并开放基础与高级技能。怪物系统沿用 Mew 的职责拆分：技能负责射程、形状、阵营、遮挡和目标合法性；AI 只在合法的“站位 + 动作 + 目标”中评分；需要玩家学习的高威胁循环使用固定 Pattern。首版不建立运行时 `threatValue`，遭遇强度由明确配方和倍率表达。

## Scope

- 建立玩家、AI 与执行前重验证共用的技能目标查询契约。
- 支持移动后施法、空格 AOE 中心、AOE 目标展开、supercover LOS 与单次技能图执行。
- 为四类敌人提供评分画像，并为精英突进者和精英毒法师提供单位级 Pattern 状态。
- 将 Run 改为 7 层只前进结构；实际战斗数量为 5、6 或 7。
- 每场胜利只成长一名最低等级存活角色，并在主属性达到 7 时提供一次高级技能里程碑保底。
- 建立怪物定义、遭遇配方、布局与已解析遭遇数据；不加入运行时威胁预算。
- 扩展现有 gameplay-test Battle/Map 适配器，覆盖结构化 AI 结果、固定种子和角色成长断言。
- 更新设计、计划与 OKF 综合页，并完成 Unity 编译、自动测试和 OKF 校验。

## Tasks

### 1. 共享技能合法性与执行

- 用 `IAbilityTargetingProvider`、`AbilityTargetQuery`、`AbilityTargetResult`、`AbilityTargetOption` 替代 AI 专属查询草稿。
- 查询输入包含施法者、假设起点和棋盘；输出包含目标点与实际受影响单位。
- LOS 使用 supercover：起点和终点不阻挡，中间活单位与布局阻挡，尸体不阻挡；AOE 仅检查中心点 LOS。
- `IPlannedAbilityExecutor.ExecuteAsync(AiActionPlan)` 在执行前重验证，技能图、资源消耗只发生一次。

验收：玩家高亮、AI 枚举和执行前校验消费同一合法性结果；AOE 多目标不会多次释放。

### 2. AI 候选、评分与 Pattern

- 候选为“当前/可达起点 × 合法技能选项”，完整保存目的地、技能、目标点和目标集合。
- 移动失败立即停止施法；结果使用 `AiActionExecutionResult` 和 `AiTurnResult` 表达。
- 增加 `AbilityRangeFit`、`FollowUpValue`，新配置使用 `GraphWeight × ProfileWeight`，旧配置保留兼容模式。
- 稳定种子打破平分；默认无随机噪声。
- Pattern 状态按单位保存：EliteCharger 为 `ChargeStrike → MeleeAttack`，ElitePoisonCaster 为 `PoisonCloud → MeleeAttack`；只有指定技能成功才推进，非法、受控或执行失败时使用 Generic fallback 且不推进。

验收：两只共享 Brain 的怪物 Pattern 游标互不影响；失败不会推进步骤。

### 3. Run 地图与成长

- 7 层结构：1–3 必战，4 为补给/可选普通混编战/随机事件，5 必战，6 为商店/可选精英混编战/随机事件，7 为单一 Special 终战。
- 只沿 outgoing 前进，已访问节点不会重新变为可选。
- 固定法师、死灵法师、亚马逊，等级 1，七项基础属性均为 5。
- 每次胜利只让最低等级存活角色 `+1` 等级并获得 `+1` 属性；平级按稳定队伍顺序。
- 固定职业主属性的 base 值首次达到 7 且尚无高级技能时，一个职业候选槽保证起始分支的高级技能；其余候选保持随机，新技能从下一场战斗生效。

验收：第 4/5/6 次胜利后分别能保证 1/2/3 名角色获得高级技能，并能在之后的战斗使用。

### 4. 四类怪物与遭遇配方

- Charger：近战与 `ChargeStrike`。
- Ranged：远程与 `HeavyShot`。
- AOE：近战与 `PoisonCloud`（射程 4、允许空中心、3×3 方形、仅敌方、中毒 3 回合且每回合 2 点）。
- Support：近战与 `Expose`（射程 4、LOS、2 回合、承伤 +30%，已暴露目标跳过并偏好后续集火）。
- 普通配方：N1 C2+R1；N2 R2+S1；N3 A1+C2+S1；N4 R2+A1+C1；N5 S2+C1+A1；N6 C2+R1+A1。
- 精英配方：E1 A1+C2+S1；E2 R2+A1+C1，HP ×1.3、输出 ×1.15。
- Special：单个 EliteCharger 或 ElitePoisonCaster，HP ×1.8、输出 ×1.25。
- 布局：`open`、`center_blocker`、`split_flank`。

验收：相同 seed 和配方得到稳定的已解析遭遇；运行时没有 `threatValue` 字段或动态预算器。

### 5. 自动化、调试与知识同步

- Battle 适配器加载真实 Brain 并暴露结构化 AI 回合结果；支持技能、目的地、目标点、目标数、fallback 和 Pattern 步骤断言。
- Map 适配器支持 `setRunSeed`、`strictAsset`、角色等级与 `SkillId` 断言。
- 增加几何、倍率、Pattern、成长保底以及代表性 gameplay specs。
- 调试命令覆盖 `runreset`、`runseed int|short|full`、`runstatus`、`encounter recipe`、`encounter monster`、`aidebug`。
- 运行 `catalog_impact.py report --strict-unmapped --worktree`，只同步本任务 scope；共享合法性接口同时映射 monster-ai 与 skill-graph，并新增 gameplay-test-framework scope 覆盖相关工具、适配器和 specs。

验收：Unity 编译无错误，相关 EditMode/PlayMode 与 gameplay-test 工具测试通过，OKF bundle 和单元测试通过。

## 非目标

- 不做全局阵形系统。
- 不做运行时 `threatValue`、动态遭遇预算或第五类怪物。
- 不开放大师技能。
- 不新增 UI 测试适配器。
- 不在本计划中自动提交或推送。
