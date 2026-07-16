---
name: skill-graph-creation
description: "Use when user wants to create a new skill through conversational Q&A — guides agent through intent recognition, SkillGraphSpec generation, validation, gameplay tests, and Unity asset application"
---

# SkillGraph 技能创建向导

通过最少提问把技能意图收束成 `SkillGraphSpec`，测试优先地验证语义，再通过 Unity/MCP 工具生成资产。系统边界见 `../../docs/skill-graph-system.md`。

## Quick Reference

| 阶段 | 输出 |
|---|---|
| 意图识别 | 已知参数、缺失决策、默认值 |
| Spec | `SkillGraphSpec` JSON |
| 测试 | `*.gameplay-test.md` → `.plan.json` |
| Unity 校验 | Spec/Graph validation 结果 |
| 应用 | `SkillGraphAsset` 与必要 Ability bridge |

## When to use

- 用户要求创建或扩展一个 SkillGraph 技能。
- 简短技能描述需要补齐目标、范围、阶段或状态规则。
- 用户希望 test-first 创建技能或通过 MCP 落地资产。

## Workflow

### 1. 读取当前约束

先核对技能目录、相关权威设计、现有相似 SkillGraph 和 Gameplay Test。写 C# 时另行加载项目代码规则；修改 Unity 资产时不得直接编辑 YAML。

### 2. 只问高影响缺失项

从用户描述提取：目标阵营/形状、距离、伤害/治疗、Buff、位移、投射物、召唤、阶段顺序、消耗和失败行为。已明确的内容不重复提问；低风险数值可给出显式默认值。

### 3. 生成 Spec

在仓库根目录执行：

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js generate-skill-graph-spec -t "<技能描述>" -o <skill-spec.json>
```

如果返回 `needsClarification`，只追问 `questionsToAsk` 中仍影响语义的项。已有结构化答案文件时可用：

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js generate-skill-graph-spec-answers -a <answers.json> -o <skill-spec.json>
```

不要用 shell 拼接命令临时写 JSON；创建受版本控制的输入时使用正常文件编辑流程。

### 4. 测试优先

```powershell
node Tools/gameplay-test-spec/dist/src/cli.js generate-test-from-spec -s <skill-spec.json> -o <scenario.gameplay-test.md>
node Tools/gameplay-test-spec/dist/src/cli.js validate-spec -s <scenario.gameplay-test.md>
node Tools/gameplay-test-spec/dist/src/cli.js compile-spec -s <scenario.gameplay-test.md> -o <scenario.plan.json>
```

检查生成测试是否真的覆盖用户约束，尤其是目标范围、命中顺序、Buff 存在性、投射物结束状态和落点。生成器不能表达的关键语义应补充受支持断言，不能只接受一个宽松模板。

### 5. Unity 校验与应用

1. 使用 `SkillGraphSpecCompiler`/MCP validation 检查 Spec 和目标图。
2. 只对确定性问题使用 `SkillGraphSpecAutoFixer`。
3. 向用户展示识别结果、关键节点/阶段、测试覆盖和待生成资产；涉及用户选择时先确认。
4. 通过 Unity MCP/项目资产工具应用 Spec，必要时生成 `SkillGraphAbilityConfig` 桥接。
5. 编译 Unity 并运行对应 PlayMode/Gameplay Test。

具体 MCP 工具名和参数以 `Assets/Tactics/Scripts/Editor/MCP/SkillGraphMcpTools.cs` 的当前实现为准，不从旧提示文档复制契约。

## Anti-patterns

| 错误 | 正确 |
|---|---|
| 未核对目录和相似技能就生成 | 先读取当前事实源 |
| 一次询问所有可能参数 | 只问影响语义的缺失项 |
| 直接编辑 `.asset` YAML | 使用 Unity/MCP/资产工具 |
| 先落资产后补测试 | 先生成并校验 Spec 测试 |
| 自动修复改变产品语义 | 仅修复确定性结构问题 |
| 使用固定本机 worktree 路径 | 从当前仓库根目录运行 |

## Checklist

- [ ] 用户约束、目录和相似资产已核对。
- [ ] 只补问了高影响缺失项。
- [ ] Spec 和 gameplay test 已生成并校验。
- [ ] 关键语义有专用断言。
- [ ] Unity 资产通过工具应用且未直接编辑 YAML。
- [ ] Unity 编译与相关测试通过。
