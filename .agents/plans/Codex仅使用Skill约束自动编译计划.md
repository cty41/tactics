# Codex 仅使用 Skill 约束自动编译计划

## Background

- 当前问题：
  - Codex 上的 hook trust 会触发 `config/batchWrite failed in TUI`
  - 问题已经不再局限于 hook 本身，而是 TUI 的通用配置写回链路不稳定
  - 继续维护 Codex hook 方案会增加使用和排查成本
- 目标：
  - 放弃 Codex hook 做法
  - 让 Codex 仅通过 `unity-auto-compile-guard` skill 和共享规则约束 `.cs` 修改后的编译动作
  - 保留 OpenCode 侧现有插件方案，不强行统一两端实现方式
- 预期收益：
  - 启动 Codex 时不再出现 `review hooks`
  - 不再出现 `Failed to trust hook(s): config/batchWrite failed while updating hook trust in TUI`
  - 自动编译约束仍有单一规则源，不会回到双份规则正文维护

## Scope

### In Scope

- 停止 Codex 侧所有 hook 注册入口
- 保留 Codex 的 skill + shared rule 约束链路
- 保留 OpenCode 的 plugin + shared rule 约束链路
- 清理或降级 Codex hook 相关说明，避免后续误启用

### Out of Scope

- 不修复 Codex TUI 的 `config/batchWrite` 内部缺陷
- 不改 OpenCode 插件的运行方式
- 不新增新的 Codex 插件、MCP、外部守护进程来替代 hook

## Tasks

### Task 1: 收束 Codex 配置入口

- 目标：确保 Codex 不再从任何项目级或用户级配置加载 `unity-auto-compile-guard` hooks
- 输入：
  - 项目级 `.codex/config.toml`
  - 用户级 `C:\Users\15507\.codex\config.toml`
- 输出：
  - 项目级配置中不出现任何 `[[hooks.*]]`
  - 用户级配置中不出现 `unity-auto-compile-guard` 对应的 `[[hooks.*]]`
- 验收标准：
  - 启动 Codex 时不再提示 `review hooks`
  - 用户级配置中不再存在 `# BEGIN tactics unity-auto-compile-guard` 块
  - 项目级 `.codex/config.toml` 保持说明性文本，不含 hook 注册

### Task 2: 固化 Codex 的 Skill 约束路径

- 目标：让 Codex 仅依赖 `unity-auto-compile-guard` skill 和共享规则表达自动编译约束
- 输入：
  - `.agents/skills/unity-auto-compile-guard/SKILL.md`
  - `.agents/shared-rules/unity-auto-compile.md`
- 输出：
  - 明确的“Codex 仅靠 skill 触发和复核”的约束说明
- 验收标准：
  - skill 文案覆盖触发条件、必须动作和结束前复核
  - skill 引用共享规则文件作为唯一规则正文来源
  - 不再要求或暗示用户启用 Codex hook

### Task 3: 降级 Codex Hook 历史文件为非启用状态

- 目标：保留历史实现供参考，但消除它们作为默认方案的误导
- 输入：
  - `.codex/hooks/install-user-hooks.md`
  - `.codex/hooks/sync_user_codex_hooks.ps1`
  - `.codex/hooks/unity_auto_compile_guard.py`
  - `.codex/hooks/unity_auto_compile_guard_launcher.ps1`
- 输出：
  - 文档标注为历史方案或停用方案
  - 脚本文件不再被当作推荐入口
- 验收标准：
  - `install-user-hooks.md` 明确写出 Codex 不再推荐 hook 路径
  - `sync_user_codex_hooks.ps1` 不再被文档推荐执行
  - 仓库内没有新的入口继续把用户引回 hook 方案

### Task 4: 保持 OpenCode 侧现状并校验规则一致性

- 目标：在不使用 Codex hook 的前提下，继续保持双端规则正文一致
- 输入：
  - `.opencode/plugin/auto-compile.js`
  - `.agents/shared-rules/unity-auto-compile.md`
- 输出：
  - OpenCode 继续通过 plugin 注入共享规则
  - Codex 与 OpenCode 共用同一份规则正文
- 验收标准：
  - OpenCode 插件仍从共享规则路径读取文本
  - 共享规则文案与 skill 文案不冲突
  - 不再存在第二份独立维护的业务规则正文

## Test Plan

1. 启动 Codex
- 预期：不再出现 `review hooks`
- 预期：不再出现 `Failed to trust hook(s): config/batchWrite failed while updating hook trust in TUI`

2. 执行一次涉及 `.cs` 修改的 Codex 任务
- 预期：skill 仍要求在最近一次 `.cs` 修改后调用 `refresh_unity(compile="request")`

3. 执行一次只涉及非 `.cs` 文件的 Codex 任务
- 预期：不触发自动编译约束

4. 启动 OpenCode 会话
- 预期：插件继续注入共享规则

## Assumptions

- Codex 侧接受“仅靠 skill 约束，不靠 hook 硬拦截”这一退让
- 当前优先级是稳定使用 Codex，而不是继续追查 TUI 的配置写回内部缺陷
- OpenCode 侧继续保留 plugin 路线，不要求本轮统一成纯 skill
