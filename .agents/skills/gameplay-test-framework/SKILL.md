---
name: gameplay-test-framework
description: "Use when generating, validating, compiling, or running gameplay automation tests from natural language, design docs, or batch templates — guides agents through Tools/gameplay-test-spec and the Godot runtime runner"
---

# Gameplay Test Framework

使用 Agent-first Spec 工具链创建和运行 Gameplay 自动化测试。能力与命令以 `Tools/gameplay-test-spec` 和 Godot runtime runner 为准，系统概览见 `../../docs/gameplay-test-framework.md`。

## Quick Reference

| 阶段 | 入口 |
|---|---|
| 生成/编写 | `*.gameplay-test.md` 或 `ScenarioDraft` |
| 校验 | `validate-spec -s <spec>` |
| 编译 | `compile-spec -s <spec> -o <plan>` |
| 批处理 | `batch-validate -d <dir>` / `batch-compile -d <dir> -o <dir>` |
| 执行 | Godot `GodotGameplayRuntimeRunner` |

## When to use

- 将需求或设计文档转成可执行 Gameplay 测试。
- 添加或修改 Skill、Battle、Map、UI adapter 场景。
- 排查 Spec 校验、编译或 Godot 运行失败。
- 用真实资产回归技能、战斗或 Roguelike 行为。

## Workflow

### 1. 先核对支持面

检查 `Tools/gameplay-test-spec/src` 的 schema/validator/compiler 和 Godot runtime runner 的 adapter。不要根据旧阶段文档猜 action/assertion 名称。

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
node Tools/gameplay-test-spec/dist/src/cli.js compile-spec -s <scenario.gameplay-test.md> -o <scenario.plan.json> --runtime godot
```

批处理：

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js batch-validate -d <spec-directory>
node Tools/gameplay-test-spec/dist/src/cli.js batch-compile -d <spec-directory> -o <output-directory>
node Tools/gameplay-test-spec/dist/src/cli.js batch-compile -d <spec-directory> -o <output-directory> --runtime godot
```

修改 TypeScript 后先按 package scripts 构建。校验失败必须修复 Spec 或 schema，不跳过 validator 直接改生成计划。

### 4. Godot 执行

- Godot v2 plan 必须声明 `runtime: Godot`、能力、adapter、隔离存档、watchdog 和可选 validated checkpoint。
- Runner 加载正式 `Main.tscn`，玩家动作通过 `Viewport.PushInput` 进入生产输入链，不直接调用业务服务证明输入成功。
- 每个场景只写 `user://qa-runner/<scenario>/<attempt>/`；执行前后必须证明生产主档和 backup 未变化。
- 批量结果写入 `artifacts/gameplay-specs/godot/godot-gameplay-spec-result-v1.json`，并由统一迁移门禁校验。

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

## Checklist

- [ ] 已核对当前 schema 与 adapter 支持面。
- [ ] 源 Spec 清晰且进入版本控制。
- [ ] TS 测试、validate 和 compile 通过。
- [ ] Godot 使用需要的真实 Resource 与 adapter 执行。
- [ ] 失败信息能定位到具体步骤/断言。
