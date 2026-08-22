---
name: gameplay-test-framework
description: "Use when generating, validating, compiling, or running gameplay automation tests from natural language, design docs, or batch templates — guides agents through Tools/gameplay-test-spec and Unity PlayMode execution"
---

# Gameplay Test Framework

使用 Agent-first Spec 工具链创建和运行 Gameplay 自动化测试。能力与命令以 `Tools/gameplay-test-spec` 和 Unity adapters 为准，系统概览见 `../../docs/gameplay-test-framework.md`。

## Quick Reference

| 阶段 | 入口 |
|---|---|
| 生成/编写 | `*.gameplay-test.md` 或 `ScenarioDraft` |
| 校验 | `validate-spec -s <spec>` |
| 编译 | `compile-spec -s <spec> -o <plan>` |
| 批处理 | `batch-validate -d <dir>` / `batch-compile -d <dir> -o <dir>` |
| 执行 | Unity `GameplayRuntimeRunner` / PlayMode 测试 |

## When to use

- 将需求或设计文档转成可执行 Gameplay 测试。
- 添加或修改 Skill、Battle、Map、UI adapter 场景。
- 排查 Spec 校验、编译或 Unity 运行失败。
- 用真实资产回归技能、战斗或 Roguelike 行为。

## Workflow

### 1. 先核对支持面

检查 `Tools/gameplay-test-spec/src` 的 schema/validator/compiler 和 `Assets/Tactics/Scripts/Common/Testing/Gameplay/` 的 adapter。不要根据旧阶段文档猜 action/assertion 名称。

当前 adapter 包括 Skill、Battle、Map、UI；它们按场景需要组合，不存在必须先完成某一 adapter 才能使用其他 adapter 的阶段限制。

### 2. 创建源 Spec

- Agent 将自然语言收束成受控 `ScenarioSpec`/`ScenarioDraft`。
- 优先维护 `*.gameplay-test.md`，不要手写 `.plan.json`。
- 需要真实 ScriptableObject 语义时使用项目已有真实资产 setup；框架自身单元测试才使用最小测试世界。
- 一个场景证明一个清晰行为，别用日志字符串代替状态断言。

### 3. 校验与编译

在仓库根目录执行：

```powershell
npm --prefix Tools/gameplay-test-spec test
node Tools/gameplay-test-spec/dist/src/cli.js validate-spec -s <scenario.gameplay-test.md>
node Tools/gameplay-test-spec/dist/src/cli.js compile-spec -s <scenario.gameplay-test.md> -o <scenario.plan.json>
```

批处理：

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js batch-validate -d <spec-directory>
node Tools/gameplay-test-spec/dist/src/cli.js batch-compile -d <spec-directory> -o <output-directory>
```

修改 TypeScript 后先按 package scripts 构建。校验失败必须修复 Spec 或 schema，不跳过 validator 直接改生成计划。

### 4. Unity 执行

- 让 Unity Runner 消费编译后的 plan。
- 使用实际需要的 adapter，并确认其能解析所有 setup/action/assertion。
- 资产行为测试应引用真实资产；记录失败步骤、诊断码和可观察状态。
- 若改动 `.cs`，遵守 Unity 自动编译与测试规则。

## 测试执行升级阶梯（强制）

术语：**exact test** 是能精确命中目标行为的最小测试；**focused gates** 包括 exact test、相关 fixture（含条件性连续第二轮）和 related gates。

严格按以下唯一顺序升级，不得跳级：

1. **Exact RED**：先运行 exact test；必须得到非零测试数，并确认失败原因正是预期要修复的行为。
2. **Exact GREEN**：修复后重复同一精确测试，直到通过。
3. **Related fixture**：运行承载该行为的相关 fixture。
4. **Fixture 连续第二轮**：改动涉及生命周期、输入、场景、缓存 UI 或异步状态时，fixture 首轮通过后立即连续再跑一轮，以捕获残留状态和顺序依赖。
5. **Related gates**：运行与改动相关的其他编译、校验和测试门禁。
6. **Full suite**：只有上述所有 focused 门禁均为绿色后，才允许运行全量套件。
7. **Review/commit**：当前任务引入的失败全部修复后才可进入；无关基线失败按下述证据与政策规则处理。

每次测试记录 job ID、测试 count、最终 status 和首个失败。`total=0` 不是通过，而是无效运行，必须修正筛选或发现问题后重跑。升级时若出现新的下游失败，立即停止扩围，将其最小精确复现作为新的 Exact RED；修复后先取得该 exact test 的 GREEN，再从**受影响的最早后续门禁**继续升级，已有证据证明不受影响的前置门禁无需盲目重跑。

Full suite 发现疑似无关失败时，先在基线/HEAD 上复现，或引用可核验的已知基线证据，再分类并单独报告；不得混入当前任务修复。当前任务引入的失败必须修复。无关基线失败不得永久阻塞，也不得由执行者自行放行：只有记录复现与分类证据，并获得用户明确允许或仓库政策允许后，才能继续 review/commit；否则停止在该边界并报告。禁止改变测试顺序来掩盖污染、跳过流程、盲目 `sleep`、弱化断言，或拆分 fixture 制造假绿。

## Unity Job 所有权与并发规则

- 同一 Editor/Test Runner 同一时间只能有一个 **mutating job owner**；由该 owner 串行驱动会改变该 Editor 状态的操作。
- 同一 Editor/Test Runner 内不得并发运行 PlayMode、refresh/compile、domain reload 或另一个测试 job。独立 Editor 可各自拥有 owner，但不得共享或竞争同一 Editor/Test Runner 的状态；彼此独立的只读分析仍可并行。
- 依赖失败链保留在同一调查上下文中，沿已有证据继续收敛，避免每个下游失败都冷启动新 worker 或新调查。
- 仅当源文件变更确实需要时执行 refresh；Editor 处于 compile/ready 过渡期间不得重复 refresh，先等待并核实状态。

## 长任务进度与 stall 判定

- 任务超过 60 秒时主动报告：job ID、`active`/`finished`/`stalled` 状态、`completed/total`、当前阶段、最近更新时间，以及下一次 focused 检查内容。
- 只要 `completed`、阶段、日志时间或其他可验证信号仍在推进，任务就是 `active`，不得仅因耗时而判定 stalled。
- 自最近一次可验证进度起 90 秒没有进展，即触发 focused 检查，不以 Editor 是否 ready 为前提。检查当前 job/Test Runner、editor state 和 console；若 compile/domain reload 或其他新时间戳证明仍在推进，则标记 `active`，并从该更新时间重置 90 秒窗口。
- 若 focused 检查确认 job 卡死，停止或取消该 job，释放该 Editor/Test Runner 的唯一 owner，并将 Editor/Test Runner 清理到已知状态；随后只重跑当前 focused gate，不重启整套阶梯。不得在没有新证据时反复等待或无限重置窗口。
- 用户回复之间的空闲不计入测试执行时间；进度判断以 job 和 Editor 的实际时间戳/状态为准。

## Agent-first SkillGraph 辅助命令

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js generate-skill-graph-spec -t "<技能描述>" -o <skill-spec.json>
node Tools/gameplay-test-spec/dist/src/cli.js generate-test-from-spec -s <skill-spec.json> -o <scenario.gameplay-test.md>
```

自然语言 `generate-spec` 是辅助入口，不是绕过 Agent 收束和人工确认的理由。

## Anti-patterns

| 错误 | 正确 |
|---|---|
| 手写 `.plan.json` | 维护 Spec 并由 compiler 生成 |
| 依赖旧“Phase”支持矩阵 | 读取当前 schema、adapter 和测试 |
| 用轻量测试替身证明真实资产行为 | 显式加载真实资产 |
| 跳过 validate | 先校验再编译 |
| 只看最终日志 | 使用专用状态断言 |
| focused gates 未绿就跑全量 | 按升级阶梯先完成 exact test、fixture 与 related gates |
| 当前任务失败未修复就审查，或自行忽略基线失败 | 修复任务失败；基线失败凭证据和用户/仓库政策决定边界 |
| 多个 job 竞争同一 Editor/Test Runner | 为该 Editor 指定唯一 owner；独立 Editor 各自隔离，只读分析可并行 |
| 新下游失败后重启全套阶梯 | 将其设为 Exact RED，GREEN 后从最早受影响的后续门禁恢复 |
| 长任务只说“正在跑” | 报告 job ID、状态、进度、阶段、更新时间与下次检查 |
| 无进度时无限等待或重启全套 | 90 秒触发检查；确认卡死后清理并只重跑当前 focused gate |
| 把 `total=0` 当成测试结果 | 视为无效运行，修正发现/筛选后重跑 |

## Checklist

- [ ] 已核对当前 schema 与 adapter 支持面。
- [ ] 源 Spec 清晰且进入版本控制。
- [ ] TS 测试、validate 和 compile 通过。
- [ ] Unity 使用需要的真实资产与 adapter 执行。
- [ ] 失败信息能定位到具体步骤/断言。
- [ ] 已按 Exact RED → Exact GREEN → related fixture → 条件性 fixture 连续第二轮 → related gates → full suite 的唯一顺序升级，且 focused gates 全绿后才运行全量。
- [ ] 每次运行均有非零测试数，并记录 job ID、count、status 与首个失败；新下游失败取得 exact GREEN 后已从最早受影响的后续门禁恢复，未盲目重跑不受影响的前置门禁。
- [ ] 当前任务引入的失败已修复；无关全量失败已有基线复现/已知证据和分类，review/commit 决策符合用户许可或仓库政策。
- [ ] 同一 Editor/Test Runner 只有一个 mutating job owner；独立 Editor 各自隔离状态，只读分析之外没有竞争同一 Editor 的并发操作。
- [ ] 依赖失败沿同一调查上下文处理；refresh 仅在源变更需要且 Editor 不处于 compile/ready 过渡时执行。
- [ ] 超过 60 秒的任务已报告规定进度字段；90 秒无可验证进度已检查 job/Test Runner、editor state、console，活跃证据会重置窗口，确认卡死后已清理并只重跑当前 focused gate。
- [ ] 未用改测试顺序、跳流程、盲目等待、弱断言或拆 fixture 的方式制造假绿。
