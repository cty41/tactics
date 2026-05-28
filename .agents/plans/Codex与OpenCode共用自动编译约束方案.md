# Codex / OpenCode 共用自动编译约束方案

## Summary

本方案将“修改 `.cs` 后必须编译”的规则收敛为一份共享规则源，并分别通过 Codex hooks 与 OpenCode 插件做极薄适配，避免长期维护两套独立业务规则文本。

目标：

- 共用一份规则正文
- Codex 侧尽量阻止“改完 `.cs` 却未编译就结束”
- OpenCode 侧继续通过系统提示注入相同规则

## Implementation Changes

- 新增共享规则文件：`.agents/shared-rules/unity-auto-compile.md`
- 新增共享 skill：`.agents/skills/unity-auto-compile-guard/SKILL.md`
- 新增 Codex 项目配置：`.codex/config.toml`
- 新增 Codex hook 脚本：`.codex/hooks/unity_auto_compile_guard.py`
- 修改 OpenCode 插件：`.opencode/plugin/auto-compile.js`

关键行为：

- 只有 `.cs` 变更会触发自动编译约束
- 最近一次 `.cs` 修改之后，必须调用 `refresh_unity(compile="request")`
- 再次修改 `.cs` 后，之前的编译不再算满足条件
- OpenCode 插件不再硬编码规则正文，而是读取共享规则文件
- Codex hook 用会话级临时状态记录“是否存在未编译的 `.cs` 改动”

## Test Plan

1. 修改单个 `.cs`
- 预期：进入待编译状态
- 预期：未调用 `refresh_unity(compile="request")` 时，Codex Stop hook 会继续追问

2. 连续修改多个 `.cs`
- 预期：最后一次 `.cs` 修改后仍要求重新编译

3. 仅修改非 `.cs`
- 预期：不触发该规则

4. 调用 `refresh_unity` 但不带 `compile="request"`
- 预期：不算满足条件

5. OpenCode 会话启动
- 预期：插件把共享规则文件注入到 system prompt

## Assumptions

- `refresh_unity` 是项目内稳定可用的编译工具名
- Codex hooks 在本项目中可通过 `.codex/config.toml` 启用
- 项目主要运行在 Windows，因此 hook 配置优先覆盖 `command_windows`
- 允许使用平台极薄适配层，但不允许继续维护双份业务规则正文
