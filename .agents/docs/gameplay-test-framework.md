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

设计文档可在明确的 `gameplay-contract` fenced block 中声明稳定 Contract ID。ScenarioSpec/ScenarioDraft 通过可选 `contractIds` 引用合同，compiler 将其原样带入 Godot plan，便于批量覆盖报告定位“已覆盖、缺少 spec、DSL 暂不支持”。普通散文不会被当成合同。

Runner 加载正式场景，通过 `Viewport.PushInput` 进入生产 GUI/Input 链。每个场景只使用 `user://qa-runner/<scenario>/<attempt>/`，执行前后证明生产主档与 backup 未变化，并在退出时清理 Main、临时节点和隔离目录。

## 验证边界

- TypeScript 单元测试证明 schema、validator、compiler 与作者 spec。
- Godot runtime batch 证明生产输入、规则、事务、重载和清理。
- 固定 Seed、AI-vs-AI 与 checkpoint 是诊断/回归证据，不是平衡或体验通过。
- 视觉、动画、可读性、真实 Editor Reload 和手感只由人工验收记录。

## 使用入口

具体命令和约束见 [Gameplay Test Framework Skill](../skills/gameplay-test-framework/SKILL.md)；当前支持面必须从 `Tools/gameplay-test-spec`、Godot runner 和测试读取，不从旧阶段计划推断。

LLM 前端只负责从文档提出带原文行号的合同候选，或为单个合同提出 ScenarioDraft。默认 provider 由仓库外用户配置决定，当前正式配置为 OpenCode Go `deepseek-v4-flash`；CLI 每次进程先从 `/models` 发现 exact model，再调用 JSON Output，失败不自动切换 provider。Ollama 仍可通过 `--provider ollama` 显式使用。

在线模型输出仍必须经过原文逐字证据、Zod、Contract ID、DSL capability 和 compiler 的确定性门禁。`validate-contracts`、`contract-coverage`、`validate-*` 与 `compile-*` 永不联网。在线 provider 的请求审计只记录 provider、resolved model、耗时、token usage 和 request ID，不记录 Key、Authorization、文档正文或响应正文。

配置位于 `%LOCALAPPDATA%\Tactics\gameplay-test-spec\`：`providers.json` 保存非敏感设置，`secrets.json` 保存 OpenCode Go Key。模板见 `Tools/gameplay-test-spec/examples/`；secrets 必须撤销继承 ACL 并仅授权当前用户、SYSTEM 或 Administrators，否则 CLI 拒绝联网。任何曾粘贴到会话、日志或命令行的 Key 都必须先撤销再使用。
