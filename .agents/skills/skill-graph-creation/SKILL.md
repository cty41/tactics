---
name: skill-graph-creation
description: "Use when user wants to create a new skill through conversational Q&A — guides agent through intent recognition, question-driven detail collection, and automated SkillGraphSpec + gameplay-test generation"
---

# SkillGraph 技能创建向导

通过提问引导用户完善技能细节，自动生成 SkillGraphSpec JSON 和 gameplay-test.md。

## Quick Reference

| 阶段 | 动作 | 输出 |
|------|------|------|
| 意图识别 | 解析用户描述 | 技能模式 + 已知参数 |
| 提问补全 | 只问缺失信息 | 完整参数集 |
| 生成图 | bash CLI `generate-skill-graph-spec` | SkillGraphSpec JSON |
| 生成测试 | 根据 Spec 生成 gameplay-test.md | `.gameplay-test.md` 文件 |
| 确认 | 展示结果等用户确认 | 执行 ApplySpec |

## When to use

- 用户输入类似"帮我做一个新技能"、"创建一个冰霜新星技能"
- 用户给出简短的技能描述，需要补全细节
- 用户想要 test-first 方式创建技能

## Workflow

### Step 1: 意图识别 + 提问

读取用户描述，识别目标类型、伤害类型、效果类型、弹道、位移等维度。

**只问缺失的，不问已明确的。** 如果描述已足够完整，跳过提问。

```
识别到: 范围伤害 + 魔法 + 施加冰冻
需要补充:
1. 伤害范围半径? (默认 2)
2. 基础魔法伤害值? (默认 5)
3. 冰冻持续回合? (默认 2)
4. 最大施法距离? (默认 3)
```

### Step 2: 生成 SkillGraphSpec JSON

通过 bash tool 调用 TypeScript CLI：

```bash
node Tools/gameplay-test-spec/dist/src/cli.js generate-skill-graph-spec -t "冰霜新星, 半径2, 伤害5, 冰冻2回合, 距离3"
```

如果返回 `needsClarification: true`，根据 `questionsToAsk` 继续提问。

如果返回完整 `spec`，直接使用。

也可以用 `generate-skill-graph-spec-answers` 传入结构化参数（先写 JSON 文件再调用）：

```bash
echo '{"displayName":"冰霜新星","targetType":"area","effects":["damage","buff"],"damageType":"Magical","baseDamage":5,"areaRadius":2,"maxRange":3,"buffName":"Frozen","buffDuration":2,"buffIsUnique":true}' > _answers.json
node Tools/gameplay-test-spec/dist/src/cli.js generate-skill-graph-spec-answers -a _answers.json
```

### Step 3: 生成 gameplay-test.md（测试优先）

使用 CLI 自动从 SkillGraphSpec JSON 生成 gameplay-test.md：

```bash
node Tools/gameplay-test-spec/dist/src/cli.js generate-test-from-spec -s _spec.json -o Tests/gameplay-specs/new-skill.gameplay-test.md
```

支持的模式：`singleTargetDamage` / `projectile` / `areaDamage` / `selfHeal` / `allyHeal` / `charge` / `knockback` / `applyBuff`

组合技能（如范围+伤害+Buff）会自动推断正确的 graphKind 和断言。

### Step 4: 展示并确认

向用户展示：
1. 识别到的模式
2. SkillGraphSpec JSON
3. gameplay-test.md 内容
4. 询问是否执行

### Step 5: 执行（用户确认后）

```
1. 保存 gameplay-test.md 到 Tests/gameplay-specs/
2. ApplySpec(graphPath, spec) → 落地 SkillGraphAsset
3. CreateAbilityConfigForGraph → 生成桥接配置
4. 编译 plan.json
5. 运行 PlayMode 测试验证
```

## CLI 命令

| 命令 | 用途 | 输出 |
|------|------|------|
| `generate-skill-graph-spec -t "描述"` | NL → SkillGraphSpec | JSON stdout |
| `generate-skill-graph-spec-answers -a answers.json` | 结构化 → SkillGraphSpec | JSON stdout |
| `generate-spec -t "描述" -o output.md` | NL → gameplay-test.md | 文件 |
| `compile-spec -s spec.md -o plan.json` | spec → plan.json | 文件 |

所有 CLI 命令通过 bash tool 调用，工作目录为项目根目录 `D:\codes\tactics-worktrees\w1`。

## 节点链速查表

| 模式 | 节点链 | graphKind |
|------|--------|-----------|
| 单体近战 | Start → SelectPrimaryTarget → ApplyDamage → Finish | singleTargetDamage |
| 单体远程 | Start → SelectPrimaryTarget → ProjectileLaunch → OnHit → ApplyDamage → Finish | projectile |
| 范围伤害 | Start → SelectTargetPoint → CollectTargetsInArea → ForEachTarget → ApplyDamage → (loop) → Finish | areaDamage |
| 自身治疗 | Start → SelectSelf → ApplyHeal → Finish | selfHeal |
| 友军治疗 | Start → SelectAlly → ApplyHeal → Finish | allyHeal |
| 冲锋 | Start → SelectPrimaryTarget → DashToTarget → Finish | charge |
| 击退 | Start → SelectPrimaryTarget → ApplyKnockback → Finish | knockback |
| 伤害+Buff | Start → SelectPrimaryTarget → ApplyDamage → ApplyBuff → Finish | singleTargetDamage |
| 范围+伤害+Buff | Start → SelectTargetPoint → CollectTargetsInArea → ForEachTarget → ApplyDamage → ApplyBuff → (loop) → Finish | areaDamage |

## Anti-patterns

| ❌ 错误 | ✅ 正确 |
|---------|---------|
| 直接生成 SkillGraphSpec 不问问题 | 先识别意图，只问缺失信息 |
| 生成图后才写测试 | 先生成 gameplay-test.md（测试优先） |
| 用 generate-spec 处理范围/组合技能 | 根据 SkillGraphSpec 输出手动生成 |
| 一次性问所有参数 | 只问缺失的，已明确的跳过 |
| 不展示结果直接 ApplySpec | 展示 JSON 等用户确认 |

## 完整对话示例

```
用户: 冰霜新星, 对周围敌人造成魔法伤害并施加冰冻

Agent:
  识别到技能模式: 范围 + 魔法伤害 + Buff(冰冻)
  
  需要补充几个参数:
  1. 伤害范围半径? (默认 2)
  2. 基础魔法伤害值? (默认 5)
  3. 冰冻持续回合? (默认 2)
  4. 最大施法距离? (默认 3)

用户: 半径2, 伤害5, 冰冻2回合, 距离3

Agent:
  bash: node Tools/gameplay-test-spec/dist/src/cli.js generate-skill-graph-spec -t "冰霜新星, 半径2, 伤害5, 冰冻2回合, 距离3"
  
  [展示 SkillGraphSpec JSON]
  
  生成 gameplay-test.md...
  [展示 YAML]
  
  确认后执行 ApplySpec + 生成桥接 + 运行测试?

用户: 确认

Agent:
  [执行 ApplySpec, 创建桥接, 运行测试]
  完成! 63 个测试全部通过。
```
