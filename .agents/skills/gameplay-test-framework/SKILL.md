---
name: gameplay-test-framework
description: "Use when generating, validating, compiling, or running gameplay automation tests from natural language, design docs, or batch templates — guides agents through Tools/gameplay-test-spec and Unity PlayMode execution"
---

# Gameplay Test Framework

面向 `Tactics` 项目的 gameplay 自动化测试工作流技能。它只负责指导 agent 如何使用现有工具链，不承载生成器、校验器或 Unity 运行时逻辑本身。

## Quick Reference

| 步骤 | 命令 / 入口 | 说明 |
|------|-------------|------|
| 1 | `Tools/gameplay-test-spec` | gameplay test spec 宿主工具目录 |
| 2 | `npm install` | 首次使用或依赖缺失时执行 |
| 3 | `npm test` | 验证 TS 工具链并运行 `dist/tests` 下全部测试 |
| 4 | `node dist/src/cli.js generate-spec` | 自然语言 -> `*.gameplay-test.md` |
| 5 | `node dist/src/cli.js validate-spec` | 校验 spec frontmatter 与语义 |
| 6 | `node dist/src/cli.js compile-spec` | spec -> `*.plan.json` |
| 7 | `Tactics.Tests.PlayMode` | Unity PlayMode 计划执行入口 |
| 8 | `.agents/docs/skill-graph-playtest-template.md` | 批量模板输入源 |

## When to use

- 用户要求把自然语言需求转成可执行 gameplay 测试
- 用户要求根据 `.agents/docs/` 中的策划文档生成测试用例
- 用户要求根据 `.agents/docs/skill-graph-playtest-template.md` 批量展开一组测试用例
- 用户要求补齐或回归当前已支持的技能语义，例如 buff、aoe、knockback、ally heal、mark、counter、charge
- 用户要求执行或排查 `gameplay-test-spec` 工具链
- 用户要求运行 `GameplayRuntimeRunner`、`SkillGameplayStepAdapter` 或 `plan.json` 驱动的 PlayMode 测试
- 用户要求为新的 gameplay 场景补充自动化回归

## Workflow

### Step 1: 读取需求源

优先使用 `.agents/docs/` 中的设计文档；如果用户只给了自然语言，也可以直接作为输入，但必须先归一化成 spec。

### Step 2: 生成 spec

在 `Tools/gameplay-test-spec` 下执行：

```bash
npm install
npm test
node dist/src/cli.js generate-spec --text "..." --out path/to/scenario.gameplay-test.md
```

输出的 `*.gameplay-test.md` 是正式 spec，人可读且可审查。
真实样例和回归夹具统一放在 `Tests/gameplay-specs/`，`*.plan.json` 是它们的编译产物。

### Batch Template Workflow

当输入是 `.agents/docs/skill-graph-playtest-template.md` 这类批量模板时，先把模板拆成多个独立场景，再逐个走标准链路。

1. 读取每个模板条目中的 `## ClassName`、`### SkillName`、`补充：`
2. 归一化成单条自然语言输入，例如 `{ClassName} {SkillName} 技能测试：{测试点1}，{测试点2}`
3. 为每个条目生成独立的 `*.gameplay-test.md`
4. 对每个条目执行 `validate-spec`
5. 对通过校验的条目执行 `compile-spec`
6. 汇总成功、歧义、失败三类结果

批量模板流程只是对单条工作流的循环展开，不是另一套命令体系。不要把它继续拆回 `.mimocode/command/`。

### Step 3: 校验 spec

```bash
node dist/src/cli.js validate-spec --spec path/to/scenario.gameplay-test.md
```

只接受 `valid=true` 的 spec。若返回 `needsClarification=true` 或存在 `error` 级诊断，必须先补字段或改写描述。

### Step 4: 编译 plan

```bash
node dist/src/cli.js compile-spec --spec path/to/scenario.gameplay-test.md --out path/to/scenario.plan.json
```

`*.plan.json` 是 Unity 执行层唯一消费的输入，不要手写，不要把自由文本直接喂给 Unity。

### Step 5: 在 Unity 中执行

- 使用 `Assets/Tactics/Tests/PlayMode/GameplayRuntimePlanTests.cs` 的文件驱动计划模式作为参考
- 运行时从 `Tests/gameplay-specs/*.plan.json` 读取 plan
- 运行 `Tactics.Tests.PlayMode`
- 通过 `GameplayRuntimeRunner`、`SkillGameplayStepAdapter` 和 `ExecutableScenarioPlanLoader` 执行 plan
- `GameplayRuntimeAbilityPlanTests` 也必须通过同一套文件驱动 fixture

### Step 6: 解释结果

结果优先看：

1. `GameplayTestResult.Passed`
2. `Diagnostics`
3. `Assertions`
4. `Probes`

如果是 `.cs` 改动，执行结束前必须遵守 `unity-auto-compile-guard`，最近一次脚本修改后要有 Unity 编译确认。

## Supported MVP Scope

当前 MVP 只把以下路径当作稳定支持：

- `Skill` adapter
- `self heal`
- `single target damage`
- `mana success / insufficient / out of range / no valid target`
- `invalid graph rejected before execution`
- `buff / status`
- `area damage`
- `knockback`
- `ally heal`
- `mark`
- `counter`
- `charge`
- `Tests/gameplay-specs/` 文件驱动 fixture
- `ExecutableScenarioPlanLoader` 的最小校验
- `GameplayRuntimeRunner` 的 `timeoutMs` 执行

当前不把这些当作 MVP 稳定能力：

- `Battle` 的完整流程自动化
- `UI` 交互的全量自动化
- `Map` / `Roguelike` 节点 run 的完整自动化
- 手写 `plan.json`
- 跳过 `validate-spec`

## Command Examples

```bash
# 在工具目录初始化依赖
cd Tools/gameplay-test-spec
npm install

# 运行工具链测试
npm test

# 从自然语言生成 spec
node dist/src/cli.js generate-spec --text "自身治疗技能，caster HP 从 6 到 10" --out C:\Temp\self-heal.gameplay-test.md

# 校验 spec
node dist/src/cli.js validate-spec --spec C:\Temp\self-heal.gameplay-test.md

# 编译 plan
node dist/src/cli.js compile-spec --spec C:\Temp\self-heal.gameplay-test.md --out C:\Temp\self-heal.plan.json
```

## Anti-patterns

| ❌ 错误 | ✅ 正确 | 原因 |
|---------|---------|------|
| 直接手写 `plan.json` | 先生成 spec，再 compile | 编译链路会丢失校验和诊断 |
| 自由文本直接喂 Unity | 先生成 `*.gameplay-test.md` | Unity 只消费结构化 plan |
| 跳过 `validate-spec` | 先校验再编译 | 避免把歧义输入送进执行层 |
| 把批量模板流程放回 `.mimocode/command/` | 统一放进 skill | 避免重复定义同一条工作流 |
| 把 skill 当成实现代码 | skill 只提供工作流 | 真相源是 `Tools/gameplay-test-spec` |
| 在 PlayMode 测试里继续内嵌大段 `ExecutableScenarioPlan` | 改为读取 `Tests/gameplay-specs/*.plan.json` | 文件驱动 fixture 才能和 TS 回归对齐 |
| 将 `node_modules` / `dist` 提交进仓库 | 保持本地构建产物忽略 | 这两个目录是本地工具输出 |
| 修改 `.cs` 后不确认编译 | 遵守 `unity-auto-compile-guard` | Unity C# 状态必须重新确认 |

## Checklist

- [ ] 已确认输入源是设计文档还是自由文本
- [ ] 已通过 `npm test`
- [ ] 已生成 `*.gameplay-test.md`
- [ ] 已通过 `validate-spec`
- [ ] 已通过 `compile-spec`
- [ ] `Tests/gameplay-specs/` 下的 spec/plan fixture 已同步
- [ ] 当前语义回归 fixture（buff / aoe / knockback / ally heal / mark / counter / charge）已同步
- [ ] 批量模板生成流程已从 `.mimocode/command/` 收口到 skill
- [ ] `npm test` 覆盖了全部 compiled TS tests
- [ ] 已使用 Unity PlayMode 执行 `Tactics.Tests.PlayMode`
- [ ] loader negative tests 和 timeout test 已通过
- [ ] 若有 `.cs` 改动，已遵守 Unity 编译确认流程
