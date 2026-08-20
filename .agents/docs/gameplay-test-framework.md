# Gameplay Test Framework

## 文档定位

Gameplay Test Framework 让 Agent 用受控 Markdown 场景生成确定性的 Godot runtime plan，并通过正式 `Main.tscn`、生产输入链和隔离存档验证战斗、技能、地图、UI 与旅程行为。

## 数据流

```text
*.gameplay-test.md / ScenarioSpec
  -> TypeScript validate
  -> compile --runtime godot
  -> GodotGameplayRuntimeRunner
  -> structured result + cleanup evidence
```

源 spec 是维护对象，plan 是生成物。schema、capability、adapter、checkpoint 或 runtime 不匹配时必须 fail-closed；不得手改 `.plan.json` 绕过 compiler。

Runner 加载正式场景，通过 `Viewport.PushInput` 进入生产 GUI/Input 链。每个场景只使用 `user://qa-runner/<scenario>/<attempt>/`，执行前后证明生产主档与 backup 未变化，并在退出时清理 Main、临时节点和隔离目录。

## 验证边界

- TypeScript 单元测试证明 schema、validator、compiler 与作者 spec。
- Godot runtime batch 证明生产输入、规则、事务、重载和清理。
- 固定 Seed、AI-vs-AI 与 checkpoint 是诊断/回归证据，不是平衡或体验通过。
- 视觉、动画、可读性、真实 Editor Reload 和手感只由人工验收记录。

## 使用入口

具体命令和约束见 [Gameplay Test Framework Skill](../skills/gameplay-test-framework/SKILL.md)；当前支持面必须从 `Tools/gameplay-test-spec`、Godot runner 和测试读取，不从旧阶段计划推断。
