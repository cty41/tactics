---
name: gameplay-test-framework
description: "Use when generating, validating, compiling, or running gameplay automation tests from natural language, design docs, or batch templates — guides agents through Tools/gameplay-test-spec and Unity PlayMode execution"
---

# Gameplay Test Framework

面向 `Tactics` 项目的 gameplay 自动化测试工作流技能。它只负责指导 agent 如何使用现有工具链，不承载生成器、校验器或 Unity 运行时逻辑本身。

**Phase 7 变更**：框架已从"自然语言生成器"收束为"Agent-First Spec Compiler"。自然语言理解主要由 agent 负责，工具主链路为 `spec/controlled input -> validate -> compile -> run`。

## Quick Reference

| 步骤 | 命令 / 入口 | 说明 |
|------|-------------|------|
| 1 | `Tools/gameplay-test-spec` | gameplay test spec 宿主工具目录 |
| 2 | `npm install` | 首次使用或依赖缺失时执行 |
| 3 | `npm test` | 验证 TS 工具链并运行 `dist/tests` 下全部测试 |
| 4 | agent 直接输出 `ScenarioDraft` 或 `ScenarioSpec` | **推荐** Agent-First 输入方式 |
| 5 | `node dist/src/cli.js generate-spec` | 自然语言 -> `*.gameplay-test.md`（辅助入口） |
| 6 | `node dist/src/cli.js validate-spec` | 校验 spec frontmatter 与语义 |
| 7 | `node dist/src/cli.js compile-spec` | spec -> `*.plan.json` |
| 8 | `Tactics.Tests.PlayMode` | Unity PlayMode 计划执行入口 |

## When to use

- 用户要求把需求转成可执行 gameplay 测试（agent 负责理解需求并输出受控描述）
- 用户要求根据 `.agents/docs/` 中的策划文档生成测试用例
- 用户要求补齐或回归当前已支持的技能语义
- 用户要求执行或排查 `gameplay-test-spec` 工具链
- 用户要求运行 `GameplayRuntimeRunner`、`SkillGameplayStepAdapter` 或 `plan.json` 驱动的 PlayMode 测试
- 用户要求为新的 gameplay 场景补充自动化回归

## Spec 支持矩阵

详细的 adapter 支持能力请参考：

- **Skill adapter**: 本文档下方的 Skill 语义说明
- **Battle adapter**: `.agents/docs/battle-spec-support-matrix.md`

### 主线约束

1. `.gameplay-test.md` 是主要输入载体，`ScenarioSpec` 是内部结构化真相层
2. `generate-spec` 保留为自然语言到 `.gameplay-test.md` 的辅助入口
3. 禁止新增手写 `plan.json` 主路径，新能力优先走 `.gameplay-test.md -> ScenarioSpec -> validator -> compiler -> plan.json`
4. Battle 优先：在 Battle spec-first 主链未收口前，不正式扩面 Map/UI

### 资产模式边界

| 模式 | 使用场景 | Setup 标记 | Fixture 目录 |
|------|----------|-----------|-------------|
| **轻量模式** | Skill-only 逻辑测试 | `createSkillTestWorld` | `Tests/gameplay-specs/` (根目录) |
| **真实资产模式** | 需要加载真实 ScriptableObject | `useRealAssets` + `loadSkillGraphAsset` | `Tests/gameplay-specs/battle-assets/` |

**规则**：
- `battle-assets/` 目录下的 fixture 默认要求真实资产模式
- 轻量模式 fixture 不受影响，继续使用 `createSkillTestWorld`
- 两种模式可并存，不互相替换
- `useRealAssets` 必须在 `loadSkillGraphAsset` 之前调用
- `loadSkillGraphAsset` 需要 `useRealAssets` 已启用，否则报 Asset failure

## Agent-First Workflow（推荐）

### Step 1: Agent 理解需求并输出受控描述

Agent 直接输出 `ScenarioDraft` 或完整 `ScenarioSpec`，无需依赖关键词模板命中：

```typescript
// ScenarioDraft 格式（简化版，自动补齐 requiredAdapters）
interface ScenarioDraft {
  feature: string;           // 例如 "SkillGraph"
  scenario: string;          // 例如 "FireballAreaDamage"
  tags?: string[];           // 可选标签
  requiredAdapters?: Adapter[]; // 默认 ["Skill"]
  setup: Array<{
    kind: string;
    parameters: Record<string, unknown>;
  }>;
  actions: Array<{
    kind: string;
    target?: string;
    parameters: Record<string, unknown>;
  }>;
  assertions: Array<{
    kind: string;
    target?: string;
    expected?: unknown;
    parameters: Record<string, unknown>;
  }>;
  timeoutMs?: number;        // 默认 10000
}
```

### Step 2: 校验

```bash
node dist/src/cli.js validate-spec --spec path/to/scenario.gameplay-test.md
```

校验器会检查：
- alias 引用完整性
- graph/action/assertion 种类合法性
- 语义规则（areaDamage 需要 targetPoint、applyBuff 需要 buffName/duration/selection 等）

### Step 3: 编译

```bash
node dist/src/cli.js compile-spec --spec path/to/scenario.gameplay-test.md --out path/to/scenario.plan.json
```

### Step 4: Unity 执行

- 运行时从 `Tests/gameplay-specs/*.plan.json` 读取 plan
- 运行 `Tactics.Tests.PlayMode`
- 通过 `GameplayRuntimeRunner`、`SkillGameplayStepAdapter` 执行

## Legacy Workflow（辅助入口）

`generate-spec` 仍保留作为兼容入口，但不再是主推荐方式：

```bash
node dist/src/cli.js generate-spec --text "..." --out path/to/scenario.gameplay-test.md
```

## Supported Assertion Types

### 基础断言（已有）
- `executionStateEquals` - 执行状态断言
- `unitHealthEquals` - 单位生命值断言
- `unitManaEquals` - 单位法力值断言
- `unitHasBuff` - 单位拥有 buff 断言
- `unitBuffDurationEquals` - buff 持续时间断言
- `unitCellEquals` - 单位位置断言
- `lastErrorContains` - 错误信息断言
- `stepMessageContains` - 步骤消息断言
- `validationErrorCodeIncludes` - 验证错误码断言

### Phase 7 新增断言（接口已扩展，但部分仍在收口中）
- `unitBuffCountEquals` - buff 数量断言（支持同名 buff 堆叠检测）
- `unitBuffIsUnique` - buff 唯一性断言（确保无重复 buff）
- `unitCountInArea` - 区域内单位数量断言（需要 centerAlias 和 radius 参数）
- `projectileLaunched` - 投射物发射断言
- `projectileHitTarget` - 投射物命中目标断言
- `projectileCompleted` - 投射物生命周期完成断言
- `multiStageStateEquals` - 多阶段状态断言（需要 stageIndex 参数）

## Supported Graph Kinds

- `selfHeal` - 自我治疗
- `singleTargetDamage` - 单体伤害
- `invalidSelfHeal` - 无效图（用于验证测试）
- `areaDamage` - 范围伤害（**需要 targetPointAlias**）
- `knockback` - 击退
- `allyHeal` - 友军治疗
- `applyBuff` - 应用 buff（**需要 buffName/duration/selectionKind**）
- `charge` - 冲锋

## Semantic Validation Rules

校验器会自动检查以下语义规则：

1. **areaDamage** 必须在 `executeSkillGraph` 中指定 `targetPointAlias`
2. **applyBuff** 建议指定 `buffName`、`duration`、`selectionKind`
3. **unitBuffCountEquals/unitBuffIsUnique** 需要 `target` 和 `buffName`
4. **unitCountInArea** 需要 `centerAlias` 和正数 `radius`
5. **projectile* 断言** 需要 `target`
6. **multiStageStateEquals** 需要非负 `stageIndex`

## Command Examples

```bash
# 在工具目录初始化依赖
cd Tools/gameplay-test-spec
npm install

# 运行工具链测试
npm test

# 校验 spec
node dist/src/cli.js validate-spec --spec Tests/gameplay-specs/mage-fireball.gameplay-test.md

# 编译 plan
node dist/src/cli.js compile-spec --spec Tests/gameplay-specs/mage-fireball.gameplay-test.md --out Tests/gameplay-specs/mage-fireball.plan.json

# 从自然语言生成 spec（辅助入口）
node dist/src/cli.js generate-spec --text "自身治疗技能，caster HP 从 6 到 10" --out C:\Temp\self-heal.gameplay-test.md
```

## Anti-patterns

| ❌ 错误 | ✅ 正确 | 原因 |
|---------|---------|------|
| 直接手写 `plan.json` | 先生成 spec，再 compile | 编译链路会丢失校验和诊断 |
| 自由文本直接喂 Unity | agent 输出受控描述 -> validate -> compile | Unity 只消费结构化 plan |
| 跳过 `validate-spec` | 先校验再编译 | 避免把歧义输入送进执行层 |
| 依赖 generator 关键词模板 | agent 直接输出 ScenarioDraft | Phase 7: Agent-First 设计 |
| 把 skill 当成实现代码 | skill 只提供工作流 | 真相源是 `Tools/gameplay-test-spec` |
| 新增手写 `plan.json` 作为主路径 | 优先走 `.gameplay-test.md -> ScenarioSpec -> compiler` | plan.json 是编译产物，不是主要维护对象 |
| Battle 能力绕过 spec 直接写 plan | 先定版 Battle spec 契约 | 避免 spec/validator/runtime 漂移 |

## Checklist

- [ ] 已确认输入源是设计文档还是 agent 受控描述
- [ ] 已通过 `npm test`
- [ ] 已生成 `*.gameplay-test.md`（或 agent 直接输出 ScenarioDraft）
- [ ] 已通过 `validate-spec`
- [ ] 已通过 `compile-spec`
- [ ] `Tests/gameplay-specs/` 下的 spec/plan fixture 已同步
- [ ] 新增断言（buff count/unique、projectile、multi-stage）已有对应 fixture
- [ ] 已使用 Unity PlayMode 执行 `Tactics.Tests.PlayMode`
- [ ] 若有 `.cs` 改动，已遵守 Unity 编译确认流程
